namespace Norn.Adapter;

/// <summary>
/// Death-related game facts <c>Norn.UI</c> needs but doesn't own the
/// source of truth for. Not <c>GameCore</c>, not subject to
/// mirror-discipline. Each such fact carries a <c>game-derived:</c> tag
/// naming its source and the game version confirmed against.
/// </summary>
public static class DeathMechanics
{
    // game-derived: duplicate of GameCore, not independently sourced —
    // Norn.GameCore.Player.m_timeSinceDeath's field initializer. Re-exported
    // here rather than left for Norn.UI to hardcode its own copy — Norn.UI
    // can't reference GameCore directly, so without this,
    // the only alternative is a second, disconnected literal in UI that
    // wouldn't follow GameCore's own copy on a future patch. If GameCore's
    // initializer changes, only this line needs a matching manual update.
    public const float NeverDiedSentinel = 999999f;

    // game-derived: Player.m_hardDeathCooldown — decompiled Player.cs
    // initializes this public field to 10f, but it's Unity-serialized (no
    // [HideInInspector]) so the shipped prefab value overrides that at
    // runtime. Extracted from a re-imported AssetRipper export of Valheim
    // 0.221.10 (same export AppearanceColors.cs uses):
    // Assets/PrefabInstance/Player.prefab (the live, active gameplay
    // prefab) has 600. Two other occurrences — a disabled duplicate Player
    // object in Assets/Scenes/start.unity and one in
    // Assets/PrefabInstance/Backgroundscene.prefab, both inactive
    // main-menu-background decoration, not linked prefab instances — read
    // 10 instead. Those two are judged stale/menu-specific rather than
    // authoritative; 600 is not a unanimous 3-for-3 extraction the way
    // AppearanceColors' values were.
    //
    // This is Player.HardDeath()'s actual comparison threshold: a death
    // with Player.m_timeSinceDeath <= this value costs no skills (the
    // "corpse run" grace window; strictly-greater-than is the only case
    // that applies the penalty, so equality still protects).
    public const float HardDeathCooldownSeconds = 600f;
}
