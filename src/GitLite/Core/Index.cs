using System.Text;

namespace GitLite.Core;

/// <summary>A single staged file: its mode, blob id and repo-relative path.</summary>
record IndexEntry(int Mode, string Sha, string Path);

/// <summary>
/// The staging area (".gitlite/index"): the set of blobs that the next commit
/// will snapshot. Stored as a small binary file: magic + version + entries.
/// </summary>
sealed class StagingArea
{
    private readonly string _path;
    private readonly List<IndexEntry> _entries = new();

    public IReadOnlyList<IndexEntry> Entries => _entries;

    private StagingArea(string path) => _path = path;

    public static StagingArea Load(string gitDir)
    {
        var area = new StagingArea(Path.Combine(gitDir, "index"));
        if (!File.Exists(area._path))
            return area;

        using var br = new BinaryReader(File.OpenRead(area._path));
        byte[] magic = br.ReadBytes(4);
        if (!magic.SequenceEqual(Encoding.ASCII.GetBytes("GLIX")))
            throw new GitLiteException("corrupt index file");
        if (br.ReadUInt32() != 1)
            throw new GitLiteException("unsupported index version");
        uint count = br.ReadUInt32();
        for (int i = 0; i < count; i++)
        {
            int mode = (int)br.ReadUInt32();
            string sha = Convert.ToHexString(br.ReadBytes(20)).ToLowerInvariant();
            uint len = br.ReadUInt32();
            string path = Encoding.UTF8.GetString(br.ReadBytes((int)len));
            area._entries.Add(new IndexEntry(mode, sha, path));
        }
        return area;
    }

    public void Stage(string path, int mode, string sha)
    {
        _entries.RemoveAll(e => e.Path == path);
        _entries.Add(new IndexEntry(mode, sha, path));
        _entries.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
    }

    public void Unstage(string path) => _entries.RemoveAll(e => e.Path == path);

    public void Clear() => _entries.Clear();

    public void Save()
    {
        using var bw = new BinaryWriter(File.Create(_path));
        bw.Write(Encoding.ASCII.GetBytes("GLIX"));
        bw.Write(1u);
        bw.Write((uint)_entries.Count);
        foreach (var e in _entries)
        {
            bw.Write((uint)e.Mode);
            bw.Write(Convert.FromHexString(e.Sha));
            byte[] pb = Encoding.UTF8.GetBytes(e.Path);
            bw.Write((uint)pb.Length);
            bw.Write(pb);
        }
    }
}
