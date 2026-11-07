namespace PersonalFinance.Infrastructure.Persistence.Profiles;

public sealed class ProfileAccessRecord
{
    public Guid ProfileId { get; set; }
    public Guid AccountId { get; set; }
    public string Role { get; set; } = string.Empty;
}
