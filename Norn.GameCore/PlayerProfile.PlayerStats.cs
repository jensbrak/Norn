namespace Norn.GameCore;

public partial class PlayerProfile
{
    // mirrors: PlayerProfile.PlayerStats
    // source:  Valheim 1.0.7
    // note:    RESTRUCTURED AT 1.0.7, AND IT IS THE LARGEST CHANGE IN THIS FILE.
    //          Through 0.221.10 this class held exactly one member, m_stats, and
    //          the six metadata dictionaries lived directly on PlayerProfile.
    //          1.0.7 moved all six in here, added three more (m_pickableStats,
    //          m_foodEatenStats, m_piecesPlacedStats), turned m_enemyStats into
    //          a 5-element array indexed by KillModifiers, and made
    //          PlayerProfile hold PlayerStats[10] rather than a single instance.
    //          The dictionaries also moved position within the payload — they
    //          are now read inside the statistics block, before m_firstSpawn,
    //          instead of late in the profile after the date-created field.
    // note:    The m_stats constructor loop is a hardcoded literal in source,
    //          not PlayerStatType.Count. It was 105 through 0.221.10 and is 205
    //          at 1.0.7 — kept as written, literal for literal. This is a
    //          dictionary pre-seeded with 205 keys, not a 205-entry array.
    // note:    The m_enemyStats seeding loop is likewise a hardcoded 5 in
    //          source's constructor, even though the array is declared with
    //          length 5 on the field. Both are carried.
    // note:    ORDERED-COLLECTION DIVERGENCE, pre-existing and unchanged in
    //          kind — only its blast radius grew, from six collections to nine.
    //          Source uses Dictionary<string, float> for every one of these;
    //          this mirror uses List<KeyValuePair<string, float>> via
    //          OrderedCollections so insertion order survives a round trip.
    //          A Dictionary does not guarantee enumeration order, and the
    //          writer re-emits these in enumeration order, so a Dictionary here
    //          would break R1 byte identity on any profile with more than one
    //          entry. m_stats is exempt: it is keyed by a contiguous enum and
    //          the writer emits it by index, never by enumeration.
    // note:    MEMBER ORDER follows the decompiler's own emission order —
    //          indexer, constructor, then fields — rather than the
    //          fields-first shape this file carried at 0.221.10. Matching what
    //          a fresh decompile prints is what keeps the side-by-side diff
    //          readable.
    /// <summary>One difficulty slot's statistics: scalar stats plus nine name-keyed collections.</summary>
    public class PlayerStats
    {
        public float this[PlayerStatType type]
        {
            get => m_stats[type];
            set => m_stats[type] = value;
        }

        public PlayerStats()
        {
            for (int i = 0; i < 205; i++)
            {
                m_stats[(PlayerStatType)i] = 0f;
            }

            for (int j = 0; j < 5; j++)
            {
                m_enemyStats[j] = new List<KeyValuePair<string, float>>();
            }
        }

        public Dictionary<PlayerStatType, float> m_stats = new Dictionary<PlayerStatType, float>();

        public List<KeyValuePair<string, float>> m_knownWorlds = new List<KeyValuePair<string, float>>();

        public List<KeyValuePair<string, float>> m_knownWorldKeys = new List<KeyValuePair<string, float>>();

        public List<KeyValuePair<string, float>> m_knownCommands = new List<KeyValuePair<string, float>>();

        public List<KeyValuePair<string, float>>[] m_enemyStats = new List<KeyValuePair<string, float>>[5];

        public List<KeyValuePair<string, float>> m_itemPickupStats = new List<KeyValuePair<string, float>>();

        public List<KeyValuePair<string, float>> m_itemCraftStats = new List<KeyValuePair<string, float>>();

        public List<KeyValuePair<string, float>> m_pickableStats = new List<KeyValuePair<string, float>>();

        public List<KeyValuePair<string, float>> m_foodEatenStats = new List<KeyValuePair<string, float>>();

        public List<KeyValuePair<string, float>> m_piecesPlacedStats = new List<KeyValuePair<string, float>>();
    }
}
