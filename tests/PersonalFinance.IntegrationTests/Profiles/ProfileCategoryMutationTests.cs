using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Profiles;

public sealed class ProfileCategoryMutationTests
{
    [Fact]
    public async Task Category_metadata_migration_preserves_existing_accounts_sessions_profiles_and_system_categories()
    {
        await using var host = new AccountTestHost();
        await host.WithDatabaseAsync(async db =>
        {
            await db.GetService<IMigrator>().MigrateAsync("20261106110747_AddProfileCreation", TestContext.Current.CancellationToken);
            return true;
        });
        var accountId = await AddAccount(host);
        var profileId = Guid.NewGuid();
        var categoryIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        string sessionId;
        using (var scope = host.Services.CreateScope())
        {
            sessionId = await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().CreateAsync(
                new BffSession(accountId, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddHours(1)),
                TestContext.Current.CancellationToken);
        }
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profileId, OwnerAccountId = accountId, Name = "Existing profile", MainCurrencyCode = "GBP" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO [Categories] ([Id], [ProfileId], [Name], [Kind]) VALUES ({0}, {1}, {2}, {3}), ({4}, {1}, {5}, {6}), ({7}, {1}, {8}, {9})",
                [categoryIds[0], profileId, "Income", "income", categoryIds[1], "Savings", "savings", categoryIds[2], "Debt", "debt"],
                TestContext.Current.CancellationToken);
            return true;
        });

        await host.MigrateAsync();

        using var scopeAfter = host.Services.CreateScope();
        var session = await scopeAfter.ServiceProvider.GetRequiredService<IBffSessionStore>().FindAsync(sessionId, TestContext.Current.CancellationToken);
        Assert.Equal(accountId, session!.AccountId);
        await host.WithDatabaseAsync(async db =>
        {
            Assert.True(await db.Users.AnyAsync(account => account.Id == accountId, TestContext.Current.CancellationToken));
            Assert.Equal("Existing profile", (await db.BudgetProfiles.SingleAsync(profile => profile.Id == profileId, TestContext.Current.CancellationToken)).Name);
            var categories = await db.Categories.Where(category => category.ProfileId == profileId).OrderBy(category => category.Kind).ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(categoryIds.OrderBy(id => id), categories.Select(category => category.Id).OrderBy(id => id));
            Assert.All(categories, category =>
            {
                Assert.Null(category.Comment);
                Assert.Null(category.TrackingStoppedAt);
                Assert.Equal(8, category.Version.Length);
            });
            return true;
        });
    }

    [Fact]
    public async Task Writers_create_ordinary_categories_and_edit_any_category_without_changing_identity_or_kind()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var owner = await AddAccount(host);
        var editor = await AddAccount(host);
        var viewer = await AddAccount(host);
        var profileId = Guid.NewGuid();
        var otherProfileId = Guid.NewGuid();
        var incomeId = Guid.NewGuid();
        var foreignCategoryId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profileId, OwnerAccountId = owner, Name = "Home", MainCurrencyCode = "GBP" });
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = otherProfileId, OwnerAccountId = owner, Name = "Other", MainCurrencyCode = "GBP" });
            db.Categories.AddRange(
                new CategoryRecord { Id = incomeId, ProfileId = profileId, Name = "Income", Kind = "income" },
                new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Savings", Kind = "savings" },
                new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Debt", Kind = "debt" },
                new CategoryRecord { Id = foreignCategoryId, ProfileId = otherProfileId, Name = "Foreign", Kind = "ordinary" });
            db.ProfileAccess.AddRange(
                new ProfileAccessRecord { ProfileId = profileId, AccountId = editor, Role = "editor" },
                new ProfileAccessRecord { ProfileId = profileId, AccountId = viewer, Role = "view_only" });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        using var ownerClient = await SignedInClient(host, owner);
        using var editorClient = await SignedInClient(host, editor);
        using var viewerClient = await SignedInClient(host, viewer);
        using var noProofClient = await SignedInClient(host, owner, includeAntiforgery: false);
        using var noProof = await noProofClient.PostAsJsonAsync($"/api/budget-profiles/{profileId}/categories", new { name = "No proof" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, noProof.StatusCode);
        Assert.True(noProof.Headers.CacheControl?.NoStore);
        using var createdResponse = await editorClient.PostAsJsonAsync($"/api/budget-profiles/{profileId}/categories",
            new { name = "  Home  ", comment = " household  ", kind = "income", profileId = otherProfileId }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<CategoryMutationResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        var categories = await ownerClient.GetFromJsonAsync<CategoriesResponse>($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        var ordinary = Assert.Single(categories!.Categories, category => category.Id == created.Id);
        Assert.Equal("ordinary", ordinary.Kind);
        Assert.Equal("Home", ordinary.Name);
        Assert.Equal("household", ordinary.Comment);
        Assert.NotEmpty(ordinary.Version);
        using var ordinaryEdit = await editorClient.PutAsJsonAsync($"/api/budget-profiles/{profileId}/categories/{created.Id}",
            new { name = "  Home edited  ", comment = " updated ", version = ordinary.Version }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ordinaryEdit.StatusCode);

        using var editedResponse = await ownerClient.PutAsJsonAsync($"/api/budget-profiles/{profileId}/categories/{incomeId}",
            new { name = "  Earnings  ", comment = " recurring income ", version = ordinary.Version }, TestContext.Current.CancellationToken);
        // The expected version belongs to the category being edited, so fetch its authoritative version first.
        Assert.Equal(HttpStatusCode.Conflict, editedResponse.StatusCode);
        var income = Assert.Single(categories.Categories, category => category.Id == incomeId);
        using var systemEdit = await ownerClient.PutAsJsonAsync($"/api/budget-profiles/{profileId}/categories/{incomeId}",
            new { name = "  Earnings  ", comment = " recurring income ", version = income.Version }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, systemEdit.StatusCode);

        var saved = await ownerClient.GetFromJsonAsync<CategoriesResponse>($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        var ordinaryAfterEdit = Assert.Single(saved!.Categories, category => category.Id == created.Id);
        Assert.Equal("Home edited", ordinaryAfterEdit.Name);
        Assert.Equal("updated", ordinaryAfterEdit.Comment);
        Assert.Equal("ordinary", ordinaryAfterEdit.Kind);
        var renamed = Assert.Single(saved!.Categories, category => category.Id == incomeId);
        Assert.Equal("Earnings", renamed.Name);
        Assert.Equal("income", renamed.Kind);
        Assert.Equal("recurring income", renamed.Comment);
        Assert.NotEqual(income.Version, renamed.Version);

        var foreign = await ownerClient.GetFromJsonAsync<CategoriesResponse>($"/api/budget-profiles/{otherProfileId}/categories", TestContext.Current.CancellationToken);
        var foreignCategory = Assert.Single(foreign!.Categories, category => category.Id == foreignCategoryId);
        using var substituted = await ownerClient.PutAsJsonAsync($"/api/budget-profiles/{profileId}/categories/{foreignCategoryId}",
            new { name = "Cross-profile edit", comment = (string?)null, version = foreignCategory.Version }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, substituted.StatusCode);

        using var viewerCreate = await viewerClient.PostAsJsonAsync($"/api/budget-profiles/{profileId}/categories", new { name = "Blocked" }, TestContext.Current.CancellationToken);
        using var viewerEdit = await viewerClient.PutAsJsonAsync($"/api/budget-profiles/{profileId}/categories/{incomeId}",
            new { name = "Blocked", comment = (string?)null, version = renamed.Version }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, viewerCreate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, viewerEdit.StatusCode);

        await host.WithDatabaseAsync(async db =>
        {
            var records = await db.Categories.Where(category => category.ProfileId == profileId).ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(4, records.Length);
            Assert.Equal("ordinary", Assert.Single(records, record => record.Id == created.Id).Kind);
            Assert.Equal("income", Assert.Single(records, record => record.Id == incomeId).Kind);
            return true;
        });
    }

    [Fact]
    public async Task Invalid_category_metadata_is_rejected_and_stale_edit_preserves_saved_data()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var owner = await AddAccount(host);
        var profileId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profileId, OwnerAccountId = owner, Name = "Home", MainCurrencyCode = "GBP" });
            db.Categories.AddRange(
                new CategoryRecord { Id = categoryId, ProfileId = profileId, Name = "Income", Kind = "income" },
                new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Savings", Kind = "savings" },
                new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Debt", Kind = "debt" });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using var client = await SignedInClient(host, owner);
        var categories = await client.GetFromJsonAsync<CategoriesResponse>($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        var category = Assert.Single(categories!.Categories, item => item.Id == categoryId);
        foreach (var input in new object[]
        {
            new { name = " \t ", comment = (string?)null },
            new { name = new string('x', 101), comment = (string?)null },
            new { name = "Valid", comment = new string('x', 2001) }
        })
        {
            var invalid = JsonDocument.Parse(JsonSerializer.Serialize(input)).RootElement;
            using var response = await client.PutAsJsonAsync($"/api/budget-profiles/{profileId}/categories/{categoryId}",
                new { name = invalid.GetProperty("name").GetString(), comment = invalid.GetProperty("comment").GetString(), version = category.Version }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var firstEdit = await client.PutAsJsonAsync($"/api/budget-profiles/{profileId}/categories/{categoryId}",
            new { name = "First edit", comment = "saved", version = category.Version }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstEdit.StatusCode);
        using var staleEdit = await client.PutAsJsonAsync($"/api/budget-profiles/{profileId}/categories/{categoryId}",
            new { name = "Stale draft", comment = "must not overwrite", version = category.Version }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, staleEdit.StatusCode);
        var saved = await client.GetFromJsonAsync<CategoriesResponse>($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        var persisted = Assert.Single(saved!.Categories, item => item.Id == categoryId);
        Assert.Equal("First edit", persisted.Name);
        Assert.Equal("saved", persisted.Comment);
    }

    [Fact]
    public async Task Ordinary_category_deletion_retains_children_blocks_new_tracking_and_protects_system_categories()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var owner = await AddAccount(host);
        var editor = await AddAccount(host);
        var viewer = await AddAccount(host);
        var profileId = Guid.NewGuid();
        var ordinaryId = Guid.NewGuid();
        var incomeId = Guid.NewGuid();
        var savingsId = Guid.NewGuid();
        var debtId = Guid.NewGuid();
        var activeChildId = Guid.NewGuid();
        var deletedChildId = Guid.NewGuid();
        var oldChildTimestamp = DateTimeOffset.UtcNow.AddDays(-2);
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profileId, OwnerAccountId = owner, Name = "Home", MainCurrencyCode = "GBP" });
            db.Categories.AddRange(
                new CategoryRecord { Id = ordinaryId, ProfileId = profileId, Name = "Groceries", Kind = "ordinary" },
                new CategoryRecord { Id = incomeId, ProfileId = profileId, Name = "Wages", Kind = "income" },
                new CategoryRecord { Id = savingsId, ProfileId = profileId, Name = "Savings", Kind = "savings" },
                new CategoryRecord { Id = debtId, ProfileId = profileId, Name = "Debt", Kind = "debt" });
            db.Subcategories.AddRange(
                new SubcategoryRecord { Id = activeChildId, CategoryId = ordinaryId, Name = "Market", Comment = "retained" },
                new SubcategoryRecord { Id = deletedChildId, CategoryId = ordinaryId, Name = "Old market", TrackingStoppedAt = oldChildTimestamp });
            db.ProfileAccess.AddRange(
                new ProfileAccessRecord { ProfileId = profileId, AccountId = editor, Role = "editor" },
                new ProfileAccessRecord { ProfileId = profileId, AccountId = viewer, Role = "view_only" });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        using var ownerClient = await SignedInClient(host, owner);
        using var noProofClient = await SignedInClient(host, owner, includeAntiforgery: false);
        using var editorClient = await SignedInClient(host, editor);
        using var viewerClient = await SignedInClient(host, viewer);
        var before = await ownerClient.GetFromJsonAsync<JsonDocument>($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        var ordinary = Assert.Single(before!.RootElement.GetProperty("categories").EnumerateArray(), value => value.GetProperty("id").GetGuid() == ordinaryId);
        var systemCategories = before.RootElement.GetProperty("categories").EnumerateArray()
            .Where(value => value.GetProperty("kind").GetString() != "ordinary").ToArray();
        foreach (var systemCategory in systemCategories)
        {
            var systemId = systemCategory.GetProperty("id").GetGuid();
            using var beforeRenameDelete = await DeleteCategoryAsync(editorClient, profileId, systemId,
                systemCategory.GetProperty("version").GetString()!);
            Assert.Equal(HttpStatusCode.Conflict, beforeRenameDelete.StatusCode);
            using var rename = await editorClient.PutAsJsonAsync($"/api/budget-profiles/{profileId}/categories/{systemId}",
                new { name = $"Renamed {systemCategory.GetProperty("kind").GetString()}", comment = (string?)null, version = systemCategory.GetProperty("version").GetString() },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        }
        using var noProofDelete = await DeleteCategoryAsync(noProofClient, profileId, ordinaryId, ordinary.GetProperty("version").GetString()!);
        Assert.Equal(HttpStatusCode.BadRequest, noProofDelete.StatusCode);
        Assert.True(noProofDelete.Headers.CacheControl?.NoStore);
        using var viewerDelete = await DeleteCategoryAsync(viewerClient, profileId, ordinaryId, ordinary.GetProperty("version").GetString()!);
        Assert.Equal(HttpStatusCode.NotFound, viewerDelete.StatusCode);
        using var stopped = await DeleteCategoryAsync(ownerClient, profileId, ordinaryId, ordinary.GetProperty("version").GetString()!);
        Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);
        Assert.True(stopped.Headers.CacheControl?.NoStore);
        var renamed = await ownerClient.GetFromJsonAsync<JsonDocument>($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        foreach (var systemCategory in renamed!.RootElement.GetProperty("categories").EnumerateArray()
                     .Where(value => value.GetProperty("kind").GetString() != "ordinary"))
        {
            using var afterRenameDelete = await DeleteCategoryAsync(editorClient, profileId,
                systemCategory.GetProperty("id").GetGuid(), systemCategory.GetProperty("version").GetString()!);
            Assert.Equal(HttpStatusCode.Conflict, afterRenameDelete.StatusCode);
        }
        await host.WithDatabaseAsync(async db =>
        {
            await Assert.ThrowsAsync<SqlException>(async () => await db.Categories.Where(value => value.Id == incomeId)
                .ExecuteUpdateAsync(update => update.SetProperty(value => value.TrackingStoppedAt, DateTimeOffset.UtcNow),
                    TestContext.Current.CancellationToken));
            return true;
        });

        var after = await ownerClient.GetFromJsonAsync<JsonDocument>($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        var retainedCategory = Assert.Single(after!.RootElement.GetProperty("categories").EnumerateArray(), value => value.GetProperty("id").GetGuid() == ordinaryId);
        Assert.True(retainedCategory.GetProperty("trackingStoppedAt").ValueKind != System.Text.Json.JsonValueKind.Null);
        using var staleDelete = await DeleteCategoryAsync(editorClient, profileId, ordinaryId, ordinary.GetProperty("version").GetString()!);
        Assert.Equal(HttpStatusCode.Conflict, staleDelete.StatusCode);
        var firstTimestamp = retainedCategory.GetProperty("trackingStoppedAt").GetDateTimeOffset();
        using var repeatedDelete = await DeleteCategoryAsync(editorClient, profileId, ordinaryId, retainedCategory.GetProperty("version").GetString()!);
        Assert.Equal(HttpStatusCode.OK, repeatedDelete.StatusCode);
        var afterRepeat = await ownerClient.GetFromJsonAsync<JsonDocument>($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        retainedCategory = Assert.Single(afterRepeat!.RootElement.GetProperty("categories").EnumerateArray(), value => value.GetProperty("id").GetGuid() == ordinaryId);
        Assert.Equal(firstTimestamp, retainedCategory.GetProperty("trackingStoppedAt").GetDateTimeOffset());
        var children = retainedCategory.GetProperty("subcategories").EnumerateArray().ToArray();
        Assert.Equal(2, children.Length);
        var retainedActive = Assert.Single(children, value => value.GetProperty("id").GetGuid() == activeChildId);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, retainedActive.GetProperty("trackingStoppedAt").ValueKind);
        Assert.False(retainedActive.GetProperty("isAvailableForTracking").GetBoolean());
        Assert.Equal("retained", retainedActive.GetProperty("comment").GetString());
        var retainedDeleted = Assert.Single(children, value => value.GetProperty("id").GetGuid() == deletedChildId);
        Assert.Equal(oldChildTimestamp, retainedDeleted.GetProperty("trackingStoppedAt").GetDateTimeOffset());
        Assert.False(retainedDeleted.GetProperty("isAvailableForTracking").GetBoolean());

        using var blockedCreate = await ownerClient.PostAsJsonAsync($"/api/budget-profiles/{profileId}/categories/{ordinaryId}/subcategories",
            new { name = "Cannot add" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, blockedCreate.StatusCode);
        using var blockedEdit = await ownerClient.PutAsJsonAsync($"/api/budget-profiles/{profileId}/categories/{ordinaryId}/subcategories/{activeChildId}",
            new { name = "Cannot edit", comment = (string?)null, version = retainedActive.GetProperty("version").GetString() }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, blockedEdit.StatusCode);
        using var childDelete = await DeleteSubcategoryAsync(editorClient, profileId, ordinaryId, activeChildId,
            retainedActive.GetProperty("version").GetString()!);
        Assert.Equal(HttpStatusCode.OK, childDelete.StatusCode);

        await host.WithDatabaseAsync(async db =>
        {
            var parent = await db.Categories.SingleAsync(value => value.Id == ordinaryId, TestContext.Current.CancellationToken);
            Assert.Equal("ordinary", parent.Kind);
            Assert.Equal("Renamed income", (await db.Categories.SingleAsync(value => value.Id == incomeId, TestContext.Current.CancellationToken)).Name);
            Assert.NotNull((await db.Subcategories.SingleAsync(value => value.Id == activeChildId, TestContext.Current.CancellationToken)).TrackingStoppedAt);
            Assert.Equal(oldChildTimestamp, (await db.Subcategories.SingleAsync(value => value.Id == deletedChildId, TestContext.Current.CancellationToken)).TrackingStoppedAt);
            return true;
        });
    }

    [Fact]
    public async Task Concurrent_category_retirement_and_child_creation_leave_no_new_active_tracking()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var owner = await AddAccount(host);
        var profileId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profileId, OwnerAccountId = owner, Name = "Home", MainCurrencyCode = "GBP" });
            db.Categories.AddRange(
                new CategoryRecord { Id = categoryId, ProfileId = profileId, Name = "Groceries", Kind = "ordinary" },
                new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Income", Kind = "income" },
                new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Savings", Kind = "savings" },
                new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Debt", Kind = "debt" });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using var client = await SignedInClient(host, owner);
        using var loaded = await client.GetAsync($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await loaded.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var category = Assert.Single(document.RootElement.GetProperty("categories").EnumerateArray(), value => value.GetProperty("id").GetGuid() == categoryId);
        var version = category.GetProperty("version").GetString()!;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var createRequests = Enumerable.Range(0, 4).Select(async index =>
        {
            await gate.Task;
            return await client.PostAsJsonAsync($"/api/budget-profiles/{profileId}/categories/{categoryId}/subcategories",
                new { name = $"Child {index}" }, TestContext.Current.CancellationToken);
        }).ToArray();
        var deleteRequest = Task.Run(async () =>
        {
            await gate.Task;
            return await DeleteCategoryAsync(client, profileId, categoryId, version);
        }, TestContext.Current.CancellationToken);
        gate.SetResult();
        var creates = await Task.WhenAll(createRequests);
        using var deleted = await deleteRequest;
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.All(creates, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.NotFound }));

        using var finalResponse = await client.GetAsync($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        using var finalDocument = JsonDocument.Parse(await finalResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var finalCategory = Assert.Single(finalDocument.RootElement.GetProperty("categories").EnumerateArray(), value => value.GetProperty("id").GetGuid() == categoryId);
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, finalCategory.GetProperty("trackingStoppedAt").ValueKind);
        var children = finalCategory.GetProperty("subcategories").EnumerateArray().ToArray();
        Assert.Equal(creates.Count(response => response.StatusCode == HttpStatusCode.Created), children.Length);
        Assert.All(children, child =>
        {
            Assert.False(child.GetProperty("isAvailableForTracking").GetBoolean());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, child.GetProperty("trackingStoppedAt").ValueKind);
        });
        foreach (var response in creates) response.Dispose();
    }

    private static async Task<HttpResponseMessage> DeleteCategoryAsync(HttpClient client, Guid profileId, Guid categoryId, string version)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/budget-profiles/{profileId}/categories/{categoryId}")
        {
            Content = JsonContent.Create(new { version })
        };
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> DeleteSubcategoryAsync(HttpClient client, Guid profileId, Guid categoryId, Guid subcategoryId, string version)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/budget-profiles/{profileId}/categories/{categoryId}/subcategories/{subcategoryId}")
        {
            Content = JsonContent.Create(new { version })
        };
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> AddAccount(AccountTestHost host)
    {
        var id = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser { Id = id, UserName = id + "@example.test", Email = id + "@example.test", EmailConfirmed = true });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return id;
    }

    private static async Task<HttpClient> SignedInClient(AccountTestHost host, Guid accountId, bool includeAntiforgery = true)
    {
        using var scope = host.Services.CreateScope();
        var session = await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().CreateAsync(
            new BffSession(accountId, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddHours(1)),
            TestContext.Current.CancellationToken);
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost:7207"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + session);
        if (includeAntiforgery)
        {
            var token = await client.GetFromJsonAsync<AntiforgeryToken>("/api/auth/antiforgery", TestContext.Current.CancellationToken);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.RequestToken);
        }
        return client;
    }

    private sealed record AntiforgeryToken(string RequestToken);
    private sealed record CategoryMutationResponse(Guid Id);
    private sealed record CategoriesResponse(Guid ProfileId, string Role, IReadOnlyList<CategoryResponse> Categories);
    private sealed record CategoryResponse(Guid Id, string Name, string Kind, string? Comment, string Version);
}
