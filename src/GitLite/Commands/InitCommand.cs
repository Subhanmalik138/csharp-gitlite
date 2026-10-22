using GitLite.Core;

namespace GitLite.Commands;

static class InitCommand
{
    public static int Run(string[] args)
    {
        try
        {
            string target = args.Length > 0 ? args[0] : ".";
            var repo = Repository.Init(target);
            Console.WriteLine($"Initialized empty GitLite repository in {repo.GitDir}/");
            return 0;
        }
        catch (GitLiteException ex) { return Fail(ex); }
    }

    internal static int Fail(GitLiteException ex)
    {
        Console.Error.WriteLine($"gitlite: {ex.Message}");
        return 1;
    }
}
