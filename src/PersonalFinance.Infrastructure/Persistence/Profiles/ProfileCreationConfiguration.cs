using Microsoft.EntityFrameworkCore;

namespace PersonalFinance.Infrastructure.Persistence.Profiles;

internal static class ProfileCreationConfiguration
{
    internal static void ConfigureProfileCreation(this ModelBuilder builder)
    {
        builder.Entity<ProfileCurrencyRecord>(currency =>
        {
            currency.ToTable("ProfileCurrencies");
            currency.HasKey(value => new { value.ProfileId, value.CurrencyCode });
            currency.Property(value => value.CurrencyCode).HasMaxLength(3);
            currency.HasOne<BudgetProfileRecord>().WithMany().HasForeignKey(value => value.ProfileId).OnDelete(DeleteBehavior.Cascade);
            currency.HasOne<CurrencyRecord>().WithMany().HasForeignKey(value => value.CurrencyCode).OnDelete(DeleteBehavior.NoAction);
        });
        builder.Entity<CategoryRecord>(category =>
        {
            category.ToTable("Categories", table => table.HasCheckConstraint("CK_Categories_Kind", "[Kind] IN ('income', 'savings', 'debt')"));
            category.HasKey(value => value.Id);
            category.Property(value => value.Name).HasMaxLength(100).IsRequired();
            category.Property(value => value.Kind).HasMaxLength(7).IsRequired();
            category.HasIndex(value => new { value.ProfileId, value.Kind }).IsUnique().HasFilter("[Kind] IN ('income', 'savings', 'debt')");
            category.HasOne<BudgetProfileRecord>().WithMany().HasForeignKey(value => value.ProfileId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<PaymentMethodRecord>(method =>
        {
            method.ToTable("PaymentMethods", table =>
            {
                table.HasCheckConstraint("CK_PaymentMethods_Kind", "[Kind] IN ('cash', 'credit_card')");
                table.HasCheckConstraint("CK_PaymentMethods_CardShape", "([Kind] = 'cash' AND [ProtectedNumber] IS NULL AND [NumberFingerprint] IS NULL AND [MatchingKeyId] IS NULL AND [LastFour] IS NULL AND [RetiredAt] IS NULL) OR ([Kind] = 'credit_card' AND [ProtectedNumber] IS NOT NULL AND DATALENGTH([ProtectedNumber]) > 0 AND [NumberFingerprint] IS NOT NULL AND DATALENGTH([NumberFingerprint]) = 32 AND [MatchingKeyId] IS NOT NULL AND LEN([MatchingKeyId]) = 64 AND [LastFour] IS NOT NULL AND DATALENGTH([LastFour]) = 8 AND [LastFour] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%')");
            });
            method.HasKey(value => value.Id);
            method.Property(value => value.Kind).HasMaxLength(11).IsRequired();
            method.Property(value => value.Nickname).HasMaxLength(100);
            method.Property(value => value.NumberFingerprint).HasMaxLength(32);
            method.Property(value => value.MatchingKeyId).HasMaxLength(64);
            method.Property(value => value.LastFour).HasMaxLength(4);
            method.Property(value => value.Version).IsRowVersion();
            method.HasIndex(value => new { value.ProfileId, value.NumberFingerprint }).IsUnique().HasFilter("[Kind] = 'credit_card'");
            method.HasIndex(value => new { value.ProfileId, value.Kind }).IsUnique().HasFilter("[Kind] = 'cash'");
            method.HasOne<BudgetProfileRecord>().WithMany().HasForeignKey(value => value.ProfileId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<TagRecord>(tag =>
        {
            tag.ToTable("Tags", table => table.HasCheckConstraint("CK_Tags_SystemRole", "[SystemRole] IS NULL OR [SystemRole] IN ('iou', 'iou_repayment')"));
            tag.HasKey(value => value.Id);
            tag.Property(value => value.Name).HasMaxLength(100).IsRequired();
            tag.Property(value => value.SystemRole).HasMaxLength(13);
            tag.HasIndex(value => new { value.ProfileId, value.SystemRole }).IsUnique().HasFilter("[SystemRole] IS NOT NULL");
            tag.HasOne<BudgetProfileRecord>().WithMany().HasForeignKey(value => value.ProfileId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
