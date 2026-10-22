using System.Text;

namespace GitLite.Core;

/// <summary>
/// A commit object: the snapshotted tree, parent commits, author identity and
/// message. Serialized exactly like Git so the format stays inspectable.
/// </summary>
sealed class Commit
{
    public string Tree { get; }
    public List<string> Parents { get; } = new();
    public string AuthorLine { get; }
    public string Message { get; }

    private Commit(string tree, string authorLine, string message)
    {
        Tree = tree;
        AuthorLine = authorLine;
        Message = message;
    }

    public string AuthorName => AuthorLine.Split('<')[0].Trim();

    public static string Write(ObjectStore store, string tree, IEnumerable<string> parents,
                               string authorLine, string message)
    {
        var sb = new StringBuilder();
        sb.Append("tree ").AppendLine(tree);
        foreach (string p in parents)
            sb.Append("parent ").AppendLine(p);
        sb.Append("author ").AppendLine(authorLine);
        sb.Append("committer ").AppendLine(authorLine);
        sb.AppendLine();
        sb.AppendLine(message.TrimEnd());
        return store.Write("commit", Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public static Commit Parse(byte[] content)
    {
        string text = Encoding.UTF8.GetString(content);
        int blank = text.IndexOf("\n\n", StringComparison.Ordinal);
        if (blank < 0)
            throw new GitLiteException("corrupt commit object");

        string tree = "";
        string author = "";
        var parents = new List<string>();
        foreach (string line in text[..blank].Split('\n'))
        {
            if (line.StartsWith("tree ")) tree = line[5..];
            else if (line.StartsWith("parent ")) parents.Add(line[7..]);
            else if (line.StartsWith("author ")) author = line[7..];
        }
        if (tree == "" || author == "")
            throw new GitLiteException("corrupt commit object");
        var commit = new Commit(tree, author, text[(blank + 2)..].TrimEnd());
        commit.Parents.AddRange(parents);
        return commit;
    }
}
