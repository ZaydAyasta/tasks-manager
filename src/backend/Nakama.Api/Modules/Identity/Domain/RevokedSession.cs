using System.Security.Cryptography;
using System.Text;

namespace Nakama.Api.Modules.Identity.Domain;

internal sealed class RevokedSession
{
    public string Fingerprint { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }

    public static string FingerprintOf(string sessionProof) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionProof)));
}
