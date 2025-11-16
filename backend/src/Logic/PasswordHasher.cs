using System.Security.Cryptography;
using System.Text;
using Isopoh.Cryptography.Argon2;
using Isopoh.Cryptography.SecureArray;

namespace HashingDemo.Logic;

/// <summary>
/// Provides helper methods for password hashing and verification using Argon2id.
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
        // Ensure iterations falls within a reasonable range for Argon2 time cost.
        // This keeps the demo responsive even if someone sets a very large value
        // (e.g., the default 10,000) while still allowing iteration tuning to
        // change the resulting hash.
        var effectiveIterations = Math.Clamp(iterations, 1, 10);

        // Argon2id configuration
        // MemoryCost (m) = 65536 KiB = 64 MB
        // TimeCost (t) = effectiveIterations parameter (typically 3-4 for Argon2)
        // Lanes = 4
        // Threads = 1 (single-threaded for simplicity)
        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing,
            Password = Encoding.UTF8.GetBytes(password),
            Salt = salt,
            MemoryCost = 65536, // 64 MB in KiB
            TimeCost = effectiveIterations,
            Lanes = 4,
            Threads = 1,
            HashLength = 32 // 256 bits = 32 bytes
        };

        using var argon2A = new Argon2(config);
        using var secureArray = argon2A.Hash();
        var hash = secureArray.Buffer.ToArray();

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
