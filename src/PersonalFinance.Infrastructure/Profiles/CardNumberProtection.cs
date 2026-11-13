using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Domain.Profiles;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed record CardKeyReadiness(bool Persistent);

public sealed class CardNumberProtection(IDataProtectionProvider provider, IConfiguration configuration, CardKeyReadiness readiness) : ICardNumberProtection
{
    public ProtectedCard Protect(Guid profileId, Guid methodId, CardNumber number)
    {
        byte[]? key = null;
        try
        {
            if (!readiness.Persistent) throw new CardSavingUnavailableException();
            key = Convert.FromBase64String(configuration["Cards:MatchingKey"] ?? "");
            if (key.Length != 32) throw new CardSavingUnavailableException();
            var fingerprint = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(profileId.ToString("N") + ":" + number.Value));
            var keyId = Convert.ToHexString(SHA256.HashData(key));
            var protector = provider.CreateProtector("PersonalFinance.CardNumber", "v1", profileId.ToString("N"), methodId.ToString("N"));
            return new(protector.Protect(number.Value), fingerprint, keyId, number.LastFour);
        }
        catch (Exception error) when (error is FormatException or CryptographicException or IOException or UnauthorizedAccessException)
        { throw new CardSavingUnavailableException(); }
        finally { if (key is not null) CryptographicOperations.ZeroMemory(key); }
    }
}
