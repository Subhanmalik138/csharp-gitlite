using Xunit;
using GitLite.Core;

namespace GitLite.Tests;

public class DiffTests
{
    [Fact]
    public void Unified_shows_changed_lines_with_context()
    {
        string result = Diff.Unified("a/f.txt", "b/f.txt",
            "line1\nline2\nline3\n", "line1\nline2 changed\nline3\nline4\n");

        Assert.Contains("@@ -1,3 +1,4 @@", result);
        Assert.Contains("-line2", result);
        Assert.Contains("+line2 changed", result);
        Assert.Contains("+line4", result);
        Assert.Contains(" line1", result); // context lines are space-prefixed
    }

    [Fact]
    public void Identical_texts_produce_headers_only()
    {
        string result = Diff.Unified("a/f.txt", "b/f.txt", "same\n", "same\n");

        Assert.Contains("--- a/f.txt", result);
        Assert.Contains("+++ b/f.txt", result);
        Assert.DoesNotContain("@@", result);
    }

    [Fact]
    public void Empty_old_text_lists_everything_as_addition()
    {
        string result = Diff.Unified("a/f.txt", "b/f.txt", "", "one\ntwo\n");

        Assert.Contains("@@ -1,0 +1,2 @@", result);
        Assert.Contains("+one", result);
        Assert.Contains("+two", result);
    }

    [Fact]
    public void Distant_changes_become_separate_hunks()
    {
        string oldText = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"l{i}")) + "\n";
        string newText = string.Join("\n", Enumerable.Range(1, 30).Select(i => i == 2 || i == 29 ? $"L{i}" : $"l{i}")) + "\n";

        string result = Diff.Unified("a/f.txt", "b/f.txt", oldText, newText);

        Assert.Equal(2, result.Split("@@").Length / 2); // two hunk headers
    }
}
