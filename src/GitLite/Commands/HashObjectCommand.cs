using GitLite.Core;

namespace GitLite.Commands;

/// <summary>gitlite hash-object [-w] &lt;file&gt; — hash a file as a blob object.</summary>
static class HashObjectCommand
{
    public static int Run(string[] args)
    {
        try
        {
            bool write = args.Contains("-w");
            string? file = args.FirstOrDefault(a => a != "-w");
            if (file is null)
                throw new GitLiteException("usage: gitlite hash-object [-w] <file>");
            if (!File.Exists(file))
                throw new GitLiteException($"no such file: {file}");

            var repo = Repository.Find(Path.GetDirectoryName(Path.GetFullPath(file)));
            byte[] content = File.ReadAllBytes(file);
            string sha = write ? repo.Objects.WriteBlob(content)
                               : ObjectStore.Hash("blob", content);
            Console.WriteLine(sha);
            return 0;
        }
        catch (GitLiteException ex) { return InitCommand.Fail(ex); }
    }
}
