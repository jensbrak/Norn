namespace Norn.Adapter;

/// <summary>
/// An RGB color, decomposed from <c>Norn.GameCore.Primitives.Vector3</c>
/// so no GameCore.Primitives type crosses the Adapter boundary.
/// </summary>
public readonly record struct ColorDto(float R, float G, float B);
