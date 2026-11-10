namespace PersonalFinance.Infrastructure.Persistence.Profiles;

public sealed class CategoryRecord
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string Kind { get; set; } = string.Empty;
    public byte[] Version { get; set; } = [];
}
