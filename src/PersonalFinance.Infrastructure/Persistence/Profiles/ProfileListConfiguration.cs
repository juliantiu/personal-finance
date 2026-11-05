using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Accounts;

namespace PersonalFinance.Infrastructure.Persistence.Profiles;

internal static class ProfileListConfiguration
{
    internal static void ConfigureProfileList(this ModelBuilder builder)
    {
        builder.Entity<CurrencyRecord>(currency =>
        {
            currency.ToTable("Currencies");
            currency.HasKey(value => value.Code);
            currency.Property(value => value.Code).HasMaxLength(3);
            currency.Property(value => value.Name).HasMaxLength(100).IsRequired();
            currency.Property(value => value.Symbol).HasMaxLength(10).IsRequired();
            currency.HasData(
                new CurrencyRecord { Code = "GBP", Name = "Pound sterling", Symbol = "£" },
                new CurrencyRecord { Code = "USD", Name = "US dollar", Symbol = "$" });
        });
        builder.Entity<BudgetProfileRecord>(profile =>
        {
            profile.ToTable("BudgetProfiles");
            profile.HasKey(value => value.Id);
            profile.Property(value => value.Name).HasMaxLength(100).IsRequired();
            profile.Property(value => value.MainCurrencyCode).HasMaxLength(3).IsRequired();
            profile.HasOne<AccountIdentityUser>().WithMany().HasForeignKey(value => value.OwnerAccountId)
                .OnDelete(DeleteBehavior.NoAction);
            profile.HasOne<CurrencyRecord>().WithMany().HasForeignKey(value => value.MainCurrencyCode)
                .OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<ProfileAccessRecord>(access =>
        {
            access.ToTable("ProfileAccess", table => table.HasCheckConstraint("CK_ProfileAccess_Role", "[Role] IN ('editor', 'view_only')"));
            access.HasKey(value => new { value.ProfileId, value.AccountId });
            access.Property(value => value.Role).HasMaxLength(9).IsRequired();
            access.HasOne<BudgetProfileRecord>().WithMany().HasForeignKey(value => value.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            access.HasOne<AccountIdentityUser>().WithMany().HasForeignKey(value => value.AccountId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
