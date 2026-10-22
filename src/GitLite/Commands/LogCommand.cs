using GitLite.Core;

namespace GitLite.Commands;

/// <summary>gitlite log [--oneline] — walk the commit history from HEAD.</summary>
static class LogCommand
{
    public static int Run(string[] args)
    {
        try
        {
            bool oneline = args.Contains("--oneline");
            var repo = Repository.Find();
            string? sha = repo.Refs.ResolveHead();
            if (sha is null)
                throw new GitLiteException("no commits yet");

            while (sha is not null)
            {
                var (_, content) = repo.Objects.Read(sha, "commit");
                var commit = Commit.Parse(content);
                if (oneline)
                {
                    Console.WriteLine($"{sha[..7]} {commit.Message.Split('\n')[0]}");
                }
                else
                {
                    Console.WriteLine($"commit {sha}");
                    Console.WriteLine($"Author: {commit.AuthorName}");
                    Console.WriteLine();
                    foreach (string line in commit.Message.Split('\n'))
                        Console.WriteLine($"    {line}");
                    Console.WriteLine();
                }
                sha = commit.Parents.FirstOrDefault();
            }
            return 0;
        }
        catch (GitLiteException ex) { return InitCommand.Fail(ex); }
    }
}
