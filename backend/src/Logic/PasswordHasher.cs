using System.Security.Cryptography;
using System.Text;

namespace HashingDemo.Logic;

/// <summary>
/// Provides helper methods for password hashing and verification.
/// This implementation is for educational purposes only and should not be used in production.
/// </summary>
public static class PasswordHasher
{
    public static string ToHexString(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    public static byte[] FromHexString(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    public static byte[] GenerateSalt(int size = 16)
    {
        var salt = new byte[size];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }
        return salt;
    }

    public static string ComputePasswordHash(string password, byte[] salt, int iterations)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var combined = new byte[passwordBytes.Length + salt.Length];
        Buffer.BlockCopy(passwordBytes, 0, combined, 0, passwordBytes.Length);
        Buffer.BlockCopy(salt, 0, combined, passwordBytes.Length, salt.Length);

        var hash = Sha256Pure.ComputeHash(combined);

        // Apply password stretching
        for (var i = 1; i < iterations; i++)
        {
            hash = Sha256Pure.ComputeHash(hash);
        }

        return ToHexString(hash);
    }

    public static bool ConstantTimeEquals(string hexHash1, string hexHash2)
    {
        var hash1 = FromHexString(hexHash1);
        var hash2 = FromHexString(hexHash2);

        if (hash1.Length != hash2.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < hash1.Length; i++)
        {
            diff |= hash1[i] ^ hash2[i];
        }

        return diff == 0;
    }
}
