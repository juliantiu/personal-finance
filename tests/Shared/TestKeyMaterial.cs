using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PersonalFinance.Tests.Support;

public sealed class TestKeyMaterial : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "PersonalFinance_Keys_Test_" + Guid.NewGuid().ToString("N"));
    public Dictionary<string, string?> Settings { get; } = new()
    {
        ["Authentication:KeyPersistence:Enabled"] = "true",
        ["Authentication:KeyPersistence:ApplicationName"] = "PersonalFinance.Tests"
    };
    public X509Certificate2 HttpsCertificate { get; }

    public TestKeyMaterial()
    {
        Directory.CreateDirectory(Root);
        Settings["Cards:MatchingKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Settings["Authentication:KeyPersistence:KeysPath"] = Path.Combine(Root, "ring");
        foreach (var name in new[] { "ServerSigning", "ServerEncryption", "ClientSigning", "ClientEncryption", "DataProtection" })
        {
            using var certificate = CreateCertificate(name);
            var password = Guid.NewGuid().ToString("N");
            var path = Path.Combine(Root, name + ".pfx");
            File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
            Settings[$"Authentication:Certificates:{name}:Path"] = path;
            Settings[$"Authentication:Certificates:{name}:Password"] = password;
        }
        HttpsCertificate = CreateCertificate("localhost");
    }

    private static X509Certificate2 CreateCertificate(string name)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=" + name, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        if (name == "localhost")
        {
            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName("localhost");
            san.AddIpAddress(System.Net.IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());
        }
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null,
            name == "localhost" ? X509KeyStorageFlags.Exportable :
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }

    public void Dispose()
    {
        HttpsCertificate.Dispose();
        var fullPath = Path.GetFullPath(Root);
        var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(tempRoot + "PersonalFinance_Keys_Test_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to delete a non-test key directory.");
        if (Directory.Exists(fullPath)) Directory.Delete(fullPath, recursive: true);
    }
}
