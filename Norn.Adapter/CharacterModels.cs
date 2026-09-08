namespace Norn.Adapter;

// game-derived: Player.m_modelIndex / VisEquipment.m_models (Valheim
// 0.221.4/0.221.10, identical in both trees — confirmed via
// the decompiled source). Not wire format: the raw int already round-trips
// correctly with zero knowledge of what it means,
// so this label mapping lives here, not GameCore, and isn't subject to
// mirror-discipline. No enum or named constant exists anywhere in decompiled
// source for this — 0 = male / 1 = female is inferred from context
// (CharacterAnimEvent's m_maleOffset/m_femaleOffset fields keyed off
// index == 1; PlayerCustomizaton resets to male on enable; beard-cycling is
// blocked at index 1), not asserted directly. The true vanilla array length
// (VisEquipment.m_models.Length) is prefab/asset data invisible to
// decompiled C# — these are the only two values anything in source ever
// references, not a proven upper bound.
//
// Domain-general (both General's read-only display and its model-index
// picker want the same label), so this sits in Norn.Adapter rather than
// Norn.UI — unlike AppearanceColors.cs, which serves exactly one
// presentation-only control.
public static class CharacterModels
{
    public static readonly IReadOnlyList<(int Index, string Label)> Known =
    [
        (0, "Male"),
        (1, "Female"),
    ];
}
