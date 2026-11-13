namespace PersonalFinance.Domain.Profiles;

public sealed class PaymentMethodValidationException : Exception
{
    public string Field { get; }

    internal PaymentMethodValidationException(string field, string message) : base(message) => Field = field;
}
