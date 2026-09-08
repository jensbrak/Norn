using Norn.GameCore;
using Norn.GameCore.Primitives;

namespace Norn.Adapter;

/// <summary>
/// Bridges a loaded <see cref="PlayerProfile"/> to its inner <see cref="Player"/>.
/// </summary>
/// <remarks>
/// The game's own bridge (<c>PlayerProfile.LoadPlayerData</c>) is not mirrored in
/// GameCore — see the OMISSION note on <see cref="PlayerProfile.LoadPlayerDataFromDisk"/>,
/// which calls out this exact gap as Adapter's job. <see cref="Player.m_playerData"/>
/// is opaque to GameCore; decoding it into a <see cref="Player"/> is a read-only
/// concern of the Adapter, not the mirror.
/// </remarks>
public static class PlayerLoader
{
    /// <summary>
    /// Decodes <paramref name="profile"/>'s inner player-data blob, or <c>null</c>
    /// when the profile carries none — also on a present-but-corrupted blob
    /// that fails to decode, same as every other external-data reader in
    /// this module (<c>LocalizationCatalog</c>/<c>SharedItemDataCatalog</c>/
    /// <c>WorldIdentityCatalog</c> all fail to an empty/absent result rather
    /// than throwing). A truncated or otherwise malformed blob used to throw
    /// straight out of <c>Player.Load</c>, crashing the whole profile open
    /// instead of degrading the way a profile with no inner blob at all
    /// already does — every caller of this method already treats a
    /// <c>null</c> result as "no inner blob to edit" and no-ops the
    /// affected mutators accordingly, so folding "corrupted" into "absent"
    /// costs nothing new (found in review). Catches broadly, deliberately:
    /// <see cref="ZPackage"/>'s own note documents that it adds no length
    /// guards anywhere and "the decision to add one belongs to the layer
    /// that opens untrusted files" — this is that layer, and a corrupt
    /// length prefix there can throw anything from
    /// <see cref="EndOfStreamException"/> (truncated scalar read) to
    /// <see cref="OutOfMemoryException"/> (a huge corrupt length driving an
    /// allocation), not a short, enumerable list of BCL exception types.
    /// </summary>
    public static Player? Load(PlayerProfile profile)
    {
        if (profile.m_playerData is null)
        {
            return null;
        }

        try
        {
            var pkg = new ZPackage(profile.m_playerData);
            var player = new Player();
            player.Load(pkg);

            // Also null when the decode succeeded but didn't consume the
            // whole blob (found in review, verified against a real fixture:
            // four appended bytes decoded "fine" and then re-encoded four
            // bytes shorter). Player.Load's version gates are all
            // `v >= N` with no upper bound — a blob from a future inner
            // version carrying fields this build's mirror doesn't read at
            // all parses without complaint, leaving its tail unread, and
            // Player.Save then writes back only what it currently knows.
            // Editing such a save would silently delete the part Norn
            // didn't understand, so treat "not fully understood" exactly
            // like "no inner blob to edit" — every caller already no-ops
            // its mutators on null.
            //
            // Deliberately NOT gated on PlayerDataVersion itself: Save
            // always writes its own current version literal, the same
            // upgrade-on-save behavior PlayerProfile's outer envelope
            // already has by design (see ProfileRoundTripTests' R1/R2
            // split). A version number that changes on save is that
            // documented policy, not data loss; unread trailing bytes are.
            if (pkg.GetPos() != pkg.Size())
            {
                return null;
            }

            return player;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// The write half of the bridge — encodes <paramref name="player"/> back
    /// into the byte shape <see cref="PlayerProfile.m_playerData"/> expects.
    /// First used by <see cref="CharacterEditor"/> for skin/hair color
    /// editing — any inner-blob field edit needs this same
    /// decode-mutate-reencode round trip, not just color.
    /// </summary>
    public static byte[] Save(Player player)
    {
        var pkg = new ZPackage();
        player.Save(pkg);
        return pkg.GetArray();
    }
}
