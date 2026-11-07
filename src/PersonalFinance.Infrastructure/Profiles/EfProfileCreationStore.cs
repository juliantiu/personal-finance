using PersonalFinance.Application.Profiles;
using PersonalFinance.Domain.Profiles;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence.Profiles;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfProfileCreationStore(AccountIdentityDbContext database) : IProfileCreationStore
{
    public async Task SaveAsync(BudgetProfile profile, CancellationToken cancellationToken)
    {
        database.BudgetProfiles.Add(new BudgetProfileRecord
        { Id = profile.Id, OwnerAccountId = profile.OwnerAccountId, Name = profile.Name, MainCurrencyCode = profile.MainCurrency });
        database.ProfileCurrencies.AddRange(profile.AdditionalCurrencies.Select(currency =>
            new ProfileCurrencyRecord { ProfileId = profile.Id, CurrencyCode = currency }));
        database.Categories.AddRange(profile.Categories.Select(category => new CategoryRecord
        { Id = category.Id, ProfileId = profile.Id, Name = category.Name, Kind = category.Kind.ToString().ToLowerInvariant() }));
        database.PaymentMethods.Add(new PaymentMethodRecord { Id = profile.CashPaymentMethodId, ProfileId = profile.Id, Kind = "cash" });
        database.Tags.AddRange(profile.Tags.Select(tag => new TagRecord
        { Id = tag.Id, ProfileId = profile.Id, Name = tag.Name, SystemRole = tag.Role == SystemTagRole.Iou ? "iou" : "iou_repayment" }));
        await database.SaveChangesAsync(cancellationToken);
    }
}
