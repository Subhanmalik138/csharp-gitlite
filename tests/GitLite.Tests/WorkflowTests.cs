using Xunit;
using GitLite.Commands;
using GitLite.Core;

namespace GitLite.Tests;

/// <summary>End-to-end command workflows against throwaway repositories.</summary>
[Collection("cwd-serial")]
public class WorkflowTests
{
    private static TempDir NewRepo()
    {
        var dir = new TempDir();
        string original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir.Path);
            Assert.Equal(0, InitCommand.Run(Array.Empty<string>()));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
        return dir;
    }

    private static int InDir(string path, Func<int> action)
    {
        string original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(path);
            return action();
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public void Init_Add_Commit_produces_a_commit_on_main()
    {
        using var dir = NewRepo();
        Environment.SetEnvironmentVariable("GITLITE_NAME", "Tester");
        Environment.SetEnvironmentVariable("GITLITE_EMAIL", "tester@example.com");

        int exit = InDir(dir.Path, () =>
        {
            File.WriteAllText("note.txt", "hello\n");
            if (AddCommand.Run(new[] { "note.txt" }) != 0) return 1;
            return CommitCommand.Run(new[] { "-m", "first" });
        });

        Assert.Equal(0, exit);
        var repo = Repository.Find(dir.Path);
        string? head = repo.Refs.ResolveHead();
        Assert.NotNull(head);
        var (_, content) = repo.Objects.Read(head!, "commit");
        Assert.Equal("first", Commit.Parse(content).Message);
    }

    [Fact]
    public void Branch_and_Checkout_restore_each_branch_worktree()
    {
        using var dir = NewRepo();
        Environment.SetEnvironmentVariable("GITLITE_NAME", "Tester");
        Environment.SetEnvironmentVariable("GITLITE_EMAIL", "tester@example.com");

        int exit = InDir(dir.Path, () =>
        {
            File.WriteAllText("v.txt", "v1\n");
            if (AddCommand.Run(new[] { "v.txt" }) != 0) return 10;
            if (CommitCommand.Run(new[] { "-m", "v1" }) != 0) return 11;
            if (BranchCommand.Run(new[] { "feature" }) != 0) return 12;
            File.WriteAllText("v.txt", "v2\n");
            if (AddCommand.Run(new[] { "v.txt" }) != 0) return 13;
            if (CommitCommand.Run(new[] { "-m", "v2" }) != 0) return 14;
            if (CheckoutCommand.Run(new[] { "feature" }) != 0) return 15;
            if (File.ReadAllText("v.txt") != "v1\n") return 16;
            if (CheckoutCommand.Run(new[] { "main" }) != 0) return 17;
            if (File.ReadAllText("v.txt") != "v2\n") return 18;
            return 0;
        });

        Assert.Equal(0, exit);
    }

    [Fact]
    public void Checkout_refuses_to_clobber_dirty_worktree()
    {
        using var dir = NewRepo();
        Environment.SetEnvironmentVariable("GITLITE_NAME", "Tester");
        Environment.SetEnvironmentVariable("GITLITE_EMAIL", "tester@example.com");

        int exit = InDir(dir.Path, () =>
        {
            File.WriteAllText("v.txt", "v1\n");
            AddCommand.Run(new[] { "v.txt" });
            CommitCommand.Run(new[] { "-m", "v1" });
            BranchCommand.Run(new[] { "feature" });
            File.WriteAllText("v.txt", "dirty\n"); // uncommitted change
            return CheckoutCommand.Run(new[] { "feature" });
        });

        Assert.Equal(1, exit); // guard tripped
        Assert.Equal("dirty\n", File.ReadAllText(Path.Combine(dir.Path, "v.txt"))); // untouched
    }
}
