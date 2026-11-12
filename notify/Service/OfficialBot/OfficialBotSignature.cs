using System.Text;
using Microsoft.Extensions.Options;
using Notify.Domain.Config.Options;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Notify.Service.OfficialBot;

public class OfficialBotSignature
{
    private readonly Ed25519PrivateKeyParameters? privateKey;

    public OfficialBotSignature(IOptions<OfficialBotOption> options)
    {
        var secret = Encoding.UTF8.GetBytes(options.Value.AppSecret);
        if (secret.Length == 0)
            return;
        // QQ specifies the first 32 bytes of the repeated secret, not a hash of the secret.
        var seed = new byte[32];
        for (var i = 0; i < seed.Length; i++)
            seed[i] = secret[i % secret.Length];
        privateKey = new Ed25519PrivateKeyParameters(seed, 0);
    }

    public bool Verify(string timestamp, byte[] body, string signature)
    {
        if (privateKey == null || string.IsNullOrWhiteSpace(timestamp) || signature.Length != 128)
            return false;
        try
        {
            var signer = new Ed25519Signer();
            signer.Init(false, privateKey.GeneratePublicKey());
            var prefix = Encoding.UTF8.GetBytes(timestamp);
            signer.BlockUpdate(prefix, 0, prefix.Length);
            signer.BlockUpdate(body, 0, body.Length);
            return signer.VerifySignature(Convert.FromHexString(signature));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public string SignChallenge(string eventTimestamp, string plainToken)
    {
        if (privateKey == null)
            throw new InvalidOperationException("Official bot AppSecret is not configured.");
        var signer = new Ed25519Signer();
        signer.Init(true, privateKey);
        var data = Encoding.UTF8.GetBytes(eventTimestamp + plainToken);
        signer.BlockUpdate(data, 0, data.Length);
        return Convert.ToHexString(signer.GenerateSignature()).ToLowerInvariant();
    }
}
