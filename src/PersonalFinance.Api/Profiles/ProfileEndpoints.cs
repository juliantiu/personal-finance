using Mediator;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Contracts.Profiles;

namespace PersonalFinance.Api.Profiles;
public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/budget-profiles", ListAsync);
        endpoints.MapGet("/api/budget-profiles/{profileId:guid}", GetAsync);
        endpoints.MapGet("/api/budget-profiles/{profileId:guid}/currency-settings", GetCurrencySettingsAsync);
        endpoints.MapGet("/api/budget-profiles/{profileId:guid}/categories", GetCategoriesAsync);
        endpoints.MapPost("/api/budget-profiles/{profileId:guid}/categories", CreateCategoryAsync);
        endpoints.MapPut("/api/budget-profiles/{profileId:guid}/categories/{categoryId:guid}", UpdateCategoryAsync);
        endpoints.MapDelete("/api/budget-profiles/{profileId:guid}/categories/{categoryId:guid}", StopTrackingCategoryAsync);
        endpoints.MapPost("/api/budget-profiles/{profileId:guid}/categories/{categoryId:guid}/subcategories", CreateSubcategoryAsync);
        endpoints.MapPut("/api/budget-profiles/{profileId:guid}/categories/{categoryId:guid}/subcategories/{subcategoryId:guid}", UpdateSubcategoryAsync);
        endpoints.MapDelete("/api/budget-profiles/{profileId:guid}/categories/{categoryId:guid}/subcategories/{subcategoryId:guid}", StopTrackingSubcategoryAsync);
        endpoints.MapPost("/api/budget-profiles", CreateAsync);
    }

    private static async Task<IResult> GetCategoriesAsync(Guid profileId, IMediator mediator, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            var categories = await mediator.Send(new GetProfileCategoriesQuery(profileId), cancellationToken);
            if (categories is null) return Results.NotFound();
            return Results.Ok(new ProfileCategoriesResponse(categories.ProfileId, categories.Role,
                categories.Categories.Select(category => new ProfileCategoryResponse(category.Id, category.Name, category.Kind,
                    category.Comment, Convert.ToBase64String(category.Version), category.TrackingStoppedAt, category.Subcategories.Select(subcategory =>
                        new ProfileSubcategoryResponse(subcategory.Id, subcategory.CategoryId, subcategory.Name,
                            subcategory.Comment, subcategory.TrackingStoppedAt, subcategory.IsAvailableForTracking,
                            Convert.ToBase64String(subcategory.Version))).ToArray())).ToArray()));
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign in to access this budget profile.");
        }
    }

    private static async Task<IResult> StopTrackingCategoryAsync(Guid profileId, Guid categoryId,
        [FromBody] StopTrackingCategoryRequest request, IMediator mediator, ICurrentAccount currentAccount,
        IAntiforgery antiforgery, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            if (await currentAccount.GetAccountIdAsync(cancellationToken) is null) throw new UnauthenticatedAccountException();
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(statusCode: 400, title: "The category request could not be validated. Refresh the page and try again.");
            }
            var result = await mediator.Send(new StopTrackingProfileCategoryCommand(profileId, categoryId, request.Version), cancellationToken);
            return result switch
            {
                StopTrackingProfileCategoryResult.Stopped => Results.Ok(),
                StopTrackingProfileCategoryResult.Protected => Results.Conflict(new { title = "System categories cannot stop tracking." }),
                StopTrackingProfileCategoryResult.Conflict => Results.Conflict(new { title = "This category changed since you opened it. Reload the latest category before deleting it." }),
                _ => Results.NotFound()
            };
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: 401, title: "Sign in to manage profile categories.");
        }
    }

    private static async Task<IResult> CreateCategoryAsync(Guid profileId, CreateProfileCategoryRequest request, IMediator mediator,
        ICurrentAccount currentAccount, IAntiforgery antiforgery, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            if (await currentAccount.GetAccountIdAsync(cancellationToken) is null) throw new UnauthenticatedAccountException();
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(statusCode: 400, title: "The category request could not be validated. Refresh the page and try again.");
            }
            var id = await mediator.Send(new CreateProfileCategoryCommand(profileId, request.Name, request.Comment), cancellationToken);
            return id is null ? Results.NotFound() : Results.Created($"/api/budget-profiles/{profileId}/categories/{id}", new CreatedProfileCategoryResponse(id.Value));
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: 401, title: "Sign in to manage profile categories.");
        }
    }

    private static async Task<IResult> UpdateCategoryAsync(Guid profileId, Guid categoryId, UpdateProfileCategoryRequest request,
        IMediator mediator, ICurrentAccount currentAccount, IAntiforgery antiforgery, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            if (await currentAccount.GetAccountIdAsync(cancellationToken) is null) throw new UnauthenticatedAccountException();
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(statusCode: 400, title: "The category request could not be validated. Refresh the page and try again.");
            }
            var result = await mediator.Send(new UpdateProfileCategoryCommand(profileId, categoryId,
                request.Name, request.Comment, request.Version), cancellationToken);
            return result switch
            {
                UpdateProfileCategoryResult.Updated => Results.Ok(),
                UpdateProfileCategoryResult.Conflict => Results.Conflict(new { title = "This category changed since you opened it. Your draft is still available; reload the latest category before saving again." }),
                _ => Results.NotFound()
            };
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: 401, title: "Sign in to manage profile categories.");
        }
    }

    private static async Task<IResult> CreateSubcategoryAsync(Guid profileId, Guid categoryId,
        CreateProfileSubcategoryRequest request, IMediator mediator, ICurrentAccount currentAccount,
        IAntiforgery antiforgery, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            if (await currentAccount.GetAccountIdAsync(cancellationToken) is null) throw new UnauthenticatedAccountException();
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(statusCode: 400, title: "The subcategory request could not be validated. Refresh the page and try again.");
            }
            var id = await mediator.Send(new CreateProfileSubcategoryCommand(profileId, categoryId, request.Name, request.Comment), cancellationToken);
            return id is null ? Results.NotFound() : Results.Created(
                $"/api/budget-profiles/{profileId}/categories/{categoryId}/subcategories/{id}", new CreatedProfileSubcategoryResponse(id.Value));
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: 401, title: "Sign in to manage profile subcategories.");
        }
    }

    private static async Task<IResult> UpdateSubcategoryAsync(Guid profileId, Guid categoryId, Guid subcategoryId,
        UpdateProfileSubcategoryRequest request, IMediator mediator, ICurrentAccount currentAccount,
        IAntiforgery antiforgery, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            if (await currentAccount.GetAccountIdAsync(cancellationToken) is null) throw new UnauthenticatedAccountException();
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(statusCode: 400, title: "The subcategory request could not be validated. Refresh the page and try again.");
            }
            var result = await mediator.Send(new UpdateProfileSubcategoryCommand(profileId, categoryId, subcategoryId,
                request.Name, request.Comment, request.Version), cancellationToken);
            return result switch
            {
                UpdateProfileSubcategoryResult.Updated => Results.Ok(),
                UpdateProfileSubcategoryResult.Conflict => Results.Conflict(new { title = "This subcategory changed since you opened it. Your draft is still available; reload the latest subcategory before saving again." }),
                _ => Results.NotFound()
            };
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: 401, title: "Sign in to manage profile subcategories.");
        }
    }

    private static async Task<IResult> StopTrackingSubcategoryAsync(Guid profileId, Guid categoryId, Guid subcategoryId,
        [FromBody] StopTrackingSubcategoryRequest request, IMediator mediator, ICurrentAccount currentAccount,
        IAntiforgery antiforgery, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            if (await currentAccount.GetAccountIdAsync(cancellationToken) is null) throw new UnauthenticatedAccountException();
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(statusCode: 400, title: "The subcategory request could not be validated. Refresh the page and try again.");
            }
            var result = await mediator.Send(new StopTrackingProfileSubcategoryCommand(profileId, categoryId, subcategoryId,
                request.Version), cancellationToken);
            return result switch
            {
                StopTrackingProfileSubcategoryResult.Stopped => Results.Ok(),
                StopTrackingProfileSubcategoryResult.Conflict => Results.Conflict(new { title = "This subcategory changed since you opened it. Reload the latest subcategory before deleting it." }),
                _ => Results.NotFound()
            };
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: 401, title: "Sign in to manage profile subcategories.");
        }
    }

    private static async Task<IResult> GetCurrencySettingsAsync(Guid profileId, IMediator mediator, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            var settings = await mediator.Send(new GetProfileCurrencySettingsQuery(profileId), cancellationToken);
            if (settings is null) return Results.NotFound();
            return Results.Ok(new ProfileCurrencySettingsResponse(
                settings.ProfileId,
                settings.MainCurrency,
                settings.AdditionalCurrencies,
                settings.Role,
                settings.SupportedCurrencies.Select(currency =>
                    new SupportedCurrencyResponse(currency.Code, currency.Name, currency.Symbol)).ToArray()));
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign in to access this budget profile.");
        }
    }

    private static async Task<IResult> GetAsync(Guid profileId, IMediator mediator, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            var profile = await mediator.Send(new GetAccessibleProfileQuery(profileId), cancellationToken);
            if (profile is null) return Results.NotFound();
            return Results.Ok(new ProfileDetailsResponse(
                profile.Id,
                profile.Name,
                profile.MainCurrency,
                profile.AdditionalCurrencies,
                profile.Role,
                profile.Categories.Select(category => new ProfileCategoryDetailsResponse(category.Id, category.Name, category.Kind)).ToArray(),
                profile.PaymentMethods.Select(method => new ProfilePaymentMethodDetailsResponse(method.Kind)).ToArray(),
                profile.Tags.Select(tag => new ProfileTagDetailsResponse(tag.Name, tag.Role)).ToArray()));
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign in to access this budget profile.");
        }
    }

    private static async Task<IResult> CreateAsync(CreateProfileRequest request, IMediator mediator, ICurrentAccount currentAccount,
        IAntiforgery antiforgery, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            if (await currentAccount.GetAccountIdAsync(cancellationToken) is null) throw new UnauthenticatedAccountException();
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(statusCode: 400, title: "The creation request could not be validated. Refresh the page and try again.");
            }
            var id = await mediator.Send(new CreateProfileCommand(request.Name, request.MainCurrency, request.AdditionalCurrencies), cancellationToken);
            return Results.Created($"/api/budget-profiles/{id}", new CreatedProfileResponse(id));
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: 401, title: "Sign in to create a budget profile.");
        }
    }

    private static async Task<IResult> ListAsync(IMediator mediator, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            var profiles = await mediator.Send(new ListAccessibleProfilesQuery(), cancellationToken);
            return Results.Ok(profiles.Select(profile => new ProfileListItem(profile.Id, profile.Name, profile.MainCurrency, profile.Role)));
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign in to access budget profiles.");
        }
    }
}
