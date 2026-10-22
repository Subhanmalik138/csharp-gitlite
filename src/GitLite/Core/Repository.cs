namespace GitLite.Core;

/// <summary>A GitLite working repository: work tree + the .gitlite directory.</summary>
sealed class Repository
{
    public string WorkTree { get; }
    public string GitDir { get; }
    public ObjectStore Objects { get; }

    private Repository(string workTree, string gitDir)
    {
        WorkTree = workTree;
        GitDir = gitDir;
        Objects = new ObjectStore(gitDir);
    }

    /// <summary>Creates a fresh repository at <paramref name="path"/>.</summary>
    public static Repository Init(string path)
    {
        string workTree = Path.GetFullPath(path);
        string gitDir = Path.Combine(workTree, ".gitlite");
        if (Directory.Exists(gitDir))
            throw new GitLiteException($"already a GitLite repository: {gitDir}");

        Directory.CreateDirectory(Path.Combine(gitDir, "objects"));
        Directory.CreateDirectory(Path.Combine(gitDir, "refs", "heads"));
        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/main\n");
        return new Repository(workTree, gitDir);
    }

    /// <summary>Finds the repository containing <paramref name="startDir"/> by walking up.</summary>
    public static Repository Find(string? startDir = null)
    {
        string? dir = Path.GetFullPath(startDir ?? Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            string gitDir = Path.Combine(dir, ".gitlite");
            if (Directory.Exists(gitDir))
                return new Repository(dir, gitDir);
            dir = Path.GetDirectoryName(dir);
        }
        throw new GitLiteException("not a GitLite repository (or any parent): .gitlite not found");
    }
}
