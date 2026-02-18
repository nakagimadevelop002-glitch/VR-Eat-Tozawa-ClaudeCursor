using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using UnityEngine;
using UnityNaturalMCP.Editor;

/// <summary>
/// VR_Eatプロジェクト固有の [McpServerToolType] 属性付きクラスを自動検出し登録するビルダー。
/// サブモジュール外のプロジェクト固有ツール（PythonServerTool等）を登録する。
/// </summary>
[CreateAssetMenu(fileName = "VREatMcpToolBuilder",
    menuName = "UnityNaturalMCP/VR_Eat MCP Tool Builder")]
public class VREatMcpToolBuilder : McpBuilderScriptableObject
{
    public override void Build(IMcpServerBuilder builder)
    {
        var assembly = typeof(VREatMcpToolBuilder).Assembly;

        var toolTypes = assembly.GetTypes()
            .Where(t => Attribute.IsDefined(t, typeof(McpServerToolTypeAttribute)));

        var withToolsMethod = FindWithToolsMethod();
        if (withToolsMethod == null)
        {
            Debug.LogError("[VREatMcpToolBuilder] WithTools method not found.");
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
                Debug.LogError($"[VREatMcpToolBuilder] Failed to register {toolType.Name}: {e}");
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
