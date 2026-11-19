using PersonalFinance.Application.Profiles;
using Xunit;

namespace PersonalFinance.Application.Tests.Profiles;

public sealed class RevealCardTests
{
    [Fact]
    public async Task Reveal_passes_current_account_and_exact_resource_before_decrypting()
    {
        var account = Guid.NewGuid(); var profile = Guid.NewGuid(); var method = Guid.NewGuid();
        var reader = new Reader(); var protection = new Protection();
        var result = await new RevealCardHandler(new Account(account), reader, protection).Handle(new(profile, method), TestContext.Current.CancellationToken);
        Assert.Equal((account, profile, method), reader.Seen);
        Assert.Equal((profile, method, "synthetic ciphertext"), protection.Seen);
        Assert.Equal(CardRevealStatus.Revealed, result.Status);
        Assert.Equal("4111111111111111", result.Number);
        Assert.DoesNotContain(result.Number!, result.ToString());
    }

    [Theory]
    [InlineData(CardRevealStatus.Forbidden)]
    [InlineData(CardRevealStatus.UnavailableMethod)]
    public async Task Denied_lookup_never_decrypts(CardRevealStatus status)
    {
        var protection = new Protection();
        var result = await new RevealCardHandler(new Account(Guid.NewGuid()), new Reader { Status = status }, protection)
            .Handle(new(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(status, result.Status);
        Assert.Null(result.Number);
        Assert.Null(protection.Seen);
    }

    [Fact]
    public async Task Anonymous_account_never_looks_up_or_decrypts()
    {
        var reader = new Reader(); var protection = new Protection();
        await Assert.ThrowsAsync<UnauthenticatedAccountException>(() => new RevealCardHandler(new Account(null), reader, protection)
            .Handle(new(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken).AsTask());
        Assert.Null(reader.Seen);
        Assert.Null(protection.Seen);
    }

    [Fact]
    public async Task Failed_decryption_is_safe_and_has_no_number()
    {
        var result = await new RevealCardHandler(new Account(Guid.NewGuid()), new Reader(), new Protection { Fail = true })
            .Handle(new(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(CardRevealStatus.Unavailable, result.Status);
        Assert.Null(result.Number);
    }

    [Fact]
    public async Task Failed_lookup_is_safe_and_never_decrypts()
    {
        var protection = new Protection();
        var result = await new RevealCardHandler(new Account(Guid.NewGuid()), new Reader { Fail = true }, protection)
            .Handle(new(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(CardRevealStatus.Unavailable, result.Status);
        Assert.Null(result.Number);
        Assert.Null(protection.Seen);
    }

    private sealed class Account(Guid? id) : ICurrentAccount
    {
        public Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken) => Task.FromResult(id);
    }
    private sealed class Reader : ICardRevealReader
    {
        public CardRevealStatus Status = CardRevealStatus.Revealed;
        public bool Fail;
        public (Guid, Guid, Guid)? Seen;
        public Task<CardRevealLookup> GetAsync(Guid accountId, Guid profileId, Guid methodId, CancellationToken cancellationToken)
        {
            if (Fail) throw new CardRevealUnavailableException();
            Seen = (accountId, profileId, methodId);
            return Task.FromResult(new CardRevealLookup(Status, "synthetic ciphertext"));
        }
    }
    private sealed class Protection : ICardRevealProtection
    {
        public bool Fail;
        public (Guid, Guid, string)? Seen;
        public string Unprotect(Guid profileId, Guid methodId, string ciphertext)
        {
            if (Fail) throw new CardRevealUnavailableException();
            Seen = (profileId, methodId, ciphertext);
            return "4111111111111111";
        }
    }
}
