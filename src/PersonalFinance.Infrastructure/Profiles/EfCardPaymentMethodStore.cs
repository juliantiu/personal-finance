using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Domain.Profiles;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence.Profiles;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfCardPaymentMethodStore(AccountIdentityDbContext database) : ICardPaymentMethodStore
{
    public async Task<string?> GetRoleAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken)
    {
        try
        {
            return await database.BudgetProfiles.Where(profile => profile.Id == profileId)
            .Select(profile => profile.OwnerAccountId == accountId ? "owner" : database.ProfileAccess
                .Where(access => access.ProfileId == profileId && access.AccountId == accountId)
                .Select(access => access.Role).FirstOrDefault())
            .SingleOrDefaultAsync(cancellationToken);
        }
        catch (SqlException) { throw new CardSavingUnavailableException(); }
    }

    public async Task<CardSaveResult> SaveAsync(Guid accountId, PaymentMethod method, ProtectedCard card, CancellationToken cancellationToken)
    {
        if (method.Kind != PaymentMethodKind.CreditCard)
            throw new ArgumentException("The card store accepts only credit-card payment methods.", nameof(method));
        try { return await SaveCoreAsync(accountId, method, card, cancellationToken); }
        catch (Exception error) when (error is DbUpdateException or SqlException)
        { throw new CardSavingUnavailableException(); }
    }

    private async Task<CardSaveResult> SaveCoreAsync(Guid accountId, PaymentMethod method, ProtectedCard card, CancellationToken cancellationToken)
    {
        var profileId = method.ProfileId;
        var methodId = method.Id;
        var nickname = method.Nickname;
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // A database-owned transaction lock also prevents mismatched key configurations from racing past readiness checks.
        await database.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'PersonalFinance.CardWrites', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51000, 'Card write unavailable.', 1;", cancellationToken);
        var role = await GetRoleAsync(accountId, profileId, cancellationToken);
        if (role is null) return new(CardSaveStatus.UnavailableProfile);
        if (role is not ("owner" or "editor")) return new(CardSaveStatus.Forbidden);
        if (await database.PaymentMethods.AnyAsync(existing => existing.Kind == "credit_card" && existing.MatchingKeyId != card.KeyId, cancellationToken))
            return new(CardSaveStatus.Unavailable);
        if (await database.PaymentMethods.AnyAsync(existing => existing.ProfileId == profileId && existing.NumberFingerprint == card.Fingerprint, cancellationToken))
            return new(CardSaveStatus.Duplicate);
        var record = new PaymentMethodRecord
        {
            Id = methodId, ProfileId = profileId, Kind = "credit_card", ProtectedNumber = card.Ciphertext,
            NumberFingerprint = card.Fingerprint, MatchingKeyId = card.KeyId, LastFour = card.LastFour, Nickname = nickname
        };
        database.PaymentMethods.Add(record);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException sql &&
            sql.Number is 2601 or 2627 && sql.Message.Contains("IX_PaymentMethods_ProfileId_NumberFingerprint", StringComparison.Ordinal))
        {
            database.Entry(record).State = EntityState.Detached;
            return new(CardSaveStatus.Duplicate);
        }
        return new(CardSaveStatus.Saved, new(methodId, "credit_card", "•••• " + card.LastFour, record.Version, false, nickname));
    }
}
