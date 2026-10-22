using GitLite.Core;

namespace GitLite.Commands;

/// <summary>gitlite add &lt;file&gt;... — stage files (or whole directories) for the next commit.</summary>
static class AddCommand
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length == 0)
                throw new GitLiteException("usage: gitlite add <file>...");
            var repo = Repository.Find();
            var index = StagingArea.Load(repo.GitDir);
            foreach (string arg in args)
            {
                string full = Path.GetFullPath(arg);
                if (Directory.Exists(full))
                {
                    foreach (string f in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
                        StageFile(repo, index, f);
                }
                else if (File.Exists(full))
                {
                    StageFile(repo, index, full);
                }
                else
                {
                    throw new GitLiteException($"no such file: {arg}");
                }
            }
            index.Save();
            return 0;
        }
        catch (GitLiteException ex) { return InitCommand.Fail(ex); }
    }

    internal static void StageFile(Repository repo, StagingArea index, string fullPath)
    {
        string rel = Path.GetRelativePath(repo.WorkTree, fullPath).Replace('\\', '/');
        if (rel == ".gitlite" || rel.StartsWith(".gitlite/"))
            return; // never stage the object database itself

        int mode = TreeBuilder.ModeFile;
#pragma warning disable CA1416 // UnixFileMode is Unix-only; guarded by the try/catch below
        try
        {
            if ((File.GetUnixFileMode(fullPath) & UnixFileMode.UserExecute) != 0)
                mode = TreeBuilder.ModeExec;
        }
        catch (Exception) { /* non-Unix platforms: keep 100644 */ }
#pragma warning restore CA1416

        string sha = repo.Objects.WriteBlob(File.ReadAllBytes(fullPath));
        index.Stage(rel, mode, sha);
    }
}
