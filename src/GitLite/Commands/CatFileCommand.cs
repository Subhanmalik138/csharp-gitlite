using System.Text;
using GitLite.Core;

namespace GitLite.Commands;

/// <summary>gitlite cat-file (-p|-t) &lt;object&gt; — inspect a stored object.</summary>
static class CatFileCommand
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length != 2 || (args[0] != "-p" && args[0] != "-t"))
                throw new GitLiteException("usage: gitlite cat-file (-p|-t) <object>");

            var repo = Repository.Find();
            var (type, content) = repo.Objects.Read(args[1]);

            if (args[0] == "-t")
                Console.WriteLine(type);
            else
                Console.Write(Encoding.UTF8.GetString(content));
            return 0;
        }
        catch (GitLiteException ex) { return InitCommand.Fail(ex); }
    }
}
