using System.Net;
using System.Net.Http.Json;
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

public sealed class ProfileSubcategoryTests
{
    [Fact]
    public async Task Participants_review_subcategories_and_writers_create_edit_and_reject_stale_or_foreign_changes()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var owner = await AddAccount(host);
        var editor = await AddAccount(host);
        var viewer = await AddAccount(host);
        var profileId = Guid.NewGuid();
        var otherProfileId = Guid.NewGuid();
        var categories = new[]
        {
            new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Income", Kind = "income" },
            new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Savings", Kind = "savings" },
            new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Debt", Kind = "debt" },
            new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Other", Kind = "ordinary" },
            new CategoryRecord { Id = Guid.NewGuid(), ProfileId = otherProfileId, Name = "Foreign", Kind = "ordinary" }
        };
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.AddRange(
                new BudgetProfileRecord { Id = profileId, OwnerAccountId = owner, Name = "Home", MainCurrencyCode = "GBP" },
                new BudgetProfileRecord { Id = otherProfileId, OwnerAccountId = owner, Name = "Other", MainCurrencyCode = "GBP" });
            db.Categories.AddRange(categories);
            db.ProfileAccess.AddRange(
                new ProfileAccessRecord { ProfileId = profileId, AccountId = editor, Role = "editor" },
                new ProfileAccessRecord { ProfileId = profileId, AccountId = viewer, Role = "view_only" });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        using var ownerClient = await SignedInClient(host, owner);
        using var editorClient = await SignedInClient(host, editor);
        using var viewerClient = await SignedInClient(host, viewer);
        using var noProofClient = await SignedInClient(host, owner, includeAntiforgery: false);
        using var noProof = await noProofClient.PostAsJsonAsync(
            $"/api/budget-profiles/{profileId}/categories/{categories[0].Id}/subcategories", new { name = "No proof" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, noProof.StatusCode);
        Assert.True(noProof.Headers.CacheControl?.NoStore);

        var childIds = new Dictionary<Guid, Guid>();
        foreach (var category in categories.Where(category => category.ProfileId == profileId))
        {
            foreach (var invalidInput in new object[]
            {
                new { name = " \t ", comment = (string?)null },
                new { name = new string('x', 101), comment = (string?)null },
                new { name = "Valid", comment = new string('x', 2001) }
            })
            {
                using var invalid = await editorClient.PostAsJsonAsync(
                    $"/api/budget-profiles/{profileId}/categories/{category.Id}/subcategories", invalidInput, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            }
            var name = category.Kind + " child";
            using var createdResponse = await editorClient.PostAsJsonAsync(
                $"/api/budget-profiles/{profileId}/categories/{category.Id}/subcategories",
                new { name = $"  {name}  ", comment = " line one\nline two ", categoryId = categories[^1].Id, profileId = otherProfileId },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
            Assert.True(createdResponse.Headers.CacheControl?.NoStore);
            var created = await createdResponse.Content.ReadFromJsonAsync<CreatedResponse>(TestContext.Current.CancellationToken);
            childIds[category.Id] = created!.Id;
        }
        using var duplicateNameResponse = await editorClient.PostAsJsonAsync(
            $"/api/budget-profiles/{profileId}/categories/{categories[0].Id}/subcategories",
            new { name = "income child", comment = (string?)null }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, duplicateNameResponse.StatusCode);
        var duplicateName = await duplicateNameResponse.Content.ReadFromJsonAsync<CreatedResponse>(TestContext.Current.CancellationToken);
        Assert.NotEqual(childIds[categories[0].Id], duplicateName!.Id);

        await host.WithDatabaseAsync(async db =>
        {
            Assert.Equal(5, await db.Subcategories.CountAsync(TestContext.Current.CancellationToken));
            var savingsChild = childIds[categories[1].Id];
            var plan = await db.SavingsPlans.SingleAsync(value => value.SubcategoryId == savingsChild, TestContext.Current.CancellationToken);
            Assert.Null(plan.GoalAmount);
            Assert.Null(plan.ResetNote);
            Assert.Equal(1, await db.SavingsPlans.CountAsync(TestContext.Current.CancellationToken));
            return true;
        });

        var saved = await ownerClient.GetFromJsonAsync<CategoriesResponse>(
            $"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        using var readResponse = await ownerClient.GetAsync($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        Assert.True(readResponse.Headers.CacheControl?.NoStore);
        foreach (var category in saved!.Categories)
        {
            var child = Assert.Single(category.Subcategories, value => value.Id == childIds[category.Id]);
            Assert.Equal(category.Id, child.CategoryId);
            Assert.Equal("line one\nline two", child.Comment);
            if (category.Id == categories[0].Id)
            {
                var duplicate = Assert.Single(category.Subcategories, value => value.Id == duplicateName.Id);
                Assert.Equal(child.Name, duplicate.Name);
            }
        }
        var viewerRead = await viewerClient.GetFromJsonAsync<CategoriesResponse>(
            $"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        Assert.Equal(5, viewerRead!.Categories.Sum(category => category.Subcategories.Count));

        var savingsCategory = Assert.Single(saved.Categories, category => category.Kind == "savings");
        var savingsChildResponse = Assert.Single(savingsCategory.Subcategories);
        using var updated = await editorClient.PutAsJsonAsync(
            $"/api/budget-profiles/{profileId}/categories/{savingsCategory.Id}/subcategories/{savingsChildResponse.Id}",
            new { name = "Holiday", comment = "planned", version = savingsChildResponse.Version }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var stale = await editorClient.PutAsJsonAsync(
            $"/api/budget-profiles/{profileId}/categories/{savingsCategory.Id}/subcategories/{savingsChildResponse.Id}",
            new { name = "Stale", comment = "overwrite", version = savingsChildResponse.Version }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var foreignParent = await editorClient.PostAsJsonAsync(
            $"/api/budget-profiles/{profileId}/categories/{categories[^1].Id}/subcategories", new { name = "Foreign" }, TestContext.Current.CancellationToken);
        using var missingParent = await editorClient.PostAsJsonAsync(
            $"/api/budget-profiles/{profileId}/categories/{Guid.NewGuid()}/subcategories", new { name = "Missing" }, TestContext.Current.CancellationToken);
        using var viewerWrite = await viewerClient.PostAsJsonAsync(
            $"/api/budget-profiles/{profileId}/categories/{categories[0].Id}/subcategories", new { name = "Blocked" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignParent.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingParent.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, viewerWrite.StatusCode);

        var after = await ownerClient.GetFromJsonAsync<CategoriesResponse>(
            $"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        var savedChild = Assert.Single(Assert.Single(after!.Categories, category => category.Kind == "savings").Subcategories);
        Assert.Equal("Holiday", savedChild.Name);
        Assert.Equal("planned", savedChild.Comment);
    }

    [Fact]
    public async Task Savings_subcategory_and_empty_plan_roll_back_together_when_plan_insert_fails()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var owner = await AddAccount(host);
        var profileId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profileId, OwnerAccountId = owner, Name = "Home", MainCurrencyCode = "GBP" });
            db.Categories.Add(new CategoryRecord { Id = categoryId, ProfileId = profileId, Name = "Savings", Kind = "savings" });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        await host.WithDatabaseAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER [FailSavingsPlanInsert] ON [SavingsPlans] AFTER INSERT AS THROW 51000, 'Synthetic SavingsPlan failure', 1;",
                TestContext.Current.CancellationToken);
            return true;
        });

        using var client = await SignedInClient(host, owner);
        var failed = false;
        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/budget-profiles/{profileId}/categories/{categoryId}/subcategories", new { name = "Emergency" }, TestContext.Current.CancellationToken);
            failed = response.StatusCode == HttpStatusCode.InternalServerError;
        }
        catch (HttpRequestException) { failed = true; }
        Assert.True(failed);
        await host.WithDatabaseAsync(async db =>
        {
            Assert.Empty(await db.Subcategories.ToArrayAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await db.SavingsPlans.ToArrayAsync(TestContext.Current.CancellationToken));
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER [FailSavingsPlanInsert];", TestContext.Current.CancellationToken);
            return true;
        });
    }

    [Fact]
    public async Task Subcategory_migration_preserves_existing_profiles_and_categories()
    {
        await using var host = new AccountTestHost();
        await host.WithDatabaseAsync(async db =>
        {
            await db.GetService<IMigrator>().MigrateAsync("20261110180556_AddCategoryMetadata", TestContext.Current.CancellationToken);
            return true;
        });
        var owner = await AddAccount(host);
        var profileId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profileId, OwnerAccountId = owner, Name = "Existing", MainCurrencyCode = "GBP" });
            db.Categories.Add(new CategoryRecord { Id = categoryId, ProfileId = profileId, Name = "Savings", Kind = "savings" });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await host.MigrateAsync();

        await host.WithDatabaseAsync(async db =>
        {
            Assert.Equal("Existing", (await db.BudgetProfiles.SingleAsync(value => value.Id == profileId, TestContext.Current.CancellationToken)).Name);
            var category = await db.Categories.SingleAsync(value => value.Id == categoryId, TestContext.Current.CancellationToken);
            Assert.Equal("Savings", category.Name);
            Assert.Empty(await db.Subcategories.ToArrayAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await db.SavingsPlans.ToArrayAsync(TestContext.Current.CancellationToken));
            return true;
        });
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
    private sealed record CreatedResponse(Guid Id);
    private sealed record CategoriesResponse(Guid ProfileId, string Role, IReadOnlyList<CategoryResponse> Categories);
    private sealed record CategoryResponse(Guid Id, string Name, string Kind, string? Comment, string Version, IReadOnlyList<SubcategoryResponse> Subcategories);
    private sealed record SubcategoryResponse(Guid Id, Guid CategoryId, string Name, string? Comment, string Version);
}
