using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 空のGameObjectをシーンに作成するMCPツール。
/// </summary>
[McpServerToolType, Description("Create GameObjects in the Unity scene")]
internal sealed class CreateEmptyGameObjectTool
{
    [McpServerTool, Description("Create an empty GameObject in the active scene. Equivalent to 'GameObject > Create Empty' in the Unity Editor.")]
    public async ValueTask<string> CreateEmptyGameObject(
        [Description("The name of the new GameObject. Defaults to 'GameObject' if not specified.")]
        string name = "GameObject")
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create Empty GameObject '{name}'");
            return $"Created empty GameObject: '{gameObject.name}' (InstanceID: {gameObject.GetInstanceID()})";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
