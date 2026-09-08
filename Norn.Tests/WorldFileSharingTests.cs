using Norn.GameCore;

namespace Norn.Tests;

/// <summary>
/// Confirms the actual, empirically-verified boundary of what
/// <see cref="Norn.GameCore.World.ReadPayloadFromDisk"/>'s sharing mode
/// protects against — an initial
/// pass at this claimed the sharing mode made Norn blocking another
/// process's write "structurally impossible regardless of timing," which
/// this same test file disproved (<see cref="A_writer_demanding_full_exclusivity_is_blocked_even_by_our_permissive_reader"/>)
/// before that claim ever shipped in a doc comment. The real property:
/// Windows denies a new open request for <c>FileShare.None</c> whenever
/// <em>any</em> other handle is already open on the file, regardless of
/// that existing handle's own share flags — our side choosing maximally
/// permissive flags cannot override another caller's own demand for total
/// exclusivity. What the fix <em>does</em> guarantee: whenever the other
/// side's own write request isn't fully exclusive (e.g. it allows
/// concurrent readers, plausible for a sync client that wants the game
/// itself to keep working during a sync), Norn's read no longer blocks it —
/// the default <c>File.OpenRead</c> sharing mode would have. So this is a
/// real, meaningful improvement, not a guarantee — timing avoidance
/// (<c>WorldIdentityShim</c>'s <c>IsGameRunning</c> gate) still matters for
/// the fully-exclusive case, contrary to what the first pass at this
/// concluded.
/// <para>
/// All three tests duplicate <c>ReadPayloadFromDisk</c>'s exact
/// <see cref="FileStream"/> constructor call rather than calling the
/// production method directly, since that method opens, reads, and closes
/// within one synchronous call with nothing to interleave a second handle
/// against — if that method's sharing flags ever change, these must change
/// with them, or they stop proving anything.
/// </para>
/// </summary>
public class WorldFileSharingTests
{
    [Fact]
    public void A_writer_that_only_excludes_other_writers_is_not_blocked_by_our_reader()
    {
        // Windows-specific mandatory file-locking semantics (this class's
        // own doc comment already frames it that way) — POSIX advisory
        // locking on Linux/macOS may not produce the same IOException for
        // these open combinations, so these were previously unguarded and
        // could fail on non-Windows CI for a platform difference, not a
        // real production regression (found in review).
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows-specific mandatory file-locking semantics.");

        using var temp = TempFile.Create(".fwl");
        File.WriteAllBytes(temp.Path, [1, 2, 3, 4]);

        using var readerHandle = new FileStream(temp.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        // A writer that still permits concurrent readers (FileShare.Read) —
        // a plausible shape for a sync client that wants the game itself to
        // keep working during a sync.
        var exception = Record.Exception(() =>
        {
            using var writerHandle = new FileStream(temp.Path, FileMode.Open, FileAccess.Write, FileShare.Read);
        });

        Assert.Null(exception);
    }

    /// <summary>
    /// The one test here that exercises the production reader itself. The
    /// other three construct their own <see cref="FileStream"/> with the
    /// flags <c>ReadPayloadFromDisk</c> is believed to use, which makes them
    /// experiments about Windows behavior rather than regression protection:
    /// reverting the production method to a plain <c>File.OpenRead</c> would
    /// leave every one of them green (found in review). This one calls
    /// <see cref="World.ReadPayloadFromDisk"/> directly while a
    /// reader-permitting writer holds the file open — the exact scenario the
    /// production sharing flags exist for — so that revert would fail here.
    /// </summary>
    [Fact]
    public void Our_reader_succeeds_while_a_reader_permitting_writer_holds_the_file()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows-specific mandatory file-locking semantics.");

        var source = TestPaths.WorldFixtureFiles().FirstOrDefault();
        Assert.SkipWhen(source is null, "No .fwl fixture present.");

        using var scratch = TempFile.Create(".fwl");
        File.Copy(source!, scratch.Path, overwrite: true);

        using var writerHandle = new FileStream(scratch.Path, FileMode.Open, FileAccess.Write, FileShare.Read);

        var payload = World.ReadPayloadFromDisk(scratch.Path);
        var world = new World();

        Assert.True(world.Load(payload), "The fixture world failed to load through the production reader.");
        Assert.False(string.IsNullOrEmpty(world.m_name));
    }

    [Fact]
    public void The_default_File_OpenRead_sharing_mode_would_have_blocked_that_same_writer()
    {
        // Negative control: proves the test above exercises a real
        // difference, not something Windows would have allowed anyway.
        // Windows-specific mandatory file-locking semantics (this class's
        // own doc comment already frames it that way) — POSIX advisory
        // locking on Linux/macOS may not produce the same IOException for
        // these open combinations, so these were previously unguarded and
        // could fail on non-Windows CI for a platform difference, not a
        // real production regression (found in review).
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows-specific mandatory file-locking semantics.");

        using var temp = TempFile.Create(".fwl");
        File.WriteAllBytes(temp.Path, [1, 2, 3, 4]);

        using var readerHandle = File.OpenRead(temp.Path);

        var exception = Record.Exception(() =>
        {
            using var writerHandle = new FileStream(temp.Path, FileMode.Open, FileAccess.Write, FileShare.Read);
        });

        Assert.IsType<IOException>(exception);
    }

    [Fact]
    public void A_writer_demanding_full_exclusivity_is_blocked_even_by_our_permissive_reader()
    {
        // The real boundary: FileShare.None means the caller demands nobody
        // else has this file open at all, in any mode. Windows enforces
        // that regardless of how permissive an already-open handle's own
        // share flags are — our fix cannot help here, only not having the
        // file open at that moment could.
        // Windows-specific mandatory file-locking semantics (this class's
        // own doc comment already frames it that way) — POSIX advisory
        // locking on Linux/macOS may not produce the same IOException for
        // these open combinations, so these were previously unguarded and
        // could fail on non-Windows CI for a platform difference, not a
        // real production regression (found in review).
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Windows-specific mandatory file-locking semantics.");

        using var temp = TempFile.Create(".fwl");
        File.WriteAllBytes(temp.Path, [1, 2, 3, 4]);

        using var readerHandle = new FileStream(temp.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        var exception = Record.Exception(() =>
        {
            using var writerHandle = new FileStream(temp.Path, FileMode.Open, FileAccess.Write, FileShare.None);
        });

        Assert.IsType<IOException>(exception);
    }
}
