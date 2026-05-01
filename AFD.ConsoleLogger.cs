// Copyright (c) 2026 Kayser
// SPDX-License-Identifier: MIT
using Mafi;
using Mafi.Logging;
using UnityEngine;

namespace AutoForestryDesignations;

/// <summary>
/// Forwards Mafi log messages to Unity's Debug console instead of file logging.
/// Only active in Debug builds.
/// </summary>
public static class ConsoleLogger
{
    private static bool s_isSubscribed;

    /// <summary>
    /// Enables console logging by subscribing to the Log.LogReceived event.
    /// This call is removed in Release builds.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    public static void Enable()
    {
        if (s_isSubscribed)
            return;

        Log.LogReceived += OnLogReceived;
        s_isSubscribed = true;
        UnityEngine.Debug.Log("[AFD] Console logging enabled");
    }

    /// <summary>
    /// Disables console logging by unsubscribing from the Log.LogReceived event.
    /// This call is removed in Release builds.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    public static void Disable()
    {
        if (!s_isSubscribed)
            return;

        Log.LogReceived -= OnLogReceived;
        s_isSubscribed = false;
        UnityEngine.Debug.Log("[AFD] Console logging disabled");
    }

    private static void OnLogReceived(LogEntry logEntry)
    {
        // Filter for AutoForestryDesignations logs only (optional - remove if you want all logs)
        if (!logEntry.Message.Contains("[AFD]") && !logEntry.Message.Contains("[AutoDepth]"))
            return;

        // Format the log entry similar to the file logger
        string formattedMessage = FormatLogEntry(logEntry);

        // Forward to appropriate Unity debug method based on log type
        switch (logEntry.Type)
        {
            case Mafi.Logging.LogType.Warning:
                UnityEngine.Debug.LogWarning(formattedMessage);
                break;

            case Mafi.Logging.LogType.Error:
            case Mafi.Logging.LogType.Exception:
                UnityEngine.Debug.LogError(formattedMessage);
                break;

            default:
                UnityEngine.Debug.Log(formattedMessage);
                break;
        }
    }

    private static string FormatLogEntry(LogEntry logEntry)
    {
        // Format: [Type] HH:mm:ss ThreadName: Message
        string type = LogTypeToString(logEntry.Type);
        string timestamp = logEntry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
        string threadName = string.IsNullOrEmpty(logEntry.ThreadName) 
            ? "---" 
            : (logEntry.ThreadName.Length >= 3 
                ? logEntry.ThreadName.Substring(0, 3) 
                : logEntry.ThreadName.PadRight(3));

        return $"[{type}] {timestamp} ~{threadName}: {logEntry.Message}";
    }

    private static string LogTypeToString(Mafi.Logging.LogType logType)
    {
        return logType switch
        {
            Mafi.Logging.LogType.Debug => "DBG",
            Mafi.Logging.LogType.Info => "INF",
            Mafi.Logging.LogType.Warning => "WRN",
            Mafi.Logging.LogType.Error => "ERR",
            Mafi.Logging.LogType.Exception => "EXC",
            Mafi.Logging.LogType.GameProgress => "GPS",
            _ => "UNK",
        };
    }
}
