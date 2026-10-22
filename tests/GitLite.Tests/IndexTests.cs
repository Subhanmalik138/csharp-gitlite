using Xunit;
using GitLite.Core;

namespace GitLite.Tests;

public class IndexTests
{
    [Fact]
    public void Stage_Save_Load_roundtrip_preserves_sorted_entries()
    {
        using var dir = new TempDir();
        string gitDir = Path.Combine(dir.Path, ".gitlite");
        Directory.CreateDirectory(gitDir);

        var index = StagingArea.Load(gitDir);
        index.Stage("b.txt", TreeBuilder.ModeFile, new string('1', 40));
        index.Stage("a.txt", TreeBuilder.ModeFile, new string('2', 40));
        index.Save();

        var loaded = StagingArea.Load(gitDir);
        Assert.Equal(2, loaded.Entries.Count);
        Assert.Equal("a.txt", loaded.Entries[0].Path);
        Assert.Equal("b.txt", loaded.Entries[1].Path);
        Assert.Equal(new string('2', 40), loaded.Entries[0].Sha);
        Assert.Equal(TreeBuilder.ModeFile, loaded.Entries[0].Mode);
    }

    [Fact]
    public void Stage_existing_path_replaces_entry()
    {
        using var dir = new TempDir();
        string gitDir = Path.Combine(dir.Path, ".gitlite");
        Directory.CreateDirectory(gitDir);

        var index = StagingArea.Load(gitDir);
        index.Stage("a.txt", TreeBuilder.ModeFile, new string('1', 40));
        index.Stage("a.txt", TreeBuilder.ModeExec, new string('2', 40));
        index.Save();

        var loaded = StagingArea.Load(gitDir);
        Assert.Single(loaded.Entries);
        Assert.Equal(new string('2', 40), loaded.Entries[0].Sha);
        Assert.Equal(TreeBuilder.ModeExec, loaded.Entries[0].Mode);
    }

    [Fact]
    public void Unstage_removes_entry()
    {
        using var dir = new TempDir();
        string gitDir = Path.Combine(dir.Path, ".gitlite");
        Directory.CreateDirectory(gitDir);

        var index = StagingArea.Load(gitDir);
        index.Stage("a.txt", TreeBuilder.ModeFile, new string('1', 40));
        index.Unstage("a.txt");
        index.Save();

        Assert.Empty(StagingArea.Load(gitDir).Entries);
    }

    [Fact]
    public void Load_missing_file_returns_empty_index()
    {
        using var dir = new TempDir();
        string gitDir = Path.Combine(dir.Path, ".gitlite");
        Directory.CreateDirectory(gitDir);

        Assert.Empty(StagingArea.Load(gitDir).Entries);
    }
}
