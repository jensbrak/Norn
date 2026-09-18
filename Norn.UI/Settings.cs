namespace Norn.UI;

/// <summary>
/// The single source of truth for every user-facing app preference — adding
/// a setting is adding one property here with a <see cref="SettingAttribute"/>;
/// <see cref="SettingsWindow"/> (generated UI) and <see cref="SettingsStore"/>
/// (JSON persistence) both derive from this reflectively, so neither needs a
/// matching edit. A plain mutable POCO, not a record: settings are changed in
/// place, one property at a time from the generated window — never replaced
/// wholesale the way <c>Norn.Adapter</c>'s DTOs are.
/// <para>
/// Grouping and ordering come from declaration order alone, top to bottom —
/// no separate "Order" field to keep in sync with how the properties actually
/// read in this file. Group order (General, Confirmations, Interface,
/// Worlds, Inventory) and each group's own row order follow a "most
/// significant first, loosely" rule — not alphabetical, not
/// insertion order.
/// </para>
/// <para>
/// Deciding whether a new field belongs here or in <see cref="AppState"/>:
/// see ARCHITECTURE.md's "AppState vs. Settings" test
/// (forward-acting behavioral default vs. passive view/continuity state) —
/// "does this persist across launches" alone doesn't distinguish the two.
/// </para>
/// </summary>
public sealed class Settings
{
    // Deliberately not itself version-aware (no on/major/minor granularity)
    // — plain on/off, one less decision for a feature
    // whose trigger condition (WelcomePolicy.ShouldShow) is already fixed
    // logic, not something a setting needs to tune.
    [Setting(Group = "General", Description = "Show a welcome message on first launch and after a major or minor update.")]
    public bool ShowWelcomePopup { get; set; } = true;

    // Not "Confirmations": that group is for adding a yes/no gate in front
    // of something already happening (ConfirmExit, ConfirmSave). This is a
    // feature toggle — off means the rename offer never exists at all, same
    // shape as ScanForLocalWorlds's own topical "Worlds" group below, not
    // "an extra confirmation this feature happens to also show."
    [Setting(Group = "General", Description = "Offer to rename the save file after changing the player name.")]
    public bool OfferFileRename { get; set; } = true;

    [Setting(Group = "Confirmations", Description = "Ask before exiting Norn, even with no unsaved changes.")]
    public bool ConfirmExit { get; set; } = true;

    // Only the toolbar Save button and Ctrl+S check this — never the
    // dirty-guard dialogs' own "Save" choice (switching files, closing with
    // unsaved changes). Choosing Save there already *is* the confirmation;
    // asking again would double-prompt on a decision the user just made.
    [Setting(Group = "Confirmations", Description = "Ask before saving changes to disk.")]
    public bool ConfirmSave { get; set; } = true;

    // Norn's first non-bool setting — SettingsWindow.BuildControl grew a
    // generic enum -> ComboBox case for this, not a one-off special case,
    // so the next enum-typed setting (any flavor) gets it for free.
    [Setting(Group = "Interface", Description = "Follow the OS theme, or force light/dark regardless of it.")]
    public AppThemePreference Theme { get; set; } = AppThemePreference.System;

    // Deliberately not gating individual mutators (an earlier version of
    // this setting did, on Inventory's Delete specifically): nothing is
    // actually destructive until Save writes it to disk — Revert already
    // fully undoes any in-session mutation, no matter which button caused
    // it. A per-action confirm also turned out to need a "how risky is
    // this specific button" judgment call that didn't hold up under
    // scrutiny: a spawn point can matter as much as
    // a whole world's map data — data size isn't a proxy for what someone
    // would regret losing).
    //
    // Settled on a different, simpler split instead (same day): every icon
    // button gets its own tooltip explaining what it actually does — no
    // "is this one dangerous enough" classification, no "is it already
    // explained elsewhere" check, every button the same way, always.
    // Context/caveats worth knowing (a cross-tab side effect, a real
    // gameplay consequence) live separately, in a row's description or a
    // tab's legend, judged only on whether there's something worth saying
    // — the two layers don't gate each other.
    [Setting(Group = "Interface", Description = "Show a tooltip on every icon button explaining what it does.")]
    public bool ShowButtonTooltips { get; set; } = true;

    // Topmost of the three: this one governs an app-wide startup behavior
    // (whether the .fwl scan runs at all), heavier-weight as a setting than
    // the other two, which only control visibility of dismissible legend
    // text on this one tab.
    [Setting(Group = "Worlds", Description = "Scan for local world files on startup, to resolve world names/seeds on the Worlds tab.")]
    public bool ScanForLocalWorlds { get; set; } = true;

    [Setting(Group = "Worlds", Description = "Show the warning about what removal actions on this tab affect.")]
    public bool ShowWorldsRemovalWarning { get; set; } = true;

    [Setting(Group = "Worlds", Description = "Show the explanation of where world names/seeds come from.")]
    public bool ShowWorldsIdentityInfo { get; set; } = true;

    [Setting(Group = "Worlds", Description = "Show the warning about what \"Explore All\" does to a world's map.")]
    public bool ShowExploreWorldWarning { get; set; } = true;

    // Moved off AppState, 2026-09-02: a stuck-checked last-used value and a
    // deliberate, values-based default read the same way in AppState's own
    // "silently remembered, never exposed" storage, but they aren't the same
    // thing — the worked example is exactly the "forward-acting
    // behavioral default" shape this file exists for, not AppState's
    // "passive view/continuity state" shape (sort order stays AppState for
    // that reason).
    //
    // Both default true, 2026-09-02 (revised from an initial false/false —
    // a follow-up review): "Set crafter tag" now replicates real game
    // behavior safely, since the per-item gate already stops it
    // from ever applying to an item the game itself couldn't tag — nothing
    // left for a cautious default to protect against. Defaulting the add
    // amount to max isn't a personal preference either, but is judged the
    // likely majority want for people using Norn (a convenience toggle, not
    // a values-based restraint the way crafter tag was originally treated)
    // — both are one Settings toggle away from off for whoever disagrees,
    // which is the point of these being real Settings instead of neither
    // configurable nor findable.
    [Setting(Group = "Inventory", Description = "Default state of the \"Set crafter tag\" checkbox when adding an item.")]
    public bool DefaultSetCrafterTagOnAdd { get; set; } = true;

    [Setting(Group = "Inventory", Description = "Default the amount field to the item's max stack when adding an item (otherwise starts at 1).")]
    public bool DefaultAmountToMaxOnAdd { get; set; } = true;
}
