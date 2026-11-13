namespace PersonalFinance.Domain.Profiles;

public sealed class CardNumber
{
    public string Value { get; }
    public string LastFour => Value[^4..];

    private CardNumber(string value) => Value = value;

    public static bool TryCreate(string? input, out CardNumber? number)
    {
        number = null;
        if (input is null) return false;
        var canonical = input.Replace(" ", "").Replace("-", "");
        if (canonical.Length is < 12 or > 19 || canonical.Any(character => character is < '0' or > '9')) return false;
        var sum = 0;
        for (var index = canonical.Length - 1; index >= 0; index--)
        {
            var digit = canonical[index] - '0';
            if ((canonical.Length - 1 - index) % 2 == 1)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
        }
        if (sum % 10 != 0) return false;
        number = new CardNumber(canonical);
        return true;
    }

    public override string ToString() => "•••• " + LastFour;
}
