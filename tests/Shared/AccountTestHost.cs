using System.Collections.Concurrent;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Contracts.Accounts;
using PersonalFinance.Infrastructure.Email;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Tests.Support;

// Linked into the integration and browser projects; never uses developer connection strings.
public sealed class AccountTestHost(string clientOrigin = "http://localhost:5173") : WebApplicationFactory<Program>
{
    private readonly string databaseName = "PersonalFinance_UC001_Test_" + Guid.NewGuid().ToString("N");
    public CapturedEmailTransport Email { get; } = new();
    public bool MissingDatabase { get; init; }
    public bool MissingEmail { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Identity", MissingDatabase ? "" :
            $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true");
        builder.UseSetting("Email:ClientBaseUrl", clientOrigin + "/");
        builder.UseSetting("Email:SenderAddress", MissingEmail ? "" : "sender@example.test");
        builder.UseSetting("Email:Smtp4Dev:Host", "127.0.0.1");
        builder.UseSetting("Email:Smtp4Dev:Port", "2525");
        builder.UseSetting("Email:Smtp4Dev:SecureSocketOptions", "None");
        builder.UseSetting("Email:Smtp4Dev:Username", "");
        builder.UseSetting("Email:Smtp4Dev:Password", "");
        builder.UseSetting("Cors:AllowedOrigins:0", clientOrigin);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailTransport>();
            services.AddSingleton<IEmailTransport>(Email);
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        });
    }

    public async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AccountIdentityDbContext>().Database.MigrateAsync();
    }

    public async Task<T> WithDatabaseAsync<T>(Func<AccountIdentityDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AccountIdentityDbContext>());
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            if (!MissingDatabase && !MissingEmail)
                await WithDatabaseAsync(async db =>
                {
                    // Only the unique database owned by this fixture may be removed.
                    if (db.Database.GetDbConnection().Database != databaseName)
                        throw new InvalidOperationException("Refusing to delete a database not owned by this test.");
                    return await db.Database.EnsureDeletedAsync();
                });
        }
        finally { await base.DisposeAsync(); }
    }
}

public sealed class CapturedEmailTransport : IEmailTransport
{
    public ConcurrentQueue<OutgoingEmailMessage> Messages { get; } = new();
    public bool FailDelivery { get; set; }

    public Task SendAsync(OutgoingEmailMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailDelivery) throw new ConfirmationEmailDeliveryException();
        Messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public Uri LinkFor(string email) => new(Messages.Last(message => message.Recipient == email)
        .PlainTextBody.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Single(line => line.StartsWith("http", StringComparison.Ordinal)));

    public ConfirmEmailRequest ConfirmationFor(string email)
    {
        var parts = LinkFor(email).Fragment.TrimStart('#').Split('&')
            .Select(part => part.Split('=', 2))
            .ToDictionary(part => part[0], part => Uri.UnescapeDataString(part[1]));
        return new(Guid.Parse(parts["accountId"]), parts["token"]);
    }
}
