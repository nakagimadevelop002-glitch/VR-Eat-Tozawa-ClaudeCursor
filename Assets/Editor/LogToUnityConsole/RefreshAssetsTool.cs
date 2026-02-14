using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

/// <summary>
/// アセットのリフレッシュを実行するMCPツール。
/// スクリプトのコンパイルはユーザーがUnityエディタをクリックすることでトリガーされる。
/// </summary>
[McpServerToolType, Description("Refresh Unity assets")]
internal sealed class RefreshAssetsTool
{
    [McpServerTool, Description("Execute AssetDatabase.Refresh to detect file changes. After calling this, the user must click on Unity Editor to trigger script compilation.")]
    public async ValueTask<string> RefreshAssets()
    {
        try
        {
            await UniTask.SwitchToMainThread();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return "Assets refreshed. Please click on Unity Editor to trigger script compilation.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
