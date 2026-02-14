using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Bridge to display messages from MCP/Cursor in Unity Console.
/// Polls a file and logs its content when updated.
/// </summary>
[InitializeOnLoad]
internal static class McpLogBridge
{
    private static readonly string LogFilePath =
        Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "mcp_log_request.txt");
    private static string _lastContent;
    private static long _lastWriteTime;

    static McpLogBridge()
    {
        EditorApplication.update += OnUpdate;
    }

    private static void OnUpdate()
    {
        if (!File.Exists(LogFilePath)) return;

        var info = new FileInfo(LogFilePath);
        if (info.LastWriteTimeUtc.Ticks == _lastWriteTime) return;

        _lastWriteTime = info.LastWriteTimeUtc.Ticks;
        try
        {
            var content = File.ReadAllText(LogFilePath).Trim();
            if (string.IsNullOrEmpty(content) || content == _lastContent) return;
            _lastContent = content;
            Debug.Log($"[MCP] {content}");
            File.WriteAllText(LogFilePath, "");
        }
        catch { /* ignore */ }
    }
}
