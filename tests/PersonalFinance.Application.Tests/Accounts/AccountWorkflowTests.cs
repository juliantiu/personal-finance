using FluentValidation;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Application.Accounts.ConfirmEmail;
using PersonalFinance.Application.Accounts.RegisterAccount;
using PersonalFinance.Application.Accounts.ResendConfirmation;
using PersonalFinance.Application.Common;
using Xunit;

namespace PersonalFinance.Application.Tests.Accounts;

public sealed class AccountWorkflowTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static readonly RegisterAccountCommand ValidRegistration =
        new("person@example.test", "abcdefghijkl", "abcdefghijkl");

    [Fact]
    public async Task Registration_creates_account_then_sends_its_confirmation()
    {
        var identity = new FakeIdentity();
        var sender = new FakeEmailSender();
        var result = await new RegisterAccountHandler(identity, sender).Handle(ValidRegistration, Cancellation);

        Assert.Same(AccountRequestAccepted.Instance, result);
        Assert.Equal((ValidRegistration.Email, ValidRegistration.Password), Assert.Single(identity.Creations));
        Assert.Same(identity.Confirmation, Assert.Single(sender.Messages));
        Assert.Equal(Cancellation, identity.LastCancellation);
        Assert.Equal(Cancellation, sender.LastCancellation);
    }

    [Fact]
    public async Task Duplicate_registration_returns_same_result_without_sending_a_link()
    {
        var sender = new FakeEmailSender();
        var result = await new RegisterAccountHandler(new FakeIdentity { Confirmation = null }, sender)
            .Handle(ValidRegistration, Cancellation);

        Assert.Same(AccountRequestAccepted.Instance, result);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Delivery_failure_preserves_created_account_and_generic_result()
    {
        var identity = new FakeIdentity();
        var result = await new RegisterAccountHandler(identity, new FakeEmailSender { FailDelivery = true })
            .Handle(ValidRegistration, Cancellation);

        Assert.Single(identity.Creations);
        Assert.Same(AccountRequestAccepted.Instance, result);
    }

    [Fact]
    public async Task Registration_does_not_disguise_unexpected_sender_errors_as_success()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new RegisterAccountHandler(new FakeIdentity(), new FakeEmailSender { UnexpectedError = true })
                .Handle(ValidRegistration, Cancellation));
    }

    [Fact]
    public async Task Registration_propagates_cancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new RegisterAccountHandler(new FakeIdentity(), new FakeEmailSender())
                .Handle(ValidRegistration, source.Token));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resend_does_not_reveal_whether_an_unverified_account_exists(bool eligible)
    {
        var identity = new FakeIdentity();
        if (!eligible) identity.Confirmation = null;
        var sender = new FakeEmailSender();

        var result = await new ResendConfirmationHandler(identity, sender)
            .Handle(new(ValidRegistration.Email), Cancellation);

        Assert.Same(AccountRequestAccepted.Instance, result);
        Assert.Equal(eligible ? 1 : 0, sender.Messages.Count);
        Assert.Equal(ValidRegistration.Email, Assert.Single(identity.ResendEmails));
        Assert.Empty(identity.Creations);
    }

    [Fact]
    public async Task Resend_delivery_failure_keeps_the_same_generic_result()
    {
        var result = await new ResendConfirmationHandler(new FakeIdentity(), new FakeEmailSender { FailDelivery = true })
            .Handle(new(ValidRegistration.Email), Cancellation);

        Assert.Same(AccountRequestAccepted.Instance, result);
    }

    [Theory]
    [InlineData(true, ConfirmEmailResult.Confirmed)]
    [InlineData(false, ConfirmEmailResult.InvalidOrExpired)]
    public async Task Confirmation_uses_identity_token_validation(bool valid, ConfirmEmailResult expected)
    {
        var identity = new FakeIdentity { ValidToken = valid };
        var command = new ConfirmEmailCommand(Guid.NewGuid(), "opaque-test-token");

        var result = await new ConfirmEmailHandler(identity).Handle(command, Cancellation);

        Assert.Equal(expected, result);
        Assert.Equal((command.AccountId, command.Token), Assert.Single(identity.Confirmations));
        Assert.Equal(Cancellation, identity.LastCancellation);
    }

    [Fact]
    public async Task Validation_stops_invalid_registration_before_account_creation()
    {
        var handlerCalled = false;
        var pipeline = new ValidationBehavior<RegisterAccountCommand, AccountRequestAccepted>([new RegisterAccountValidator()]);

        var error = await Assert.ThrowsAsync<RequestValidationException>(async () =>
            await pipeline.Handle(new("invalid-email", "short", "different"), (_, _) =>
            {
                handlerCalled = true;
                return ValueTask.FromResult(AccountRequestAccepted.Instance);
            }, Cancellation));

        Assert.False(handlerCalled);
        Assert.Contains("Email", error.Errors.Keys);
        Assert.Contains("Password", error.Errors.Keys);
        Assert.Contains("ConfirmPassword", error.Errors.Keys);
        Assert.DoesNotContain("short", error.ToString());
        Assert.DoesNotContain("different", string.Join(" ", error.Errors.Values.SelectMany(value => value)));
    }

    [Fact]
    public async Task Validation_passes_valid_registration_to_the_handler()
    {
        var identity = new FakeIdentity();
        var handler = new RegisterAccountHandler(identity, new FakeEmailSender());
        var pipeline = new ValidationBehavior<RegisterAccountCommand, AccountRequestAccepted>([new RegisterAccountValidator()]);

        await pipeline.Handle(ValidRegistration, handler.Handle, Cancellation);

        Assert.Single(identity.Creations);
    }

    [Fact]
    public async Task Confirmation_requires_account_and_token()
    {
        var result = await new ConfirmEmailValidator().ValidateAsync(new ConfirmEmailCommand(Guid.Empty, ""), Cancellation);

        Assert.Contains(result.Errors, error => error.PropertyName == "AccountId");
        Assert.Contains(result.Errors, error => error.PropertyName == "Token");
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    public async Task Resend_rejects_invalid_email(string email)
    {
        var result = await new ResendConfirmationValidator().ValidateAsync(new ResendConfirmationCommand(email), Cancellation);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Account_messages_do_not_print_personal_details_or_tokens()
    {
        var id = Guid.NewGuid();
        Assert.Equal(nameof(EmailConfirmation), new EmailConfirmation(id, "person@example.test", "secret").ToString());
        Assert.Equal(nameof(ConfirmEmailCommand), new ConfirmEmailCommand(id, "secret").ToString());
        Assert.Equal(nameof(ResendConfirmationCommand), new ResendConfirmationCommand("person@example.test").ToString());
    }

    private sealed class FakeIdentity : IAccountIdentity
    {
        public EmailConfirmation? Confirmation { get; set; } = new(Guid.NewGuid(), "person@example.test", "opaque-test-token");
        public bool ValidToken { get; set; }
        public List<(string Email, string Password)> Creations { get; } = [];
        public List<string> ResendEmails { get; } = [];
        public List<(Guid AccountId, string Token)> Confirmations { get; } = [];
        public CancellationToken LastCancellation { get; private set; }

        public Task<EmailConfirmation?> CreateAccountAsync(string email, string password, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCancellation = cancellationToken;
            Creations.Add((email, password));
            return Task.FromResult(Confirmation);
        }

        public Task<EmailConfirmation?> CreateEmailConfirmationAsync(string email, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCancellation = cancellationToken;
            ResendEmails.Add(email);
            return Task.FromResult(Confirmation);
        }

        public Task<bool> ConfirmEmailAsync(Guid accountId, string token, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCancellation = cancellationToken;
            Confirmations.Add((accountId, token));
            return Task.FromResult(ValidToken);
        }
    }

    private sealed class FakeEmailSender : IConfirmationEmailSender
    {
        public bool FailDelivery { get; init; }
        public bool UnexpectedError { get; init; }
        public List<EmailConfirmation> Messages { get; } = [];
        public CancellationToken LastCancellation { get; private set; }

        public Task SendAsync(EmailConfirmation confirmation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCancellation = cancellationToken;
            if (FailDelivery) throw new ConfirmationEmailDeliveryException();
            if (UnexpectedError) throw new InvalidOperationException("Synthetic sender failure.");
            Messages.Add(confirmation);
            return Task.CompletedTask;
        }
    }
}
