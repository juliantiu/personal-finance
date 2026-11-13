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
        endpoints.MapGet("/api/budget-profiles/{profileId:guid}/payment-methods", ListPaymentMethodsAsync);
        endpoints.MapPost("/api/budget-profiles", CreateAsync);
        endpoints.MapPost("/api/budget-profiles/{profileId:guid}/payment-methods", SaveCardAsync);
    }

    private static async Task<IResult> SaveCardAsync(Guid profileId, SaveCardRequest request, IMediator mediator,
        ICurrentAccount currentAccount, IAntiforgery antiforgery, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            if (await currentAccount.GetAccountIdAsync(cancellationToken) is null) throw new UnauthenticatedAccountException();
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            { return Results.Problem(statusCode: 400, title: "The request could not be validated. Refresh and try again."); }
            var result = await mediator.Send(new SaveCardCommand(profileId, request.Number, request.Nickname), cancellationToken);
            return result.Status switch
            {
                CardSaveStatus.Saved => Results.Created($"/api/budget-profiles/{profileId}/payment-methods/{result.Method!.Id}", ToResponse(result.Method)),
                CardSaveStatus.UnavailableProfile => Results.NotFound(),
                CardSaveStatus.Forbidden => Results.Problem(statusCode: 403, title: "You don't have permission to save cards in this profile."),
                CardSaveStatus.Duplicate => Results.ValidationProblem(new Dictionary<string, string[]> { ["Number"] = ["This card is already saved in this profile."] }, statusCode: 409),
                _ => Results.Problem(statusCode: 503, title: "Card saving is temporarily unavailable. Refresh payment methods before retrying.")
            };
        }
        catch (UnauthenticatedAccountException) { return Results.Problem(statusCode: 401, title: "Sign in to save a card."); }
    }

    private static ProfilePaymentMethodDetailsResponse ToResponse(ProfilePaymentMethodDetails method)
        => new(method.Id, method.Kind, method.MaskedIdentifier, method.Version is null ? null : Convert.ToBase64String(method.Version), method.IsRetired, method.Nickname);

    private static async Task<IResult> ListPaymentMethodsAsync(Guid profileId, IMediator mediator, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            var methods = await mediator.Send(new ListPaymentMethodsQuery(profileId), cancellationToken);
            if (methods is null) return Results.NotFound();
            return Results.Ok(new ProfilePaymentMethodsResponse(methods.ProfileId, methods.ProfileName, methods.Role,
                methods.Methods.Select(ToResponse).ToArray()));
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
                profile.Categories.Select(category => new ProfileCategoryDetailsResponse(category.Name, category.Kind)).ToArray(),
                profile.PaymentMethods.Select(ToResponse).ToArray(),
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
