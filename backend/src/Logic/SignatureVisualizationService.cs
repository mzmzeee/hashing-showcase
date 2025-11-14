using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace HashingDemo.Logic;

public sealed class SignatureVisualizationService
{
    public SignatureVisualizationData Create(string message, string signatureBase64, string publicKeyPem)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (signatureBase64 is null)
        {
            throw new ArgumentNullException(nameof(signatureBase64));
        }

        if (publicKeyPem is null)
        {
            throw new ArgumentNullException(nameof(publicKeyPem));
        }

        var messageBytes = Encoding.UTF8.GetBytes(message);
        var messageHashBytes = System.Security.Cryptography.SHA256.HashData(messageBytes);
        var messageHashHex = Convert.ToHexString(messageHashBytes).ToLowerInvariant();

        var signatureBytes = Convert.FromBase64String(signatureBase64);
        var decryptedBlock = DecryptSignatureBlock(signatureBytes, publicKeyPem);
        var decryptedHashBytes = CopyTail(decryptedBlock, 32);
        var decryptedHashHex = Convert.ToHexString(decryptedHashBytes).ToLowerInvariant();

        var hashesMatch = string.Equals(messageHashHex, decryptedHashHex, StringComparison.OrdinalIgnoreCase);

        return new SignatureVisualizationData(
            message,
            messageHashHex,
            signatureBase64,
            decryptedHashHex,
            messageHashHex,
            hashesMatch);
    }

    private static byte[] DecryptSignatureBlock(byte[] signatureBytes, string publicKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        var parameters = rsa.ExportParameters(includePrivateParameters: false);

        var modulus = new BigInteger(parameters.Modulus!, isUnsigned: true, isBigEndian: true);
        var exponent = new BigInteger(parameters.Exponent!, isUnsigned: true, isBigEndian: true);
        var signatureValue = new BigInteger(signatureBytes, isUnsigned: true, isBigEndian: true);

        var decryptedValue = BigInteger.ModPow(signatureValue, exponent, modulus);
        var decryptedBytes = decryptedValue.ToByteArray(isUnsigned: true, isBigEndian: true);

        var keySizeBytes = (rsa.KeySize + 7) / 8;
        if (decryptedBytes.Length < keySizeBytes)
        {
            var padded = new byte[keySizeBytes];
            Buffer.BlockCopy(decryptedBytes, 0, padded, keySizeBytes - decryptedBytes.Length, decryptedBytes.Length);
            decryptedBytes = padded;
        }

        return decryptedBytes;
    }

    private static byte[] CopyTail(byte[] source, int length)
    {
        if (length <= 0)
        {
            return Array.Empty<byte>();
        }

        if (source.Length <= length)
        {
            return source.ToArray();
        }

        var buffer = new byte[length];
        Buffer.BlockCopy(source, source.Length - length, buffer, 0, length);
        return buffer;
    }
}

public sealed record SignatureVisualizationData(
    string Message,
    string MessageHashHex,
    string SignatureBase64,
    string DecryptedHashHex,
    string RecomputedHashHex,
    bool HashesMatch);
