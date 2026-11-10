using Mediator;
using Microsoft.AspNetCore.Antiforgery;
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
        endpoints.MapPut("/api/budget-profiles/{profileId:guid}/currency-settings", UpdateCurrencySettingsAsync);
        endpoints.MapPost("/api/budget-profiles", CreateAsync);
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
                settings.ConfigurationVersion,
                settings.SupportedCurrencies.Select(currency =>
                    new SupportedCurrencyResponse(currency.Code, currency.Name, currency.Symbol)).ToArray()));
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign in to access this budget profile.");
        }
    }

    private static async Task<IResult> UpdateCurrencySettingsAsync(Guid profileId, UpdateProfileCurrencySettingsRequest request,
        IMediator mediator, ICurrentAccount currentAccount, IAntiforgery antiforgery, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            if (await currentAccount.GetAccountIdAsync(cancellationToken) is null) throw new UnauthenticatedAccountException();
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(statusCode: 400, title: "The currency settings request could not be validated. Refresh and try again.");
            }

            var result = await mediator.Send(new UpdateProfileCurrencySettingsCommand(
                profileId, request.AdditionalCurrencies, request.ConfigurationVersion), cancellationToken);
            return result switch
            {
                ProfileCurrencySettingsUpdateResult.Updated => Results.NoContent(),
                ProfileCurrencySettingsUpdateResult.NotFound => Results.NotFound(),
                ProfileCurrencySettingsUpdateResult.Forbidden => Results.Problem(statusCode: 403, title: "You can't change currency settings for this profile."),
                ProfileCurrencySettingsUpdateResult.Invalid => Results.Problem(statusCode: 400, title: "Additional currencies must be supported and different from the main currency."),
                ProfileCurrencySettingsUpdateResult.Conflict => Results.Problem(statusCode: 409, title: "Currency settings changed since you opened them. Reload the latest settings before saving."),
                _ => Results.Problem(statusCode: 500, title: "Currency settings could not be saved.")
            };
        }
        catch (UnauthenticatedAccountException)
        {
            return Results.Problem(statusCode: 401, title: "Sign in to change currency settings.");
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
                profile.Categories.Select(category => new ProfileCategoryDetailsResponse(category.Name, category.Kind)).ToArray(),
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
