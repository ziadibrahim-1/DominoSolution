using System;
using System.Collections.Generic;
using System.IO;

namespace Domino.Server.Helpers;

/// <summary>
/// Writes the final game result to results.txt in the exact required format:
/// Game Room_Name="<name>", Player Name="<p1>", Player Points="<s1>", Player Name="<p2>", ...
///
/// Uses lock{} because multiple games could end at the same time (unlikely but possible).
/// File.AppendAllText is NOT thread-safe on its own.
/// </summary>
public static class ResultsWriter
{
    private static readonly string _filePath = "./results.txt";
    private static readonly object _fileLock = new();

    /// <param name="roomName">The display name of the room</param>
    /// <param name="playerNames">Dictionary of playerId -> playerName</param>
    /// <param name="finalScores">Dictionary of playerId -> total score (from GameEnd message)</param>
    public static void AppendResult(
        string roomName,
        Dictionary<string, string> playerNames,
        Dictionary<string, int> finalScores)
    {
        // Build parts in order: room name first, then each player
        var parts = new List<string>
        {
            $"Game Room_Name=\"{roomName}\""
        };

        foreach (var (playerId, score) in finalScores)
        {
            var name = playerNames.TryGetValue(playerId, out var n) ? n : playerId;
            parts.Add($"Player Name=\"{name}\"");
            parts.Add($"Player Points=\"{score}\"");
        }

        var line = string.Join(", ", parts);

        lock (_fileLock)
            File.AppendAllText(_filePath, line + Environment.NewLine);

        Console.WriteLine($"[Results] Saved: {line}");
    }
}
