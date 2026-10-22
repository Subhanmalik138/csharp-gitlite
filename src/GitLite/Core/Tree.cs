using System.Text;

namespace GitLite.Core;

/// <summary>A single tree entry: mode, file name and the object it points to.</summary>
record TreeEntry(int Mode, string Name, string Sha);

/// <summary>
/// Builds Git-compatible tree objects from a flat index entry list.
/// Unchanged subtrees are never rebuilt: because trees are content-addressed,
/// an identical directory always hashes to the same id.
/// </summary>
static class TreeBuilder
{
    public const int ModeFile = 33188; // 0100644
    public const int ModeExec = 33261; // 0100755
    public const int ModeDir = 16384;  // 0040000

    public static string WriteTree(ObjectStore store, IReadOnlyList<IndexEntry> entries)
    {
        var items = entries.Select(e => (Segments: e.Path.Split('/'), Entry: e)).ToList();
        return WriteLevel(store, items);
    }

    private static string WriteLevel(ObjectStore store, List<(string[] Segments, IndexEntry Entry)> items)
    {
        var treeEntries = new List<TreeEntry>();
        foreach (var group in items.GroupBy(i => i.Segments[0]))
        {
            var files = group.Where(i => i.Segments.Length == 1).ToList();
            var dirs = group.Where(i => i.Segments.Length > 1).ToList();

            if (files.Count > 0)
            {
                var f = files[0]; // index paths are unique, so at most one file per name
                treeEntries.Add(new TreeEntry(f.Entry.Mode, group.Key, f.Entry.Sha));
            }
            if (dirs.Count > 0)
            {
                var sub = dirs.Select(d => (d.Segments[1..], d.Entry)).ToList();
                treeEntries.Add(new TreeEntry(ModeDir, group.Key, WriteLevel(store, sub)));
            }
        }

        // Git's tree ordering: sort by name, treating directories as "name/".
        treeEntries.Sort((a, b) => string.CompareOrdinal(
            a.Mode == ModeDir ? a.Name + "/" : a.Name,
            b.Mode == ModeDir ? b.Name + "/" : b.Name));

        using var ms = new MemoryStream();
        foreach (var e in treeEntries)
        {
            byte[] head = Encoding.ASCII.GetBytes($"{Convert.ToString(e.Mode, 8)} {e.Name}\0");
            ms.Write(head, 0, head.Length);
            byte[] sha = Convert.FromHexString(e.Sha);
            ms.Write(sha, 0, sha.Length);
        }
        return store.Write("tree", ms.ToArray());
    }

    public static List<TreeEntry> Parse(byte[] content)
    {
        var entries = new List<TreeEntry>();
        int i = 0;
        while (i < content.Length)
        {
            int space = Array.IndexOf(content, (byte)' ', i);
            int nul = Array.IndexOf(content, (byte)0, space);
            int mode = Convert.ToInt32(Encoding.ASCII.GetString(content, i, space - i), 8);
            string name = Encoding.UTF8.GetString(content, space + 1, nul - space - 1);
            string sha = Convert.ToHexString(content, nul + 1, 20).ToLowerInvariant();
            entries.Add(new TreeEntry(mode, name, sha));
            i = nul + 21;
        }
        return entries;
    }

    /// <summary>Flattens a tree into path → (mode, blob id), recursing into subtrees.</summary>
    public static Dictionary<string, (int Mode, string Sha)> Flatten(ObjectStore store, string treeSha)
    {
        var result = new Dictionary<string, (int Mode, string Sha)>(StringComparer.Ordinal);
        FlattenInto(store, treeSha, "", result);
        return result;
    }

    private static void FlattenInto(ObjectStore store, string treeSha, string prefix,
                                    Dictionary<string, (int Mode, string Sha)> acc)
    {
        var (_, content) = store.Read(treeSha, "tree");
        foreach (var e in Parse(content))
        {
            if (e.Mode == ModeDir)
                FlattenInto(store, e.Sha, prefix + e.Name + "/", acc);
            else
                acc[prefix + e.Name] = (e.Mode, e.Sha);
        }
    }
}
