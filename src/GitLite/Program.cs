// GitLite — a tiny Git-style version control system, built from scratch.
// Commands are registered here; each later stage adds its own command class.
using GitLite.Commands;

return GitLiteApp.Run(args);

static class GitLiteApp
{
    private static readonly Dictionary<string, Func<string[], int>> Commands = new()
    {
        ["init"] = InitCommand.Run,
        ["hash-object"] = HashObjectCommand.Run,
        ["cat-file"] = CatFileCommand.Run,
        ["add"] = AddCommand.Run,
        ["commit"] = CommitCommand.Run,
        ["log"] = LogCommand.Run,
        ["branch"] = BranchCommand.Run,
        ["checkout"] = CheckoutCommand.Run,
        ["status"] = StatusCommand.Run,
        ["diff"] = DiffCommand.Run,
    };

    internal static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var name = args[0];
        if (Commands.TryGetValue(name, out var run))
            return run(args[1..]);

        Console.Error.WriteLine($"gitlite: '{name}' is not a gitlite command. See 'gitlite --help'.");
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("usage: gitlite <command> [<args>]");
        Console.WriteLine();
        Console.WriteLine("Commands are added stage by stage; run 'gitlite <command> --help' for details.");
    }
}
