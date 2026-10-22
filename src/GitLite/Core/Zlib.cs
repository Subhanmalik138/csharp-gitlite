using System.IO.Compression;
using System.Security.Cryptography;

namespace GitLite.Core;

/// <summary>
/// zlib (RFC 1950) compression — the same envelope real Git uses around its
/// objects, so a GitLite object file can be decompressed with any zlib tool.
/// </summary>
static class Zlib
{
    public static byte[] Compress(byte[] data)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78); // CMF: deflate, 32K window
        ms.WriteByte(0x9C); // FLG: default compression, valid check bits
        using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(data, 0, data.Length);
        uint adler = Adler32(data);
        ms.WriteByte((byte)(adler >> 24));
        ms.WriteByte((byte)(adler >> 16));
        ms.WriteByte((byte)(adler >> 8));
        ms.WriteByte((byte)adler);
        return ms.ToArray();
    }

    public static byte[] Decompress(byte[] data)
    {
        if (data.Length < 6)
            throw new GitLiteException("corrupt object: too short for a zlib stream");
        using var ms = new MemoryStream(data, 2, data.Length - 6); // skip header, drop Adler32
        using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
        using var out_ = new MemoryStream();
        deflate.CopyTo(out_);
        return out_.ToArray();
    }

    private static uint Adler32(byte[] data)
    {
        const uint mod = 65521;
        uint a = 1, b = 0;
        foreach (byte x in data)
        {
            a = (a + x) % mod;
            b = (b + a) % mod;
        }
        return (b << 16) | a;
    }
}

/// <summary>SHA-1 helpers (same digest Git uses for object identity).</summary>
static class Sha1
{
    public static string HexOf(byte[] data) =>
        Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();
}
