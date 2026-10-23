namespace PersonalFinance.Application.Common;

public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The request contains invalid input.")
{
    // Keep field messages, never the FluentValidation failure's attempted value or request object.
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
