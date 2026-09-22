using System.Collections.Generic;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    public static LevelGenerator Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public static LevelConfig GetConfigForLevel(int levelNumber)
    {
        LevelConfig config = new LevelConfig();
        config.levelNumber = levelNumber;

        // Level scaling formula
        if (levelNumber <= 2)
        {
            config.floorCount = 3;
            config.botCount = 4;
            config.tileDropDelay = 0.8f;
            config.botSpeed = 4.8f;
            config.gridRadius = 6;
            config.floorPatterns = new FloorPattern[]
            {
                FloorPattern.Solid,
                FloorPattern.DonutRing,
                FloorPattern.Solid
            };
        }
        else if (levelNumber <= 5)
        {
            config.floorCount = 3;
            config.botCount = 5;
            config.tileDropDelay = 0.7f;
            config.botSpeed = 5.2f;
            config.gridRadius = 6;
            config.floorPatterns = new FloorPattern[]
            {
                FloorPattern.DonutRing,
                FloorPattern.CrossPaths,
                FloorPattern.Solid
            };
        }
        else if (levelNumber <= 10)
        {
            config.floorCount = 4;
            config.botCount = 6;
            config.tileDropDelay = 0.6f;
            config.botSpeed = 5.6f;
            config.gridRadius = 6;
            config.floorPatterns = new FloorPattern[]
            {
                FloorPattern.SwissCheese,
                FloorPattern.DonutRing,
                FloorPattern.TwoIslands,
                FloorPattern.Solid
            };
        }
        else
        {
            // Procedural scaling for Level 11+
            config.floorCount = Mathf.Min(5, 4 + (levelNumber - 10) / 5);
            config.botCount = Mathf.Min(8, 6 + (levelNumber - 10) / 3);
            config.tileDropDelay = Mathf.Max(0.4f, 0.6f - (levelNumber - 10) * 0.02f);
            config.botSpeed = Mathf.Min(7.0f, 5.6f + (levelNumber - 10) * 0.1f);
            config.gridRadius = 6;

            FloorPattern[] pool = new FloorPattern[]
            {
                FloorPattern.DonutRing,
                FloorPattern.CrossPaths,
                FloorPattern.SwissCheese,
                FloorPattern.TwoIslands,
                FloorPattern.OuterRingOnly,
                FloorPattern.Solid
            };

            config.floorPatterns = new FloorPattern[config.floorCount];
            // Always make bottom floor solid or near-solid so game doesn't instantly end
            for (int i = 0; i < config.floorCount - 1; i++)
            {
                int pIdx = (levelNumber + i * 3) % (pool.Length - 1);
                config.floorPatterns[i] = pool[pIdx];
            }
            config.floorPatterns[config.floorCount - 1] = FloorPattern.Solid;
        }

        return config;
    }

    public static bool ShouldSpawnTile(int q, int r, int gridRadius, FloorPattern pattern)
    {
        // Distance from hex center (axial distance)
        int dist = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2;

        switch (pattern)
        {
            case FloorPattern.Solid:
                return true;

            case FloorPattern.DonutRing:
                // Center hole: tiles within radius 2 are missing
                if (dist <= 2) return false;
                return true;

            case FloorPattern.CrossPaths:
                // Keep the 3 main axes + outer rim, skip quadrant interiors
                if (q == 0 || r == 0 || (q + r) == 0 || dist >= gridRadius - 1) return true;
                return false;

            case FloorPattern.SwissCheese:
                // Guaranteed center platform so players can spawn, random missing tiles elsewhere
                if (dist <= 1) return true;
                int hash = Mathf.Abs((q * 73 + r * 37) % 7);
                return hash != 0; // ~14% tiles missing

            case FloorPattern.OuterRingOnly:
                // Only outer 2 rings exist
                if (dist >= gridRadius - 2) return true;
                return false;

            case FloorPattern.TwoIslands:
                // Vertical gap straight down the middle (X near 0)
                float xOffset = q + r / 2.0f;
                if (Mathf.Abs(xOffset) < 1.2f) return false;
                return true;

            default:
                return true;
        }
    }
}
