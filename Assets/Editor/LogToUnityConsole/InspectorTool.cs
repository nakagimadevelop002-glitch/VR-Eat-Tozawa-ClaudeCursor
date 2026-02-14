using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[McpServerToolType, Description("Inspect and manipulate GameObjects, Components, and Properties in the Unity scene.")]
internal sealed class InspectorTool
{
    // ========== MCP Methods (Facade) ==========

    [McpServerTool, Description("Get the hierarchy tree of the active scene. Returns root GameObjects and their children up to the specified depth.")]
    public async ValueTask<string> Ins_GetHierarchyInfo(
        [Description("Maximum depth to traverse. 0 = root only, 1 = root + direct children, etc. Defaults to 1.")]
        int maxDepth = 1)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var sb = new StringBuilder();
            sb.AppendLine($"Scene: {scene.name} ({roots.Length} root objects)");
            sb.AppendLine("---");

            foreach (var root in roots)
            {
                HierarchyNodeInfo.AppendTo(sb, root, 0, maxDepth);
            }

            return sb.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Get detailed information about a specific GameObject including name, active state, tag, layer, components, and children.")]
    public async ValueTask<string> Ins_GetGameObjectInfo(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345'). Path is relative to scene root.")]
        string target)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var info = GameObjectInfo.From(go);
            return info.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Get all serialized properties of a specific component on a GameObject. Shows property names, types, and current values.")]
    public async ValueTask<string> Ins_GetComponentProperties(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Component type name (e.g. 'Transform', 'BoxCollider') or index in brackets (e.g. '[0]', '[1]'). Type name matching is case-insensitive.")]
        string component)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var comp = ComponentResolver.Resolve(go, component);
            var info = ComponentInfo.From(comp);
            return info.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set a GameObject's active state (SetActive). Supports Undo.")]
    public async ValueTask<string> Ins_SetActive(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("True to activate, false to deactivate.")]
        bool active)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            Undo.RecordObject(go, $"Set Active '{go.name}' to {active}");
            go.SetActive(active);
            EditorUtility.SetDirty(go);
            return $"'{go.name}' activeSelf set to {active}.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Rename a GameObject. Supports Undo.")]
    public async ValueTask<string> Ins_RenameGameObject(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("The new name for the GameObject.")]
        string newName)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New name must not be empty.");

            var go = GameObjectResolver.Resolve(target);
            var oldName = go.name;
            Undo.RecordObject(go, $"Rename '{oldName}' to '{newName}'");
            go.name = newName;
            EditorUtility.SetDirty(go);
            return $"Renamed '{oldName}' to '{newName}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set a serialized property value on a component. Use GetComponentProperties first to see available property paths and types. Supports Undo.")]
    public async ValueTask<string> Ins_SetPropertyValue(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Component type name (e.g. 'Transform', 'BoxCollider') or index in brackets (e.g. '[0]', '[1]').")]
        string component,
        [Description("The serialized property path (e.g. 'm_LocalPosition.x', 'm_IsActive'). Use GetComponentProperties to find valid paths.")]
        string propertyPath,
        [Description("The value to set as a string. Examples: '42', '3.14', 'true', 'Hello', '(1.0, 2.0, 3.0)' for Vector3.")]
        string value)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var comp = ComponentResolver.Resolve(go, component);
            var result = PropertyAccessor.WriteProperty(comp, propertyPath, value);
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Add a component to a GameObject by type name. Supports Undo.")]
    public async ValueTask<string> Ins_AddComponent(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Full or short type name of the component to add (e.g. 'BoxCollider', 'Rigidbody', 'UnityEngine.UI.Image').")]
        string componentType)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var type = ComponentResolver.ResolveType(componentType);
            var comp = Undo.AddComponent(go, type);
            return $"Added {comp.GetType().Name} to '{go.name}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Remove a component from a GameObject. Cannot remove Transform. Supports Undo.")]
    public async ValueTask<string> Ins_RemoveComponent(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Component type name (e.g. 'BoxCollider') or index in brackets (e.g. '[1]'). Cannot remove Transform ([0]).")]
        string component)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var comp = ComponentResolver.Resolve(go, component);

            if (comp is Transform)
                throw new InvalidOperationException("Cannot remove Transform component.");

            Undo.DestroyObjectImmediate(comp);
            return $"Removed {comp.GetType().Name} from '{go.name}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Enable or disable a component (only works on components that have an 'enabled' property, e.g. Renderer, Collider, MonoBehaviour). Supports Undo.")]
    public async ValueTask<string> Ins_SetComponentEnabled(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Component type name (e.g. 'MeshRenderer') or index in brackets (e.g. '[1]').")]
        string component,
        [Description("True to enable, false to disable.")]
        bool enabled)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var comp = ComponentResolver.Resolve(go, component);

            if (comp is Behaviour behaviour)
            {
                Undo.RecordObject(behaviour, $"Set {comp.GetType().Name} enabled={enabled}");
                behaviour.enabled = enabled;
                EditorUtility.SetDirty(behaviour);
                return $"{comp.GetType().Name} on '{go.name}' enabled set to {enabled}.";
            }

            if (comp is Renderer renderer)
            {
                Undo.RecordObject(renderer, $"Set {comp.GetType().Name} enabled={enabled}");
                renderer.enabled = enabled;
                EditorUtility.SetDirty(renderer);
                return $"{comp.GetType().Name} on '{go.name}' enabled set to {enabled}.";
            }

            if (comp is Collider collider)
            {
                Undo.RecordObject(collider, $"Set {comp.GetType().Name} enabled={enabled}");
                collider.enabled = enabled;
                EditorUtility.SetDirty(collider);
                return $"{comp.GetType().Name} on '{go.name}' enabled set to {enabled}.";
            }

            throw new InvalidOperationException(
                $"{comp.GetType().Name} does not have an 'enabled' property.");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    // ========== Inner Classes ==========

    /// <summary>パスまたはInstanceIDからGameObjectを解決する</summary>
    private static class GameObjectResolver
    {
        public static GameObject Resolve(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("Target must not be empty.");

            // InstanceID形式: "#12345"
            if (target.StartsWith("#") && int.TryParse(target.Substring(1), out var instanceId))
            {
                var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                if (obj == null)
                    throw new ArgumentException($"No GameObject found with InstanceID {instanceId}.");
                return obj;
            }

            // パス形式: "Canvas/Button" or "/Canvas/Button"
            var path = target.TrimStart('/');
            var go = FindByPath(path);
            if (go == null)
                throw new ArgumentException($"No GameObject found at path '{target}'.");
            return go;
        }

        private static GameObject FindByPath(string path)
        {
            var parts = path.Split('/');
            if (parts.Length == 0) return null;

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var root = roots.FirstOrDefault(r => r.name == parts[0]);
            if (root == null) return null;
            if (parts.Length == 1) return root;

            var current = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                var child = current.Find(parts[i]);
                if (child == null) return null;
                current = child;
            }

            return current.gameObject;
        }
    }

    /// <summary>型名またはインデックスからComponentを解決する</summary>
    private static class ComponentResolver
    {
        public static UnityEngine.Component Resolve(GameObject go, string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("Component identifier must not be empty.");

            // インデックス形式: "[0]", "[1]"
            if (identifier.StartsWith("[") && identifier.EndsWith("]"))
            {
                var indexStr = identifier.Substring(1, identifier.Length - 2);
                if (!int.TryParse(indexStr, out var index))
                    throw new ArgumentException($"Invalid component index: '{identifier}'.");

                var all = go.GetComponents<UnityEngine.Component>();
                if (index < 0 || index >= all.Length)
                    throw new ArgumentException($"Component index {index} out of range. GameObject '{go.name}' has {all.Length} components.");
                return all[index];
            }

            // 型名形式: "Transform", "BoxCollider" (case-insensitive)
            var components = go.GetComponents<UnityEngine.Component>();
            var match = components.FirstOrDefault(c =>
                c != null && c.GetType().Name.Equals(identifier, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                var available = string.Join(", ", components
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name));
                throw new ArgumentException(
                    $"No component '{identifier}' found on '{go.name}'. Available: {available}");
            }

            return match;
        }

        public static Type ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("Component type name must not be empty.");

            // フルネームで検索 (e.g. "UnityEngine.UI.Image")
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t =>
                    typeof(UnityEngine.Component).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    (t.FullName != null && t.FullName.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                     t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)));

            if (type == null)
                throw new ArgumentException($"Component type '{typeName}' not found.");

            return type;
        }
    }

    /// <summary>SerializedPropertyの読み書き</summary>
    private static class PropertyAccessor
    {
        public static List<PropertySummary> ReadAll(UnityEngine.Component component)
        {
            var result = new List<PropertySummary>();
            var so = new SerializedObject(component);
            var iterator = so.GetIterator();

            if (!iterator.NextVisible(true)) return result;

            do
            {
                result.Add(PropertySummary.From(iterator));
            }
            while (iterator.NextVisible(false));

            return result;
        }

        public static string WriteProperty(UnityEngine.Component component, string propertyPath, string value)
        {
            var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyPath);
            if (prop == null)
                throw new ArgumentException(
                    $"Property '{propertyPath}' not found on {component.GetType().Name}.");

            Undo.RecordObject(component, $"Set {propertyPath} on {component.GetType().Name}");

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (!int.TryParse(value, out var intVal))
                        throw new ArgumentException($"Cannot parse '{value}' as int.");
                    prop.intValue = intVal;
                    break;

                case SerializedPropertyType.Boolean:
                    if (!bool.TryParse(value, out var boolVal))
                        throw new ArgumentException($"Cannot parse '{value}' as bool.");
                    prop.boolValue = boolVal;
                    break;

                case SerializedPropertyType.Float:
                    if (!float.TryParse(value, out var floatVal))
                        throw new ArgumentException($"Cannot parse '{value}' as float.");
                    prop.floatValue = floatVal;
                    break;

                case SerializedPropertyType.String:
                    prop.stringValue = value;
                    break;

                case SerializedPropertyType.Enum:
                    if (int.TryParse(value, out var enumIndex))
                    {
                        prop.enumValueIndex = enumIndex;
                    }
                    else
                    {
                        var names = prop.enumDisplayNames;
                        var idx = Array.IndexOf(names, value);
                        if (idx < 0)
                            throw new ArgumentException(
                                $"Invalid enum value '{value}'. Valid: {string.Join(", ", names)}");
                        prop.enumValueIndex = idx;
                    }
                    break;

                case SerializedPropertyType.Color:
                    prop.colorValue = ParseColor(value);
                    break;

                case SerializedPropertyType.Vector2:
                    prop.vector2Value = ParseVector2(value);
                    break;

                case SerializedPropertyType.Vector3:
                    prop.vector3Value = ParseVector3(value);
                    break;

                case SerializedPropertyType.Vector4:
                    prop.vector4Value = ParseVector4(value);
                    break;

                case SerializedPropertyType.Vector2Int:
                    var v2 = ParseVector2(value);
                    prop.vector2IntValue = new Vector2Int((int)v2.x, (int)v2.y);
                    break;

                case SerializedPropertyType.Vector3Int:
                    var v3 = ParseVector3(value);
                    prop.vector3IntValue = new Vector3Int((int)v3.x, (int)v3.y, (int)v3.z);
                    break;

                case SerializedPropertyType.LayerMask:
                    if (!int.TryParse(value, out var maskVal))
                        throw new ArgumentException($"Cannot parse '{value}' as LayerMask int.");
                    prop.intValue = maskVal;
                    break;

                default:
                    throw new ArgumentException(
                        $"Setting property type '{prop.propertyType}' is not supported.");
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            return $"Set {component.GetType().Name}.{propertyPath} = {value}";
        }

        private static Vector2 ParseVector2(string value)
        {
            var nums = ParseFloatTuple(value, 2);
            return new Vector2(nums[0], nums[1]);
        }

        private static Vector3 ParseVector3(string value)
        {
            var nums = ParseFloatTuple(value, 3);
            return new Vector3(nums[0], nums[1], nums[2]);
        }

        private static Vector4 ParseVector4(string value)
        {
            var nums = ParseFloatTuple(value, 4);
            return new Vector4(nums[0], nums[1], nums[2], nums[3]);
        }

        private static Color ParseColor(string value)
        {
            var nums = ParseFloatTuple(value, 4);
            return new Color(nums[0], nums[1], nums[2], nums[3]);
        }

        private static float[] ParseFloatTuple(string value, int expected)
        {
            var cleaned = value.Trim().TrimStart('(').TrimEnd(')');
            var parts = cleaned.Split(',');
            if (parts.Length != expected)
                throw new ArgumentException(
                    $"Expected {expected} comma-separated values, got {parts.Length} in '{value}'.");

            var result = new float[expected];
            for (int i = 0; i < expected; i++)
            {
                if (!float.TryParse(parts[i].Trim(), out result[i]))
                    throw new ArgumentException($"Cannot parse '{parts[i].Trim()}' as float.");
            }
            return result;
        }
    }

    // ========== Data Types ==========

    /// <summary>ヒエラルキーノードの表示情報</summary>
    private static class HierarchyNodeInfo
    {
        public static void AppendTo(StringBuilder sb, GameObject go, int currentDepth, int maxDepth)
        {
            var indent = new string(' ', currentDepth * 2);
            var activeMarker = go.activeSelf ? "" : " [Inactive]";
            var childCount = go.transform.childCount;
            var childInfo = childCount > 0 ? $" ({childCount} children)" : "";

            sb.AppendLine($"{indent}- {go.name} [ID:{go.GetInstanceID()}]{activeMarker}{childInfo}");

            if (currentDepth < maxDepth)
            {
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    AppendTo(sb, go.transform.GetChild(i).gameObject, currentDepth + 1, maxDepth);
                }
            }
        }
    }

    /// <summary>GameObjectの詳細情報</summary>
    private sealed class GameObjectInfo
    {
        public string Name;
        public int InstanceID;
        public bool ActiveSelf;
        public bool ActiveInHierarchy;
        public string Tag;
        public string Layer;
        public string[] Components;
        public string[] Children;

        public static GameObjectInfo From(GameObject go)
        {
            var components = go.GetComponents<UnityEngine.Component>();
            var children = new string[go.transform.childCount];
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i);
                children[i] = $"{child.name} [ID:{child.gameObject.GetInstanceID()}]";
            }

            return new GameObjectInfo
            {
                Name = go.name,
                InstanceID = go.GetInstanceID(),
                ActiveSelf = go.activeSelf,
                ActiveInHierarchy = go.activeInHierarchy,
                Tag = go.tag,
                Layer = LayerMask.LayerToName(go.layer),
                Components = components
                    .Select(c => c != null ? c.GetType().Name : "(missing script)")
                    .ToArray(),
                Children = children
            };
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"GameObject: {Name}");
            sb.AppendLine($"  InstanceID: {InstanceID}");
            sb.AppendLine($"  ActiveSelf: {ActiveSelf}");
            sb.AppendLine($"  ActiveInHierarchy: {ActiveInHierarchy}");
            sb.AppendLine($"  Tag: {Tag}");
            sb.AppendLine($"  Layer: {Layer}");
            sb.AppendLine($"  Components ({Components.Length}):");
            for (int i = 0; i < Components.Length; i++)
                sb.AppendLine($"    [{i}] {Components[i]}");
            if (Children.Length > 0)
            {
                sb.AppendLine($"  Children ({Children.Length}):");
                foreach (var child in Children)
                    sb.AppendLine($"    - {child}");
            }

            return sb.ToString();
        }
    }

    /// <summary>コンポーネントの詳細情報（プロパティ一覧付き）</summary>
    private sealed class ComponentInfo
    {
        public string GameObjectName;
        public string ComponentType;
        public List<PropertySummary> Properties;

        public static ComponentInfo From(UnityEngine.Component component)
        {
            return new ComponentInfo
            {
                GameObjectName = component.gameObject.name,
                ComponentType = component.GetType().Name,
                Properties = PropertyAccessor.ReadAll(component)
            };
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Component: {ComponentType} (on '{GameObjectName}')");
            sb.AppendLine($"  Properties ({Properties.Count}):");
            foreach (var prop in Properties)
            {
                sb.AppendLine($"    {prop.Path} ({prop.Type}) = {prop.Value}");
            }

            return sb.ToString();
        }
    }

    /// <summary>SerializedPropertyの要約情報</summary>
    private sealed class PropertySummary
    {
        public string Path;
        public string Type;
        public string Value;

        public static PropertySummary From(SerializedProperty property)
        {
            return new PropertySummary
            {
                Path = property.propertyPath,
                Type = property.propertyType.ToString(),
                Value = ReadValue(property)
            };
        }

        private static string ReadValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return prop.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString("G");
                case SerializedPropertyType.String:
                    return $"\"{prop.stringValue}\"";
                case SerializedPropertyType.Enum:
                    return prop.enumDisplayNames != null && prop.enumValueIndex >= 0 &&
                           prop.enumValueIndex < prop.enumDisplayNames.Length
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : prop.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null
                        ? $"{prop.objectReferenceValue.name} ({prop.objectReferenceValue.GetType().Name})"
                        : "None";
                case SerializedPropertyType.Color:
                    return prop.colorValue.ToString();
                case SerializedPropertyType.Vector2:
                    return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return prop.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return prop.vector4Value.ToString();
                case SerializedPropertyType.Rect:
                    return prop.rectValue.ToString();
                case SerializedPropertyType.Bounds:
                    return prop.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return prop.quaternionValue.eulerAngles.ToString();
                case SerializedPropertyType.Vector2Int:
                    return prop.vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int:
                    return prop.vector3IntValue.ToString();
                case SerializedPropertyType.RectInt:
                    return prop.rectIntValue.ToString();
                case SerializedPropertyType.BoundsInt:
                    return prop.boundsIntValue.ToString();
                case SerializedPropertyType.LayerMask:
                    return prop.intValue.ToString();
                case SerializedPropertyType.ArraySize:
                    return prop.intValue.ToString();
                case SerializedPropertyType.AnimationCurve:
                    return $"AnimationCurve ({prop.animationCurveValue?.length ?? 0} keys)";
                default:
                    return $"({prop.propertyType})";
            }
        }
    }
}
