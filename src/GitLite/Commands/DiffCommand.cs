using System.Text;
using GitLite.Core;

namespace GitLite.Commands;

/// <summary>
/// gitlite diff — work tree vs index.
/// gitlite diff --staged — index vs HEAD.
/// gitlite diff &lt;commit&gt; — work tree vs a commit.
/// </summary>
static class DiffCommand
{
    public static int Run(string[] args)
    {
        try
        {
            bool staged = args.Contains("--staged");
            string? commitArg = args.FirstOrDefault(a => a != "--staged");
            if (staged && commitArg is not null)
                throw new GitLiteException("usage: gitlite diff [--staged] [<commit>]");

            var repo = Repository.Find();

            // Old side always comes from stored objects; the new side may be the
            // live work tree, whose blobs were hashed but never written.
            Dictionary<string, byte[]> oldBytes;
            Dictionary<string, byte[]> newBytes;

            if (staged)
            {
                string? head = repo.Refs.ResolveHead();
                oldBytes = head is null ? new() : BlobsOfTree(repo, head);
                var index = StagingArea.Load(repo.GitDir);
                newBytes = index.Entries.ToDictionary(e => e.Path,
                    e => repo.Objects.Read(e.Sha, "blob").Content);
            }
            else
            {
                Dictionary<string, (int Mode, string Sha)> oldTree = commitArg is null
                    ? StagingArea.Load(repo.GitDir).Entries.ToDictionary(e => e.Path, e => (0, e.Sha))
                    : TreeOf(repo, repo.Objects.Resolve(commitArg));
                oldBytes = BlobsOfTreeEntries(repo, oldTree);
                newBytes = FilesOf(repo, WorkTree.ScanBlobs(repo).Keys);
            }

            var sb = new StringBuilder();
            foreach (string path in oldBytes.Keys.Union(newBytes.Keys).OrderBy(p => p, StringComparer.Ordinal))
            {
                oldBytes.TryGetValue(path, out byte[]? o);
                newBytes.TryGetValue(path, out byte[]? n);
                o ??= Array.Empty<byte>();
                n ??= Array.Empty<byte>();
                if (o.SequenceEqual(n))
                    continue;
                sb.AppendLine($"diff --git a/{path} b/{path}");
                if (WorkTree.IsBinary(o) || WorkTree.IsBinary(n))
                {
                    sb.AppendLine($"Binary files a/{path} and b/{path} differ");
                    continue;
                }
                sb.Append(Diff.Unified($"a/{path}", $"b/{path}",
                    Encoding.UTF8.GetString(o), Encoding.UTF8.GetString(n)));
            }
            Console.Write(sb.ToString());
            return 0;
        }
        catch (GitLiteException ex) { return InitCommand.Fail(ex); }
    }

    private static Dictionary<string, (int Mode, string Sha)> TreeOf(Repository repo, string commitSha)
    {
        var (_, content) = repo.Objects.Read(commitSha, "commit");
        return TreeBuilder.Flatten(repo.Objects, Commit.Parse(content).Tree);
    }

    private static Dictionary<string, byte[]> BlobsOfTree(Repository repo, string commitSha)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (path, (_, sha)) in TreeOf(repo, commitSha))
            result[path] = repo.Objects.Read(sha, "blob").Content;
        return result;
    }

    private static Dictionary<string, byte[]> BlobsOfTreeEntries(
        Repository repo, Dictionary<string, (int Mode, string Sha)> tree)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (path, (_, sha)) in tree)
            result[path] = repo.Objects.Read(sha, "blob").Content;
        return result;
    }

    private static Dictionary<string, byte[]> FilesOf(Repository repo, IEnumerable<string> paths)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (string path in paths)
            result[path] = File.ReadAllBytes(Path.Combine(repo.WorkTree, path));
        return result;
    }
}
