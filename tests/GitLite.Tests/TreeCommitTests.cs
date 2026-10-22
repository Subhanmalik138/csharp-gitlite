using Xunit;
using System.Text;
using GitLite.Core;

namespace GitLite.Tests;

public class TreeCommitTests
{
    private static ObjectStore NewStore(TempDir dir) => new(dir.Path);

    [Fact]
    public void WriteTree_Parse_roundtrip_with_nested_directories()
    {
        using var dir = new TempDir();
        var store = NewStore(dir);
        string blobA = store.WriteBlob("a"u8.ToArray());
        string blobB = store.WriteBlob("b"u8.ToArray());

        var entries = new List<IndexEntry>
        {
            new(TreeBuilder.ModeFile, blobA, "README.md"),
            new(TreeBuilder.ModeFile, blobB, "src/main.cs"),
        };
        string treeSha = TreeBuilder.WriteTree(store, entries);

        var (_, content) = store.Read(treeSha, "tree");
        var parsed = TreeBuilder.Parse(content);
        Assert.Equal(2, parsed.Count);
        var subdir = Assert.Single(parsed, e => e.Name == "src");
        Assert.Equal(TreeBuilder.ModeDir, subdir.Mode);

        var flat = TreeBuilder.Flatten(store, treeSha);
        Assert.Equal(2, flat.Count);
        Assert.Equal(blobA, flat["README.md"].Sha);
        Assert.Equal(blobB, flat["src/main.cs"].Sha);
    }

    [Fact]
    public void WriteTree_reuses_identical_subtrees()
    {
        using var dir = new TempDir();
        var store = NewStore(dir);
        string blob = store.WriteBlob("x"u8.ToArray());
        var entries = new List<IndexEntry> { new(TreeBuilder.ModeFile, blob, "f.txt") };

        Assert.Equal(TreeBuilder.WriteTree(store, entries),
                     TreeBuilder.WriteTree(store, entries));
    }

    [Fact]
    public void Commit_write_parse_roundtrip()
    {
        using var dir = new TempDir();
        var store = NewStore(dir);
        string tree = TreeBuilder.WriteTree(store, new List<IndexEntry>());
        string parent = store.WriteBlob("parent"u8.ToArray()); // stand-in id

        string sha = Commit.Write(store, tree, new[] { parent },
            "Ada <ada@example.com> 1700000000 +0000", "add everything\n\nbody line");
        var (_, content) = store.Read(sha, "commit");
        var commit = Commit.Parse(content);

        Assert.Equal(tree, commit.Tree);
        Assert.Equal(new[] { parent }, commit.Parents);
        Assert.Equal("Ada", commit.AuthorName);
        Assert.Equal("add everything\n\nbody line", commit.Message);
    }

    [Fact]
    public void Commit_parse_rejects_corrupt_objects()
    {
        Assert.Throws<GitLiteException>(() => Commit.Parse("not a commit"u8.ToArray()));
    }
}
