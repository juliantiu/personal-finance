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
        endpoints.MapPost("/api/budget-profiles", CreateAsync);
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
