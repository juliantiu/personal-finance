namespace PersonalFinance.Infrastructure.Persistence.Profiles;

public sealed class PaymentMethodRecord
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? ProtectedNumber { get; set; }
    public byte[]? NumberFingerprint { get; set; }
    public string? MatchingKeyId { get; set; }
    public string? LastFour { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public byte[] Version { get; set; } = [];
}
