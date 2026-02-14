using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using UnityEngine;
using UnityNaturalMCP.Editor;

/// <summary>
/// 同一アセンブリ内の [McpServerToolType] 属性付きクラスを自動検出し登録する共通ビルダー。
/// 新しいツールクラスを追加する際、このファイルを変更する必要はない。
/// </summary>
[CreateAssetMenu(fileName = "CustomMcpToolBuilder",
    menuName = "UnityNaturalMCP/Custom MCP Tool Builder")]
public class CustomMcpToolBuilder : McpBuilderScriptableObject
{
    public override void Build(IMcpServerBuilder builder)
    {
        var assembly = typeof(CustomMcpToolBuilder).Assembly;

        var toolTypes = assembly.GetTypes()
            .Where(t => Attribute.IsDefined(t, typeof(McpServerToolTypeAttribute)));

        var withToolsMethod = FindWithToolsMethod();
        if (withToolsMethod == null)
        {
            Debug.LogError("[CustomMcpToolBuilder] WithTools method not found.");
            return;
        }

        foreach (var toolType in toolTypes)
        {
            try
            {
                var genericMethod = withToolsMethod.MakeGenericMethod(toolType);
                genericMethod.Invoke(null, new object[] { builder, null });
            }
            catch (Exception e)
            {
                Debug.LogError($"[CustomMcpToolBuilder] Failed to register {toolType.Name}: {e}");
                throw;
            }
        }
    }

    private static MethodInfo FindWithToolsMethod()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Type.EmptyTypes; }
            })
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .FirstOrDefault(m =>
                m.Name == "WithTools" &&
                m.IsGenericMethodDefinition &&
                m.GetGenericArguments().Length == 1);
    }
}
