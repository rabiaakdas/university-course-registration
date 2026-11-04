using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace UniversityRegistration.Accounts;

// Owned by the DI container; verification never holds private key parameters.
public sealed class UniversityJwtKeys : IDisposable
{
    private readonly RSA publicRsa;
    private readonly RSA? privateRsa;
    public RsaSecurityKey VerificationKey { get; }
    public RsaSecurityKey? SigningKey { get; }
    private UniversityJwtKeys(RSA publicRsa, RSA? privateRsa)
    {
        this.publicRsa = publicRsa; this.privateRsa = privateRsa;
        VerificationKey = new RsaSecurityKey(publicRsa);
        if (privateRsa is not null) SigningKey = new RsaSecurityKey(privateRsa);
    }
    public static UniversityJwtKeys Load(string publicPath, string? privatePath)
    {
        var pub = RSA.Create(); RSA? priv = null;
        try
        {
            var publicPem = File.ReadAllText(publicPath);
            if (publicPem.Contains("PRIVATE KEY", StringComparison.Ordinal)) throw new CryptographicException();
            pub.ImportFromPem(publicPem);
            if (pub.KeySize < 2048) throw new CryptographicException();
            if (privatePath is not null)
            {
                priv = RSA.Create(); priv.ImportFromPem(File.ReadAllText(privatePath));
                _ = priv.ExportParameters(true); // Require private material, not a public-only PEM.
                if (!pub.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(priv.ExportSubjectPublicKeyInfo())) throw new CryptographicException();
            }
            return new UniversityJwtKeys(pub, priv);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or CryptographicException)
        {
            pub.Dispose(); priv?.Dispose();
            throw new InvalidOperationException("JWT anahtar dosyaları okunamadı veya geçersiz/eşleşmeyen RSA anahtarı içeriyor.");
        }
    }
    public void Dispose() { publicRsa.Dispose(); privateRsa?.Dispose(); }
}
