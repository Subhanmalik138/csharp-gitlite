using GitLite.Core;

namespace GitLite.Commands;

/// <summary>gitlite status — staged, unstaged and untracked changes.</summary>
static class StatusCommand
{
    public static int Run(string[] args)
    {
        try
        {
            var repo = Repository.Find();
            string? head = repo.Refs.ResolveHead();
            var headFiles = head is null
                ? new Dictionary<string, (int Mode, string Sha)>(StringComparer.Ordinal)
                : TreeOf(repo, head);

            var index = StagingArea.Load(repo.GitDir);
            var indexMap = index.Entries.ToDictionary(e => e.Path, e => e,
                StringComparer.Ordinal);
            var work = WorkTree.ScanBlobs(repo);

            var stagedNew = indexMap.Keys.Where(p => !headFiles.ContainsKey(p)).OrderBy(p => p).ToList();
            var stagedMod = indexMap.Keys
                .Where(p => headFiles.TryGetValue(p, out var h) && h.Sha != indexMap[p].Sha)
                .OrderBy(p => p).ToList();
            var stagedDel = headFiles.Keys.Where(p => !indexMap.ContainsKey(p)).OrderBy(p => p).ToList();

            var unstagedMod = work.Keys
                .Where(p => indexMap.TryGetValue(p, out var e) && e.Sha != work[p])
                .OrderBy(p => p).ToList();
            var unstagedDel = indexMap.Keys.Where(p => !work.ContainsKey(p)).OrderBy(p => p).ToList();

            var untracked = work.Keys
                .Where(p => !indexMap.ContainsKey(p) && !headFiles.ContainsKey(p))
                .OrderBy(p => p).ToList();

            string? branch = repo.Refs.HeadRefName();
            Console.WriteLine(branch is null ? "Not currently on any branch"
                : $"On branch {branch["refs/heads/".Length..]}");

            if (stagedNew.Count + stagedMod.Count + stagedDel.Count +
                unstagedMod.Count + unstagedDel.Count + untracked.Count == 0)
            {
                Console.WriteLine("nothing to commit, working tree clean");
                return 0;
            }

            if (stagedNew.Count + stagedMod.Count + stagedDel.Count > 0)
            {
                Console.WriteLine("Changes to be committed:");
                foreach (string p in stagedNew) Console.WriteLine($"  new file:   {p}");
                foreach (string p in stagedMod) Console.WriteLine($"  modified:   {p}");
                foreach (string p in stagedDel) Console.WriteLine($"  deleted:    {p}");
                Console.WriteLine();
            }
            if (unstagedMod.Count + unstagedDel.Count > 0)
            {
                Console.WriteLine("Changes not staged for commit:");
                foreach (string p in unstagedMod) Console.WriteLine($"  modified:   {p}");
                foreach (string p in unstagedDel) Console.WriteLine($"  deleted:    {p}");
                Console.WriteLine();
            }
            if (untracked.Count > 0)
            {
                Console.WriteLine("Untracked files:");
                foreach (string p in untracked) Console.WriteLine($"  {p}");
                Console.WriteLine();
            }
            return 0;
        }
        catch (GitLiteException ex) { return InitCommand.Fail(ex); }
    }

    private static Dictionary<string, (int Mode, string Sha)> TreeOf(Repository repo, string commitSha)
    {
        var (_, content) = repo.Objects.Read(commitSha, "commit");
        return TreeBuilder.Flatten(repo.Objects, Commit.Parse(content).Tree);
    }
}
