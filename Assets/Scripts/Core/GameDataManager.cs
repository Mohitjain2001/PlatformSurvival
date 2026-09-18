using UnityEngine;

/// <summary>
/// Static data holder to transfer match results from GameplayScene to WinScene / GameOverScene.
/// </summary>
public static class GameDataManager
{
    public static int LastPlacementRank { get; set; } = 1;
    public static int TotalParticipants { get; set; } = 5;
    public static bool IsVictory { get; set; } = false;

    public static void RecordMatchResult(int rank, int total, bool victory)
    {
        LastPlacementRank = rank;
        TotalParticipants = total;
        IsVictory = victory;
    }
}
