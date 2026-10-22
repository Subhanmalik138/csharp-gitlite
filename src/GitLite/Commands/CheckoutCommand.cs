using GitLite.Core;

namespace GitLite.Commands;

/// <summary>gitlite checkout &lt;branch&gt; — switch branches, updating the work tree.</summary>
static class CheckoutCommand
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length != 1)
                throw new GitLiteException("usage: gitlite checkout <branch>");
            var repo = Repository.Find();

            string refName = "refs/heads/" + args[0];
            string target = repo.Refs.ReadRef(refName)
                ?? throw new GitLiteException($"unknown branch '{args[0]}'");
            string? current = repo.Refs.ResolveHead();

            var targetFiles = TreeOf(repo, target);
            var currentFiles = current is null
                ? new Dictionary<string, (int Mode, string Sha)>(StringComparer.Ordinal)
                : TreeOf(repo, current);

            // Safety first: never silently clobber local work.
            foreach (var (path, (_, sha)) in currentFiles)
            {
                string full = Path.Combine(repo.WorkTree, path);
                if (File.Exists(full) && ObjectStore.Hash("blob", File.ReadAllBytes(full)) != sha)
                    throw new GitLiteException(
                        $"local changes to '{path}' would be overwritten — commit them first");
            }
            foreach (var (path, (_, sha)) in targetFiles)
            {
                if (currentFiles.ContainsKey(path))
                    continue;
                string full = Path.Combine(repo.WorkTree, path);
                if (File.Exists(full) && ObjectStore.Hash("blob", File.ReadAllBytes(full)) != sha)
                    throw new GitLiteException($"untracked file '{path}' would be overwritten by checkout");
            }

            // Remove files that don't exist in the target tree...
            foreach (string path in currentFiles.Keys.Where(p => !targetFiles.ContainsKey(p)))
            {
                string full = Path.Combine(repo.WorkTree, path);
                if (File.Exists(full))
                    File.Delete(full);
            }
            PruneEmptyDirs(repo.WorkTree);

            // ...and materialize the target tree.
            foreach (var (path, (mode, sha)) in targetFiles)
            {
                string full = Path.Combine(repo.WorkTree, path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                var (_, blob) = repo.Objects.Read(sha, "blob");
                File.WriteAllBytes(full, blob);
            }

            // The index always mirrors the checked-out tree.
            var index = StagingArea.Load(repo.GitDir);
            index.Clear();
            foreach (var (path, (mode, sha)) in targetFiles.OrderBy(e => e.Key, StringComparer.Ordinal))
                index.Stage(path, mode, sha);
            index.Save();

            repo.Refs.SetHead(refName);
            Console.WriteLine($"Switched to branch '{args[0]}'");
            return 0;
        }
        catch (GitLiteException ex) { return InitCommand.Fail(ex); }
    }

    private static Dictionary<string, (int Mode, string Sha)> TreeOf(Repository repo, string commitSha)
    {
        var (_, content) = repo.Objects.Read(commitSha, "commit");
        return TreeBuilder.Flatten(repo.Objects, Commit.Parse(content).Tree);
    }

    private static void PruneEmptyDirs(string root)
    {
        foreach (string dir in Directory.GetDirectories(root))
        {
            if (Path.GetFileName(dir) == ".gitlite")
                continue;
            PruneEmptyDirs(dir);
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
    }
}
