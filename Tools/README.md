# Norn Tools

This directory contains tools needed by Norn at build or shipping time.

## Vade

_Note: Formerly `Vide` in a standalone repository, now archived:
<https://github.com/jensbrak/Vide> at commit `72c360e`. `Vade` starts fresh here with no
history of its own; that commit is the last standalone state, and the archived repository
is where the history before this point lives._

`Vade` is short for "VAlheim Data Extract" and this is exactly what it does: extracts data from Valheim.
It does not extract data directly but depends on a Unity asset ripper to do the heavy lifting first.
From there, `Vade` is simply a very crude and tailormade text extraction tool:
It converts selected output of a rip to data that Norn can use to display inventory items and names properly.

### Ripping checklist 

1. Get a release (or the source code) from: https://github.com/AssetRipper/AssetRipper 
1. Unpack (or build) AssetRipper and run it.
1. Load Valheim game content into AssetRipper:
	1. Select `File > Open Folder`.
	1. Locate and select the Valheim installation folder, typically `<Steam>\steamapps\common\Valheim\`.
	1. Wait for AssetRipper to load game content.
1. Export loaded game content from AssetRipper:
	1. Select `Export > Export all Files`.
	1. Select (create if needed) an export folder ***outside the Valheim installation folder***, e.g., `c:\tmp\`.
	1. Wait for AssetRipper to export game content.

### Usage

```
Vade source destination [/v] [/n]

  source        The assets root of an AssetRipper export, i.e. the `Assets`
                directory produced by the checklist above.
  destination   Where to write the CSVs.
  /v            Verbose: per-item progress, and the reasons items were
                classified as internal rather than player-facing.
  /n            Skip `LocalizationData.csv`, which is otherwise always written.
```

Both CSVs are written by default, because Norn needs both — `SharedItemData.csv`
for the item catalog, `LocalizationData.csv` for every display name outside it.
`/n` exists for the rare case of wanting only the item data; it is not something
a deploy for Norn ever wants.

The deploying-for-Norn call writes straight into the app's content directory, so
there is no copy step afterwards. Run it from the repo root, substituting your
own export path:

```
dotnet run --project Tools/Vade -- "<assets-root>" "Norn.UI\Content" /v
```

**In a POSIX shell, use the long-form switches.** Git Bash, WSL and similar
rewrite a lone `/v` into a filesystem path before the program ever sees it, so
short switches are silently dropped — nothing fails, they just do not take
effect. `--verbose` and `--no-localization` mean the same thing and survive any
shell:

```
dotnet run --project Tools/Vade -- "<assets-root>" "Norn.UI/Content" --verbose
```

Worth reading the tail of a verbose run rather than just the exit code. Three
lines there are the ones that matter on a patch day:

- the registered/player-facing/internal split, which should be roughly
  1500/1100/400 — a large swing means the classification is picking up or
  dropping a category;
- a warning naming items that have **no translation**, which are named by their
  raw token. A handful is normal (the game ships some that way); a sudden crowd
  of them means a localization file is missing from `LOCALIZATION_FILES`, which
  is exactly what happened on the first 1.0.7 run;
- a warning that registered item guids had **no prefab on disk**, which means the
  asset rip is incomplete rather than the game having changed.

### Credits

Original idea that `Vide` was built on was made by [Brandon-T](https://github.com/Brandon-T) in a discussion about `Loki` [here](https://github.com/Wufflez/Loki/issues/30). `Vade` however, has evolved far beyond those ideas but still, thanks!

