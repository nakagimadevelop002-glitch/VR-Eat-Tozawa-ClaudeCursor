using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEngine;

/// <summary>
/// MCP tool to display messages in Unity Console.
/// </summary>
[McpServerToolType, Description("Display messages in Unity Console")]
internal sealed class LogToUnityConsoleTool
{
    [McpServerTool, Description("Display a message in Unity Console. The message will appear in Unity Editor's Console window.")]
    public async ValueTask<string> LogToUnityConsole(
        [Description("The message to display in Unity Console.")]
        string message = "")
    {
        try
        {
            await UniTask.SwitchToMainThread();
            Debug.Log($"[MCP] {message}");
            return $"Logged to Unity Console: {message}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Display a warning message in Unity Console (yellow icon).")]
    public async ValueTask<string> LogWarningToUnityConsole(
        [Description("The warning message to display in Unity Console.")]
        string message = "")
    {
        try
        {
            await UniTask.SwitchToMainThread();
            Debug.LogWarning($"[MCP] {message}");
            return $"Logged warning to Unity Console: {message}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Display an error message in Unity Console (red icon).")]
    public async ValueTask<string> LogErrorToUnityConsole(
        [Description("The error message to display in Unity Console.")]
        string message = "")
    {
        try
        {
            await UniTask.SwitchToMainThread();
            Debug.LogError($"[MCP] {message}");
            return $"Logged error to Unity Console: {message}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
