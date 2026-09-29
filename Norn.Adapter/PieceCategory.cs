namespace Norn.Adapter;

// game-derived: Piece.PieceCategory, confirmed against Valheim 1.0.16. The
// Hammer's build-menu tabs; Hoe and Cultivator pieces are all Misc. Max and
// All (= 100) are the enum's own sentinels, not categories a piece carries.
public enum PieceCategory
{
    Misc,
    Crafting,
    BuildingWorkbench,
    BuildingStonecutter,
    Furniture,
    DeepNorth,
    Feasts,
    Food,
    Meads,
}
