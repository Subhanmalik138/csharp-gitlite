using Xunit;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using GitLite.Core;

namespace GitLite.Tests;

public class ObjectStoreTests
{
    [Fact]
    public void Write_then_Read_returns_type_and_content()
    {
        using var dir = new TempDir();
        var store = new ObjectStore(dir.Path);

        string sha = store.WriteBlob("hello gitlite"u8.ToArray());
        var (type, content) = store.Read(sha);

        Assert.Equal("blob", type);
        Assert.Equal("hello gitlite"u8.ToArray(), content);
    }

    [Fact]
    public void Write_is_idempotent_for_identical_content()
    {
        using var dir = new TempDir();
        var store = new ObjectStore(dir.Path);

        string a = store.WriteBlob("same"u8.ToArray());
        string b = store.WriteBlob("same"u8.ToArray());

        Assert.Equal(a, b);
        Assert.Single(Directory.GetFiles(dir.Path, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Sha_matches_git_object_format()
    {
        // Git hashes "<type> <size>\0" + content; prove we do exactly that.
        using var dir = new TempDir();
        var store = new ObjectStore(dir.Path);

        string sha = store.WriteBlob("hello"u8.ToArray());
        byte[] expected = "blob 5\0hello"u8.ToArray();
        string want = Convert.ToHexString(SHA1.HashData(expected)).ToLowerInvariant();

        Assert.Equal(want, sha);
    }

    [Fact]
    public void Stored_file_decompresses_with_standard_zlib()
    {
        using var dir = new TempDir();
        var store = new ObjectStore(dir.Path);

        string sha = store.WriteBlob("data"u8.ToArray());
        byte[] raw = File.ReadAllBytes(Path.Combine(dir.Path, "objects", sha[..2], sha[2..]));

        using var ms = new MemoryStream(raw, 2, raw.Length - 6); // strip zlib header/trailer
        using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
        using var out_ = new MemoryStream();
        deflate.CopyTo(out_);

        Assert.Equal("blob 4\0data"u8.ToArray(), out_.ToArray());
    }

    [Fact]
    public void Read_with_wrong_expected_type_throws()
    {
        using var dir = new TempDir();
        var store = new ObjectStore(dir.Path);

        string sha = store.WriteBlob("x"u8.ToArray());
        Assert.Throws<GitLiteException>(() => store.Read(sha, "tree"));
    }

    [Fact]
    public void Read_unknown_sha_throws()
    {
        using var dir = new TempDir();
        var store = new ObjectStore(dir.Path);

        Assert.Throws<GitLiteException>(() => store.Read(new string('0', 40)));
    }

    [Fact]
    public void Resolve_expands_unambiguous_prefix()
    {
        using var dir = new TempDir();
        var store = new ObjectStore(dir.Path);

        string sha = store.WriteBlob("prefix me"u8.ToArray());
        Assert.Equal(sha, store.Resolve(sha[..7]));
    }

    [Fact]
    public void Resolve_rejects_too_short_prefix()
    {
        using var dir = new TempDir();
        var store = new ObjectStore(dir.Path);

        Assert.Throws<GitLiteException>(() => store.Resolve("abc"));
    }
}
