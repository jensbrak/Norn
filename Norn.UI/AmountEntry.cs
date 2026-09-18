namespace Norn.UI;

// game-derived: the amount-entry NumericUpDown's own Maximum is deliberately
// NOT set to a selected item's real catalog MaxStack — Avalonia's
// NumericUpDown rejects each keystroke live once the in-progress number
// would exceed Maximum (confirmed by running the app: typing "21" against a
// real Maximum of 20 has its final "1" rejected mid-keystroke, reverting the
// field to "2" instead of letting the user finish typing and clamping once
// on commit). Setting Maximum to a generous, mostly-unreachable ceiling
// instead avoids that live-reject entirely for any real item, since no
// catalog entry exceeds it; the true per-item bound is still enforced,
// correctly, once — at commit — by CharacterEditor.SetItemStack's own clamp.
// 999 (Coins, Norn.UI/Content/SharedItemData.csv) is the highest MaxStack
// anywhere in the catalog, confirmed 2026-09-18.
public static class AmountEntry
{
    public const decimal Ceiling = 999;
}
