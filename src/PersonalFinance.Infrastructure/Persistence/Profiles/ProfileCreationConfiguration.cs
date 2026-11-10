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
            category.ToTable("Categories", table => table.HasCheckConstraint("CK_Categories_Kind", "[Kind] IN ('income', 'savings', 'debt', 'ordinary')"));
            category.HasKey(value => value.Id);
            category.Property(value => value.Name).HasMaxLength(100).IsRequired();
            category.Property(value => value.Comment).HasMaxLength(2000);
            category.Property(value => value.Kind).HasMaxLength(8).IsRequired();
            category.Property(value => value.Version).IsRowVersion();
            category.HasIndex(value => new { value.ProfileId, value.Kind }).IsUnique().HasFilter("[Kind] IN ('income', 'savings', 'debt')");
            category.HasOne<BudgetProfileRecord>().WithMany().HasForeignKey(value => value.ProfileId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<PaymentMethodRecord>(method =>
        {
            method.ToTable("PaymentMethods", table => table.HasCheckConstraint("CK_PaymentMethods_Kind", "[Kind] = 'cash'"));
            method.HasKey(value => value.Id);
            method.Property(value => value.Kind).HasMaxLength(4).IsRequired();
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
