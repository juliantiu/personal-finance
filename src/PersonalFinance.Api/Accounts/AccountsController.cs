using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Application.Accounts.ConfirmEmail;
using PersonalFinance.Application.Accounts.RegisterAccount;
using PersonalFinance.Application.Accounts.ResendConfirmation;
using PersonalFinance.Contracts.Accounts;

namespace PersonalFinance.Api.Accounts;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController(IMediator mediator, IAccountIdentity accountIdentity) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<AccountRequestAcknowledgement>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterAccountRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new RegisterAccountCommand(request.Email, request.Password, request.ConfirmPassword), cancellationToken);
        return Accepted(new AccountRequestAcknowledgement("Request accepted."));
    }

    [HttpPost("confirm-email")]
    [ProducesResponseType<ConfirmEmailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirm(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ConfirmEmailCommand(request.AccountId, request.Token), cancellationToken);
        return result == ConfirmEmailResult.Confirmed
            ? Ok(new ConfirmEmailResponse(true))
            : BadRequest(new ProblemDetails { Title = "Confirmation link is invalid or expired.", Status = StatusCodes.Status400BadRequest });
    }

    [HttpPost("resend-confirmation")]
    [ProducesResponseType<AccountRequestAcknowledgement>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Resend(ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ResendConfirmationCommand(request.Email), cancellationToken);
        return Accepted(new AccountRequestAcknowledgement("Request accepted."));
    }

    [HttpPost("sign-in")]
    [EnableRateLimiting("SignIn")]
    // Comment ID: METHOD-010. Returns the same generic unauthorized response for every unsuccessful sign-in.
    public async Task<IActionResult> SignIn(SignInRequest request, CancellationToken cancellationToken)
    {
        var accepted = await accountIdentity.SignInAsync(request.Email, request.Password, cancellationToken);
        return accepted
            ? Ok(new AccountRequestAcknowledgement("Sign-in accepted."))
            : Unauthorized(new ProblemDetails { Title = "Sign-in failed. Check your details and try again.", Status = StatusCodes.Status401Unauthorized });
    }
}
