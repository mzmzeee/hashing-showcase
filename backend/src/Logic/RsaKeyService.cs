using System.Security.Cryptography;

namespace HashingDemo.Logic;

public class RsaKeyService
{
    public (string publicKey, string privateKey) GenerateKeys()
    {
        using var rsa = RSA.Create(2048);
        var publicKey = rsa.ExportRSAPublicKeyPem();
        var privateKey = rsa.ExportRSAPrivateKeyPem();
        return (publicKey, privateKey);
    }
}
