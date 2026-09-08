# Contributing

Thanks for looking. Norn is a hobby project, so please read this before
starting anything substantial — a few of its rules are unusual, and a pull
request that violates one will be declined no matter how good the code is.

Start with [ARCHITECTURE.md](ARCHITECTURE.md). It is short, and it explains
why the constraints below exist rather than just asserting them.

---

## Getting set up

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and either
Windows or Linux.

```sh
dotnet build                     # build everything
dotnet run --project Norn.UI     # run the app
dotnet test                      # run the test suite
```

There is nothing else to install. No code generators, no pre-build steps, no
local services.

## The test suite and the save corpus

`dotnet test` runs everything, but a portion of the suite — the round-trip
tests that are the real proof of correctness — needs actual Valheim save files
to run against.

**Those files are not in this repository, and they never will be.** They are
personal save data. The tests detect their absence and **skip cleanly rather
than fail**, so a fresh clone gives you a green suite with a number of skips.
That is expected and is not a broken checkout.

**To run the full suite, supply your own saves.** Copy any `.fch` character
files (and `.fwl` world files, if you are touching world lookup) into:

```
reference/save-files/
reference/world-files/
```

That directory is gitignored. Use copies, not your only backup — the tests
only read, but there is no reason to take the risk.

**More versions is better than more files.** The suite can only verify version
gates for save versions it actually has an example of. A single save from an
older Valheim version is worth more than ten from the version you play now.

## Before you change anything

### `Norn.GameCore` is not normal code

It mirrors the load/save methods found in Valheim's **decompiled**
assemblies — Norn's authors have never had access to Iron Gate's own source —
and must stay diffable against a freshly decompiled copy of that same code,
line by line. Merging methods, reordering
reads, replacing loops with LINQ, extracting helpers, and renaming fields to
something clearer are all **defects** in that project, not improvements.

This is the single most common way a well-intentioned change gets rejected.
[ARCHITECTURE.md](ARCHITECTURE.md) section 3 explains why in full.

`Norn.Adapter` and `Norn.UI` are ordinary code — write them well and tidy them
freely.

### Never copy Valheim source into this repository

Not files, not classes, not method bodies, not into a comment "for reference."
The decompiled source is read as a reference and the read *order* and
*structure* are reimplemented from it.

This is not a stylistic preference. Committing game source cannot be undone
without rewriting every later commit, which makes it the one genuinely
irreversible mistake available here. Keep the decompiled tree outside your
working copy entirely so the mistake is structurally hard to make.

### The project boundaries are enforced by tests

`Norn.UI` references only `Norn.Adapter`. `Norn.Adapter` references only
`Norn.GameCore`. `Norn.GameCore` references only
`Norn.GameCore.Primitives`. Adding a shortcut reference will fail the suite,
not just annoy a reviewer.

### The achievement boundary

Norn does not add functionality whose purpose or plausible effect is bypassing
or manufacturing a Steam achievement. Displaying state the save already
contains is fine; granting state so an achievement unlocks without having been
played for is not. See [ARCHITECTURE.md](ARCHITECTURE.md) section 6 — pull
requests crossing this line will be declined.

## Comments and documentation

**Code must stand on its own.** A comment explains its own reasoning, or it
points at one of the documents that ships alongside the code — `README.md`,
`ARCHITECTURE.md`, `FORMAT.md`, `CONTRIBUTING.md`. Comments do not reference
anything a reader of this repository cannot open.

**Prefer stating the fact to citing a source for it.** "Dictionary enumeration
order is not contracted by .NET, so insertion order is preserved explicitly"
is useful to the next reader. "See the design note" is not.

Mirrored methods in `Norn.GameCore` additionally carry a provenance header
naming what they mirror, the game version they were derived from, and any
place the structure had to diverge along with why.

## Making an editable field editable

Editing is opened one field at a time, never wholesale. A field counts as
editable only once:

1. its `Adapter` DTO has a **tested inverse mapping** — read then write then
   read produces the same value, and there is a test asserting it, and
2. the UI control writes through `Adapter` rather than reaching around it into
   `GameCore`.

Until both hold, the field stays read-only. A pull request that makes a field
editable should include the test, not just the control.

## After a Valheim update

This is the workflow the whole architecture exists to make cheap. The goal is
under an hour, verified mechanically.

1. **Decompile the updated `assembly_valheim` from your own game install** into
   a directory outside this repository.
2. **Diff the relevant `Load`/`Save` methods** against their mirrors in
   `Norn.GameCore`. Because the mirror preserves the game's own decomposition,
   names, and ordering, this diff is usually small and readable.
3. **Apply the delta to `Norn.GameCore` only.** If a change seems to require
   touching `Adapter` or `UI`, that is worth a second look — usually it means
   the mirror drifted rather than that the game changed shape.
4. **Update the version strings** in the provenance headers of every method you
   touched, and the version constants described in [FORMAT.md](FORMAT.md).
5. **Bridge `Adapter`** if a type's shape genuinely changed.
6. **Re-check game-derived constants.** Facts taken from the game that are not
   wire format — level curves, colour ranges, asset-derived data — are tagged
   with `// game-derived:` comments naming their source and the version they
   were confirmed against. Grep for that tag and re-verify each one; a patch can
   invalidate these without changing a single byte of the save format.
7. **Regenerate the item catalog if Valheim's item roster changed.**
   `Norn.UI/Content/SharedItemData.csv`/`LocalizationData.csv` are extracted,
   not hand-maintained — a new patch's new or renamed items won't show up
   correctly until these are refreshed. `Tools/Vade` does the extraction; see
   [its README](Tools/README.md) for the ripping checklist and usage.
8. **Run the full suite against as many save versions as you have.** New gates
   for versions absent from your corpus are untestable, so review them
   especially carefully.

[FORMAT.md](FORMAT.md) has the consolidated gate inventory, which is the
fastest place to see what a version bump actually affects.

## Commit messages

One line saying what changed and why, then a body if it needs one. Explain the
reasoning, not the diff — the diff is already in the commit.

```
Short summary in the imperative mood, under ~72 characters

Why this change was needed, and anything a future reader would otherwise
have to reconstruct. Note deliberate omissions and known limitations here
rather than leaving them to be rediscovered.

Include how it was verified, especially for anything touching GameCore.
```

If you touched `Norn.GameCore`, say in the commit what you verified against —
which save versions, and whether the round-trip tests actually ran or skipped.

## Pull requests

- Keep them focused. One concern per pull request.
- Say how you tested it, and be explicit about what you could not test —
  particularly save versions you have no example of.
- Unfinished work is fine to open for discussion; just say so.
- If you are unsure whether an idea fits, open an issue before writing the
  code. Some things are deliberately out of scope, and it is better to find
  that out first.

Since this is a hobby project, review may take a while. That is not
disinterest.
