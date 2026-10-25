using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Accounts;

namespace PersonalFinance.Infrastructure.Persistence;

public sealed class AccountIdentityDbContext(DbContextOptions<AccountIdentityDbContext> options)
    : IdentityUserContext<AccountIdentityUser, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AccountIdentityUser>(user =>
        {
            user.ToTable("UserAccounts");
            user.HasIndex(account => account.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique();
        });
    }
}
