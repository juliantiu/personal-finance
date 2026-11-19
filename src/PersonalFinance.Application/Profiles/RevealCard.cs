using Mediator;

namespace PersonalFinance.Application.Profiles;

public sealed record RevealCardCommand(Guid ProfileId, Guid MethodId) : ICommand<CardRevealResult>;

public enum CardRevealStatus { Revealed, UnavailableMethod, Forbidden, Unavailable }

public sealed class CardRevealResult(CardRevealStatus status, string? number = null)
{
    public CardRevealStatus Status { get; } = status;
    public string? Number { get; } = number;
}

public sealed class CardRevealLookup(CardRevealStatus status, string? ciphertext = null)
{
    public CardRevealStatus Status { get; } = status;
    public string? Ciphertext { get; } = ciphertext;
}

public interface ICardRevealReader
{
    Task<CardRevealLookup> GetAsync(Guid accountId, Guid profileId, Guid methodId, CancellationToken cancellationToken);
}

public interface ICardRevealProtection
{
    string Unprotect(Guid profileId, Guid methodId, string ciphertext);
}

public sealed class CardRevealUnavailableException() : Exception("Card reveal is temporarily unavailable.");

public sealed class RevealCardHandler(ICurrentAccount account, ICardRevealReader reader, ICardRevealProtection protection)
    : ICommandHandler<RevealCardCommand, CardRevealResult>
{
    public async ValueTask<CardRevealResult> Handle(RevealCardCommand command, CancellationToken cancellationToken)
    {
        var accountId = await account.GetAccountIdAsync(cancellationToken) ?? throw new UnauthenticatedAccountException();
        try
        {
            var lookup = await reader.GetAsync(accountId, command.ProfileId, command.MethodId, cancellationToken);
            if (lookup.Status != CardRevealStatus.Revealed) return new(lookup.Status);
            if (lookup.Ciphertext is null) return new(CardRevealStatus.Unavailable);
            cancellationToken.ThrowIfCancellationRequested();
            return new(CardRevealStatus.Revealed, protection.Unprotect(command.ProfileId, command.MethodId, lookup.Ciphertext));
        }
        catch (CardRevealUnavailableException) { return new(CardRevealStatus.Unavailable); }
    }
}
