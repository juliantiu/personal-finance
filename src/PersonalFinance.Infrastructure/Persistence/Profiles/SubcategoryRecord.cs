namespace PersonalFinance.Infrastructure.Persistence.Profiles;

public sealed class SubcategoryRecord
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public byte[] Version { get; set; } = [];
}
