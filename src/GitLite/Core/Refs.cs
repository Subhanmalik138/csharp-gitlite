namespace GitLite.Core;

/// <summary>
/// Branch refs and HEAD. Branches are plain files under refs/heads holding a
/// commit id; HEAD is either "ref: refs/heads/&lt;name&gt;" or a raw id (detached).
/// </summary>
sealed class Refs
{
    private readonly string _gitDir;

    public Refs(string gitDir) => _gitDir = gitDir;

    /// <summary>The branch HEAD points at, e.g. "refs/heads/main", or null if detached.</summary>
    public string? HeadRefName()
    {
        string head = File.ReadAllText(Path.Combine(_gitDir, "HEAD")).Trim();
        return head.StartsWith("ref: ", StringComparison.Ordinal) ? head[5..] : null;
    }

    /// <summary>The commit id HEAD currently resolves to, or null if unborn.</summary>
    public string? ResolveHead()
    {
        string head = File.ReadAllText(Path.Combine(_gitDir, "HEAD")).Trim();
        return head.StartsWith("ref: ", StringComparison.Ordinal) ? ReadRef(head[5..]) : head;
    }

    public string? ReadRef(string name)
    {
        string path = RefPath(name);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    public void WriteRef(string name, string sha)
    {
        string path = RefPath(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sha + "\n");
    }

    public bool DeleteRef(string name)
    {
        string path = RefPath(name);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public void SetHead(string refName) =>
        File.WriteAllText(Path.Combine(_gitDir, "HEAD"), $"ref: {refName}\n");

    public IEnumerable<string> ListBranches()
    {
        string dir = Path.Combine(_gitDir, "refs", "heads");
        if (!Directory.Exists(dir))
            yield break;
        foreach (string f in Directory.GetFiles(dir).OrderBy(x => x, StringComparer.Ordinal))
            yield return Path.GetFileName(f);
    }

    private string RefPath(string name) =>
        Path.Combine(_gitDir, name.Replace('/', Path.DirectorySeparatorChar));
}
