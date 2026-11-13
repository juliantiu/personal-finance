namespace PersonalFinance.Domain.Profiles;

public enum PaymentMethodKind { Cash, CreditCard }

public sealed class PaymentMethod
{
    public const int MaximumNicknameLength = 100;
    public const string DefaultCashNickname = "Cash";
    public Guid Id { get; }
    public Guid ProfileId { get; }
    public PaymentMethodKind Kind { get; }
    public string? Nickname { get; }
    public CardNumber? Number { get; }

    private PaymentMethod(Guid profileId, PaymentMethodKind kind, string? nickname, CardNumber? number)
    {
        if (profileId == Guid.Empty) throw new ArgumentException("A profile is required.", nameof(profileId));
        Id = Guid.NewGuid();
        ProfileId = profileId;
        Kind = kind;
        Nickname = nickname;
        Number = number;
    }

    public static PaymentMethod CreateCash(Guid profileId) => new(profileId, PaymentMethodKind.Cash, DefaultCashNickname, null);

    public static PaymentMethod CreateCreditCard(Guid profileId, string? number, string? nickname = null)
    {
        var normalizedNickname = NormalizeNickname(nickname);
        if (!CardNumber.TryCreate(number, out var cardNumber))
            throw new PaymentMethodValidationException(nameof(Number), "Enter 12–19 digits with a valid card checksum. Spaces and hyphens are allowed.");
        return new(profileId, PaymentMethodKind.CreditCard, normalizedNickname, cardNumber);
    }

    private static string? NormalizeNickname(string? nickname)
    {
        var normalized = nickname?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        if (normalized.Length > MaximumNicknameLength)
            throw new PaymentMethodValidationException(nameof(Nickname), "Use at most 100 characters after trimming.");
        return normalized;
    }
}
