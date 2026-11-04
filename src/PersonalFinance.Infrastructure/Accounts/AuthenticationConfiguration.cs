// Comment ID: CLASS-037. Validates BFF deployment settings and owns the configured credentials and persistent key ring.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalFinance.Infrastructure.Accounts;

public sealed class AuthenticationConfiguration : IDisposable
{
    public required Uri Issuer { get; init; }
    public required Uri ClientOrigin { get; init; }
    public required string[] AllowedOrigins { get; init; }
    public string? KeysPath { get; private set; }
    public string? ApplicationName { get; private set; }
    private readonly Dictionary<string, X509Certificate2> certificates = [];
    public bool Persistent => KeysPath is not null;

    // Comment ID: METHOD-054. Resolves exact origins and loads persistent credentials when requested or required outside Development.
    public static AuthenticationConfiguration Load(IConfiguration configuration, bool isDevelopment)
    {
        var issuer = ReadOrigin(configuration["Authentication:OpenIddict:Issuer"], "Authentication:OpenIddict:Issuer", isDevelopment);
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0) throw new InvalidOperationException("Cors:AllowedOrigins requires an exact client origin.");
        for (var index = 0; index < origins.Length; index++)
        {
            var origin = ReadOrigin(origins[index], $"Cors:AllowedOrigins:{index}", isDevelopment);
            if (origins[index] != origin.GetLeftPart(UriPartial.Authority))
                throw new InvalidOperationException($"Cors:AllowedOrigins:{index} must be an exact origin without a trailing slash.");
        }
        var client = ReadOrigin(configuration["Authentication:OpenIddict:ClientOrigin"] ?? origins[0],
            "Authentication:OpenIddict:ClientOrigin", isDevelopment);
        if (!origins.Contains(client.GetLeftPart(UriPartial.Authority), StringComparer.Ordinal))
            throw new InvalidOperationException("Authentication:OpenIddict:ClientOrigin must be included in Cors:AllowedOrigins.");

        var result = new AuthenticationConfiguration { Issuer = issuer, ClientOrigin = client, AllowedOrigins = origins };
        try
        {
            var persistent = configuration.GetValue<bool>("Authentication:KeyPersistence:Enabled");
            if (!isDevelopment && !persistent)
                throw new InvalidOperationException("Authentication:KeyPersistence:Enabled must be true outside Development.");
            if (persistent)
            {
                var path = configuration["Authentication:KeyPersistence:KeysPath"];
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                    throw new InvalidOperationException("Authentication:KeyPersistence:KeysPath requires an absolute persistent directory.");
                var name = configuration["Authentication:KeyPersistence:ApplicationName"];
                if (string.IsNullOrWhiteSpace(name))
                    throw new InvalidOperationException("Authentication:KeyPersistence:ApplicationName is required and must remain stable across restarts.");
                result.KeysPath = path;
                result.ApplicationName = name;
                foreach (var purpose in new[] { "ServerSigning", "ServerEncryption", "ClientSigning", "ClientEncryption", "DataProtection" })
                    result.certificates.Add(purpose, LoadCertificate(configuration, purpose));
                if (result.certificates.Values.Select(certificate => certificate.Thumbprint).Distinct().Count() != 5)
                    throw new InvalidOperationException("Authentication:Certificates requires distinct certificates for each purpose.");
            }
            else if (configuration.GetSection("Authentication:Certificates").GetChildren().Any(section =>
                !string.IsNullOrWhiteSpace(section["Path"]) || !string.IsNullOrWhiteSpace(section["Password"])))
                throw new InvalidOperationException("Authentication:KeyPersistence:Enabled must be true when certificates are configured.");
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    // Comment ID: METHOD-055. Rejects wildcard, credential-bearing, non-root, and insecure non-loopback URLs without echoing values.
    private static Uri ReadOrigin(string? value, string setting, bool isDevelopment)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host) ||
            value!.Contains('*') || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            (uri.Scheme != "https" && !(isDevelopment && uri.Scheme == "http" && uri.IsLoopback)))
            throw new InvalidOperationException($"{setting} requires a root HTTPS origin (HTTP loopback is allowed only in Development).");
        return uri;
    }

    // Comment ID: METHOD-056. Loads a currently valid RSA private certificate from backend settings and reports only its setting name on failure.
    private static X509Certificate2 LoadCertificate(IConfiguration configuration, string purpose)
    {
        var key = $"Authentication:Certificates:{purpose}";
        var path = configuration[key + ":Path"];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException($"{key}:Path requires an absolute PFX file path.");
        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12FromFile(path, configuration[key + ":Password"],
                X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"{key} could not be loaded; check the file, password, and access permissions.");
        }
        using var rsa = certificate.GetRSAPrivateKey();
        if (!certificate.HasPrivateKey || rsa is null || rsa.KeySize < 2048 ||
            certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
        {
            certificate.Dispose();
            throw new InvalidOperationException($"{key} requires a currently valid RSA certificate with a private key of at least 2048 bits.");
        }
        return certificate;
    }

    // Comment ID: METHOD-057. Supplies the loaded certificate for one OpenIddict or Data Protection purpose.
    public X509Certificate2 Certificate(string purpose) => certificates[purpose];

    // Comment ID: METHOD-058. Persists the application key ring and encrypts its keys at rest using the configured protection certificate.
    public void ConfigureDataProtection(IServiceCollection services)
    {
        if (!Persistent) return;
        services.AddDataProtection().SetApplicationName(ApplicationName!)
            .PersistKeysToFileSystem(new DirectoryInfo(KeysPath!))
            .ProtectKeysWithCertificate(Certificate("DataProtection"));
    }

    // Comment ID: METHOD-059. Releases private certificate handles when host startup fails or the application shuts down.
    public void Dispose()
    {
        foreach (var certificate in certificates.Values) certificate.Dispose();
        certificates.Clear();
    }
}
