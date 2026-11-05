using Mediator;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Contracts.Profiles;

namespace PersonalFinance.Api.Profiles;
public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/api/budget-profiles", ListAsync);

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
