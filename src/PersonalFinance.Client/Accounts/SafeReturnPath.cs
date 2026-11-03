namespace PersonalFinance.Client.Accounts;

// Comment ID: CLASS-006. Validates local navigation targets used after authentication.
public static class SafeReturnPath
{
    // Comment ID: METHOD-011. Accepts only same-origin absolute paths and rejects URI forms that can escape local routing.
    public static string? Validate(string? value, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] != '/' || value.StartsWith("//", StringComparison.Ordinal) ||
            value.Contains('\\') || value.Any(char.IsControl) ||
            value.Contains("%2f", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("%5c", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!Uri.TryCreate(baseUri, value, out var target) ||
            !string.Equals(target.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(target.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            target.Port != baseUri.Port)
            return null;

        return target.PathAndQuery + target.Fragment;
    }
}
