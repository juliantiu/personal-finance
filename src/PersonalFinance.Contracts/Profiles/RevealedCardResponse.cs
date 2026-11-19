namespace PersonalFinance.Contracts.Profiles;

public sealed class RevealedCardResponse(string number)
{
    public string Number { get; } = number;
}
