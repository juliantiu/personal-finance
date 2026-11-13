using Mediator;
using FluentValidation;
using PersonalFinance.Application.Common;
using PersonalFinance.Domain.Profiles;

namespace PersonalFinance.Application.Profiles;

public sealed class SaveCardCommand(Guid profileId, string? number, string? nickname = null) : ICommand<CardSaveResult>
{
    public Guid ProfileId { get; } = profileId;
    public string? Number { get; } = number;
    public string? Nickname { get; } = nickname;
}

public enum CardSaveStatus { Saved, UnavailableProfile, Forbidden, Duplicate, Unavailable }

public sealed record CardSaveResult(CardSaveStatus Status, ProfilePaymentMethodDetails? Method = null);

public sealed class ProtectedCard(string ciphertext, byte[] fingerprint, string keyId, string lastFour)
{
    public string Ciphertext { get; } = ciphertext;
    public byte[] Fingerprint { get; } = fingerprint;
    public string KeyId { get; } = keyId;
    public string LastFour { get; } = lastFour;
}

public interface ICardNumberProtection
{
    ProtectedCard Protect(Guid profileId, Guid methodId, CardNumber number);
}

public interface ICardPaymentMethodStore
{
    Task<string?> GetRoleAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken);
    Task<CardSaveResult> SaveAsync(Guid accountId, PaymentMethod method, ProtectedCard card, CancellationToken cancellationToken);
}

public sealed class CardSavingUnavailableException() : Exception("Card saving is temporarily unavailable.");

public sealed class SaveCardHandler(ICurrentAccount account, ICardPaymentMethodStore store, ICardNumberProtection protection)
    : ICommandHandler<SaveCardCommand, CardSaveResult>
{
    public async ValueTask<CardSaveResult> Handle(SaveCardCommand command, CancellationToken cancellationToken)
    {
        var accountId = await account.GetAccountIdAsync(cancellationToken) ?? throw new UnauthenticatedAccountException();
        try
        {
            var role = await store.GetRoleAsync(accountId, command.ProfileId, cancellationToken);
            if (role is null) return new(CardSaveStatus.UnavailableProfile);
            if (role is not ("owner" or "editor")) return new(CardSaveStatus.Forbidden);
            // Validate sensitive request shape here, after authorization, rather than in the pre-handler pipeline.
            var inputValidator = new InlineValidator<SaveCardCommand>();
            inputValidator.RuleFor(request => request.Number).NotEmpty().WithMessage("Enter a card number.");
            var input = await inputValidator.ValidateAsync(command, cancellationToken);
            if (!input.IsValid)
                throw new RequestValidationException(new Dictionary<string, string[]> { ["Number"] = input.Errors.Select(error => error.ErrorMessage).ToArray() });
            var method = PaymentMethod.CreateCreditCard(command.ProfileId, command.Number, command.Nickname);
            var card = protection.Protect(method.ProfileId, method.Id, method.Number!);
            return await store.SaveAsync(accountId, method, card, cancellationToken);
        }
        catch (PaymentMethodValidationException error)
        { throw new RequestValidationException(new Dictionary<string, string[]> { [error.Field] = [error.Message] }); }
        catch (CardSavingUnavailableException) { return new(CardSaveStatus.Unavailable); }
    }
}
