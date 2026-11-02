namespace PersonalFinance.Infrastructure.Persistence;

// Comment ID: CLASS-004. Represents a persisted BFF session with a protected payload and searchable lifecycle metadata.
public sealed class BffSessionEntity
{
    public required string Id { get; set; }
    public required Guid AccountId { get; set; }
    public required byte[] ProtectedPayload { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
}
