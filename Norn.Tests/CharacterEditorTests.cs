using System.Linq;
using Norn.Adapter;
using Norn.GameCore;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="CharacterEditor"/>'s player-name edit, the proof field for
/// the editing seam. Byte-identity for everything
/// <i>not</i> being edited is already the existing round-trip harness's job
/// (ProfileRoundTripTests, PlayerRoundTripTests) — these tests cover only what's
/// new: that a set field is dirty, that Save persists it, and that Revert discards
/// it.
/// </summary>
/// <summary>
/// Untagged (no <see cref="CatalogCollection"/>), unlike the old version of
/// this class — none of the tests remaining here reference any shared
/// catalog static at all (verified by grep, not assumed), so they're free
/// to run in parallel with everything, including
/// <see cref="CharacterEditorCatalogTests"/>, which now holds the 7 tests
/// that do touch <see cref="SharedItemDataCatalog"/> (found in review: this
/// whole 37-method class used to be tagged, serializing even the 30
/// catalog-independent tests against every other catalog-touching test
/// class in the suite for no reason).
/// </summary>
public class CharacterEditorTests
{
    /// <summary>
    /// Shared setup for the theories below that just need an open editor on
    /// a scratch copy of a corpus file — skip if <paramref name="fileName"/>'s
    /// version is out of range, copy to a temp file, open it. ~25 methods
    /// used to retype this identical block (found in review); caller still
    /// owns disposing the returned <see cref="TempFile"/> (a scratch copy
    /// used by <c>Save</c>, so it can't be cleaned up before the test body
    /// runs).
    /// </summary>
    private static (TempFile Scratch, CharacterEditor Editor) OpenScratchCopy(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");

        var scratch = TempFile.Create();

        // Owns the scratch file until it is handed to the caller (found in
        // review): the copy, open and assertion below all happen before the
        // return, so anything throwing there previously left a file nobody
        // was responsible for disposing.
        try
        {
            File.Copy(corpusPath, scratch.Path);

            var editor = CharacterEditor.Open(scratch.Path);
            Assert.NotNull(editor);

            return (scratch, editor!);
        }
        catch
        {
            scratch.Dispose();
            throw;
        }
    }

    public static IEnumerable<object[]> Fixtures() =>
        TestPaths.FixtureFiles().Select(path => new object[] { Path.GetFileName(path) });

    /// <summary>
    /// Inner-blob write coverage that runs on a fresh clone with no corpus
    /// (found in review). Every other test that exercises
    /// <c>Player.Save</c> — this class's own mutator round-trips, and
    /// <c>PlayerRoundTripTests</c> — is corpus-gated and skips entirely
    /// there, while <c>FixtureRoundTripTests</c> only round-trips the outer
    /// envelope and keeps the inner blob opaque. The gap that left: a
    /// completely broken <c>Player.Save</c> had nothing unconditional to
    /// catch it.
    /// <para>
    /// Asserts graph stability rather than byte identity, for the reason
    /// <c>ProfileRoundTripTests</c> already documents at the profile level:
    /// the writer always emits its own current version literal, so bytes
    /// only match for a blob already at that version.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Inner_player_blob_survives_a_decode_encode_decode_cycle(string fileName)
    {
        var profile = new PlayerProfile(Path.Combine(TestPaths.FixtureDirectory!, fileName));
        Assert.True(profile.Load(), $"{fileName} failed to load.");

        var first = PlayerLoader.Load(profile);
        Assert.SkipWhen(first is null, $"{fileName} has no inner player-data blob.");

        profile.m_playerData = PlayerLoader.Save(first!);
        var second = PlayerLoader.Load(profile);

        Assert.NotNull(second);
        Assert.Equal(first!.m_maxHealth, second!.m_maxHealth);
        Assert.Equal(first.m_health, second.m_health);
        Assert.Equal(first.m_maxStamina, second.m_maxStamina);
        Assert.Equal(first.m_timeSinceDeath, second.m_timeSinceDeath);
        Assert.Equal(first.m_modelIndex, second.m_modelIndex);
        Assert.Equal(first.m_beardItem, second.m_beardItem);
        Assert.Equal(first.m_hairItem, second.m_hairItem);
        Assert.Equal(first.m_skills.m_skillData.Count, second.m_skills.m_skillData.Count);
        Assert.Equal(first.m_inventory.m_inventory.Count, second.m_inventory.m_inventory.Count);
        Assert.Equal(first.m_foods.Count, second.m_foods.Count);
        Assert.Equal(first.m_knownRecipes.Count, second.m_knownRecipes.Count);
    }

    /// <summary>
    /// The editor's write path end to end on a fresh clone: edit an
    /// inner-blob field, save, reopen, confirm it persisted. Companion to
    /// <see cref="Inner_player_blob_survives_a_decode_encode_decode_cycle"/>
    /// — that one covers the encode, this one covers it reaching disk
    /// through <c>CharacterEditor.Save</c> (found in review).
    /// </summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Editing_an_inner_blob_field_persists_across_save_and_reopen(string fileName)
    {
        using var scratch = TempFile.Create();
        File.Copy(Path.Combine(TestPaths.FixtureDirectory!, fileName), scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var skill = editor!.View.Skills.Skills.FirstOrDefault();
        Assert.SkipWhen(skill is null, $"{fileName} has no skills recorded.");

        var newLevel = skill!.Level >= 50f ? 10f : 60f;
        editor.SetSkillLevel(skill.Type, newLevel);
        Assert.True(editor.IsDirty);
        editor.Save();

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.Equal(newLevel, reopened!.View.Skills.Skills.Single(s => s.Type == skill.Type).Level);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Player_name_round_trips_through_save_and_reload(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;

        const string newName = "Norn-Edited-Name";
        editor!.SetPlayerName(newName);
        Assert.True(editor.IsDirty);
        Assert.Equal(newName, editor.View.Meta.PlayerName);

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.Equal(newName, reopened!.View.Meta.PlayerName);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Save_backs_up_the_previous_contents_to_dot_old(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");

        using var scratchDir = TempDirectory.Create();
        var scratchPath = Path.Combine(scratchDir.Path, "character.fch");
        File.Copy(corpusPath, scratchPath);
        var originalBytes = File.ReadAllBytes(scratchPath);

        var editor = CharacterEditor.Open(scratchPath);
        Assert.NotNull(editor);
        editor!.SetPlayerName("Norn-Edited-Name");

        editor.Save();

        var backupPath = scratchPath + ".old";
        Assert.True(File.Exists(backupPath), "Save() should back up the pre-write contents to <path>.old.");
        Assert.Equal(originalBytes, File.ReadAllBytes(backupPath));
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Revert_discards_a_pending_edit(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;
        var originalName = editor!.View.Meta.PlayerName;

        editor.SetPlayerName("Discarded");
        Assert.True(editor.IsDirty);

        Assert.True(editor.Revert());
        Assert.False(editor.IsDirty);
        Assert.Equal(originalName, editor.View.Meta.PlayerName);
    }

    /// <summary>
    /// The data-safety mechanism behind the read-only-mode design (not a
    /// running-process check): a session that hasn't touched
    /// disk since it was opened has nothing to conflict with.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void HasChangedOnDisk_is_false_immediately_after_open(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;

        Assert.False(editor!.HasChangedOnDisk());
    }

    /// <summary>
    /// The scenario item 12 actually exists for: something (most plausibly
    /// Valheim, mid-session) rewrites this exact file after Norn already
    /// loaded it. The write time is set directly rather than re-copying the
    /// file and hoping the OS clock ticks forward enough to differ — this
    /// shouldn't be a test that can flake on a fast machine.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void HasChangedOnDisk_is_true_after_an_external_write_touches_the_file(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;

        File.SetLastWriteTimeUtc(scratch.Path, DateTime.UtcNow.AddMinutes(5));

        Assert.True(editor!.HasChangedOnDisk());
    }

    /// <summary>
    /// <see cref="CharacterEditor.Save"/> stays the raw, trusting primitive
    /// it already was (the conflict prompt this baseline drives is
    /// <c>MainWindow</c>'s job, not this class's) — but it must still
    /// resync the baseline to what it just wrote, or the very next check
    /// would report a conflict against Norn's own save.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void HasChangedOnDisk_baseline_is_refreshed_by_save(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;

        File.SetLastWriteTimeUtc(scratch.Path, DateTime.UtcNow.AddMinutes(5));
        Assert.True(editor!.HasChangedOnDisk());

        editor.SetPlayerName("Norn-Edited-Name");
        editor.Save();

        Assert.False(editor.HasChangedOnDisk());
    }

    /// <summary>Same reasoning as the Save case above, for the "Reload from
    /// disk" choice a detected conflict offers — <see cref="CharacterEditor.Revert"/>
    /// re-syncs to whatever's on disk now, so the conflict it just resolved
    /// shouldn't still be reported afterward.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void HasChangedOnDisk_baseline_is_refreshed_by_revert(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;

        File.SetLastWriteTimeUtc(scratch.Path, DateTime.UtcNow.AddMinutes(5));
        Assert.True(editor!.HasChangedOnDisk());

        Assert.True(editor.Revert());
        Assert.False(editor.HasChangedOnDisk());
    }

    /// <summary>
    /// <see cref="CharacterEditor.SetPlayerName"/> updates only the
    /// <c>Meta</c> slice of <see cref="CharacterView"/> via a record
    /// <c>with</c> expression rather than a full remap — this
    /// defends that specifically. A regression back to a full
    /// <see cref="CharacterLoader.Map"/> call would produce value-equal but
    /// reference-distinct DTOs, which <c>Assert.Equal</c> would not catch;
    /// <c>Assert.Same</c> is the assertion that actually exercises the fix.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Player_name_edit_does_not_remap_the_other_DTOs(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;
        var before = editor!.View;

        editor.SetPlayerName("Norn-Edited-Name");

        Assert.Same(before.Skills, editor.View.Skills);
        Assert.Same(before.Vitals, editor.View.Vitals);
        Assert.Same(before.Inventory, editor.View.Inventory);
        Assert.Same(before.Statistics, editor.View.Statistics);
        Assert.Same(before.Worlds, editor.View.Worlds);
        Assert.NotSame(before.Meta, editor.View.Meta);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Clear_used_cheats_round_trips_through_save_and_reload(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;

        editor!.ClearUsedCheats();
        Assert.True(editor.IsDirty);
        Assert.False(editor.View.Meta.UsedCheats);
        Assert.All(editor.View.Statistics.Slots, slot =>
            Assert.Equal(0f, slot.PlayerStats.Single(s => s.Name == nameof(PlayerStatType.Cheats)).Value));

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.False(reopened!.View.Meta.UsedCheats);
        Assert.All(reopened.View.Statistics.Slots, slot =>
            Assert.Equal(0f, slot.PlayerStats.Single(s => s.Name == nameof(PlayerStatType.Cheats)).Value));
    }

    /// <summary>
    /// Same reasoning as <see cref="Player_name_edit_does_not_remap_the_other_DTOs"/>,
    /// except <c>Statistics</c> is expected to change too — <see cref="CharacterEditor.ClearUsedCheats"/>
    /// deliberately remaps that slice (see its own doc comment) rather than
    /// patching one entry out of its list by hand.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Clear_used_cheats_does_not_remap_the_unrelated_DTOs(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;
        var before = editor!.View;

        editor.ClearUsedCheats();

        Assert.Same(before.Skills, editor.View.Skills);
        Assert.Same(before.Vitals, editor.View.Vitals);
        Assert.Same(before.Inventory, editor.View.Inventory);
        Assert.Same(before.Worlds, editor.View.Worlds);
        Assert.NotSame(before.Meta, editor.View.Meta);
        Assert.NotSame(before.Statistics, editor.View.Statistics);
    }

    /// <summary>
    /// The first field edit reaching into the inner <c>m_playerData</c>
    /// blob — this is the real test of the decode-mutate-reencode
    /// round trip <see cref="CharacterEditor.SetSkinColor"/>/<see cref="CharacterEditor.SetHairColor"/>
    /// depend on, not just the DTO update <see cref="Clear_used_cheats_round_trips_through_save_and_reload"/>
    /// already covers for profile-level fields.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Skin_and_hair_color_round_trip_through_save_and_reload(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var skin = new ColorDto(0.5f, 0.4f, 0.3f);
        var hair = new ColorDto(0.2f, 0.1f, 0.05f);
        editor!.SetSkinColor(skin);
        editor.SetHairColor(hair);
        Assert.True(editor.IsDirty);
        Assert.Equal(skin, editor.View.Meta.SkinColor);
        Assert.Equal(hair, editor.View.Meta.HairColor);

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.Equal(skin, reopened!.View.Meta.SkinColor);
        Assert.Equal(hair, reopened.View.Meta.HairColor);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Skin_color_edit_does_not_remap_the_unrelated_DTOs(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);
        var before = editor!.View;

        editor.SetSkinColor(new ColorDto(0.5f, 0.4f, 0.3f));

        Assert.Same(before.Skills, editor.View.Skills);
        Assert.Same(before.Vitals, editor.View.Vitals);
        Assert.Same(before.Inventory, editor.View.Inventory);
        Assert.Same(before.Worlds, editor.View.Worlds);
        Assert.Same(before.Statistics, editor.View.Statistics);
        Assert.NotSame(before.Meta, editor.View.Meta);
    }

    /// <summary>
    /// Same shape as <see cref="Skin_and_hair_color_round_trip_through_save_and_reload"/>
    /// — the beard/hair-item mutators reach the same inner <c>m_playerData</c>
    /// blob. Uses two plain catalog-style names, not real
    /// <see cref="SharedItemDataCatalog"/> entries — <c>CharacterEditor</c>
    /// itself has no opinion on what a "valid" name is (see
    /// <see cref="CharacterEditor.SetBeardItem"/>'s doc comment); that
    /// constraint lives entirely in Norn.UI's picker, not here.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Beard_and_hair_item_round_trip_through_save_and_reload(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        editor!.SetBeardItem("Beard9");
        editor.SetHairItem("Hair14");
        Assert.True(editor.IsDirty);
        Assert.Equal("Beard9", editor.View.Meta.BeardItem);
        Assert.Equal("Hair14", editor.View.Meta.HairItem);

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.Equal("Beard9", reopened!.View.Meta.BeardItem);
        Assert.Equal("Hair14", reopened.View.Meta.HairItem);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Beard_item_edit_does_not_remap_the_unrelated_DTOs(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);
        var before = editor!.View;

        editor.SetBeardItem("Beard9");

        Assert.Same(before.Skills, editor.View.Skills);
        Assert.Same(before.Vitals, editor.View.Vitals);
        Assert.Same(before.Inventory, editor.View.Inventory);
        Assert.Same(before.Worlds, editor.View.Worlds);
        Assert.Same(before.Statistics, editor.View.Statistics);
        Assert.NotSame(before.Meta, editor.View.Meta);
    }

    /// <summary>
    /// Same shape as <see cref="Beard_and_hair_item_round_trip_through_save_and_reload"/>,
    /// for <see cref="CharacterEditor.SetModelIndex"/>. Also defends the "no
    /// coupling" decision: setting the model
    /// index must leave whatever beard/hair the corpus file already has
    /// untouched, not reset it the way the game's own creation UI would.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Model_index_round_trips_and_does_not_touch_beard_or_hair(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);
        var originalBeard = editor!.View.Meta.BeardItem;
        var originalHair = editor.View.Meta.HairItem;

        editor.SetModelIndex(1);
        Assert.True(editor.IsDirty);
        Assert.Equal(1, editor.View.Meta.ModelIndex);
        Assert.Equal(originalBeard, editor.View.Meta.BeardItem);
        Assert.Equal(originalHair, editor.View.Meta.HairItem);

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.Equal(1, reopened!.View.Meta.ModelIndex);
        Assert.Equal(originalBeard, reopened.View.Meta.BeardItem);
        Assert.Equal(originalHair, reopened.View.Meta.HairItem);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Model_index_edit_does_not_remap_the_unrelated_DTOs(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);
        var before = editor!.View;

        editor.SetModelIndex(1);

        Assert.Same(before.Skills, editor.View.Skills);
        Assert.Same(before.Vitals, editor.View.Vitals);
        Assert.Same(before.Inventory, editor.View.Inventory);
        Assert.Same(before.Worlds, editor.View.Worlds);
        Assert.Same(before.Statistics, editor.View.Statistics);
        Assert.NotSame(before.Meta, editor.View.Meta);
    }

    /// <summary>
    /// <see cref="CharacterEditor.RestoreHealth"/>/<see cref="CharacterEditor.RestoreStamina"/>/
    /// <see cref="CharacterEditor.RestoreEitr"/> all set current := max —
    /// asserted as an invariant that holds regardless of the corpus file's
    /// starting values (some may already be at max), not by forcing a
    /// below-max starting state first, since <c>CharacterEditor</c>
    /// deliberately exposes no direct "set current health" setter to do
    /// that with.
    /// <para>
    /// Dirtiness is therefore asserted <i>conditionally</i>, against whether
    /// anything was actually below max to begin with. This used to be an
    /// unconditional <c>Assert.True(editor.IsDirty)</c>, which passed on a
    /// file already at full health only because the mutators marked the
    /// session dirty even when they changed nothing — the exact defect since
    /// fixed in <c>MutateInnerBlob</c> (found in review). Asserting the
    /// conditional now covers both halves of that contract.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Restore_vitals_round_trips_through_save_and_reload(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var vitals = editor!.View.Vitals;
        var anythingToRestore = vitals.Health < vitals.MaxHealth
            || vitals.Stamina < vitals.MaxStamina
            || vitals.Eitr < vitals.MaxEitr;

        editor.RestoreHealth();
        editor.RestoreStamina();
        editor.RestoreEitr();
        Assert.Equal(anythingToRestore, editor.IsDirty);
        Assert.Equal(editor.View.Vitals.MaxHealth, editor.View.Vitals.Health);
        Assert.Equal(editor.View.Vitals.MaxStamina, editor.View.Vitals.Stamina);
        Assert.Equal(editor.View.Vitals.MaxEitr, editor.View.Vitals.Eitr);

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.Equal(reopened!.View.Vitals.MaxHealth, reopened.View.Vitals.Health);
        Assert.Equal(reopened.View.Vitals.MaxStamina, reopened.View.Vitals.Stamina);
        Assert.Equal(reopened.View.Vitals.MaxEitr, reopened.View.Vitals.Eitr);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Reset_time_since_death_round_trips_through_save_and_reload(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        // Conditional for the same reason as Restore_vitals above: a file
        // already at 0 makes this a genuine no-op, which must not dirty the
        // session (found in review).
        var wasNonZero = editor!.View.Vitals.TimeSinceDeath != 0f;

        editor.ResetTimeSinceDeath();
        Assert.Equal(wasNonZero, editor.IsDirty);
        Assert.Equal(0f, editor.View.Vitals.TimeSinceDeath);

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.Equal(0f, reopened!.View.Vitals.TimeSinceDeath);
    }

    /// <summary>
    /// Needs a corpus file with at least one active-food entry, which not
    /// every save has — skips otherwise rather than asserting on an entry
    /// that doesn't exist.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Remove_food_round_trips_through_save_and_reload(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);
        Assert.SkipWhen(editor!.View.Vitals.ActiveFoods.Count == 0, $"{fileName} has no active food.");

        var originalFoods = editor.View.Vitals.ActiveFoods.ToList();
        var expectedRemaining = originalFoods.Skip(1).ToList();

        editor.RemoveFood(0);
        Assert.True(editor.IsDirty);
        Assert.Equal(expectedRemaining, editor.View.Vitals.ActiveFoods);

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.Equal(expectedRemaining, reopened!.View.Vitals.ActiveFoods);
    }

    /// <summary>
    /// Skill editing had no behavioral test at all before this (found in
    /// review) — only mapping and serialization coverage, which cannot catch
    /// broken clamping, a missed accumulator reset, or an edit that doesn't
    /// persist. That is a concrete gap against this project's own rule that
    /// a field counts as editable only once its Adapter DTO has a tested
    /// inverse mapping.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Set_skill_level_round_trips_through_save_and_reload(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;

        var original = editor.View.Skills.Skills.FirstOrDefault();
        Assert.SkipWhen(original is null, $"{fileName} has no skills recorded.");

        // A level the skill demonstrably isn't already at, so the assertions
        // below can't pass by coincidence.
        var newLevel = original!.Level >= 50f ? 10f : 60f;

        editor.SetSkillLevel(original.Type, newLevel);
        Assert.True(editor.IsDirty);

        var updated = editor.View.Skills.Skills.Single(s => s.Type == original.Type);
        Assert.Equal(newLevel, updated.Level);
        Assert.Equal(0f, updated.Accumulator);

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        var reloaded = reopened!.View.Skills.Skills.Single(s => s.Type == original.Type);
        Assert.Equal(newLevel, reloaded.Level);
        Assert.Equal(0f, reloaded.Accumulator);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Set_skill_level_clamps_to_the_games_own_range(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;

        var original = editor.View.Skills.Skills.FirstOrDefault();
        Assert.SkipWhen(original is null, $"{fileName} has no skills recorded.");

        editor.SetSkillLevel(original!.Type, SkillProgression.MaxLevel + 500f);
        Assert.Equal(SkillProgression.MaxLevel, editor.View.Skills.Skills.Single(s => s.Type == original.Type).Level);

        editor.SetSkillLevel(original.Type, -25f);
        Assert.Equal(0f, editor.View.Skills.Skills.Single(s => s.Type == original.Type).Level);
    }

    /// <summary>An unknown skill name is a documented no-op — and since a
    /// no-op must not dirty the session either, this covers both halves of
    /// that contract.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Set_skill_level_ignores_a_skill_the_character_does_not_have(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;

        var before = editor.View.Skills;

        editor.SetSkillLevel("NotARealSkillName", 42f);

        Assert.False(editor.IsDirty);
        Assert.Same(before, editor.View.Skills);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Compensate_skills_for_death_applies_the_documented_curve_to_every_skill(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;

        var before = editor.View.Skills.Skills.ToList();
        Assert.SkipWhen(before.Count == 0, $"{fileName} has no skills recorded.");
        Assert.SkipWhen(
            before.All(s => s.Level == 0f),
            $"{fileName}'s skills are all at level 0, where compensation is a no-op.");

        editor.CompensateSkillsForDeath(SkillProgression.DefaultCompensatePercent);
        Assert.True(editor.IsDirty);

        foreach (var original in before)
        {
            var expected = SkillProgression.ApplyDeathCompensation(original.Level, SkillProgression.DefaultCompensatePercent);
            var actual = editor.View.Skills.Skills.Single(s => s.Type == original.Type);
            Assert.Equal(expected, actual.Level);
            Assert.Equal(0f, actual.Accumulator);
        }

        editor.Save();
        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        foreach (var original in before)
        {
            var expected = SkillProgression.ApplyDeathCompensation(original.Level, SkillProgression.DefaultCompensatePercent);
            Assert.Equal(expected, reopened!.View.Skills.Skills.Single(s => s.Type == original.Type).Level);
        }
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Restore_health_edit_does_not_remap_the_unrelated_DTOs(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        // Needs a character that isn't already at full health: RestoreHealth
        // is a genuine no-op there and correctly remaps nothing at all, so
        // the NotSame assertion below would have nothing to observe (found
        // in review — this used to "pass" on such files only because every
        // mutator remapped and dirtied unconditionally).
        Assert.SkipWhen(
            editor!.View.Vitals.Health == editor.View.Vitals.MaxHealth,
            $"{fileName} is already at full health; RestoreHealth is a no-op there.");

        var before = editor.View;

        editor.RestoreHealth();

        Assert.Same(before.Meta, editor.View.Meta);
        Assert.Same(before.Skills, editor.View.Skills);
        Assert.Same(before.Inventory, editor.View.Inventory);
        Assert.Same(before.Statistics, editor.View.Statistics);
        Assert.Same(before.Worlds, editor.View.Worlds);
        Assert.NotSame(before.Vitals, editor.View.Vitals);
    }

    /// <summary>Needs a corpus item that already carries a crafter tag —
    /// skips otherwise, same shape as the durability/quality tests above.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Clear_item_crafter_round_trips_through_save_and_reload(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);
        var target = editor!.View.Inventory.Items.FirstOrDefault(i => i.CrafterId != 0);
        Assert.SkipWhen(target is null, $"{fileName} has no crafter-tagged item.");

        editor.ClearItemCrafterAt(target!.GridX, target.GridY);
        Assert.True(editor.IsDirty);

        var updated = editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.Equal(0, updated.CrafterId);
        Assert.Equal("", updated.CrafterName);

        editor.Save();
        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        var reloaded = reopened!.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.Equal(0, reloaded.CrafterId);
        Assert.Equal("", reloaded.CrafterName);
    }

    /// <summary>Needs a corpus item that already carries the cheated flag —
    /// skips otherwise, same shape as the crafter-tag test above.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Clear_item_cheated_round_trips_through_save_and_reload(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);
        var target = editor!.View.Inventory.Items.FirstOrDefault(i => i.Cheated);
        Assert.SkipWhen(target is null, $"{fileName} has no cheated item.");

        editor.ClearItemCheatedAt(target!.GridX, target.GridY);
        Assert.True(editor.IsDirty);

        var updated = editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.False(updated.Cheated);

        editor.Save();
        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        var reloaded = reopened!.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.False(reloaded.Cheated);
    }

    /// <summary>Needs at least one item present — skips otherwise rather
    /// than asserting on a slot that was never occupied.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Remove_item_round_trips_through_save_and_reload(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);
        var target = editor!.View.Inventory.Items.FirstOrDefault();
        Assert.SkipWhen(target is null, $"{fileName} has an empty inventory.");

        var remainingCount = editor.View.Inventory.Items.Count - 1;
        editor.RemoveItemAt(target!.GridX, target.GridY);
        Assert.True(editor.IsDirty);
        Assert.Equal(remainingCount, editor.View.Inventory.Items.Count);
        Assert.DoesNotContain(editor.View.Inventory.Items, i => i.GridX == target.GridX && i.GridY == target.GridY);

        editor.Save();
        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.Equal(remainingCount, reopened!.View.Inventory.Items.Count);
    }

    /// <summary>
    /// <see cref="CharacterEditor.RenameFile"/> is the primitive behind the
    /// save-time file-rename offer (<c>MainWindow.TryOfferFileRename</c>):
    /// moves the on-disk file and repoints both <see cref="CharacterEditor.Path"/>
    /// and the internal <c>PlayerProfile.m_filename</c> the mirrored write
    /// path actually opens, so a <em>subsequent</em> <see cref="CharacterEditor.Save"/>
    /// reads/writes the new location, not the now-vanished old one — the
    /// thing that would silently break if only <c>Path</c> were updated.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Rename_file_moves_the_file_and_a_subsequent_save_writes_to_the_new_path(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");

        using var scratchDir = TempDirectory.Create();
        var oldPath = Path.Combine(scratchDir.Path, "Old.fch");
        var newPath = Path.Combine(scratchDir.Path, "New.fch");
        File.Copy(corpusPath, oldPath);

        var editor = CharacterEditor.Open(oldPath);
        Assert.NotNull(editor);

        editor!.RenameFile(newPath);
        Assert.Equal(newPath, editor.Path);
        Assert.False(File.Exists(oldPath), "The old path should no longer exist after a rename.");
        Assert.True(File.Exists(newPath));

        editor.SetPlayerName("Norn-Edited-Name");
        editor.Save();

        Assert.False(File.Exists(oldPath), "Save() after a rename must not recreate a file at the old path.");
        var reopened = CharacterEditor.Open(newPath);
        Assert.NotNull(reopened);
        Assert.Equal("Norn-Edited-Name", reopened!.View.Meta.PlayerName);
    }

    /// <summary>
    /// A rename resets the baseline <see cref="CharacterEditor.PlayerNameChangedThisSession"/>
    /// compares against only via <see cref="CharacterEditor.Save"/>/<see cref="CharacterEditor.Revert"/>,
    /// not by <see cref="CharacterEditor.RenameFile"/> itself — this defends
    /// that a rename alone (without the paired Save `MainWindow` always
    /// performs immediately after) doesn't silently clear the flag a caller
    /// might still need to check.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Rename_file_alone_does_not_clear_the_player_name_changed_flag(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");

        using var scratchDir = TempDirectory.Create();
        var oldPath = Path.Combine(scratchDir.Path, "Old.fch");
        var newPath = Path.Combine(scratchDir.Path, "New.fch");
        File.Copy(corpusPath, oldPath);

        var editor = CharacterEditor.Open(oldPath);
        Assert.NotNull(editor);

        editor!.SetPlayerName("Norn-Edited-Name");
        Assert.True(editor.PlayerNameChangedThisSession);

        editor.RenameFile(newPath);
        Assert.True(editor.PlayerNameChangedThisSession);

        editor.Save();
        Assert.False(editor.PlayerNameChangedThisSession);
    }

    /// <summary>
    /// Defends the "pins go with map data" mechanical fact directly
    /// clearing one world's map data must flip
    /// <see cref="WorldDto.HasMapData"/> to false and survive a save/reload,
    /// leaving every other field on that world (and every other world)
    /// untouched.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Clear_world_map_data_round_trips_through_save_and_reload(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;
        var target = editor!.View.Worlds.Worlds.FirstOrDefault(w => w.HasMapData);
        Assert.SkipWhen(target is null, $"{fileName} has no world with map data.");

        // Every *other* world's map-data state, captured before the clear:
        // without this the test could not tell a targeted clear from one
        // that wiped every world's map (found in review).
        var otherWorldsBefore = editor.View.Worlds.Worlds
            .Where(w => w.WorldId != target!.WorldId)
            .ToDictionary(w => w.WorldId, w => w.HasMapData);

        editor.ClearWorldMapData(target!.WorldId);
        Assert.True(editor.IsDirty);
        var updated = editor.View.Worlds.Worlds.Single(w => w.WorldId == target.WorldId);
        Assert.False(updated.HasMapData);
        // Everything else on that world survives the clear untouched.
        Assert.Equal(target.SpawnPoint, updated.SpawnPoint);
        Assert.Equal(target.LogoutPoint, updated.LogoutPoint);
        Assert.Equal(target.HomePoint, updated.HomePoint);

        foreach (var (worldId, hadMapData) in otherWorldsBefore)
        {
            Assert.Equal(hadMapData, editor.View.Worlds.Worlds.Single(w => w.WorldId == worldId).HasMapData);
        }

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.False(reopened!.View.Worlds.Worlds.Single(w => w.WorldId == target.WorldId).HasMapData);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Clear_world_map_data_edit_does_not_remap_the_unrelated_DTOs(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;
        var target = editor!.View.Worlds.Worlds.FirstOrDefault(w => w.HasMapData);
        Assert.SkipWhen(target is null, $"{fileName} has no world with map data.");

        var before = editor.View;
        editor.ClearWorldMapData(target!.WorldId);

        Assert.Same(before.Meta, editor.View.Meta);
        Assert.Same(before.Skills, editor.View.Skills);
        Assert.Same(before.Vitals, editor.View.Vitals);
        Assert.Same(before.Inventory, editor.View.Inventory);
        Assert.Same(before.Statistics, editor.View.Statistics);
        Assert.NotSame(before.Worlds, editor.View.Worlds);
    }

    /// <summary>
    /// Exercises <see cref="CharacterEditor.ExploreAllMap"/> — the one
    /// mutator in this class that replaces a
    /// compressed map blob wholesale via <c>Minimap.Decode</c>/<c>Encode</c>
    /// rather than patching a decoded in-memory model. Asserts the R2
    /// property Encode can actually claim:
    /// decode-mutate-encode-decode reproduces the same texture size, the
    /// same untouched <c>ExploredOthers</c>, and the same pins, with every
    /// byte of <c>Explored</c> now non-zero — and that this survives a real
    /// save-to-disk/reopen, not just the in-memory edit.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void ExploreAllMap_fills_own_exploration_and_round_trips_through_save_and_reload(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;
        var target = editor!.View.Worlds.Worlds.FirstOrDefault(w => w.HasMapData);
        Assert.SkipWhen(target is null, $"{fileName} has no world with map data.");

        var before = editor.DecodeWorldMap(target!.WorldId);
        Assert.NotNull(before);

        // Pin *content*, not just the counts the Adapter DTO exposes (found
        // in review): this test's own doc comment promises "the same pins",
        // but scrambling every pin's name, position, type or owner while
        // preserving the count would have satisfied the count assertions
        // below. WorldMapDto deliberately carries only counts, so the real
        // comparison has to drop to the GameCore decode of the raw blob.
        var pinsBefore = DecodePins(scratch.Path, target.WorldId);

        editor.ExploreAllMap(target.WorldId);
        Assert.True(editor.IsDirty);

        void AssertFullyExplored(WorldMapDto? after)
        {
            Assert.NotNull(after);
            Assert.Equal(before!.TextureSize, after!.TextureSize);
            Assert.All(after.Explored, b => Assert.NotEqual(0, b));
            Assert.Equal(before.ExploredOthers, after.ExploredOthers);
            Assert.Equal(before.OwnPinCount, after.OwnPinCount);
            Assert.Equal(before.ReceivedPinCount, after.ReceivedPinCount);
        }

        AssertFullyExplored(editor.DecodeWorldMap(target.WorldId));

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        AssertFullyExplored(reopened!.DecodeWorldMap(target.WorldId));

        var pinsAfter = DecodePins(scratch.Path, target.WorldId);
        Assert.Equal(pinsBefore.Count, pinsAfter.Count);
        for (var i = 0; i < pinsBefore.Count; i++)
        {
            Assert.Equal(pinsBefore[i].m_name, pinsAfter[i].m_name);
            Assert.Equal(pinsBefore[i].m_pos, pinsAfter[i].m_pos);
            Assert.Equal(pinsBefore[i].m_type, pinsAfter[i].m_type);
            Assert.Equal(pinsBefore[i].m_checked, pinsAfter[i].m_checked);
            Assert.Equal(pinsBefore[i].m_ownerID, pinsAfter[i].m_ownerID);
            Assert.Equal(pinsBefore[i].m_author, pinsAfter[i].m_author);
        }
    }

    /// <summary>Reads one world's pins straight from the file via the
    /// GameCore decode — <see cref="WorldMapDto"/> deliberately carries only
    /// pin counts, so content comparison can't go through the Adapter.</summary>
    private static List<Minimap.PinData> DecodePins(string path, long worldId)
    {
        var profile = new PlayerProfile(path);
        Assert.True(profile.Load());

        var blob = profile.m_worldData.Single(pair => pair.Key == worldId).Value.m_mapData;
        Assert.NotNull(blob);

        return Minimap.Decode(blob!).Pins;
    }

    /// <summary>
    /// Same shape as <see cref="Remove_item_round_trips_through_save_and_reload"/>
    /// — removal by the world's own key (<see cref="WorldDto.WorldId"/>),
    /// same "no stable index" precedent removing a list entry always needs.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Remove_world_round_trips_through_save_and_reload(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;
        var target = editor!.View.Worlds.Worlds.FirstOrDefault();
        Assert.SkipWhen(target is null, $"{fileName} has no world data.");

        var remainingCount = editor.View.Worlds.Worlds.Count - 1;
        editor.RemoveWorld(target!.WorldId);
        Assert.True(editor.IsDirty);
        Assert.Equal(remainingCount, editor.View.Worlds.Worlds.Count);
        Assert.DoesNotContain(editor.View.Worlds.Worlds, w => w.WorldId == target.WorldId);

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.Equal(remainingCount, reopened!.View.Worlds.Worlds.Count);
        Assert.DoesNotContain(reopened.View.Worlds.Worlds, w => w.WorldId == target.WorldId);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Remove_world_edit_does_not_remap_the_unrelated_DTOs(string? fileName)
    {
        var (scratch, editor) = OpenScratchCopy(fileName);
        using var _ = scratch;
        var target = editor!.View.Worlds.Worlds.FirstOrDefault();
        Assert.SkipWhen(target is null, $"{fileName} has no world data.");

        var before = editor.View;
        editor.RemoveWorld(target!.WorldId);

        Assert.Same(before.Meta, editor.View.Meta);
        Assert.Same(before.Skills, editor.View.Skills);
        Assert.Same(before.Vitals, editor.View.Vitals);
        Assert.Same(before.Inventory, editor.View.Inventory);
        Assert.Same(before.Statistics, editor.View.Statistics);
        Assert.NotSame(before.Worlds, editor.View.Worlds);
    }

    /// <summary>
    /// GameCore's <c>SavePlayerToDisk()</c> is internal, reachable only from
    /// the two assemblies it grants — <c>Norn.Tests</c> (this project) and
    /// <c>Norn.Adapter</c> — but nothing stops a second method in
    /// <c>Norn.Adapter</c> from also calling it. <see cref="CharacterEditor.Save"/>
    /// is where the <c>.old</c> backup lives (found in review); a
    /// second, backup-less call site there would compile cleanly and lose
    /// that safety silently. This is the mechanical guard: found in review,
    /// not by inspection.
    /// </summary>
    [Fact]
    public void SavePlayerToDisk_has_exactly_one_call_site_in_Adapter()
    {
        var root = TestPaths.RepositoryRoot;
        Assert.SkipWhen(root is null, "Source tree not present; Adapter source cannot be scanned.");

        var adapterDirectory = Path.Combine(root!, "Norn.Adapter");
        var callSites = Directory
            .EnumerateFiles(adapterDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(root!, path))
            .Select(path => (Path: path, Count: CountCallSites(File.ReadAllLines(path), "SavePlayerToDisk(")))
            .Where(x => x.Count > 0)
            .ToList();

        var total = callSites.Sum(x => x.Count);
        Assert.True(
            total == 1 && callSites is [{ Path: var path }] && Path.GetFileName(path) == "CharacterEditor.cs",
            "SavePlayerToDisk() should have exactly one call site in Norn.Adapter, in CharacterEditor.cs "
            + $"(where the .old backup runs immediately before it), but found: "
            + string.Join(", ", callSites.Select(x => $"{Path.GetFileName(x.Path)}×{x.Count}")));
    }

    private static bool IsBuildOutput(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                         || segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Counts real call sites of <paramref name="substring"/>,
    /// excluding comments — a doc-comment <c>&lt;see cref="..."/&gt;</c>
    /// reference, an ordinary <c>//</c> note, or a trailing same-line
    /// comment mentioning the method is not a call to it. Used to only
    /// exclude whole lines prefixed with <c>///</c>, missing ordinary
    /// <c>//</c> comments (including a trailing comment after real code on
    /// the same line) entirely — a plain comment anywhere in scope
    /// containing the literal substring would be miscounted as a real call
    /// site (found in review). Stripping from the first <c>//</c> onward is
    /// naive (doesn't account for <c>//</c> inside a string literal or a
    /// block comment), but this codebase's own convention never uses block
    /// comments, and this guard only needs to not miscount a comment, not
    /// parse C# in general.</summary>
    private static int CountCallSites(string[] lines, string substring)
    {
        return lines
            .Select(StripLineComment)
            .Sum(line =>
            {
                var count = 0;
                var index = 0;
                while ((index = line.IndexOf(substring, index, StringComparison.Ordinal)) >= 0)
                {
                    count++;
                    index += substring.Length;
                }

                return count;
            });
    }

    private static string StripLineComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index < 0 ? line : line[..index];
    }
}
