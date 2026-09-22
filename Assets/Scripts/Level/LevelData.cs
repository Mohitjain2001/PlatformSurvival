using System;
using UnityEngine;

public enum FloorPattern
{
    Solid,           // Full solid hexagon grid
    DonutRing,       // Middle center tiles missing (hole in center)
    CrossPaths,      // Cross path layout with gaps in 4 quadrants
    SwissCheese,     // Scattered missing tiles
    OuterRingOnly,   // Only outer perimeter tiles exist
    TwoIslands       // Split into two separate tile islands
}

[Serializable]
public class LevelConfig
{
    public int levelNumber = 1;
    public int floorCount = 3;
    public FloorPattern[] floorPatterns = new FloorPattern[] { FloorPattern.Solid, FloorPattern.DonutRing, FloorPattern.Solid };
    public int botCount = 4;
    public float botSpeed = 5.0f;
    public float tileDropDelay = 0.8f;
    public int gridRadius = 6;
}
