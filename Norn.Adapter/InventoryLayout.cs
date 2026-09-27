using Norn.GameCore;

namespace Norn.Adapter;

/// <summary>
/// The player inventory's grid dimensions. Neither is on the item wire —
/// <c>Inventory.Save</c> writes only a version and the items — so width is a
/// code literal and height comes from a player unique key.
/// </summary>
public static class InventoryLayout
{
    // game-derived: Humanoid.m_inventory's field initializer (8 × 4),
    // confirmed against Valheim 1.0.16. Width never changes for the player.
    public const int Width = 8;
    public const int DefaultHeight = 4;

    // game-derived: Player.InventoryRowsKey ("invrows"), Player.OnSpawned and
    // Player.SetInventorySize, confirmed against Valheim 1.0.15 and 1.0.16.
    // Stored in m_uniques as "invrows <n>" (AddUniqueKeyValue's single-space
    // join, key matched case-insensitively by TryGetUniqueKeyValue). Raised
    // one row per Haldor "extra inventory slot" purchase; OnSpawned applies it
    // clamped to 0..9, and falls back to the 8 × 4 default when absent or
    // unparseable.
    private const string RowsKey = "invrows";
    private const int MinHeight = 0;
    private const int MaxHeight = 9;

    /// <summary>The number of rows the game will give this player on spawn.</summary>
    public static int HeightOf(Player player)
    {
        foreach (var unique in player.m_uniques)
        {
            var separator = unique.IndexOf(' ');
            if (separator < 0 || !unique.AsSpan(0, separator).Equals(RowsKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(unique.AsSpan(separator + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var rows))
            {
                return Math.Clamp(rows, MinHeight, MaxHeight);
            }
        }

        return DefaultHeight;
    }
}
