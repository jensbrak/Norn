# Architecture

Norn is a cross-platform viewer and editor for Valheim character saves
(`.fch`), written in C# on .NET 10 with [Avalonia](https://avaloniaui.net/)
for the desktop shell. The namespace root is `Norn`.

This document explains why the codebase is shaped the way it is. If you only
want to build and run it, see [README.md](README.md). If you want to change
it, read this first and then [CONTRIBUTING.md](CONTRIBUTING.md).

---

## 1. The two goals everything else serves

Almost every structural decision here follows from two maintenance goals.
When a trade-off comes up, these are the tiebreakers.

**Goal A — survive Valheim patches cheaply.** Valheim updates change the save
format. When that happens, the work should be: diff the newly decompiled
`Load`/`Save` methods against Norn's mirror of them, apply the delta, run the
save corpus, ship. That should take under an hour and be verified
mechanically, not by careful reasoning about whether the change was right.

**Goal B — make new editor features cheap.** Adding an editable field or a new
tab should touch one or two files. No feature should require understanding the
save parser.

These pull in different directions, which is why the code is split the way it
is below. Goal A wants the parser frozen, boring, and identical to the game's
own code. Goal B wants the UI free to change constantly. Keeping them in
separate projects is what lets both happen at once.

## 2. Five projects, split by rate of change

The unusual thing about this solution's layout is that it is **organised by
how often code changes, not by what the code is about**. That is deliberate,
and it is the central organising idea.

```
Norn.GameCore.Primitives   changes ~never
Norn.GameCore              changes every game patch
Norn.Adapter               changes when GameCore changes shape
Norn.UI                    changes only for new features
Norn.Tests                 corpus + round-trip harness
```

**`Norn.GameCore.Primitives`** — `ZPackage`, `Vector3`, `Vector2i`,
`Quaternion`, and the binary read/write primitives. Pure C# with no
dependencies on anything else in the solution. These are format-level
constructs that have been stable across the game's whole history.

**`Norn.GameCore`** — the mirrored load/save logic and the game enums
(`SkillType`, `PlayerStatType`, `Biome`). References only `Primitives`. **This
is the only project a game patch should need to touch.** No Avalonia, no UI,
no dialogs, no console I/O — nothing but the format.

**`Norn.Adapter`** — an anti-corruption layer. It translates `GameCore`'s
types into DTOs built from ordinary .NET primitives. References only
`GameCore`. Deliberately contains no Avalonia types, no
`INotifyPropertyChanged`, and no `ObservableCollection` — change notification
is the UI's business and belongs in its own view models.

**`Norn.UI`** — the Avalonia desktop shell. References **only** `Adapter`,
never `GameCore` directly.

**`Norn.Tests`** — the verification harness described in section 5.

### These boundaries are enforced, not merely intended

`Norn.Tests` contains architecture tests that assert the reference graph
directly via `Assembly.GetReferencedAssemblies()`. If `Norn.UI` grows a
reference to `Norn.GameCore`, or `Norn.Adapter` picks up an Avalonia
dependency, the test suite fails. This runs on every platform and in CI, so it
holds regardless of anyone's editor setup or good intentions.

## 3. Structural isomorphism: the mirror discipline

This is the part that makes Goal A work, and it is the rule most likely to
surprise a contributor, because it inverts normal code-quality instincts.

**`Norn.GameCore` must stay diffable, line by line, against freshly decompiled
Valheim source.** Its job is not to be good code. Its job is to be
*recognisably the same code* as the game's, so that when the game changes, the
delta is visible by inspection rather than by reasoning.

Concretely, mirrored methods keep the game's own:

- decomposition into methods
- names, including ones that violate C# conventions (`m_`-prefixed fields)
- read and write **order**, exactly
- version gates, in the same shape and nesting
- default values

And, critically, **the following are defects here, not improvements**:

- merging two methods that could obviously be one
- reordering reads for clarity
- replacing a loop with LINQ
- extracting a shared helper
- renaming a field to something clearer

If you find yourself tidying `Norn.GameCore`, stop. That tidying is exactly
what makes the next patch expensive. Tidy `Adapter` and `UI` freely — those
are where readable, idiomatic C# belongs.

Every mirrored method carries a provenance header naming what it mirrors, the
game version it was derived from, and any place the structure had to diverge
along with why:

```csharp
// mirrors: Player.Load(ZPackage)
// source:  <game version string>
// note:    <only where our structure had to diverge, and why>
```

**No Valheim source is copied into this repository** — not files, not classes,
not method bodies. The decompiled source is read as a reference; the read
*order* and *structure* are reimplemented from it. See
[CONTRIBUTING.md](CONTRIBUTING.md) for what that means in practice.

## 4. Game knowledge that isn't wire format

Not every fact Norn needs about Valheim is a save-format fact. The skill
level-up curve and the reachable range of the appearance-colour sliders are
both derived from the game, but neither affects reading or writing a single
byte. This kind of knowledge is **not** subject to the mirror discipline above
— it can be written as ordinary, tidy C#.

It does still need to stay findable, so two rules apply:

- **Concentrate it physically.** Each such fact, or tightly-related group of
  facts, lives in its own small file named for the game system it reflects
  (`AppearanceColors.cs`, `SkillProgression.cs`, `DeathMechanics.cs`) — never
  inline inside a UI tab module or an editor mutator. The answer to "what does
  Norn assume about the game outside the parser?" should be a short list of
  files, not a property of every feature file someone would have to read in
  full.
- **Tag it.** Each fact carries a `// game-derived:` comment naming the source
  method, field, or asset it came from and the game version it was confirmed
  against. This is what makes it possible to re-check these mechanically after
  a patch instead of trusting memory.

Where a fact lives is decided by who needs it, not by where it was first used:
`Norn.UI` only if it is genuinely presentation-specific, `Norn.Adapter` if
both the read and write paths could plausibly want it.

## 5. Verification doctrine

Correctness here is established mechanically. The suite is built around three
round-trip properties:

- **R1 — byte identity.** For a save whose on-disk version equals Norn's
  writer version, `Write(Read(f))` is byte-identical to `f`.
- **R2 — graph stability.** For any save at any version,
  `Read(Write(Read(f)))` deep-equals `Read(f)`.
- **R3 — opacity.** Where a region of the file isn't interpreted, it is held
  as raw `byte[]` and re-emitted verbatim, so R1 and R2 hold even for parts
  Norn doesn't understand.

R1 is the strong one and is expected to hold. Where it genuinely cannot for a
structural reason, that region is documented in [FORMAT.md](FORMAT.md) and
downgraded to R2 — the test is never quietly weakened instead.

R3 is what made it possible to have a working, verifiably lossless editor
before the format was fully understood, and it is still what keeps unknown
regions safe today.

**The corpus cannot cover versions it does not contain.** Version gates for
save versions absent from the corpus are untestable, which is why they get
disproportionate scrutiny in review. See [CONTRIBUTING.md](CONTRIBUTING.md)
for how the corpus works and what you can run without one.

## 6. Editing is deliberate, one field at a time

Norn started read-only, and the write path is opened **per field**, never
wholesale. A field counts as editable only once it has a tested inverse
mapping in `Adapter` and the UI control writes through `Adapter` rather than
around it. Until then it stays read-only.

This is why some fields you can see are not editable. That is a recorded
decision in each case, not an oversight.

### One boundary that is not negotiable

**Norn does not add functionality whose purpose or plausible effect is
bypassing or manufacturing a Steam achievement.** Norn is a character editor,
not an achievement-unlock tool, and this holds regardless of mechanism.

The line is between **displaying** and **granting**: showing that a boss has
already been killed is a read-only field like any other, and is fine. A
mutator that sets state whose purpose is unlocking an achievement nobody
played for is not, no matter how small the change.

Any field that turns out to gate or correlate with an achievement is read-only
by default. Opening one for editing requires a deliberate, recorded decision
that names this boundary directly. Pull requests that cross it will be
declined.

## 7. Cross-platform rules

Windows and Linux are both first-class targets.

**The parser is platform-neutral and needs no platform handling at all.**
`BinaryReader`/`BinaryWriter` are little-endian by specification regardless of
host, and string encoding is identical on both. Do not add platform branches
to `GameCore` or `Primitives` — there is nothing there to handle.

**Path resolution is a pure function.** This is the one genuinely
platform-dependent piece, and it is structured so a Windows host can fully
test the Linux behaviour and vice versa:

```csharp
static string ResolveSaveDirectory(PlatformKind platform, string homeDirectory)
```

No environment reads inside, no runtime platform checks, no filesystem access.
Platform and home directory are arguments. Only a small shim actually detects
the running platform and reads the home directory; keeping that shim tiny is
what makes everything else testable from either host.

**Case sensitivity is a real bug source here.** Linux filesystems are
case-sensitive and Windows is not, so a `*.fch` glob silently also matches
`.FCH` on Windows only. Enumeration is done by explicit case-insensitive
comparison rather than relying on glob behaviour, and it is covered by tests —
this is precisely the class of bug that passes on a developer's machine and
fails for a user.

**Never assert a literal absolute path in a test.** Compare against a path
composed the same way the production code composes it, or the suite becomes
bound to whichever machine wrote it.

**UI convention: native over identical.** Norn should feel native on each
platform rather than pixel-identical between them. Where the platforms
genuinely differ — window chrome, native dialogs, keyboard shortcuts — each
platform's own convention wins. One concrete consequence: OS-provided window
chrome is not a reliable place for state that must always be visible, because
Linux desktop environments render it inconsistently. Anything load-bearing
goes in Norn's own in-window content, which Avalonia draws identically
everywhere.

## 8. Smaller conventions worth knowing

**No XAML.** All UI is built in C#. There are no `.axaml` files, theme
registration happens in `Application.Initialize()`, and lifetime wiring happens
in `Program`. This is a deliberate preference, not a technical requirement —
informed by prior experience maintaining a WPF/XAML Valheim editor, where the
markup/code-behind split was the single biggest maintenance cost. It keeps UI
construction searchable and refactorable with ordinary C# tooling.

**Local saves only.** Norn reads the `characters` and `characters_local`
directories. Steam Cloud is deliberately out of scope: Norn has no way to tell
whether a cloud-eligible file is current, stale, or mid-sync, and editing one
risks a silent overwrite either direction.

**Persisted state is split by intent.** Two stores exist, and the distinction
matters when adding to one:

- **Settings** — forward-acting behavioural defaults: things that shape what
  happens to *new* data or actions, where a different user would reasonably
  want a different answer. These are surfaced as real, named, findable rows in
  the settings UI.
- **App state** — passive view and continuity state, the "wherever I left it"
  category, like a remembered window position or the last sort order. Silent,
  never surfaced as a configurable row.

The test is whether flipping it is something a user would consciously *do*, or
something the app should just quietly remember for them.
