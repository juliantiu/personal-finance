namespace PersonalFinance.Infrastructure.Persistence;

// Comment ID: CLASS-011. Stores short-lived encrypted Identity tickets behind opaque cookie identifiers.
public sealed class AuthenticationTicketEntity
{
    public required string Id { get; set; }
    public required byte[] ProtectedPayload { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
}
