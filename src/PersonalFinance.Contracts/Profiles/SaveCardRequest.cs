namespace PersonalFinance.Contracts.Profiles;

public sealed class SaveCardRequest
{
    public string? Number { get; init; }
    public string? Nickname { get; init; }
}
