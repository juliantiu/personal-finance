using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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
            Assert.All(categories, category => { Assert.Null(category.Comment); Assert.Equal(8, category.Version.Length); });
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
