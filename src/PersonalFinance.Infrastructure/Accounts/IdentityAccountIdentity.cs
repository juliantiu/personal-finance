using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Accounts;

namespace PersonalFinance.Infrastructure.Accounts;

public sealed class IdentityAccountIdentity(UserManager<AccountIdentityUser> userManager) : IAccountIdentity
{
    public async Task<EmailConfirmation?> CreateAccountAsync(
        string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = new AccountIdentityUser { Id = Guid.NewGuid(), UserName = email, Email = email };
        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // The database index closes the race between Identity's duplicate lookup and insert.
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
                return null;

            throw new AccountIdentityOperationException();
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        cancellationToken.ThrowIfCancellationRequested();
        return new EmailConfirmation(user.Id, user.Email!, token);
    }

    public async Task<EmailConfirmation?> CreateEmailConfirmationAsync(
        string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email);
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null || user.EmailConfirmed)
            return null;

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        cancellationToken.ThrowIfCancellationRequested();
        return new EmailConfirmation(user.Id, user.Email!, token);
    }

    public async Task<bool> ConfirmEmailAsync(Guid accountId, string token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(accountId.ToString());
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null)
            return false;

        var result = await userManager.ConfirmEmailAsync(user, token);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Succeeded;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
}
