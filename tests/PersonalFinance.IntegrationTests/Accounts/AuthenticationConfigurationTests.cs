using PersonalFinance.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using OpenIddict.Client;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using PersonalFinance.Api.Accounts;
using Xunit;

namespace PersonalFinance.IntegrationTests.Accounts;

public sealed class AuthenticationConfigurationTests
{
    [Theory]
    [InlineData("Authentication:OpenIddict:Issuer", "https://localhost:7207/path")]
    [InlineData("Authentication:OpenIddict:Issuer", "https://localhost:7207/?code=private")]
    [InlineData("Authentication:OpenIddict:Issuer", "http://remote.example.test/")]
    [InlineData("Authentication:OpenIddict:ClientOrigin", "https://client.example.test/path")]
    [InlineData("Authentication:OpenIddict:ClientOrigin", "https://other.example.test")]
    [InlineData("Cors:AllowedOrigins:0", "https://*.example.test")]
    public async Task Invalid_origins_fail_at_startup_with_the_setting_name(string key, string value)
    {
        await using var host = new AccountTestHost { Settings = new() { [key] = value } };
        var error = Assert.ThrowsAny<Exception>(() => host.CreateClient());
        Assert.Contains(key.Split(':')[0], error.ToString());
        Assert.DoesNotContain("code=private", error.Message);
    }

    [Fact]
    public async Task Enabling_persistence_without_credentials_fails_before_serving_requests()
    {
        await using var host = new AccountTestHost
        {
            Settings = new() { ["Authentication:KeyPersistence:Enabled"] = "true" }
        };
        var error = Assert.ThrowsAny<Exception>(() => host.CreateClient());
        Assert.Contains("Authentication:", error.ToString());
    }

    [Fact]
    public async Task Persistent_credentials_are_distinct_and_Data_Protection_survives_restart()
    {
        using var keys = new TestKeyMaterial();
        byte[] protectedValue;
        await using (var first = new AccountTestHost { Settings = keys.Settings, UsePersistentKeys = true })
        {
            using var client = first.CreateClient();
            var server = first.Services.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;
            var oidcClient = first.Services.GetRequiredService<IOptions<OpenIddictClientOptions>>().Value;
            AssertCertificate(server.SigningCredentials.Select(value => value.Key), keys, "ServerSigning");
            AssertCertificate(server.EncryptionCredentials.Select(value => value.Key), keys, "ServerEncryption");
            AssertCertificate(oidcClient.SigningCredentials.Select(value => value.Key), keys, "ClientSigning");
            AssertCertificate(oidcClient.EncryptionCredentials.Select(value => value.Key), keys, "ClientEncryption");
            protectedValue = first.Services.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("restart-test").Protect("private-session"u8.ToArray());
        }
        await using var second = new AccountTestHost { Settings = keys.Settings, UsePersistentKeys = true };
        using var secondClient = second.CreateClient();
        var restored = second.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("restart-test").Unprotect(protectedValue);
        Assert.Equal("private-session"u8.ToArray(), restored);
        var keyFile = Assert.Single(Directory.GetFiles(Path.Combine(keys.Root, "ring"), "key-*.xml"));
        var xml = await File.ReadAllTextAsync(keyFile, TestContext.Current.CancellationToken);
        Assert.Contains("encryptedSecret", xml);
        Assert.DoesNotContain("<masterKey", xml);
    }

    private static void AssertCertificate(IEnumerable<SecurityKey> candidates, TestKeyMaterial keys, string purpose)
    {
        using var expected = X509CertificateLoader.LoadPkcs12FromFile(keys.Settings[$"Authentication:Certificates:{purpose}:Path"]!,
            keys.Settings[$"Authentication:Certificates:{purpose}:Password"], X509KeyStorageFlags.EphemeralKeySet);
        var key = Assert.IsType<X509SecurityKey>(Assert.Single(candidates));
        Assert.Equal(expected.Thumbprint, key.Certificate.Thumbprint);
    }

    [Fact]
    public async Task Changing_the_issuer_replaces_the_previously_registered_callback_exactly()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        using var scope = host.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "personal-finance-client",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            RedirectUris = { new Uri("https://old.example.test/api/auth/callback") }
        }, TestContext.Current.CancellationToken);
        var controller = ActivatorUtilities.CreateInstance<AuthenticationController>(scope.ServiceProvider);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider } };
        await controller.Start("/budget-profiles", TestContext.Current.CancellationToken);
        var application = await manager.FindByClientIdAsync("personal-finance-client", TestContext.Current.CancellationToken);
        var redirects = await manager.GetRedirectUrisAsync(application!, TestContext.Current.CancellationToken);
        Assert.Equal("https://localhost:7207/api/auth/callback", Assert.Single(redirects));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("password")]
    [InlineData("public-only")]
    [InlineData("duplicate")]
    public async Task Invalid_certificate_settings_fail_safely_at_startup(string problem)
    {
        using var keys = new TestKeyMaterial();
        const string prefix = "Authentication:Certificates:ServerSigning:";
        switch (problem)
        {
            case "missing": keys.Settings[prefix + "Path"] = Path.Combine(keys.Root, "private-filename.pfx"); break;
            case "password": keys.Settings[prefix + "Password"] = "private-password"; break;
            case "public-only":
                using (var privateCertificate = X509CertificateLoader.LoadPkcs12FromFile(keys.Settings[prefix + "Path"]!,
                    keys.Settings[prefix + "Password"], X509KeyStorageFlags.EphemeralKeySet))
                using (var publicCertificate = X509CertificateLoader.LoadCertificate(privateCertificate.Export(X509ContentType.Cert)))
                    await File.WriteAllBytesAsync(keys.Settings[prefix + "Path"]!,
                        publicCertificate.Export(X509ContentType.Pfx, keys.Settings[prefix + "Password"]), TestContext.Current.CancellationToken);
                break;
            case "duplicate":
                keys.Settings[prefix + "Path"] = keys.Settings["Authentication:Certificates:ServerEncryption:Path"];
                keys.Settings[prefix + "Password"] = keys.Settings["Authentication:Certificates:ServerEncryption:Password"];
                break;
        }
        await using var host = new AccountTestHost { Settings = keys.Settings, UsePersistentKeys = true };
        var error = Assert.ThrowsAny<Exception>(() => host.CreateClient());
        Assert.Contains("Authentication:Certificates", error.Message);
        Assert.DoesNotContain("private-password", error.Message);
        Assert.DoesNotContain("private-filename", error.Message);
    }
}
