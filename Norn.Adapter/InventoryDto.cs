namespace Norn.Adapter;

/// <summary>One inventory item. <see cref="GridX"/>/<see cref="GridY"/> are decomposed
/// from <c>Vector2i</c> so no GameCore.Primitives type crosses the boundary.</summary>
public sealed record ItemDto(
    string PrefabName,
    int Stack,
    float Durability,
    int GridX,
    int GridY,
    bool Equipped,
    int Quality,
    int Variant,
    long CrafterId,
    string CrafterName,
    IReadOnlyList<KeyValuePair<string, string>> CustomData,
    int WorldLevel,
    bool PickedUp);

/// <summary>Read-only view for the Inventory tab.</summary>
public sealed record InventoryDto(IReadOnlyList<ItemDto> Items);
