namespace Norn.Adapter;

/// <summary>
/// A world-space point, decomposed from <c>Norn.GameCore.Primitives.Vector3</c>
/// so no GameCore.Primitives type crosses the Adapter boundary.
/// </summary>
public readonly record struct PositionDto(float X, float Y, float Z);
