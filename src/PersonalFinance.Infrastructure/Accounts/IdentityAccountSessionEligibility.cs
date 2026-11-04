using Microsoft.AspNetCore.Identity;
using PersonalFinance.Application.Accounts;

namespace PersonalFinance.Infrastructure.Accounts;

// Comment ID: CLASS-023. Applies the account-exists and confirmed-email policy before BFF token use.
public sealed class IdentityAccountSessionEligibility(UserManager<AccountIdentityUser> userManager) : IAccountSessionEligibility
{
    // Comment ID: METHOD-039. Checks that an Identity account still exists and retains verified email status.
    public async Task<bool> IsActiveAsync(Guid accountId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(accountId.ToString());
        cancellationToken.ThrowIfCancellationRequested();
        return user is { EmailConfirmed: true };
    }
}
