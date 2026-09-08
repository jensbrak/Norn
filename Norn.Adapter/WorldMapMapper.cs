using Norn.GameCore;

namespace Norn.Adapter;

/// <summary>Maps a decoded <see cref="Minimap.MapData"/> to <see cref="WorldMapDto"/>.</summary>
public static class WorldMapMapper
{
    public static WorldMapDto Map(Minimap.MapData mapData)
    {
        var ownPinCount = 0;
        var receivedPinCount = 0;
        foreach (var pin in mapData.Pins)
        {
            if (pin.m_ownerID == 0)
            {
                ownPinCount++;
            }
            else
            {
                receivedPinCount++;
            }
        }

        return new WorldMapDto(mapData.TextureSize, mapData.Explored, mapData.ExploredOthers, ownPinCount, receivedPinCount);
    }
}
