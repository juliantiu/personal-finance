using Microsoft.EntityFrameworkCore;

namespace PersonalFinance.Infrastructure.Persistence.Profiles;

internal static class ProfileSubcategoryConfiguration
{
    internal static void ConfigureProfileSubcategories(this ModelBuilder builder)
    {
        builder.Entity<SubcategoryRecord>(subcategory =>
        {
            subcategory.ToTable("Subcategories");
            subcategory.HasKey(value => value.Id);
            subcategory.Property(value => value.Name).HasMaxLength(100).IsRequired();
            subcategory.Property(value => value.Comment).HasMaxLength(2000);
            subcategory.Property(value => value.TrackingStoppedAt).HasColumnType("datetimeoffset");
            subcategory.Property(value => value.Version).IsRowVersion();
            subcategory.HasOne<CategoryRecord>().WithMany().HasForeignKey(value => value.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<SavingsPlanRecord>(plan =>
        {
            plan.ToTable("SavingsPlans");
            plan.HasKey(value => value.SubcategoryId);
            plan.HasOne<SubcategoryRecord>().WithOne().HasForeignKey<SavingsPlanRecord>(value => value.SubcategoryId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
