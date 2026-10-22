using System.Text;

namespace GitLite.Core;

/// <summary>
/// Line-oriented diff producing unified output. Uses the classic O(n·m)
/// dynamic-programming LCS — simple, correct, and plenty fast for the file
/// sizes a CLI like this meets; large generated files are reported as-is.
/// </summary>
static class Diff
{
    enum Op { Equal, Delete, Insert }
    record Edit(Op Op, string Line);

    public static string Unified(string oldLabel, string newLabel,
                                 string oldText, string newText, int context = 3)
    {
        string[] a = SplitLines(oldText);
        string[] b = SplitLines(newText);
        var edits = Compute(a, b);

        var sb = new StringBuilder();
        sb.Append("--- ").AppendLine(oldLabel);
        sb.Append("+++ ").AppendLine(newLabel);

        foreach (var (gs, ge) in ChangeGroups(edits, context))
        {
            int hs = Math.Max(0, gs - context);
            int he = Math.Min(edits.Count, ge + context);

            int aStart = 1, bStart = 1, aCount = 0, bCount = 0;
            for (int t = 0; t < hs; t++)
            {
                if (edits[t].Op != Op.Insert) aStart++;
                if (edits[t].Op != Op.Delete) bStart++;
            }
            for (int t = hs; t < he; t++)
            {
                if (edits[t].Op != Op.Insert) aCount++;
                if (edits[t].Op != Op.Delete) bCount++;
            }
            sb.Append($"@@ -{aStart},{aCount} +{bStart},{bCount} @@").AppendLine();
            for (int t = hs; t < he; t++)
            {
                char c = edits[t].Op switch { Op.Equal => ' ', Op.Delete => '-', _ => '+' };
                sb.Append(c).AppendLine(edits[t].Line);
            }
        }
        return sb.ToString();
    }

    private static List<Edit> Compute(string[] a, string[] b)
    {
        int n = a.Length, m = b.Length;
        var dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                dp[i, j] = a[i] == b[j] ? dp[i + 1, j + 1] + 1
                                        : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        var edits = new List<Edit>(n + m);
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y]) { edits.Add(new Edit(Op.Equal, a[x])); x++; y++; }
            else if (dp[x + 1, y] >= dp[x, y + 1]) { edits.Add(new Edit(Op.Delete, a[x])); x++; }
            else { edits.Add(new Edit(Op.Insert, b[y])); y++; }
        }
        while (x < n) edits.Add(new Edit(Op.Delete, a[x++]));
        while (y < m) edits.Add(new Edit(Op.Insert, b[y++]));
        return edits;
    }

    /// <summary>Groups changed edits into hunks, merging ones close together.</summary>
    private static List<(int Start, int End)> ChangeGroups(List<Edit> edits, int context)
    {
        var groups = new List<(int, int)>();
        int k = 0;
        while (k < edits.Count)
        {
            if (edits[k].Op == Op.Equal) { k++; continue; }
            int s = k;
            while (k < edits.Count)
            {
                if (edits[k].Op != Op.Equal) { k++; continue; }
                int look = k;
                while (look < edits.Count && edits[look].Op == Op.Equal && look - k < 2 * context)
                    look++;
                if (look < edits.Count && edits[look].Op != Op.Equal && look - k <= 2 * context)
                { k = look; continue; }
                break;
            }
            groups.Add((s, k));
        }
        return groups;
    }

    private static string[] SplitLines(string text)
    {
        if (text.Length == 0)
            return Array.Empty<string>();
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (lines.Count > 0 && lines[^1] == "")
            lines.RemoveAt(lines.Count - 1); // trailing newline is not a line
        return lines.ToArray();
    }
}
