using PersonalFinance.Client.Accounts;
using Xunit;

namespace PersonalFinance.Client.Tests.Accounts;

public sealed class SafeReturnPathTests
{
    [Theory]
    [InlineData("/transactions?period=2026-10", "/transactions?period=2026-10")]
    [InlineData("/budget-profiles", "/budget-profiles")]
    public void Accepts_same_origin_absolute_paths(string input, string expected)
    {
        Assert.Equal(expected, SafeReturnPath.Validate(input, new Uri("https://client.example.test/")));
    }

    [Theory]
    [InlineData("https://evil.example.test/path")]
    [InlineData("//evil.example.test/path")]
    [InlineData("\\\\evil.example.test\\path")]
    [InlineData("/%2f%2fevil.example.test")]
    [InlineData("/%5cevil.example.test")]
    [InlineData("transactions")]
    [InlineData("")]
    public void Rejects_non_local_or_malformed_paths(string input)
    {
        Assert.Null(SafeReturnPath.Validate(input, new Uri("https://client.example.test/")));
    }
}
