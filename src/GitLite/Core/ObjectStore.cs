using System.Text;

namespace GitLite.Core;

/// <summary>
/// Content-addressed object database. Layout and on-disk format mirror Git:
/// objects are stored deflated under objects/xx/yyyy… and named by the SHA-1
/// of "<type> <size>\0" + content.
/// </summary>
sealed class ObjectStore
{
    private readonly string _objectsDir;

    public ObjectStore(string gitDir)
    {
        _objectsDir = Path.Combine(gitDir, "objects");
        Directory.CreateDirectory(_objectsDir);
    }

    /// <summary>Serializes and hashes an object without storing it.</summary>
    public static string Hash(string type, byte[] content)
    {
        byte[] header = Encoding.ASCII.GetBytes($"{type} {content.Length}\0");
        byte[] store = new byte[header.Length + content.Length];
        Buffer.BlockCopy(header, 0, store, 0, header.Length);
        Buffer.BlockCopy(content, 0, store, header.Length, content.Length);
        return Sha1.HexOf(store);
    }

    /// <summary>Serializes, hashes and stores an object; returns its SHA-1 hex id.</summary>
    public string Write(string type, byte[] content)
    {
        string sha = Hash(type, content);
        string dir = Path.Combine(_objectsDir, sha[..2]);
        string path = Path.Combine(dir, sha[2..]);
        if (!File.Exists(path))
        {
            byte[] header = Encoding.ASCII.GetBytes($"{type} {content.Length}\0");
            byte[] store = new byte[header.Length + content.Length];
            Buffer.BlockCopy(header, 0, store, 0, header.Length);
            Buffer.BlockCopy(content, 0, store, header.Length, content.Length);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, Zlib.Compress(store));
        }
        return sha;
    }

    public string WriteBlob(byte[] content) => Write("blob", content);

    /// <summary>Reads and type-checks an object. Accepts unambiguous SHA prefixes.</summary>
    public (string Type, byte[] Content) Read(string shaOrPrefix, string? expectType = null)
    {
        string sha = Resolve(shaOrPrefix);
        string path = Path.Combine(_objectsDir, sha[..2], sha[2..]);
        if (!File.Exists(path))
            throw new GitLiteException($"object not found: {shaOrPrefix}");

        byte[] store = Zlib.Decompress(File.ReadAllBytes(path));
        int nul = Array.IndexOf(store, (byte)0);
        if (nul < 0)
            throw new GitLiteException($"corrupt object: {sha}");
        string header = Encoding.ASCII.GetString(store, 0, nul);
        int space = header.IndexOf(' ');
        string type = header[..space];
        byte[] content = store[(nul + 1)..];

        if (expectType is not null && type != expectType)
            throw new GitLiteException($"object {sha} is a {type}, not a {expectType}");
        if (Sha1.HexOf(store) != sha)
            throw new GitLiteException($"corrupt object: {sha} (checksum mismatch)");
        return (type, content);
    }

    public bool Exists(string sha) =>
        File.Exists(Path.Combine(_objectsDir, sha[..2], sha[2..]));

    /// <summary>Expands an abbreviated SHA to full length; fails on ambiguity.</summary>
    public string Resolve(string prefix)
    {
        if (prefix.Length == 40 && Exists(prefix))
            return prefix;
        if (prefix.Length < 4)
            throw new GitLiteException($"ambiguous short id: {prefix}");
        string dir = Path.Combine(_objectsDir, prefix[..2]);
        if (!Directory.Exists(dir))
            throw new GitLiteException($"unknown object: {prefix}");
        var matches = Directory.GetFiles(dir, prefix[2..] + "*");
        return matches.Length switch
        {
            0 => throw new GitLiteException($"unknown object: {prefix}"),
            1 => prefix[..2] + Path.GetFileName(matches[0]),
            _ => throw new GitLiteException($"ambiguous id {prefix}: {matches.Length} candidates"),
        };
    }
}
