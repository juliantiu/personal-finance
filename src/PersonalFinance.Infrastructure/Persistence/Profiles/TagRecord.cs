namespace PersonalFinance.Infrastructure.Persistence.Profiles;

public sealed class TagRecord
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SystemRole { get; set; }
}
