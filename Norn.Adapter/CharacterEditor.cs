using Norn.GameCore;
using Norn.GameCore.Primitives;
using GameVersion = Norn.GameCore.Version;

namespace Norn.Adapter;

/// <summary>
/// One open save-file editing session: retains the loaded <see cref="PlayerProfile"/>
/// so <see cref="Save"/>/<see cref="Revert"/> don't require re-scanning a directory.
/// Plain, passive class — no Avalonia types and no UI-binding/reactivity pattern of
/// any kind; the UI owns all of that on top of this. The write path
/// this calls through (<see cref="PlayerProfile.SavePlayerToDisk()"/>) is internal,
/// opened to this assembly deliberately for exactly the field wired up below.
/// </summary>
public sealed class CharacterEditor
{

    private PlayerProfile _profile;

    /// <summary>
    /// The decoded inner player-data blob, retained (not just read once and
    /// discarded like <see cref="CharacterLoader.Map"/>'s own internal decode)
    /// so an inner-blob field edit has something to mutate and re-encode.
    /// <c>null</c> exactly when <see cref="PlayerProfile.m_playerData"/> is —
    /// a profile with no inner blob at all. Decoded independently
    /// of the <see cref="CharacterLoader.Map"/> call below rather than
    /// threading it through — a second small decode of a save-file-sized
    /// blob is not worth reshaping that method's existing, tested contract
    /// for.
    /// </summary>
    private Player? _player;

    /// <summary>
    /// The player name as of the last <see cref="Open"/>/<see cref="Save"/>/
    /// <see cref="Revert"/> — the baseline <see cref="PlayerNameChangedThisSession"/>
    /// compares against, so a save-time rename offer (<c>MainWindow</c>) only
    /// fires when the name was actually touched since the file was last
    /// synced to disk, not on every save of a file whose name happened to
    /// already differ from its filename.
    /// </summary>
    private string _playerNameBaseline;

    /// <summary>
    /// The file's on-disk last-write time as of the last time this session
    /// synced with disk (<see cref="Open"/>/<see cref="Save"/>/<see cref="Revert"/>)
    /// — <see cref="HasChangedOnDisk"/>'s baseline.
    /// </summary>
    private DateTime _lastKnownWriteTimeUtc;

    public string Path { get; private set; }
    public CharacterView View { get; private set; }
    public bool IsDirty { get; private set; }

    /// <summary>True once <see cref="View"/>'s player name has diverged from
    /// the baseline captured at the last open/save/revert.</summary>
    public bool PlayerNameChangedThisSession => View.Meta.PlayerName != _playerNameBaseline;

    private CharacterEditor(string path, PlayerProfile profile, Player? player, CharacterView view)
    {
        Path = path;
        _profile = profile;
        _player = player;
        View = view;
        _playerNameBaseline = view.Meta.PlayerName;
        _lastKnownWriteTimeUtc = File.GetLastWriteTimeUtc(path);
    }

    /// <summary>
    /// True when the file's on-disk last-write time no longer matches what
    /// it was the last time this session synced with disk. This is the
    /// actual data-safety mechanism behind the read-only-mode design —
    /// not "is Valheim currently running"
    /// (`GameProcessShim`, checked elsewhere, stays purely advisory and
    /// unrelated to this), but "did *something* — most plausibly Valheim,
    /// mid-session — write this exact file while this session's own pending
    /// edit was already based on an older version of it." That risk
    /// survives even after the game closes, since a pending edit computed
    /// against a stale base doesn't become safe just because the writer
    /// that raced it is gone.
    /// <para>
    /// Deliberately last-write-time only, not a content hash — a
    /// reasonable safeguard meant to minimize this risk, not one engineered
    /// to close every corner of it: a hash would
    /// only additionally catch "rewritten with byte-identical content,"
    /// which isn't worth the extra full-file read on every save. A
    /// deliberately unclosed gap for the same reason: a change landing in
    /// the instant between this check and <see cref="Save"/>'s actual write
    /// isn't caught — vanishingly unlikely for a single-user desktop app,
    /// and the same class of risk any file-based tool already accepts.
    /// </para>
    /// </summary>
    public bool HasChangedOnDisk() => File.GetLastWriteTimeUtc(Path) != _lastKnownWriteTimeUtc;

    /// <summary>
    /// Opens and retains a profile, or <c>null</c> when it's outside the
    /// compatible version range — same contract as <see cref="CharacterLoader.Load"/>.
    /// </summary>
    public static CharacterEditor? Open(string path) => Open(path, out _);

    /// <summary>
    /// Same as <see cref="Open(string)"/>, but also reports why a refusal
    /// happened when it's version-related, so a caller can tell a user
    /// something more specific than "this didn't open." <paramref name="incompatible"/>
    /// is <c>null</c> on success, and also <c>null</c> when the file fails to
    /// open for a reason other than an out-of-range profile version (a
    /// truncated or corrupt file throws before this point is ever reached).
    /// </summary>
    public static CharacterEditor? Open(string path, out IncompatibleVersion? incompatible)
    {
        // A null path is not a version problem: PlayerProfile.Load()'s own
        // `m_filename != null` guard is the one load failure that returns
        // false silently, leaving ProfileVersion at its default 0. Every
        // other load failure either sets ProfileVersion before the
        // version-compatibility check fails (a real version problem) or
        // throws before reaching here, so a null path is special-cased to
        // keep `incompatible` null for a non-version failure, per this
        // method's own documented contract.
        if (path is null)
        {
            incompatible = null;
            return null;
        }

        // Structural pre-check before the mirror sees the file at all — see
        // SaveFileFrameGuard for the allocation hazard this closes.
        SaveFileFrameGuard.ThrowIfFrameImplausible(path);

        var profile = new PlayerProfile(path);
        if (profile.Load())
        {
            incompatible = null;
            return new CharacterEditor(path, profile, PlayerLoader.Load(profile), CharacterLoader.Map(profile));
        }

        incompatible = new IncompatibleVersion(
            profile.ProfileVersion,
            (int)GameVersion.Player.OldestForwardCompatible,
            (int)GameVersion.c_PlayerVersion,
            profile.ProfileVersion > (int)GameVersion.c_PlayerVersion);
        return null;
    }

    /// <summary>
    /// The one field this slice makes editable. Updates only the
    /// <see cref="CharacterView.Meta"/> slice via a record <c>with</c>
    /// expression rather than a full <see cref="CharacterLoader.Map"/> remap
    /// — no other DTO can ever depend on the player name (verified: none of
    /// the Skills/Vitals/Inventory/Statistics/Worlds mappers read it), so a
    /// full remap would silently re-decode the entire player-data blob for
    /// no reason on every call. That matters because this is called on every
    /// keystroke, not once per edit session — the targeted update
    /// is what keeps that cheap.
    /// </summary>
    public void SetPlayerName(string name)
    {
        _profile.SetName(name);
        View = View with { Meta = View.Meta with { PlayerName = name } };
        IsDirty = true;
    }

    /// <summary>
    /// Clears <c>m_usedCheats</c> and the paired <see cref="PlayerStatType.Cheats"/>
    /// counter together — deliberately one-directional (there is no legitimate
    /// reason to programmatically mark a save as cheated when it wasn't), and
    /// deliberately paired rather than picking one as authoritative: the two
    /// fields appear related on the wire, but nothing in the mirrored read
    /// path establishes which one drives the other, so this clears both
    /// rather than leaving the stat behind. Updates only <c>Meta</c> and
    /// <c>Statistics</c> — the two DTOs that actually carry these two fields
    /// — via targeted <c>with</c> expressions, same reasoning as
    /// <see cref="SetPlayerName"/>. Unlike that hot per-keystroke path, this
    /// fires once per click, so re-running <see cref="StatisticsMapper.Map"/>
    /// for the Statistics slice (rather than patching one entry out of its
    /// list by hand) is the simpler correct choice here, not a cost concern.
    /// </summary>
    /// <remarks>
    /// <c>onEdited</c> is deliberately chrome-only and does not
    /// rebuild tab content in general, but this call replaces
    /// <c>View.Statistics</c> with a genuinely new instance (unlike the
    /// targeted <c>with</c> updates every other mutator uses), which
    /// <c>Norn.UI.MainWindow</c> detects by reference and uses to rebuild
    /// just the Statistics tab's already-built <c>Control</c> tree — no
    /// reload/revert needed for the "Cheats" counter to reflect the clear.
    /// </remarks>
    public void ClearUsedCheats()
    {
        _profile.m_usedCheats = false;

        // Every slot, not just slot 0. The game writes a scalar stat to slot 0
        // unconditionally, to slot 1 when the play was not cheated, and to the
        // active difficulty's slot — so a Cheats counter can be sitting in
        // three places at once. Clearing only slot 0 would leave the tab still
        // reporting cheats under a different selector entry, which is worse
        // than not offering the action at all.
        foreach (var stats in _profile.m_playerStats)
        {
            stats[PlayerStatType.Cheats] = 0f;
        }
        View = View with
        {
            Meta = View.Meta with { UsedCheats = false },
            Statistics = StatisticsMapper.Map(_profile),
        };
        IsDirty = true;
    }

    /// <summary>
    /// Sets skin color to an already-computed final value — a "dumb" setter
    /// taking the result, same shape as <see cref="SetPlayerName"/>, not a
    /// slider position. The domain knowledge of what values the game's own
    /// UI can actually produce (the lerp between two Unity-authored endpoint
    /// colors) lives in
    /// <c>Norn.UI</c>'s <c>AppearanceColors</c>, not here: those endpoint
    /// constants aren't wire-format facts <c>GameCore</c> needs (the two
    /// <c>Vector3</c> fields round-trip correctly regardless), so they don't
    /// belong on this side of the boundary, and <c>Adapter</c> can't
    /// reference <c>Norn.UI</c> to use them from here even if it wanted to.
    /// No-ops if <see cref="_player"/> is <c>null</c> (no inner blob to
    /// edit) — the UI is expected not to offer this control in that state,
    /// but a defensive no-op here costs nothing and avoids a crash if it
    /// ever does regardless.
    /// </summary>
    public void SetSkinColor(ColorDto color)
    {
        if (_player is null)
        {
            return;
        }

        _player.m_skinColor = new Vector3(color.R, color.G, color.B);
        _profile.m_playerData = PlayerLoader.Save(_player);
        View = View with { Meta = View.Meta with { SkinColor = color } };
        IsDirty = true;
    }

    /// <summary>Same shape and reasoning as <see cref="SetSkinColor"/>, for hair color.</summary>
    public void SetHairColor(ColorDto color)
    {
        if (_player is null)
        {
            return;
        }

        _player.m_hairColor = new Vector3(color.R, color.G, color.B);
        _profile.m_playerData = PlayerLoader.Save(_player);
        View = View with { Meta = View.Meta with { HairColor = color } };
        IsDirty = true;
    }

    /// <summary>
    /// Sets the beard prefab name directly — a "dumb" setter, same shape as
    /// <see cref="SetSkinColor"/>. What makes this an ordinary editable field
    /// rather than the unsupported-feature-gating territory an unconstrained
    /// string setter would fall into (e.g. a trophy
    /// prefab as a beard) is entirely on the caller's side: Norn.UI's picker
    /// only ever offers names resolved from <see cref="SharedItemDataCatalog"/>'s
    /// <c>Customization</c> entries, so nothing reaching here is a value the
    /// game's own character customization couldn't produce. This setter has
    /// no way to enforce that itself and doesn't try to.
    /// </summary>
    public void SetBeardItem(string itemName)
    {
        if (_player is null)
        {
            return;
        }

        _player.m_beardItem = itemName;
        _profile.m_playerData = PlayerLoader.Save(_player);
        View = View with { Meta = View.Meta with { BeardItem = itemName } };
        IsDirty = true;
    }

    /// <summary>Same shape and reasoning as <see cref="SetBeardItem"/>, for hair.</summary>
    public void SetHairItem(string itemName)
    {
        if (_player is null)
        {
            return;
        }

        _player.m_hairItem = itemName;
        _profile.m_playerData = PlayerLoader.Save(_player);
        View = View with { Meta = View.Meta with { HairItem = itemName } };
        IsDirty = true;
    }

    /// <summary>
    /// Sets the model index directly — same "dumb setter" shape as
    /// <see cref="SetBeardItem"/>, constrained by the caller (Norn.UI's
    /// picker, built from <see cref="CharacterModels.Known"/>) rather than
    /// here. Deliberately does not touch <see cref="_player"/>'s beard/hair
    /// fields: the game's own character-creation screen clears the beard
    /// when switching to the female model
    /// (<c>PlayerCustomizaton.SetPlayerModel</c>'s <c>ResetBeard()</c> call),
    /// but that is UI convenience, not something <c>VisEquipment</c>
    /// enforces — a beard renders fine on either model. Norn leaves the two
    /// independent, matching how skin/hair color already stay independent
    /// of everything else.
    /// </summary>
    public void SetModelIndex(int index)
    {
        if (_player is null)
        {
            return;
        }

        _player.m_modelIndex = index;
        _profile.m_playerData = PlayerLoader.Save(_player);
        View = View with { Meta = View.Meta with { ModelIndex = index } };
        IsDirty = true;
    }

    /// <summary>
    /// Shared shape for the Vitals/Skills/Inventory mutators below:
    /// null-guard, mutate the retained <see cref="_player"/>, re-encode via
    /// <see cref="PlayerLoader.Save"/>, let the caller patch whichever
    /// slice(s) of <see cref="View"/> actually changed, mark dirty.
    /// <see cref="SetSkinColor"/>/<see cref="SetHairColor"/> don't route
    /// through this helper — their null-check-then-mutate shape is simple
    /// enough on its own that sharing it here wouldn't remove much.
    /// <para>
    /// <paramref name="mutate"/> returns whether it actually changed
    /// anything, and nothing downstream runs when it didn't — re-encoding is
    /// not neutral, since <c>Player.Save</c> always writes its own current
    /// version literal, so re-encoding on a true no-op (an empty slot, a
    /// stale index, an unresolved item) would silently upgrade the inner
    /// blob's format and mark the session dirty for a change that never
    /// happened.
    /// </para>
    /// </summary>
    private void MutateInnerBlob(Func<Player, bool> mutate, Action<Player> updateView)
    {
        if (_player is null)
        {
            return;
        }

        if (!mutate(_player))
        {
            return;
        }

        _profile.m_playerData = PlayerLoader.Save(_player);
        updateView(_player);
        IsDirty = true;
    }

    /// <summary>Full heal — sets current health to max. A maximize action
    /// (Vitals tab's ↑ button), not a free-form value edit. Already at max
    /// is a genuine no-op: exact equality is the right test here, since the
    /// assignment would write that exact same value.</summary>
    public void RestoreHealth() => MutateInnerBlob(
        p =>
        {
            if (p.m_health == p.m_maxHealth)
            {
                return false;
            }

            p.m_health = p.m_maxHealth;
            return true;
        },
        p => View = View with { Vitals = VitalsMapper.Map(p) });

    /// <summary>Same shape as <see cref="RestoreHealth"/>, for stamina.</summary>
    public void RestoreStamina() => MutateInnerBlob(
        p =>
        {
            if (p.m_stamina == p.m_maxStamina)
            {
                return false;
            }

            p.m_stamina = p.m_maxStamina;
            return true;
        },
        p => View = View with { Vitals = VitalsMapper.Map(p) });

    /// <summary>Same shape as <see cref="RestoreHealth"/>, for eitr.</summary>
    public void RestoreEitr() => MutateInnerBlob(
        p =>
        {
            if (p.m_eitr == p.m_maxEitr)
            {
                return false;
            }

            p.m_eitr = p.m_maxEitr;
            return true;
        },
        p => View = View with { Vitals = VitalsMapper.Map(p) });

    // game-derived: Player.HardDeath/OnDeath's "corpse run" grace-window
    // behavior (Valheim 0.221.10/0.221.4, confirmed in both trees) —
    // described in prose only, no numeric threshold reproduced here.
    /// <summary>
    /// Sets the death timer to zero. This is a *grant*, not a punishment:
    /// a low <c>m_timeSinceDeath</c> is the game's own "corpse run" grace
    /// window (Player.HardDeath/OnDeath — a subsequent death within it costs
    /// no skills), so resetting this field gives the character that
    /// protection without an actual death, rather than pushing the value
    /// toward the ~999999 "never died" sentinel, which would do the
    /// opposite.
    /// </summary>
    public void ResetTimeSinceDeath() => MutateInnerBlob(
        p =>
        {
            if (p.m_timeSinceDeath == 0f)
            {
                return false;
            }

            p.m_timeSinceDeath = 0f;
            return true;
        },
        p => View = View with { Vitals = VitalsMapper.Map(p) });

    /// <summary>
    /// Removes one active-food entry by its position in
    /// <see cref="VitalsDto.ActiveFoods"/> <i>at the time of the call</i>.
    /// <c>Food</c> has no stable identity to key on instead, so the caller
    /// (<c>VitalsTabModule</c>) is responsible for re-reading the list —
    /// and therefore re-deriving every row's index — after each removal
    /// rather than reusing indices captured before an earlier removal
    /// shifted them. No-ops on a stale/out-of-range index (e.g. a
    /// double-fired UI remove click) rather than throwing, matching every
    /// other by-position mutator here.
    /// </summary>
    public void RemoveFood(int index) => MutateInnerBlob(
        p =>
        {
            if (index < 0 || index >= p.m_foods.Count)
            {
                return false;
            }

            p.m_foods.RemoveAt(index);
            return true;
        },
        p => View = View with { Vitals = VitalsMapper.Map(p) });

    /// <summary>
    /// Sets one skill's level (drag), identified by <see cref="SkillDto.Type"/>'s
    /// string form rather than an index — unlike <see cref="RemoveFood"/>'s
    /// active-food entries, a skill has a stable identity
    /// (<c>Skills.Skill.m_type</c>) to key on, so there's no reason to fall
    /// back to positional addressing. Clamped to <see cref="SkillProgression.MaxLevel"/>,
    /// the range the game itself keeps levels in. Zeroes the skill's
    /// accumulator: a level change voids XP progress toward whatever level
    /// it was previously working from, and it's what the game's own death
    /// penalty does too (<c>Skills.OnDeath</c> zeroes the accumulator
    /// alongside lowering the level — confirmed in the decompiled source,
    /// not just an editor convention). No-op if the named skill isn't
    /// present — the UI only ever offers rows for skills
    /// <see cref="SkillsMapper"/> already produced, same trust-the-caller
    /// shape as <see cref="RemoveFood"/>.
    /// </summary>
    public void SetSkillLevel(string type, float level) => MutateInnerBlob(
        p =>
        {
            var skill = p.m_skills.m_skillData.FirstOrDefault(s => s.m_type.ToString() == type);
            if (skill is null)
            {
                return false;
            }

            skill.m_level = Math.Clamp(level, 0f, SkillProgression.MaxLevel);
            skill.m_accumulator = 0f;
            return true;
        },
        p => View = View with { Skills = SkillsMapper.Map(p) });

    /// <summary>
    /// Bulk-raises every present skill's level by <paramref name="percent"/>
    /// via <see cref="SkillProgression.ApplyDeathCompensation"/> — the
    /// inverse of how <c>Skills.OnDeath</c> itself removes levels. Each
    /// result's accumulator is zeroed, same reasoning as
    /// <see cref="SetSkillLevel"/>.
    /// </summary>
    public void CompensateSkillsForDeath(float percent) => MutateInnerBlob(
        p =>
        {
            var changed = false;
            foreach (var skill in p.m_skills.m_skillData)
            {
                var compensated = SkillProgression.ApplyDeathCompensation(skill.m_level, percent);
                if (compensated == skill.m_level && skill.m_accumulator == 0f)
                {
                    continue;
                }

                skill.m_level = compensated;
                skill.m_accumulator = 0f;
                changed = true;
            }

            return changed;
        },
        p => View = View with { Skills = SkillsMapper.Map(p) });

    /// <summary>
    /// Repairs one item to its catalog-derived max durability
    /// (<see cref="SharedItemDataDto.MaxDurabilityFor"/>), addressed by grid
    /// position — matching Loki's own <c>Inventory.GetSlotAt(Vector2i)</c>
    /// addressing. No-op if the slot
    /// is empty, the item isn't in <see cref="SharedItemDataCatalog"/>, or it
    /// doesn't use durability at all — an unresolved item's repair action is
    /// simply unavailable, not an error.
    /// </summary>
    public void RepairItemAt(int x, int y) => MutateInnerBlob(
        p =>
        {
            var item = FindItem(p, x, y);
            return item is not null && Repair(item);
        },
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    /// <summary>
    /// Sets one item's quality, floored at 1 — no enforced ceiling.
    /// Valheim's own Forge of Potential refinement mechanic (an idol-based,
    /// probabilistic upgrade path) legitimately exceeds a catalog's
    /// <c>MaxQuality</c>; Norn doesn't invent a cap the real game doesn't
    /// have. No-op if the slot is empty or the item has no quality levels at all.
    /// Auto-repairs to the item's new max durability afterward — replicates a
    /// real workbench quality upgrade, which always fully repairs; decoupling
    /// them would let an edit produce a durability/quality combination the
    /// real game can never produce.
    /// </summary>
    public void SetItemQuality(int x, int y, int quality) => MutateInnerBlob(
        p =>
        {
            var item = FindItem(p, x, y);
            var shared = item is null ? null : ItemPrefabHashes.TryFindShared(item);
            if (item is null || shared is null || shared.MaxQuality <= 1)
            {
                return false;
            }

            var clamped = Math.Max(quality, 1);
            var qualityChanged = item.m_quality != clamped;
            item.m_quality = clamped;

            // Repair first, unconditionally (the quality change moves the
            // durability ceiling), then OR — not short-circuited away.
            var repaired = Repair(item);
            return repaired || qualityChanged;
        },
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    /// <summary>Fills one item's stack to its catalog max. No-op if the slot
    /// is empty or the item isn't stackable.</summary>
    public void FillItemStack(int x, int y) => MutateInnerBlob(
        p =>
        {
            var item = FindItem(p, x, y);
            return item is not null && FillStack(item);
        },
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    /// <summary>
    /// Sets one item's exact stack, clamped to <c>[1, SharedData.MaxStack]</c> —
    /// same clamp-to-bound shape as <see cref="SetItemQuality"/>. Deliberately
    /// a floor of 1, not 0: this method's contract is "1 to max," not "0 to
    /// max" — a previous, since-removed version of this method floored at 0,
    /// but reducing to nothing isn't a supported path here;
    /// <see cref="RemoveItemAt"/> is the separate, existing action for that.
    /// No-op if the slot is empty,
    /// the item isn't in the catalog, or it isn't stackable at all.
    /// </summary>
    public void SetItemStack(int x, int y, int stack) => MutateInnerBlob(
        p =>
        {
            var item = FindItem(p, x, y);
            var shared = item is null ? null : ItemPrefabHashes.TryFindShared(item);
            if (item is null || shared is null || shared.MaxStack <= 1)
            {
                return false;
            }

            var clamped = Math.Clamp(stack, 1, shared.MaxStack);
            var changed = item.m_stack != clamped;
            item.m_stack = clamped;
            return changed;
        },
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    // game-derived: InventoryGui.DoCrafting (Valheim 0.221.10/0.221.4,
    // confirmed identical in both trees) is the game's only call
    // site that stamps a real identity onto m_crafterID/m_crafterName — it
    // reads the crafting player's own GetPlayerID()/GetPlayerName()
    // unconditionally, with no ItemType gate of any kind: every crafted or
    // upgraded item gets stamped, food and materials included, not just
    // equipment.
    // note:    The real constraint isn't item type, it's whether a prefab is
    //          ever the m_item of an ObjectDB.m_recipes entry at all —
    //          CookingStation/Smelter/Fermenter outputs never reach
    //          DoCrafting (they're Instantiate'd directly, crafter fields
    //          untouched), so cooking-station output, smelted metals/coal,
    //          and fermented drinks can no more organically carry a tag than
    //          a pure drop-only item like Acorn can. The split is about
    //          which *station* made the item, not whether it's food or
    //          whether heat was involved: a cauldron is a crafting station
    //          with a recipe UI, so everything from it goes through
    //          DoCrafting and IS tagged (QueensJam, Sausages, CarrotSoup,
    //          TurnipStew, OnionSoup, BloodPudding, FishWraps — all
    //          CanHaveCrafterTag=True in the catalog); a cooking station is
    //          a conversion rack, so its output is not (CookedMeat,
    //          CookedDeerMeat, CookedWolfMeat, FishCooked, NeckTailGrilled —
    //          all False). Confirmed in-game: cooking in a cauldron stamps
    //          the player's own name on the result.
    // note:    The catalog's own extraction resolves ObjectDB.m_recipes' own
    //          m_item references against the authoritative recipe list,
    //          producing SharedItemData.csv's CanHaveCrafterTag column — the
    //          setter below gates on that per-item lookup (see the check
    //          inside the mutator itself), never on a caller-supplied
    //          item-type classification.
    /// <summary>
    /// Stamps one item as crafted by this profile's own identity — never an
    /// arbitrary crafter picker, since Norn has no knowledge of any other
    /// character's <c>PlayerId</c>/name to offer instead (matches the
    /// original scoping). No-op if the slot is
    /// empty, already stamped as this profile, or the resolved catalog entry
    /// says the item can never carry a tag — an unresolved item
    /// (not in the catalog at all) is allowed, not blocked, same "unknown
    /// isn't unsupported" reasoning as <see cref="SharedItemDataDto"/>'s own
    /// missing-column default. Overwriting an existing (possibly different)
    /// crafter is deliberately allowed with no separate confirmation — the
    /// real game does exactly this on every upgrade craft, silently
    /// replacing the previous crafter with the upgrader's identity.
    /// </summary>
    public void SetItemCrafterAt(int x, int y) => MutateInnerBlob(
        p =>
        {
            var item = FindItem(p, x, y);
            if (item is null || item.m_crafterID == _profile.m_playerID)
            {
                return false;
            }

            // Per-item gate: an unresolved item (not in the catalog
            // at all) defaults to allowed, same reasoning as
            // InventoryTabModule.CanSetCrafterToSelf — only a positively
            // resolved "false" blocks.
            var shared = ItemPrefabHashes.TryFindShared(item);
            if (shared is not null && !shared.CanHaveCrafterTag)
            {
                return false;
            }

            item.m_crafterID = _profile.m_playerID;
            item.m_crafterName = _profile.m_playerName;
            return true;
        },
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    // game-derived: ItemDrop.ItemData.GetTooltip (Valheim 0.221.10/0.221.4,
    // confirmed in both trees) keys its "Crafted by" line solely on
    // `m_crafterID != 0` — the name is never checked — so 0 is the game's
    // own "no crafter" sentinel, not a Norn convention.
    /// <summary>
    /// Clears one item's crafter tag back to the game's own "uncrafted"
    /// state. No-op if already unset.
    /// </summary>
    public void ClearItemCrafterAt(int x, int y) => MutateInnerBlob(
        p =>
        {
            var item = FindItem(p, x, y);
            if (item is null || item.m_crafterID == 0)
            {
                return false;
            }

            item.m_crafterID = 0;
            item.m_crafterName = "";
            return true;
        },
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    /// <summary>
    /// Clears one item's cheated taint. No in-game action ever clears
    /// <c>m_cheated</c> once set — same editor-exclusive precedent as
    /// <see cref="ClearUsedCheats"/>: there's no legitimate reason to
    /// programmatically mark something cheated that wasn't, so this only
    /// ever clears the flag, never sets it. No-op if already unset.
    /// </summary>
    public void ClearItemCheatedAt(int x, int y) => MutateInnerBlob(
        p =>
        {
            var item = FindItem(p, x, y);
            if (item is null || !item.m_cheated)
            {
                return false;
            }

            item.m_cheated = false;
            return true;
        },
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    // game-derived: Inventory.AddItem(string name, int stack, int quality,
    // int variant, long crafterID, string crafterName, Vector2i position,
    // bool pickedUp) (Valheim 0.221.10/0.221.4, confirmed in both trees)
    // — the real game's own
    // "materialize a new item into this inventory" call. This mutator
    // mirrors it at its simplest: fixed stack 1, quality 1, no crafter (a
    // separate, deliberate action — see SetItemCrafterAt).
    /// <summary>
    /// Inserts a brand-new item at an already-empty grid position — the only
    /// mechanism Norn's "Add Item" feature needs. No-op if the slot is
    /// already occupied, or <paramref name="prefabName"/> doesn't resolve
    /// against <see cref="SharedItemDataCatalog"/> — <c>Norn.UI</c>'s picker
    /// only ever offers catalog entries, so a miss here means a caller bug,
    /// not a reachable user path, and this fails safe rather than inserting
    /// a broken item. Durability is only set when the resolved item actually
    /// uses it (mirrors <see cref="Repair"/>'s own guard); otherwise left at
    /// <see cref="Inventory.ItemData"/>'s own class default.
    /// </summary>
    public void AddItemAt(int x, int y, string prefabName) => MutateInnerBlob(
        p =>
        {
            // Grid-bounds check: an out-of-grid position wouldn't collide
            // with FindItem's scan below, so it would otherwise insert
            // silently off-grid and round-trip with no range filter, a shape
            // the real game's own inventory-grid logic never produces.
            if (x < 0 || x >= InventoryLayout.Width || y < 0 || y >= InventoryLayout.Height)
            {
                return false;
            }

            if (FindItem(p, x, y) is not null)
            {
                return false;
            }

            var shared = SharedItemDataCatalog.TryFind(prefabName);
            if (shared is null)
            {
                return false;
            }

            var item = new Inventory.ItemData
            {
                PrefabName = prefabName,
                // Item version 108+ writes only the hash, and an item whose
                // hash is 0 is dropped by the game on load. Setting it here is
                // what makes a newly added item survive a save at all — the
                // name alone no longer reaches the file. This is the same
                // resolution Inventory.LoadOld performs for a legacy save, by
                // the game's own hash function.
                PrefabHash = StringExtensionMethods.GetStableHashCode(prefabName),
                m_stack = 1,
                m_quality = 1,
                m_gridPos = new Vector2i(x, y),
                m_pickedUp = true,
            };

            if (shared.UsesDurability)
            {
                item.m_durability = (float)shared.MaxDurabilityFor(1);
            }

            p.m_inventory.m_inventory.Add(item);
            return true;
        },
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    /// <summary>
    /// Adds <paramref name="amount"/> units of <paramref name="prefabName"/>
    /// with no target slot — merges into existing non-full stacks first
    /// (row-major grid order), then fills empty slots (row-major) with new
    /// stacks, splitting across as many as it takes. The toolbar "Add Item"
    /// button's mechanism: it never targets one specific tile, so there is
    /// nothing to anchor placement to. See <see cref="AddItemsAt"/> for the
    /// anchored variant an empty tile's own "Add item" menu entry uses.
    /// </summary>
    public void AddItems(string prefabName, int amount, bool setCrafter) =>
        AddItemsCore(prefabName, amount, setCrafter, anchor: null);

    /// <summary>
    /// Adds <paramref name="amount"/> units of <paramref name="prefabName"/>,
    /// guaranteeing a new stack lands at <paramref name="x"/>/<paramref
    /// name="y"/> first (that tile must already be empty — this is what an
    /// empty tile's own "Add item" menu entry targets, and the guarantee is
    /// the point: the user picked that exact tile). Only the overflow beyond
    /// one stack there — a requested amount bigger than <c>MaxStack</c> —
    /// spills into merging with other non-full stacks and then other empty
    /// slots, same as the anchor-less <see cref="AddItems"/>.
    /// </summary>
    public void AddItemsAt(int x, int y, string prefabName, int amount, bool setCrafter) =>
        AddItemsCore(prefabName, amount, setCrafter, anchor: (x, y));

    // game-derived: Inventory.AddItem's own real merge-by-grid-position
    // behavior (see AddItemAt's provenance note above) — this is that
    // omission, now implemented, plus the split-across-slots extension
    // AddItemAt never needed as a single-unit-only mechanism. Shares the
    // same field-setting rules (fixed quality 1, no variant, durability only
    // when the resolved item uses it) as AddItemAt via the local CreateStack
    // below, and the same "did it actually change" contract as FillStack.
    private void AddItemsCore(string prefabName, int amount, bool setCrafter, (int X, int Y)? anchor) => MutateInnerBlob(
        p =>
        {
            if (amount <= 0)
            {
                return false;
            }

            var shared = SharedItemDataCatalog.TryFind(prefabName);
            if (shared is null)
            {
                return false;
            }

            var prefabHash = StringExtensionMethods.GetStableHashCode(prefabName);
            var remaining = amount;

            Inventory.ItemData CreateStack(int x, int y, int stack)
            {
                var item = new Inventory.ItemData
                {
                    PrefabName = prefabName,
                    PrefabHash = prefabHash,
                    m_stack = stack,
                    m_quality = 1,
                    m_gridPos = new Vector2i(x, y),
                    m_pickedUp = true,
                };

                if (shared.UsesDurability)
                {
                    item.m_durability = (float)shared.MaxDurabilityFor(1);
                }

                if (setCrafter && shared.CanHaveCrafterTag)
                {
                    item.m_crafterID = _profile.m_playerID;
                    item.m_crafterName = _profile.m_playerName;
                }

                return item;
            }

            if (anchor is { X: var anchorX, Y: var anchorY })
            {
                if (anchorX < 0 || anchorX >= InventoryLayout.Width || anchorY < 0 || anchorY >= InventoryLayout.Height)
                {
                    return false;
                }

                if (FindItem(p, anchorX, anchorY) is not null)
                {
                    return false;
                }

                var placed = Math.Min(remaining, shared.MaxStack);
                p.m_inventory.m_inventory.Add(CreateStack(anchorX, anchorY, placed));
                remaining -= placed;
            }

            if (remaining > 0)
            {
                // The anchor stack just created above (if any) can never
                // appear here: CreateStack always consumes min(remaining,
                // MaxStack), so it's either already full (fails the
                // m_stack < MaxStack check) or remaining hit 0 and this
                // block doesn't run at all.
                var partials = p.m_inventory.m_inventory
                    .Where(i => i.PrefabHash == prefabHash && i.m_quality == 1 && i.m_variant == 0
                        && i.m_stack < shared.MaxStack)
                    .OrderBy(i => i.m_gridPos.y).ThenBy(i => i.m_gridPos.x);

                foreach (var item in partials)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    var topUp = Math.Min(remaining, shared.MaxStack - item.m_stack);
                    item.m_stack += topUp;
                    remaining -= topUp;
                }
            }

            if (remaining > 0)
            {
                var occupied = p.m_inventory.m_inventory.Select(i => (i.m_gridPos.x, i.m_gridPos.y)).ToHashSet();

                for (var y = 0; y < InventoryLayout.Height && remaining > 0; y++)
                {
                    for (var x = 0; x < InventoryLayout.Width && remaining > 0; x++)
                    {
                        if (occupied.Contains((x, y)))
                        {
                            continue;
                        }

                        var placed = Math.Min(remaining, shared.MaxStack);
                        p.m_inventory.m_inventory.Add(CreateStack(x, y, placed));
                        remaining -= placed;
                    }
                }
            }

            return remaining != amount;
        },
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    /// <summary>Removes one item outright, addressed by grid position. Needs
    /// no catalog data at all — matches Loki's own delete, which works even
    /// for an unresolved item. No-op when the slot was already empty.</summary>
    public void RemoveItemAt(int x, int y) => MutateInnerBlob(
        p => p.m_inventory.m_inventory.RemoveAll(i => i.m_gridPos.x == x && i.m_gridPos.y == y) > 0,
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    /// <summary>Repairs every item that resolves and uses durability
    /// — shares <see cref="Repair"/> with <see cref="RepairItemAt"/>
    /// rather than duplicating the logic. No-op when every such item was
    /// already at full durability.</summary>
    public void RepairAllItems() => MutateInnerBlob(
        p =>
        {
            var changed = false;
            foreach (var item in p.m_inventory.m_inventory)
            {
                // Not `changed || Repair(item)` — that would short-circuit
                // and stop repairing after the first success.
                changed |= Repair(item);
            }

            return changed;
        },
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    /// <summary>Fills every item's stack that resolves and is stackable —
    /// shares <see cref="FillStack"/> with <see cref="FillItemStack"/>.
    /// No-op when every such stack was already full.</summary>
    public void FillAllStacks() => MutateInnerBlob(
        p =>
        {
            var changed = false;
            foreach (var item in p.m_inventory.m_inventory)
            {
                changed |= FillStack(item);
            }

            return changed;
        },
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    /// <summary>Fills every item's stack whose resolved <see cref="ItemType"/>
    /// is in <paramref name="types"/> — the "Rearm"/"Restock" quick-fill
    /// buttons' backing mutator, a category-filtered <see cref="FillAllStacks"/>
    /// sharing the same <see cref="FillStack"/> helper. No-op when nothing in
    /// those categories was less than full.</summary>
    public void FillStacksOfType(IReadOnlySet<ItemType> types) => MutateInnerBlob(
        p =>
        {
            var changed = false;
            foreach (var item in p.m_inventory.m_inventory)
            {
                var shared = ItemPrefabHashes.TryFindShared(item);
                if (shared is null || !types.Contains(shared.ItemType))
                {
                    continue;
                }

                changed |= FillStack(item);
            }

            return changed;
        },
        p => View = View with { Inventory = InventoryMapper.Map(p) });

    private static Inventory.ItemData? FindItem(Player player, int x, int y)
        => player.m_inventory.m_inventory.FirstOrDefault(i => i.m_gridPos.x == x && i.m_gridPos.y == y);

    /// <summary>Returns whether it actually changed the item — an
    /// unresolved item, one that doesn't use durability, and one already at
    /// full durability are all no-ops, and <see cref="MutateInnerBlob"/>
    /// needs to know that so it doesn't re-encode and dirty the session for
    /// nothing.</summary>
    private static bool Repair(Inventory.ItemData item)
    {
        var shared = ItemPrefabHashes.TryFindShared(item);
        if (shared is null || !shared.UsesDurability)
        {
            return false;
        }

        var full = (float)shared.MaxDurabilityFor(item.m_quality);
        if (item.m_durability == full)
        {
            return false;
        }

        item.m_durability = full;
        return true;
    }

    /// <summary>Same "did it actually change anything" contract as
    /// <see cref="Repair"/>.</summary>
    private static bool FillStack(Inventory.ItemData item)
    {
        var shared = ItemPrefabHashes.TryFindShared(item);
        if (shared is null || shared.MaxStack <= 1 || item.m_stack == shared.MaxStack)
        {
            return false;
        }

        item.m_stack = shared.MaxStack;
        return true;
    }

    /// <summary>
    /// Writes pending edits back to <see cref="Path"/>, reproducing both
    /// halves of source's <c>FileHelpers.ReplaceOldFile</c> — not just the
    /// *rotation* half (previous file survives as <c>Path + ".old"</c>), but
    /// also the *staging* half: <see cref="PlayerProfile.SavePlayerToDisk()"/>
    /// is pointed at a <c>Path + ".tmp"</c> file via <c>_profile.m_filename</c>
    /// (the same field <see cref="RenameFile"/> already repoints), so a
    /// failed/interrupted write never touches the live file at all — only a
    /// fully-written temp file gets backed-up-and-swapped into place. The
    /// live file and its backup are therefore only ever touched after a
    /// write has already fully succeeded on disk, so neither a crash
    /// mid-write nor a retry after one can leave the live file truncated or
    /// overwrite the one intact backup with corrupt data. See the note on
    /// <c>PlayerProfile.SavePlayerToDisk(SaveFileEnvelope)</c> for why this
    /// staging lives here rather than in GameCore (I/O safety policy, not
    /// save format). This is currently GameCore's only reachable write call
    /// site from Adapter; if a second one is ever added, it needs this same
    /// staging — <c>CharacterEditorTests</c> asserts there's exactly one call
    /// site precisely so that isn't silently missed.
    /// <para>
    /// Also resyncs the whole session from the freshly-written file afterward
    /// (same reload <see cref="Revert"/> does), rather than only patching
    /// <see cref="IsDirty"/>/the write-time baseline — <c>SavePlayerToDisk</c>
    /// unconditionally re-stamps on-disk version fields to the current
    /// format version, which this session's already-in-memory
    /// <see cref="_profile"/> has no way to reflect on its own
    /// (<c>ProfileVersion</c> is set only by <c>Load</c>). Without this, a
    /// save of a file whose on-disk version differs from the format this
    /// build writes would leave <see cref="View"/> — and anything in
    /// Norn.UI bound to it — showing the pre-save version until a manual
    /// <see cref="Revert"/> or a fresh <see cref="Open(string)"/>.
    /// </para>
    /// </summary>
    public void Save()
    {
        var tempPath = Path + ".tmp";
        var originalFilename = _profile.m_filename;
        _profile.m_filename = tempPath;
        try
        {
            _profile.SavePlayerToDisk();
        }
        finally
        {
            _profile.m_filename = originalFilename;
        }

        if (File.Exists(Path))
        {
            File.Copy(Path, Path + ".old", overwrite: true);
        }

        File.Move(tempPath, Path, overwrite: true);

        var reloaded = new PlayerProfile(Path);
        reloaded.Load();
        _profile = reloaded;
        _player = PlayerLoader.Load(reloaded);
        View = CharacterLoader.Map(reloaded);
        IsDirty = false;
        _playerNameBaseline = View.Meta.PlayerName;
        _lastKnownWriteTimeUtc = File.GetLastWriteTimeUtc(Path);
    }

    /// <summary>
    /// Moves the on-disk file to <paramref name="newPath"/> and repoints
    /// both <see cref="Path"/> and <see cref="PlayerProfile.m_filename"/>
    /// (the field the mirrored write path actually opens) at the new
    /// location — so a subsequent <see cref="Save"/> reads/writes the moved
    /// file, not a since-vanished old one. Raw primitive only: validating
    /// the candidate name (illegal characters, an existing collision) is the
    /// caller's job (<c>Norn.UI.SaveFileRenaming</c>) — this trusts the
    /// caller the same way <see cref="SetSkillLevel"/> trusts its caller to
    /// only name skills that exist. Called immediately before <see cref="Save"/>
    /// (never after) so the file's pre-edit bytes are what gets moved, and
    /// <see cref="Save"/>'s own backup-rotation copy lands under the new name
    /// too, rather than orphaned under the old one.
    /// </summary>
    public void RenameFile(string newPath)
    {
        File.Move(Path, newPath);
        _profile.m_filename = newPath;
        Path = newPath;
    }

    /// <summary>
    /// Decodes one world's map-data blob for display, on demand — not
    /// retained, not part of <see cref="View"/>, and not a mutator (doesn't
    /// touch <see cref="IsDirty"/>): viewing a map isn't an edit, and eagerly
    /// decoding every world's ~8.4MB blob on every <see cref="Open"/> would
    /// cost real time/memory for something most sessions never look at
    /// Returns <c>null</c> when the world isn't
    /// known or has no map data recorded — the UI is expected to disable its
    /// button in that case (<see cref="WorldDto.HasMapData"/>), but this is
    /// safe to call regardless.
    /// </summary>
    public WorldMapDto? DecodeWorldMap(long worldId)
    {
        var worldData = _profile.m_worldData
            .Where(pair => pair.Key == worldId)
            .Select(pair => pair.Value)
            .FirstOrDefault();

        if (worldData?.m_mapData is null)
        {
            return null;
        }

        return WorldMapMapper.Map(Minimap.Decode(worldData.m_mapData));
    }

    /// <summary>
    /// Nulls one world's map-data blob — resets it to the exact state a
    /// world has before its map is ever flushed to the profile at all
    /// (confirmed directly in <c>PlayerProfile.LoadPlayerFromDisk</c>/
    /// <c>Save</c>'s own presence flag), not
    /// a special-cased "empty" blob. Takes the exploration bitmaps and every
    /// placed pin with it — both live inside the same opaque <c>byte[]</c>
    /// and Norn has no <c>Encode</c> counterpart to <see cref="Minimap.Decode"/>
    /// to re-emit one without the other — this is the only map-data write
    /// operation available, not
    /// a Norn design choice to couple the two. No-op if the world isn't
    /// known or already has no map data.
    /// </summary>
    public void ClearWorldMapData(long worldId)
    {
        var worldData = _profile.m_worldData
            .Where(pair => pair.Key == worldId)
            .Select(pair => pair.Value)
            .FirstOrDefault();

        if (worldData?.m_mapData is null)
        {
            return;
        }

        worldData.m_mapData = null;
        View = View with { Worlds = WorldsMapper.Map(_profile) };
        IsDirty = true;
    }

    // game-derived: Minimap.ExploreAll (Valheim 0.221.10/0.221.4, confirmed
    // in both trees) — the console command `exploremap`'s entire
    // implementation is a per-pixel loop over the fog texture setting
    // m_explored, never touching m_exploredOthers. This mirrors exactly that
    // scope: only Explored is filled; ExploredOthers (shared/cartography-table
    // exploration) is left as decoded.
    /// <summary>
    /// Decodes one world's map blob, sets every byte of <see cref="WorldMapDto.Explored"/>
    /// non-zero, and re-encodes it back into <c>worldData.m_mapData</c> — the
    /// one caller of <see cref="Minimap.Encode"/>.
    /// No-op if the world isn't known or has no map data (nothing to decode).
    /// Unlike every other mutator here, this replaces a compressed opaque
    /// blob wholesale rather than patching a decoded in-memory model, so
    /// there is no shared <see cref="MutateInnerBlob"/>-style helper to reuse —
    /// the shape is closer to <see cref="ClearWorldMapData"/>'s direct
    /// <c>worldData.m_mapData</c> assignment than to the inner-player-blob
    /// mutators above.
    /// </summary>
    public void ExploreAllMap(long worldId)
    {
        var worldData = _profile.m_worldData
            .Where(pair => pair.Key == worldId)
            .Select(pair => pair.Value)
            .FirstOrDefault();

        if (worldData?.m_mapData is null)
        {
            return;
        }

        var mapData = Minimap.Decode(worldData.m_mapData);
        Array.Fill(mapData.Explored, (byte)1);
        worldData.m_mapData = Minimap.Encode(mapData);
        IsDirty = true;
    }

    /// <summary>
    /// Decodes one world's map blob, clears every byte of <see cref="WorldMapDto.ExploredOthers"/>
    /// (fog revealed via a shared source — a cartography table — as opposed
    /// to this character's own exploration, which is untouched) and drops
    /// every pin whose <c>m_ownerID</c> is non-zero (received via a
    /// cartography table and not yet adopted — the same partition
    /// <see cref="WorldMapDto.ReceivedPinCount"/> counts), then re-encodes —
    /// the second caller of <see cref="Minimap.Encode"/>, same shape as
    /// <see cref="ExploreAllMap"/>. No-op if the world isn't known or has no
    /// map data.
    /// <para>
    /// Unlike <see cref="ExploreAllMap"/> (which mirrors the in-game
    /// <c>exploremap</c> console command), nothing in the game ever lets a
    /// player selectively discard only received map data while keeping
    /// their own — same editor-exclusive-capability category as
    /// <see cref="ClearWorldMapData"/>/<see cref="ClearUsedCheats"/>, not a
    /// mirror of anything.
    /// </para>
    /// </summary>
    public void ClearReceivedMapData(long worldId)
    {
        var worldData = _profile.m_worldData
            .Where(pair => pair.Key == worldId)
            .Select(pair => pair.Value)
            .FirstOrDefault();

        if (worldData?.m_mapData is null)
        {
            return;
        }

        var mapData = Minimap.Decode(worldData.m_mapData);
        Array.Clear(mapData.ExploredOthers, 0, mapData.ExploredOthers.Length);
        mapData.Pins.RemoveAll(pin => pin.m_ownerID != 0);
        worldData.m_mapData = Minimap.Encode(mapData);
        IsDirty = true;
    }

    /// <summary>
    /// Clears one world's claimed-bed spawn flag only — mirrors the game's
    /// own <c>PlayerProfile.ClearCustomSpawnPoint</c> exactly: the bool goes
    /// false, the vector is left as-is, so
    /// the result stays indistinguishable from ordinary game output rather
    /// than a save shape the game itself never produces. No-op if the world
    /// isn't known or the flag is already false.
    /// </summary>
    public void ClearWorldSpawnPoint(long worldId)
    {
        var worldData = _profile.m_worldData
            .Where(pair => pair.Key == worldId)
            .Select(pair => pair.Value)
            .FirstOrDefault();

        if (worldData is null || !worldData.m_haveCustomSpawnPoint)
        {
            return;
        }

        worldData.m_haveCustomSpawnPoint = false;
        View = View with { Worlds = WorldsMapper.Map(_profile) };
        IsDirty = true;
    }

    /// <summary>
    /// Same shape as <see cref="ClearWorldSpawnPoint"/>, mirroring the
    /// game's own <c>PlayerProfile.ClearLoguoutPoint</c> (sic) — bool false,
    /// vector left as-is. Only ever affects the world's *next* login either
    /// way, since the game itself consumes and clears this flag the first
    /// time it's read.
    /// </summary>
    public void ClearWorldLogoutPoint(long worldId)
    {
        var worldData = _profile.m_worldData
            .Where(pair => pair.Key == worldId)
            .Select(pair => pair.Value)
            .FirstOrDefault();

        if (worldData is null || !worldData.m_haveLogoutPoint)
        {
            return;
        }

        worldData.m_haveLogoutPoint = false;
        View = View with { Worlds = WorldsMapper.Map(_profile) };
        IsDirty = true;
    }

    /// <summary>
    /// Zeros one world's home point — the only available shape, since this
    /// field (unlike spawn/logout/death) has no paired have-flag to clear
    /// instead. Inherently non-durable:
    /// the game overwrites it again on the next respawn that doesn't come
    /// from a consumed logout point, regardless of this edit. No-op if the
    /// world isn't known or the point is already zero.
    /// </summary>
    public void ClearWorldHomePoint(long worldId)
    {
        var worldData = _profile.m_worldData
            .Where(pair => pair.Key == worldId)
            .Select(pair => pair.Value)
            .FirstOrDefault();

        if (worldData is null || worldData.m_homePoint == Vector3.zero)
        {
            return;
        }

        worldData.m_homePoint = Vector3.zero;
        View = View with { Worlds = WorldsMapper.Map(_profile) };
        IsDirty = true;
    }

    /// <summary>
    /// Removes one world's entire per-character entry — spawn/logout/death/
    /// home points and map data (pins included) all go together. Keyed by
    /// world UID, never list position: <c>m_worldData</c> is a
    /// <c>List&lt;KeyValuePair&lt;long, WorldPlayerData&gt;&gt;</c>, and
    /// removal-by-value rather than by index is the same precedent
    /// already established for Unlockables. A full <see cref="WorldsMapper.Map"/>
    /// remap, not a targeted <c>with</c> — removing a list entry is
    /// structural, same reasoning <see cref="ClearUsedCheats"/> uses for
    /// <c>Statistics</c>. No-op if the world isn't known.
    /// </summary>
    public void RemoveWorld(long worldId)
    {
        if (_profile.m_worldData.RemoveAll(pair => pair.Key == worldId) == 0)
        {
            return;
        }

        View = View with { Worlds = WorldsMapper.Map(_profile) };
        IsDirty = true;
    }

    /// <summary>
    /// Re-maps the <see cref="CharacterView.Worlds"/> slice against whatever
    /// <see cref="WorldIdentityCatalog"/> currently knows, without touching
    /// the profile or <see cref="IsDirty"/> — a display refresh, not an edit.
    /// <para>
    /// Exists because the world-identity scan runs on a background thread at
    /// startup and can finish <i>after</i> a character is already open
    /// (always the case in CLI-open mode) — the already-mapped
    /// <see cref="View"/> would otherwise keep showing worlds unnamed even
    /// once the scan resolves them, since nothing else revisits it.
    /// Re-mapping from the retained profile preserves any pending world
    /// edits, since those live in the profile, not in the DTO.
    /// </para>
    /// </summary>
    public void RefreshWorldIdentities()
    {
        View = View with { Worlds = WorldsMapper.Map(_profile) };
    }

    /// <summary>
    /// Discards pending edits by reloading from disk. Reload rather than
    /// snapshot-restore, deliberately: simpler, and correct by construction
    /// for files this small. Returns <c>false</c> if the file on disk no
    /// longer loads (e.g. deleted or now out of version range) — the session
    /// is left as it was so the caller can decide what to show. Every
    /// replacement is computed into a local first and only assigned to
    /// <see cref="_profile"/>/<see cref="_player"/>/<see cref="View"/> once
    /// all three are ready, so a throw partway through (e.g.
    /// <see cref="PlayerLoader.Load"/> on a corrupted inner blob) leaves
    /// every field exactly as it was, never a mix of the old session and the
    /// new one.
    /// </summary>
    public bool Revert()
    {
        var profile = new PlayerProfile(Path);
        if (!profile.Load())
        {
            return false;
        }

        var player = PlayerLoader.Load(profile);
        var view = CharacterLoader.Map(profile);

        _profile = profile;
        _player = player;
        View = view;
        IsDirty = false;
        _playerNameBaseline = View.Meta.PlayerName;
        _lastKnownWriteTimeUtc = File.GetLastWriteTimeUtc(Path);
        return true;
    }
}

/// <summary>
/// Why <see cref="CharacterEditor.Open(string, out IncompatibleVersion?)"/>
/// refused a file whose profile version fell outside the range Norn
/// understands. <see cref="SupportedMin"/>/<see cref="SupportedMax"/> are
/// read from <see cref="GameCore.Version"/> at the moment of the call, so a
/// future patch-day bump to the compatible range is reflected automatically
/// — nothing here is a second place that needs updating by hand.
/// </summary>
/// <param name="FoundVersion">The profile version actually read from the file.</param>
/// <param name="SupportedMin">The oldest profile version this build accepts.</param>
/// <param name="SupportedMax">The newest profile version this build accepts.</param>
/// <param name="TooNew">
/// <c>true</c> when <see cref="FoundVersion"/> is newer than anything this
/// build understands (almost always a Valheim update Norn hasn't caught up
/// to yet); <c>false</c> when it's older than the oldest version this build
/// still accepts.
/// </param>
public readonly record struct IncompatibleVersion(int FoundVersion, int SupportedMin, int SupportedMax, bool TooNew);
