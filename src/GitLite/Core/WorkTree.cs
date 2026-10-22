namespace GitLite.Core;

/// <summary>Work-tree helpers shared by status, diff and checkout.</summary>
static class WorkTree
{
    /// <summary>Maps every tracked-candidate file to its blob id, skipping .gitlite.</summary>
    public static Dictionary<string, string> ScanBlobs(Repository repo)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string full in Directory.GetFiles(repo.WorkTree, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(repo.WorkTree, full).Replace('\\', '/');
            if (rel == ".gitlite" || rel.StartsWith(".gitlite/"))
                continue;
            result[rel] = ObjectStore.Hash("blob", File.ReadAllBytes(full));
        }
        return result;
    }

    public static bool IsBinary(byte[] content) => content.Contains((byte)0);
}
