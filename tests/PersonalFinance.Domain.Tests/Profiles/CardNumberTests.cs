using PersonalFinance.Domain.Profiles;
using Xunit;

namespace PersonalFinance.Domain.Tests.Profiles;

public sealed class CardNumberTests
{
    [Theory]
    [InlineData("4111 1111-1111 1111", "4111111111111111")]
    [InlineData("000000000000", "000000000000")]
    [InlineData("0000000000000000000", "0000000000000000000")]
    [InlineData("079927398713", "079927398713")]
    public void Valid_numbers_are_canonical_text_with_masked_diagnostics(string input, string expected)
    {
        Assert.True(CardNumber.TryCreate(input, out var number));
        Assert.Equal(expected, number!.Value);
        Assert.Equal(expected[^4..], number.LastFour);
        Assert.Equal("•••• " + expected[^4..], number.ToString());
        Assert.DoesNotContain(expected, number.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" --- ")]
    [InlineData("00000000000")]
    [InlineData("00000000000000000000")]
    [InlineData("4111111111111112")]
    [InlineData("4111x11111111111")]
    [InlineData("•••• 1111")]
    [InlineData("４１１１１１１１１１１１１１１１")]
    [InlineData("٤١١١١١١١١١١١١١١١")]
    [InlineData("4111\t111111111111")]
    public void Invalid_input_has_no_value(string? input)
    {
        Assert.False(CardNumber.TryCreate(input, out var number));
        Assert.Null(number);
    }
}

