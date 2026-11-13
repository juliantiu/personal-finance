namespace PersonalFinance.Domain.Profiles;

public sealed class BudgetProfile
{
    public const int MaximumNameLength = 100;
    public Guid Id { get; private init; }
    public Guid OwnerAccountId { get; private init; }
    public string Name { get; private init; } = string.Empty;
    public string MainCurrency { get; private init; } = string.Empty;
    public IReadOnlyList<string> AdditionalCurrencies { get; private init; } = [];
    public IReadOnlyList<InitialCategory> Categories { get; private init; } = [];
    public Guid CashPaymentMethodId => CashPaymentMethod.Id;
    public PaymentMethod CashPaymentMethod { get; private init; } = null!;
    public IReadOnlyList<InitialTag> Tags { get; private init; } = [];

    private BudgetProfile() { }

    public static bool IsSupportedCurrency(string? currency) => currency is "GBP" or "USD";

    public static BudgetProfile Create(Guid ownerAccountId, string? name, string? mainCurrency, IReadOnlyList<string>? additionalCurrencies)
    {
        if (ownerAccountId == Guid.Empty) throw new ArgumentException("An original owner is required.", nameof(ownerAccountId));
        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > MaximumNameLength)
            throw new ArgumentException("Enter a profile name of up to 100 characters.", nameof(name));
        if (!IsSupportedCurrency(mainCurrency))
            throw new ArgumentException("Select GBP or USD as the main currency.", nameof(mainCurrency));
        var additional = additionalCurrencies?.ToArray() ?? [];
        if (additional.Any(currency => !IsSupportedCurrency(currency) || currency == mainCurrency) || additional.Distinct().Count() != additional.Length)
            throw new ArgumentException("Additional currencies must be supported, distinct, and different from the main currency.", nameof(additionalCurrencies));

        var profileId = Guid.NewGuid();
        return new BudgetProfile
        {
            Id = profileId, OwnerAccountId = ownerAccountId, Name = trimmedName, MainCurrency = mainCurrency!,
            AdditionalCurrencies = Array.AsReadOnly(additional),
            Categories = Array.AsReadOnly(new[]
            {
                new InitialCategory(Guid.NewGuid(), "Income", SystemCategoryKind.Income),
                new InitialCategory(Guid.NewGuid(), "Savings", SystemCategoryKind.Savings),
                new InitialCategory(Guid.NewGuid(), "Debt", SystemCategoryKind.Debt)
            }),
            CashPaymentMethod = PaymentMethod.CreateCash(profileId),
            Tags = Array.AsReadOnly(new[]
            {
                new InitialTag(Guid.NewGuid(), "IOU", SystemTagRole.Iou),
                new InitialTag(Guid.NewGuid(), "IOU REPAYMENT", SystemTagRole.IouRepayment)
            })
        };
    }
}
