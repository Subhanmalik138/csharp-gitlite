# GitLite

A tiny Git-style version control system in C#, built from scratch — no Git libraries, no shelling out to `git`. It implements the core ideas that make Git work: a content-addressed object database, a staging area, tree snapshots, commit history, branches, and unified diffs.

## Why this exists

Git's internals are one of those things every developer uses daily and few have built. GitLite is a from-scratch reimplementation of the essential machinery — object storage, trees, commits, refs, diffing — small enough to read in an afternoon, complete enough to actually version a project with.

## Design decisions

1. **Git-compatible object format.** Objects are stored exactly like Git stores them: the SHA-1 of `"<type> <size>\0" + content`, zlib-deflated under `objects/xx/yyyy…`. A GitLite object file can be decompressed with any standard zlib tool, and the hashing logic is verified byte-for-byte against Git's format in the test suite.

2. **Content addressing does the deduplication work.** Trees are built recursively from the index, and identical subtrees automatically hash to the same object id — unchanged directories are never rewritten. There is no separate "did this change?" bookkeeping; the hash *is* the check.

3. **A real staging area, not a snapshot shortcut.** The index (`.gitlite/index`) is a small binary file of `(mode, blob id, path)` entries kept sorted by path. `commit` snapshots the index, not the work tree — so `add`/`status`/`diff` semantics match Git: staged vs unstaged is a meaningful distinction.

4. **Checkout refuses to clobber.** Before switching branches, GitLite verifies that no tracked file has uncommitted modifications and that no untracked file would be overwritten. Losing work silently is never acceptable, even in a toy VCS.

5. **Diff is a straightforward LCS, and that's documented.** The unified-diff engine uses classic O(n·m) dynamic-programming LCS — simple, correct, and fast enough for the file sizes a CLI tool meets. A production tool would swap in Myers' O(ND) algorithm; the interface (`Diff.Unified`) is isolated so that swap is local.

## Commands

```
gitlite init [dir]                  create a repository
gitlite hash-object [-w] <file>     hash a file as a blob
gitlite cat-file (-p|-t) <object>   inspect an object (full or abbreviated id)
gitlite add <file>...               stage files or directories
gitlite commit -m <msg>             snapshot the index as a commit
gitlite log [--oneline]             walk history from HEAD
gitlite branch [-d] [<name>]        list, create or delete branches
gitlite checkout <branch>           switch branches (work tree + index follow)
gitlite status                      staged, unstaged and untracked changes
gitlite diff [--staged] [<commit>]  unified diffs
```

Author identity comes from `GITLITE_NAME` / `GITLITE_EMAIL`, falling back to the OS user.

## Quick start

```bash
dotnet build src/GitLite -c Release
alias gitlite=$PWD/src/GitLite/bin/Release/net8.0/gitlite

mkdir demo && cd demo
gitlite init
echo "hello" > a.txt
gitlite add a.txt
gitlite commit -m "first commit"
gitlite log --oneline
```

Branching:

```bash
gitlite branch feature
echo "more" >> a.txt && gitlite add a.txt && gitlite commit -m "second commit"
gitlite checkout feature   # a.txt reverts to the first commit's content
gitlite checkout main      # ...and back
```

## How to run the tests

```bash
dotnet test tests/GitLite.Tests
```

23 xUnit tests cover the object store (including byte-level Git format compatibility), the index round-trip, tree building and content-addressed subtree reuse, commit serialization, the diff engine's hunk grouping, and end-to-end command workflows (branch/checkout restores each branch's work tree; checkout refuses a dirty work tree).

> Note: in some sandboxed environments `vstest`'s test host fails to start. The tests are ordinary xUnit `[Fact]`s and pass under any normal `dotnet test` run.

## Project layout

```
src/GitLite/
  Program.cs            command dispatch
  Core/
    ObjectStore.cs      content-addressed storage (SHA-1 + zlib)
    Zlib.cs             RFC 1950 compression + SHA-1 helpers
    Repository.cs       repo discovery and layout (.gitlite/)
    Index.cs            staging area (binary .gitlite/index)
    Tree.cs             tree building, parsing, flattening
    Commit.cs           commit object serialization
    Refs.cs             branches and HEAD
    Diff.cs             LCS-based unified diff engine
    WorkTree.cs         work-tree scanning helpers
  Commands/             one class per CLI command
tests/GitLite.Tests/    xUnit suite
```

## Tech stack

- C# 12 / .NET 8, no third-party runtime dependencies
- `System.Security.Cryptography` (SHA-1), `System.IO.Compression` (deflate)
- xUnit for tests

## License

MIT — see [LICENSE](LICENSE).
