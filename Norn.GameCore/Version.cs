namespace Norn.GameCore;

// mirrors: Version
// source:  Valheim 1.0.30
// note:    NOT AN EXHAUSTIVE FIELD LIST. Only the fields, enums and methods
//          reachable from the .fch load path plus the .fwl2 load path are
//          carried, per the rule "only fields reachable from Load/Save"
//          already applied throughout GameCore. Not mirrored, and with no
//          bearing on either path: c_networkVersion, c_WorldGenVersion,
//          c_biomeDataVersion, c_SharedMapVersion, c_CachedMinimapVersion,
//          CurrentVersion, FirstVersionWithNetworkVersion/
//          PlatformRestriction/Modifiers, the Network/BiomeData/SharedMap/
//          CachedMinimap enums, and the methods GetVersionString/GetPlatform/
//          GetPlatformPrefix/GetHardwarePrefix.
// note:    SHAPE CHANGE AT 1.0.7, AND THE REASON THIS FILE GREW. Source
//          replaced its loose `const int` version fields with typed members
//          of nested enums, renamed every one of them (m_playerVersion ->
//          c_PlayerVersion, m_worldVersion -> c_WorldVersion,
//          m_itemDataVersion -> c_ItemDataVersion, m_playerDataVersion ->
//          c_PlayerDataVersion), moved the map version out of Minimap
//          entirely (Minimap.MAPVERSION no longer exists; it is now
//          Version.Map), and changed IsWorldVersionCompatible/
//          IsPlayerVersionCompatible to take the enum type rather than int.
//          The mirror follows all of it. This is transcription, not tidying:
//          the enums are what a fresh decompile now shows, so carrying them
//          is what keeps this file diffable. It also removes the note the
//          0.221.10 mirror carried about bare literals in the two
//          compatibility methods — source names the members there now, so we
//          do too, and there is no longer an inlining question to reason
//          about.
// note:    MEMBER ORDER. Within each group — fields, methods, enum members —
//          source's own declaration order is kept, including where that
//          order is strange. The groups themselves are not: a decompile
//          emits the two methods before the five constants; here the
//          constants come first. Version.Player in particular declares Stats/MapData/
//          DeathPoint before OldestForwardCompatible and then jumps to
//          FoodFix = 34; the gap at 31-33 and the out-of-sequence
//          OldestForwardCompatible = 27 are source's, not a transcription
//          slip. Version.PlayerData has a real hole at 30 (BogWitch = 29,
//          then AbandonedDN explicitly = 31) and Version.Item aliases two
//          names to 106 (PickedUp implicit, Stable explicit). Both verified
//          against source; neither is reachable by any gate on this path.
// note:    VISIBILITY DIVERGENCE. `public abstract class` in source (it was
//          `internal class` at 0.221.10), with an unused protected
//          constructor; `public static class` here, matching how every other
//          GameCore mirror exposes its constants and methods to Adapter.

/// <summary>Version constants and the profile-envelope/world-envelope compatibility checks.</summary>
public static class Version
{
    public const Player c_PlayerVersion = Player.CheatCleanSlate;

    public const World c_WorldVersion = World.CheatCleanSlate;

    public const Item c_ItemDataVersion = Item.NewCheats;

    public const PlayerData c_PlayerDataVersion = PlayerData.PostLaunch;

    public const Map c_MapVersion = Map.PinsAuthor;

    public static bool IsWorldVersionCompatible(World version)
    {
        return version <= World.CheatCleanSlate && version >= World.OldestForwardCompatible;
    }

    public static bool IsPlayerVersionCompatible(Player version)
    {
        return version <= Player.CheatCleanSlate && version >= Player.OldestForwardCompatible;
    }

    // mirrors: Version.Player
    // source:  Valheim 1.0.30
    public enum Player
    {
        Stats = 28,
        MapData,
        DeathPoint,
        OldestForwardCompatible = 27,
        FoodFix = 34,
        SeparateFog,
        CompressedMapData,
        Mistland,
        Stats2,
        Ashlands,
        FirstSpawn,
        BogWitch,
        CallToArms,
        Celebration,
        AbandonedDN,
        Chunked,
        DeepNorth,
        CheatCleanSlate
    }

    // mirrors: Version.World
    // source:  Valheim 1.0.30
    public enum World
    {
        SupportNetTime = 4,
        OldestForwardCompatible = 9,
        SupportZoneSystem = 12,
        SupportZoneSystem2,
        SupportZoneSystem3,
        SupportRandomEvent,
        SupportPriority,
        PrefabInZDO,
        SupportLocations,
        SupportZoneSystem4,
        LocationGenerated,
        LocationVersion,
        SupportDistant,
        ZDOType,
        StopSupportPriority,
        SupportRandomEvent2,
        WorldGenVersion,
        SupportByteArray,
        Mistlands,
        Hildir,
        NeedsDB,
        NewSaveFormat,
        GlobalKeys,
        NumItems,
        Ashlands,
        BogWitch,
        Combat,
        Celebration,
        DeepNorthNoChunk,
        DeepNorthNoChunk2,
        ChunkedSave,
        DeepNorth,
        CheatCleanSlate
    }

    // mirrors: Version.Item
    // source:  Valheim 1.0.30
    public enum Item
    {
        Quality = 101,
        Variant,
        CrafterID,
        CustomData,
        WorldLevel,
        PickedUp,
        Stable = 106,
        AbandonedDN,
        Smaller,
        ChunksNCheats,
        NewCheats
    }

    // mirrors: Version.PlayerData
    // source:  Valheim 1.0.30
    public enum PlayerData
    {
        Original = 2,
        SkinHair = 4,
        SkinHairColor,
        Uniques,
        MaxHealth,
        FirstSpawn,
        Trophies,
        MaxStamina,
        PlayerModel,
        Food,
        BurnRate,
        Food2,
        Stations,
        FoodStamina,
        Skills,
        KnownBiomes,
        RemoveTutorials,
        TimeSinceDeath,
        ReAddTutorials,
        KnownTexts,
        GuardianPower,
        GuardianPowerCooldown,
        FoodTime,
        EitrStamina,
        AshlandMaterials,
        MovedFirstSpawn,
        BogWitch,
        AbandonedDN = 31,
        ChunkedSaves,
        ChunkedNorth,
        PostLaunch
    }

    // mirrors: Version.Map
    // source:  Valheim 1.0.30
    // note:    Lived on Minimap as `private static int MAPVERSION = 8` through
    //          0.221.10 and moved here at 1.0.7. The value did not change; the
    //          home and the type did, and every Minimap gate that read an int
    //          literal now reads a member of this enum.
    public enum Map
    {
        Pins = 2,
        PinsChecked,
        VisibleOnMap,
        NewExplore,
        PinsOwnerID,
        Compressed,
        PinsAuthor
    }
}
