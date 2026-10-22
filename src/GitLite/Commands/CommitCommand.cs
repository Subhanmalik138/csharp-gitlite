using GitLite.Core;

namespace GitLite.Commands;

/// <summary>gitlite commit -m &lt;message&gt; [--author "Name &lt;email&gt;"] — snapshot the index.</summary>
static class CommitCommand
{
    public static int Run(string[] args)
    {
        try
        {
            string? message = null;
            string? author = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-m" && i + 1 < args.Length) message = args[++i];
                else if (args[i] == "--author" && i + 1 < args.Length) author = args[++i];
                else throw new GitLiteException("usage: gitlite commit -m <message> [--author \"Name <email>\"]");
            }
            if (message is null)
                throw new GitLiteException("usage: gitlite commit -m <message> [--author \"Name <email>\"]");

            var repo = Repository.Find();
            var index = StagingArea.Load(repo.GitDir);
            if (index.Entries.Count == 0)
                throw new GitLiteException("nothing to commit (empty index)");

            string tree = TreeBuilder.WriteTree(repo.Objects, index.Entries);
            string authorLine = author is null ? DefaultAuthor() : WithTimestamp(author);
            string? parent = repo.Refs.ResolveHead();
            string sha = Commit.Write(repo.Objects, tree,
                parent is null ? Enumerable.Empty<string>() : new[] { parent },
                authorLine, message);

            string? branch = repo.Refs.HeadRefName();
            if (branch is not null)
                repo.Refs.WriteRef(branch, sha);
            else
                throw new GitLiteException("cannot commit: HEAD is detached (checkout a branch first)");

            Console.WriteLine($"[{(branch.StartsWith("refs/heads/") ? branch[11..] : branch)} {sha[..7]}] {message.Split('\n')[0]}");
            return 0;
        }
        catch (GitLiteException ex) { return InitCommand.Fail(ex); }
    }

    /// <summary>Identity from the environment, overridable via GITLITE_NAME / GITLITE_EMAIL.</summary>
    internal static string DefaultAuthor() =>
        WithTimestamp($"{Environment.GetEnvironmentVariable("GITLITE_NAME") ?? Environment.UserName} " +
                      $"<{Environment.GetEnvironmentVariable("GITLITE_EMAIL") ?? Environment.UserName + "@" + Environment.MachineName}>");

    internal static string WithTimestamp(string nameAndEmail)
    {
        var now = DateTimeOffset.Now;
        return $"{nameAndEmail} {now.ToUnixTimeSeconds()} {now:zzz}".Replace(":", "");
    }
}
