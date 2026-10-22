using GitLite.Core;

namespace GitLite.Commands;

/// <summary>
/// gitlite branch — list branches (current marked *).
/// gitlite branch &lt;name&gt; — create a branch at HEAD.
/// gitlite branch -d &lt;name&gt; — delete a branch.
/// </summary>
static class BranchCommand
{
    public static int Run(string[] args)
    {
        try
        {
            var repo = Repository.Find();

            if (args.Length == 0)
            {
                string? current = repo.Refs.HeadRefName();
                foreach (string b in repo.Refs.ListBranches())
                    Console.WriteLine((current == "refs/heads/" + b ? "* " : "  ") + b);
                return 0;
            }

            if (args is ["-d", var name])
            {
                string refName = "refs/heads/" + name;
                if (repo.Refs.HeadRefName() == refName)
                    throw new GitLiteException($"cannot delete the current branch '{name}'");
                if (!repo.Refs.DeleteRef(refName))
                    throw new GitLiteException($"unknown branch '{name}'");
                Console.WriteLine($"Deleted branch {name}.");
                return 0;
            }

            if (args.Length == 1)
            {
                string? head = repo.Refs.ResolveHead()
                    ?? throw new GitLiteException("cannot create a branch: no commits yet");
                string refName = "refs/heads/" + args[0];
                if (repo.Refs.ReadRef(refName) is not null)
                    throw new GitLiteException($"branch '{args[0]}' already exists");
                repo.Refs.WriteRef(refName, head);
                return 0;
            }

            throw new GitLiteException("usage: gitlite branch [-d] [<name>]");
        }
        catch (GitLiteException ex) { return InitCommand.Fail(ex); }
    }
}
