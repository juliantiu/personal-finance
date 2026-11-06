using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence.Profiles;

namespace PersonalFinance.Infrastructure.Persistence;

public sealed class AccountIdentityDbContext(DbContextOptions<AccountIdentityDbContext> options)
    : IdentityUserContext<AccountIdentityUser, Guid>(options)
{
    public DbSet<BffSessionEntity> BffSessions => Set<BffSessionEntity>();
    public DbSet<AuthenticationTicketEntity> AuthenticationTickets => Set<AuthenticationTicketEntity>();
    public DbSet<BudgetProfileRecord> BudgetProfiles => Set<BudgetProfileRecord>();
    public DbSet<ProfileAccessRecord> ProfileAccess => Set<ProfileAccessRecord>();
    public DbSet<CurrencyRecord> Currencies => Set<CurrencyRecord>();
    public DbSet<ProfileCurrencyRecord> ProfileCurrencies => Set<ProfileCurrencyRecord>();
    public DbSet<CategoryRecord> Categories => Set<CategoryRecord>();
    public DbSet<PaymentMethodRecord> PaymentMethods => Set<PaymentMethodRecord>();
    public DbSet<TagRecord> Tags => Set<TagRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureProfileList();
        builder.ConfigureProfileCreation();

        builder.UseOpenIddict();

        builder.Entity<AccountIdentityUser>(user =>
        {
            user.ToTable("UserAccounts");
            user.HasIndex(account => account.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique();
        });

        builder.Entity<BffSessionEntity>(session =>
        {
            session.ToTable("BffSessions");
            session.HasKey(value => value.Id);
            session.Property(value => value.Id).HasMaxLength(64);
            session.Property(value => value.ProtectedPayload).IsRequired();
            session.HasIndex(value => value.ExpiresAt);
            session.HasOne<AccountIdentityUser>()
                .WithMany()
                .HasForeignKey(value => value.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuthenticationTicketEntity>(ticket =>
        {
            ticket.ToTable("AuthenticationTickets");
            ticket.HasKey(value => value.Id);
            ticket.Property(value => value.Id).HasMaxLength(64);
            ticket.Property(value => value.ProtectedPayload).IsRequired();
            ticket.HasIndex(value => value.ExpiresAt);
        });
    }
}
