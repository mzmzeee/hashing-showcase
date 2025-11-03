namespace HashingDemo.Logic;

/// <summary>
/// A pure C# implementation of the SHA-256 hashing algorithm.
/// This implementation is for educational purposes only and should not be used in production.
/// </summary>
public static class Sha256Pure
{
    // SHA-256 constants (K values)
    private static readonly uint[] K =
    {
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
    };

    public static byte[] ComputeHash(byte[] message)
    {
        // Initialize hash values (H)
        uint[] h =
        {
            0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
            0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19
        };

        // Pre-processing (Padding)
        var paddedMessage = PadMessage(message);

        // Process the message in successive 512-bit chunks
        for (var i = 0; i < paddedMessage.Length; i += 64)
        {
            var chunk = new byte[64];
            Array.Copy(paddedMessage, i, chunk, 0, 64);

            // Create message schedule (W)
            var w = new uint[64];
            for (var t = 0; t < 16; t++)
            {
                w[t] = (uint)((chunk[t * 4] << 24) | (chunk[t * 4 + 1] << 16) | (chunk[t * 4 + 2] << 8) | chunk[t * 4 + 3]);
            }

            for (var t = 16; t < 64; t++)
            {
                var s0 = RightRotate(w[t - 15], 7) ^ RightRotate(w[t - 15], 18) ^ (w[t - 15] >> 3);
                var s1 = RightRotate(w[t - 2], 17) ^ RightRotate(w[t - 2], 19) ^ (w[t - 2] >> 10);
                w[t] = w[t - 16] + s0 + w[t - 7] + s1;
            }

            // Initialize working variables
            var a = h[0];
            var b = h[1];
            var c = h[2];
            var d = h[3];
            var e = h[4];
            var f = h[5];
            var g = h[6];
            var H = h[7];

            // Compression function main loop
            for (var t = 0; t < 64; t++)
            {
                var s1 = RightRotate(e, 6) ^ RightRotate(e, 11) ^ RightRotate(e, 25);
                var ch = (e & f) ^ (~e & g);
                var temp1 = H + s1 + ch + K[t] + w[t];
                var s0 = RightRotate(a, 2) ^ RightRotate(a, 13) ^ RightRotate(a, 22);
                var maj = (a & b) ^ (a & c) ^ (b & c);
                var temp2 = s0 + maj;

                H = g;
                g = f;
                f = e;
                e = d + temp1;
                d = c;
                c = b;
                b = a;
                a = temp1 + temp2;
            }

            // Update hash values
            h[0] += a;
            h[1] += b;
            h[2] += c;
            h[3] += d;
            h[4] += e;
            h[5] += f;
            h[6] += g;
            h[7] += H;
        }

        // Produce final hash value
        var hash = new byte[32];
        for (var i = 0; i < 8; i++)
        {
            hash[i * 4] = (byte)(h[i] >> 24);
            hash[i * 4 + 1] = (byte)(h[i] >> 16);
            hash[i * 4 + 2] = (byte)(h[i] >> 8);
            hash[i * 4 + 3] = (byte)h[i];
        }

        return hash;
    }

    private static byte[] PadMessage(byte[] message)
    {
        var originalLengthInBits = (ulong)message.Length * 8;
        var newLength = (message.Length / 64 + 1) * 64;
        if (message.Length % 64 >= 56)
        {
            newLength += 64;
        }

        var paddedMessage = new byte[newLength];
        Array.Copy(message, paddedMessage, message.Length);

        paddedMessage[message.Length] = 0x80; // Append a single '1' bit

        // Append original length in bits as a 64-bit big-endian integer
        for (var i = 0; i < 8; i++)
        {
            paddedMessage[newLength - 1 - i] = (byte)(originalLengthInBits >> (i * 8));
        }

        return paddedMessage;
    }

    private static uint RightRotate(uint value, int count)
    {
        return (value >> count) | (value << (32 - count));
    }
}
