Help content for the Help window where each section gets indexed. Everything
above the first `## ` heading (this paragraph included) is discarded by
the parser, so it's safe as editor-facing instructions rather than content.

Note to self/guidelines: Deliberately short, concise and bulleted sentences. 
Try to avoid repeating information that is already in the main UI.
Help is about complementing the UI. Limit use of "you" or "your" in the text
(Deliberate use in "before you start" as it is important info before usage.)
Three bullets per section is actually coincidental but feels like a good number. 
Not a rule, just a guideline. Use more or less as needed.

|<---Lines wider than this line will be wrapped in UI; do it deliberately or avoid.--->|

## Norn is

- An unofficial character editor for Valheim with open source code on GitHub.
- NOT affiliated with Iron Gate AB or Coffee Stain Publishing. 
- Inspired by "Loki" but created completely from scratch with a different architecture. 

## Before you start

- Use Norn at your own risk and discretion. Make manual backups!
- Use Valheim's "Manage saves" to move your character's save file from Cloud to Local.
  (Norn only works with local save files. It does not work with cloud saves, by design.)
- Remember: "Revert" only works before saving. Saved changes are permanent!

## Updates and issues

- Report issues or request features on GitHub. See "About" to get link.
- IF Norn breaks as a result of a Valheim update - do NOT report an issue about this:
  Norn will likely be updated "ASAP" but please remember it IS a hobby project.

## Tab: General

- Known commands are console commands and usage count is within parenthesis.
- Trophies as beard or hair vary in result and is considered experimental.
  (Kudos to Wuffles/Loki for this feature!)
- Skin and hair color is an approximation of how the game works, it's not precise.  

## Tab: Vitals

- Restore only fills up to current max, it does not increase max.
- Time since death is just a counter but resetting it mimics "No skill loss" buff.
- Removing foods here only affects the effect, it does not remove food from inventory.

## Tab: Inventory

- Right click on a slot to interact with it. Actions available depend on slot contents.
- Left click: fill stack/repair item directly. Ctrl + Left click: delete slot contents.
- Crafter tag can only be applied to items the game itself would tag with crafter name.

## Tab: Skills

- Default value for "Raise all skills by" compensates for the game's 5% loss on death.
- Only skills that are unlocked are shown, the others are not part of the save file.
- Current XP and XP required to next skill level shown within parenthesis.

## Tab: Worlds

- The world name matching feature only reads local world files, never writes to them.
- Death point likely legacy. Spawn point removed: character will spawn at map center.
- Worlds removed from a character are listed in the "Removed worlds" section
  (only world exploration for the character is removed, the world itself is unaffected.)

## Tab: Unlockables

- Unlockables are deliberately read only, at least for now. Makes no sense to edit?
- Yes, "none" is a Biome as far as Valheim is concerned. 
- Uniques may be connected to the upcoming achievements feature of Valheim?

## Tab: Statistics

- Statistics are deliberately read only, at least for now. Makes no sense to edit?
- Exception is "cheats" counter which is reset by clearing cheats flag in tab General.
