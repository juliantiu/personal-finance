using Mediator;

namespace PersonalFinance.Application.Profiles;

// Comment ID: CLASS-133. Carries the name and stable kind of an initial profile category for the profile landing page.
public sealed record ProfileCategoryDetails(string Name, string Kind);
// Comment ID: CLASS-134. Carries a profile payment method's kind for the profile landing page.
public sealed record ProfilePaymentMethodDetails(string Kind);
// Comment ID: CLASS-135. Carries a profile tag's display name and optional system role for the profile landing page.
public sealed record ProfileTagDetails(string Name, string? Role);

// Comment ID: CLASS-136. Contains only the authorized profile settings, role, and initial setup records returned to a reader.
public sealed record AccessibleProfileDetails(
    Guid Id,
    string Name,
    string MainCurrency,
    IReadOnlyList<string> AdditionalCurrencies,
    string Role,
    IReadOnlyList<ProfileCategoryDetails> Categories,
    IReadOnlyList<ProfilePaymentMethodDetails> PaymentMethods,
    IReadOnlyList<ProfileTagDetails> Tags);

public interface IProfileDetailsReader
{
    // Comment ID: METHOD-141. Reads profile details only if the account currently owns the profile or has a read grant.
    Task<AccessibleProfileDetails?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken);
}

// Comment ID: CLASS-137. Identifies the profile requested by the current authenticated account.
public sealed record GetAccessibleProfileQuery(Guid ProfileId) : IQuery<AccessibleProfileDetails?>;

// Comment ID: CLASS-132. Resolves the current account and returns a profile only when current ownership or access grants permit reading it.
public sealed class GetAccessibleProfileHandler(ICurrentAccount currentAccount, IProfileDetailsReader profiles)
    : IQueryHandler<GetAccessibleProfileQuery, AccessibleProfileDetails?>
{
    // Comment ID: METHOD-140. Checks the current BFF account before asking the profile reader to evaluate current access.
    public async ValueTask<AccessibleProfileDetails?> Handle(GetAccessibleProfileQuery query, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        return await profiles.GetAsync(accountId, query.ProfileId, cancellationToken);
    }
}
