using PersonalFinance.Application.Accounts.RegisterAccount;
using Xunit;

namespace PersonalFinance.Application.Tests.Accounts;

public sealed class RegisterAccountValidationTests
{
    private readonly RegisterAccountValidator validator = new();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-an-email")]
    [InlineData("person@")]
    [InlineData("@example.test")]
    public async Task Invalid_email_is_rejected(string email)
    {
        var result = await validator.ValidateAsync(new RegisterAccountCommand(email, "abcdefghijkl", "abcdefghijkl"), TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.PropertyName == "Email");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abcdefghijk")]
    public async Task Password_shorter_than_twelve_characters_is_rejected(string password)
    {
        var result = await validator.ValidateAsync(new RegisterAccountCommand("person@example.test", password, password), TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.PropertyName == "Password");
    }

    [Fact]
    public async Task Password_confirmation_must_match()
    {
        var result = await validator.ValidateAsync(
            new RegisterAccountCommand("person@example.test", "abcdefghijkl", "different-password"), TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.PropertyName == "ConfirmPassword");
    }

    [Theory]
    [InlineData("abcdefghijkl")]
    [InlineData("123456789012")]
    [InlineData("a long passphrase")]
    public async Task Twelve_or_more_characters_need_no_character_classes(string password)
    {
        var result = await validator.ValidateAsync(new RegisterAccountCommand("person@example.test", password, password), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Command_string_does_not_expose_credentials_or_email()
    {
        var command = new RegisterAccountCommand("person@example.test", "abcdefghijkl", "abcdefghijkl");

        Assert.Equal(nameof(RegisterAccountCommand), command.ToString());
    }
}
