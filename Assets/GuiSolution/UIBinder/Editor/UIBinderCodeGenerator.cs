#if UNITY_EDITOR
namespace GuiSolution.UIBinder.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;

    public static class UIBinderCodeGenerator
    {
        private const string OutputDir = "Assets/Generated/UIBinder";
        private const string MenuPath = "Tools/UIBinder/Generate Binding Code";

        [MenuItem(MenuPath)]
        public static void GenerateAll()
        {
            if (!EditorUtility.DisplayDialog( "UIBinder Generate",
                "生成绑定代码前会自动把需要生成的 UI 脚本改成 partial class，是否继续？",
                "继续",
                "取消"))
            {
                return;
            }

            if (!Directory.Exists(OutputDir)) Directory.CreateDirectory(OutputDir);

            foreach (string oldFile in Directory.GetFiles(OutputDir, "*.UIBind.g.cs", SearchOption.TopDirectoryOnly))
            {
                File.Delete(oldFile);
            }

            int count = 0;
            foreach (Type type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (!CanGenerate(type)) continue;

                BindClassInfo info = Collect(type);
                if (!info.HasAnyBinding) continue;

                if (!EnsureSourceClassIsPartial(type))
                {
                    Debug.LogError($"<color=lime>[UIBinderCodeGenerator]</color> {type.FullName} 自动添加 partial 失败，跳过生成。");
                    continue;
                }

                string code = GenerateCode(info);
                string path = Path.Combine(OutputDir, type.FullName.Replace('.', '_').Replace('+', '_') + ".UIBind.g.cs");
                File.WriteAllText(path, code, new UTF8Encoding(false));
                count++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"<color=lime>[UIBinderCodeGenerator]</color> Generate finished. Count: {count}. Output: {OutputDir}");
        }

        private static bool EnsureSourceClassIsPartial(Type type)
        {
            if (type == null)
            {
                return false;
            }

            string scriptPath = GetMonoScriptPath(type);
            if (string.IsNullOrEmpty(scriptPath))
            {
                Debug.LogError($"<color=lime>[UIBinderCodeGenerator]</color> 找不到 {type.FullName} 对应的 MonoScript。");
                return false;
            }

            if (!scriptPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                Debug.LogError($"<color=lime>[UIBinderCodeGenerator]</color> {type.FullName} 脚本不在 Assets 下，不能自动修改：{scriptPath}");
                return false;
            }

            string fullPath = Path.GetFullPath(scriptPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"<color=lime>[UIBinderCodeGenerator]</color> 源文件不存在：{scriptPath}");
                return false;
            }

            string code = File.ReadAllText(fullPath, Encoding.UTF8);

            if (IsClassAlreadyPartial(code, type.Name))
            {
                return true;
            }

            string patched = AddPartialToClassDeclaration(code, type.Name, out bool changed);

            if (!changed)
            {
                Debug.LogError($"<color=lime>[UIBinderCodeGenerator]</color> 无法在 {scriptPath} 中定位 class {type.Name}，请手动改为 partial class。");
                return false;
            }

            File.WriteAllText(fullPath, patched, new UTF8Encoding(false));
            Debug.Log($"<color=lime>[UIBinderCodeGenerator]</color> 已自动添加 partial：{type.FullName} -> {scriptPath}");

            return true;
        }

        private static string GetMonoScriptPath(Type type)
        {
            MonoScript[] scripts = MonoImporter.GetAllRuntimeMonoScripts();

            for (int i = 0; i < scripts.Length; i++)
            {
                MonoScript script = scripts[i];
                if (script == null)
                {
                    continue;
                }

                Type scriptType = script.GetClass();
                if (scriptType != type)
                {
                    continue;
                }

                return AssetDatabase.GetAssetPath(script);
            }

            return null;
        }

        private static bool IsClassAlreadyPartial(string code, string typeName)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            string pattern = $@"\bpartial\s+class\s+{Regex.Escape(typeName)}\b";
            return Regex.IsMatch(code, pattern);
        }

        // 自动插入partial
        private static string AddPartialToClassDeclaration(string code, string typeName, out bool changed)
        {
            changed = false;

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(typeName))
            {
                return code;
            }

            string pattern = $@"(?m)^(?<indent>\s*)" +
                $@"(?<modifiers>(?:(?:public|internal|protected|private|abstract|sealed|static|unsafe|new|partial)\s+)*)" +
                $@"class\s+{Regex.Escape(typeName)}\b";

            Match match = Regex.Match(code, pattern);
            if (!match.Success)
            {
                return code;
            }

            string modifiers = match.Groups["modifiers"].Value;
            if (modifiers.Contains("partial "))
            {
                return code;
            }

            string replacement = match.Groups["indent"].Value + modifiers + "partial class " + typeName;

            changed = true;

            return code.Substring(0, match.Index) + replacement + code.Substring(match.Index + match.Length);
        }

        private static bool CanGenerate(Type type)
        {
            if (type == null) return false;
            if (type.IsAbstract) return false;
            if (type.IsGenericTypeDefinition) return false;
            if (type.IsNested) return false; // 简化处理：嵌套 MonoBehaviour 不生成。
            if (type.Assembly.FullName.StartsWith("Unity")) return false;
            return true;
        }

        private static BindClassInfo Collect(Type type)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            var info = new BindClassInfo { Type = type };

            foreach (FieldInfo field in type.GetFields(Flags))
            {
                var attr = field.GetCustomAttribute<AutoBindAttribute>(false);
                if (attr == null) continue;
                // [AutoBind] 允许空路径：空路径默认使用字段名自动寻址。
                string bindPath = string.IsNullOrEmpty(attr.TransformPath) ? field.Name : attr.TransformPath;

                if (!typeof(Component).IsAssignableFrom(field.FieldType))
                {
                    Debug.LogError($"<color=lime>[UIBinderCodeGenerator]</color> {type.FullName}.{field.Name} 使用了 [AutoBind]，但字段类型不是 Component 跳过。 ");
                    continue;
                }

                info.Fields.Add(new FieldBindInfo {
                    FieldName = field.Name,
                    FieldType = field.FieldType,
                    Path = bindPath
                });
            }

            foreach (MethodInfo method in type.GetMethods(Flags))
            {
                AddMethodIfValid<OnClickAttribute>(info, method, typeof(void), Type.EmptyTypes, a => a.ButtonName, EventKind.Button);
                AddMethodIfValid<OnToggleChangedAttribute>(info, method, typeof(void), new[] { typeof(bool) }, a => a.ToggleName, EventKind.Toggle);
                AddMethodIfValid<OnSliderChangedAttribute>(info, method, typeof(void), new[] { typeof(float) }, a => a.SliderName, EventKind.Slider);
                AddMethodIfValid<OnScrollbarChangedAttribute>(info, method, typeof(void), new[] { typeof(float) }, a => a.ScrollbarName, EventKind.Scrollbar);
                AddMethodIfValid<OnInputChangedAttribute>(info, method, typeof(void), new[] { typeof(string) }, a => a.InputName, EventKind.Input, a => a.IsEndEdit);
                AddMethodIfValid<OnDropdownChangedAttribute>(info, method, typeof(void), new[] { typeof(int) }, a => a.DropdownName, EventKind.Dropdown);
                AddMethodIfValid<OnScrollRectChangedAttribute>(info, method, typeof(void), new[] { typeof(Vector2) }, a => a.ScrollRectName, EventKind.ScrollRect);
            }

            return info;
        }

        private static void AddMethodIfValid<TAttr>(
            BindClassInfo info,
            MethodInfo method,
            Type returnType,
            Type[] parameterTypes,
            Func<TAttr, string> pathGetter,
            EventKind kind,
            Func<TAttr, bool> inputEndEditGetter = null)
            where TAttr : Attribute
        {
            var attr = method.GetCustomAttribute<TAttr>(false);
            if (attr == null) return;

            if (method.ReturnType != returnType)
            {
                Debug.LogError($"<color=lime>[UIBinderCodeGenerator]</color> {info.Type.FullName}.{method.Name} 返回值必须是 void。跳过。");
                return;
            }

            ParameterInfo[] ps = method.GetParameters();
            if (ps.Length != parameterTypes.Length)
            {
                Debug.LogError($"<color=lime>[UIBinderCodeGenerator]</color> {info.Type.FullName}.{method.Name} 参数数量错误。跳过。");
                return;
            }

            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].ParameterType != parameterTypes[i])
                {
                    Debug.LogError($"<color=lime>[UIBinderCodeGenerator]</color> {info.Type.FullName}.{method.Name} 第 {i + 1} 个参数必须是 {parameterTypes[i].Name}。跳过。");
                    return;
                }
            }

            string path = pathGetter(attr);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"<color=lime>[UIBinderCodeGenerator]</color> {info.Type.FullName}.{method.Name} 绑定路径/名字为空。跳过。");
                return;
            }

            info.Events.Add(new EventBindInfo {
                Kind = kind,
                MethodName = method.Name,
                Path = path,
                IsEndEdit = inputEndEditGetter?.Invoke(attr) ?? true
            });
        }

        private static string GenerateCode(BindClassInfo info)
        {
            var sb = new StringBuilder(4096);
            string access = info.Type.IsPublic || info.Type.IsNestedPublic ? "public" : "internal";
            string typeName = info.Type.Name;

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("// This file is generated by GuiSolution.UIBinder.Editor.UIBinderCodeGenerator. Do not modify manually.");
            sb.AppendLine("#pragma warning disable 0649");
            sb.AppendLine("#pragma warning disable 0108");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UI;");
            sb.AppendLine("using TMPro;");
            sb.AppendLine("using GuiSolution.UIBinder;");
            sb.AppendLine("using System;");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(info.Type.Namespace))
            {
                sb.AppendLine($"namespace {info.Type.Namespace}");
                sb.AppendLine("{");
            }

            sb.AppendLine($"    {access} partial class {typeName} : GuiSolution.UIBinder.IUIBindGenerated");
            sb.AppendLine("    {");

            for (int i = 0; i < info.Events.Count; i++)
            {
                EventBindInfo e = info.Events[i];
                foreach (string field in GetEventCacheFields(e, i))
                {
                    sb.AppendLine("        " + field);
                }
            }

            sb.AppendLine();
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("        private void Reset()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!TryGetComponent<GuiSolution.UIBinder.UIBinderEnv>(out _))");
            sb.AppendLine("            {");
            sb.AppendLine("                gameObject.AddComponent<GuiSolution.UIBinder.UIBinderEnv>();");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("#endif");

            sb.AppendLine();
            sb.AppendLine("        public void UIBindGenerated()");
            sb.AppendLine("        {");
            sb.AppendLine("            UIUnbindGenerated();");

            foreach (FieldBindInfo f in info.Fields)
            {
                sb.AppendLine($"            this.{f.FieldName} = GuiSolution.UIBinder.UIBindGeneratedUtil.GetComponentByPathOrName<{GetTypeName(f.FieldType)}>(this, \"{Escape(f.Path)}\", \"{Escape(f.FieldName)}\");");
            }

            for (int i = 0; i < info.Events.Count; i++)
            {
                AppendBindEvent(sb, info.Events[i], i);
            }

            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void UIUnbindGenerated()");
            sb.AppendLine("        {");

            for (int i = 0; i < info.Events.Count; i++)
            {
                AppendUnbindEvent(sb, info.Events[i], i);
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");

            if (!string.IsNullOrEmpty(info.Type.Namespace))
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private static IEnumerable<string> GetEventCacheFields(EventBindInfo e, int index)
        {
            string n = $"__uiBind_{index}";

            yield return $"private UnityEngine.Component {n}_AdapterComponent;";
            yield return $"private GuiSolution.UIBinder.IUIBindEventAdapter {n}_Adapter;";
            yield return $"private System.Delegate {n}_Delegate;";
            yield return $"private object {n}_Option;"; 
        }

        private static void AppendBindEvent(StringBuilder sb, EventBindInfo e, int index)
        {
            string n = $"__uiBind_{index}";
            string path = Escape(e.Path);
            string method = Escape(e.MethodName);
            string delegateType = GetDelegateTypeName(e);
            string expected = Escape(GetExpectedComponentDescription(e));
            string optionCode = GetBindOptionCode(e);

            sb.AppendLine($"            if ({n}_Delegate == null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                {n}_Delegate = new {delegateType}({method});");
            sb.AppendLine("            }");

            if (e.Kind == EventKind.Input)
            {
                sb.AppendLine($"            if ({n}_Option == null)");
                sb.AppendLine("            {");
                sb.AppendLine($"                {n}_Option = {optionCode};");
                sb.AppendLine("            }");
            } else
            {
                sb.AppendLine($"            {n}_Option = null;");
            }

            sb.AppendLine($"            var {n}_Node = GuiSolution.UIBinder.UIBindGeneratedUtil.FindByPathOrName(this.transform, \"{path}\");");
            sb.AppendLine($"            if ({n}_Node != null && GuiSolution.UIBinder.UIBindEventAdapterRegistry.TryBind(");
            sb.AppendLine($"                    {n}_Node,");
            sb.AppendLine($"                    ({delegateType}){n}_Delegate,");
            sb.AppendLine($"                    out {n}_AdapterComponent,");
            sb.AppendLine($"                    out {n}_Adapter,");
            sb.AppendLine($"                    {n}_Option))");
            sb.AppendLine("            {");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine($"                GuiSolution.UIBinder.UIBindGeneratedUtil.LogMissingComponent(this, \"{path}\", \"{method}\", \"{expected}\");");
            sb.AppendLine("            }");
        }

        private static void AppendUnbindEvent(StringBuilder sb, EventBindInfo e, int index)
        {
            string n = $"__uiBind_{index}";

            sb.AppendLine($"            if ({n}_AdapterComponent != null && {n}_Adapter != null && {n}_Delegate != null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                {n}_Adapter.RemoveListener({n}_AdapterComponent, {n}_Delegate, {n}_Option);");
            sb.AppendLine("            }");
            sb.AppendLine($"            {n}_AdapterComponent = null;");
            sb.AppendLine($"            {n}_Adapter = null;");
        }

        private static string GetDelegateTypeName(EventBindInfo e)
        {
            switch (e.Kind)
            {
                case EventKind.Button:
                    return "System.Action";
                case EventKind.Toggle:
                    return "System.Action<bool>";
                case EventKind.Slider:
                case EventKind.Scrollbar:
                    return "System.Action<float>";
                case EventKind.Input:
                    return "System.Action<string>";
                case EventKind.Dropdown:
                    return "System.Action<int>";
                case EventKind.ScrollRect:
                    return "System.Action<Vector2>";
                default:
                    throw new ArgumentOutOfRangeException(nameof(e.Kind), e.Kind, null);
            }
        }

        private static string GetBindOptionCode(EventBindInfo e)
        {
            if (e.Kind != EventKind.Input)
            {
                return "null";
            }

            return e.IsEndEdit
                ? "GuiSolution.UIBinder.InputBindMode.EndEdit"
                : "GuiSolution.UIBinder.InputBindMode.ValueChanged";
        }

        private static string GetExpectedComponentDescription(EventBindInfo e)
        {
            switch (e.Kind)
            {
                case EventKind.Button:
                    return "Button or registered Action adapter";
                case EventKind.Toggle:
                    return "Toggle or registered Action<bool> adapter";
                case EventKind.Slider:
                    return "Slider or registered Action<float> adapter";
                case EventKind.Scrollbar:
                    return "Scrollbar or registered Action<float> adapter";
                case EventKind.Input:
                    return "InputField or TMP_InputField or registered Action<string> adapter";
                case EventKind.Dropdown:
                    return "Dropdown or TMP_Dropdown or registered Action<int> adapter";
                case EventKind.ScrollRect:
                    return "ScrollRect or registered Action<Vector2> adapter";
                default:
                    throw new ArgumentOutOfRangeException(nameof(e.Kind), e.Kind, null);
            }
        }

        private static string GetTypeName(Type type)
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            if (type == typeof(void)) return "void";

            if (!type.IsGenericType)
            {
                return "global::" + type.FullName.Replace('+', '.');
            }

            string typeName = type.GetGenericTypeDefinition().FullName;
            typeName = typeName.Substring(0, typeName.IndexOf('`')).Replace('+', '.');
            Type[] args = type.GetGenericArguments();
            var argNames = new string[args.Length];
            for (int i = 0; i < args.Length; i++) argNames[i] = GetTypeName(args[i]);
            return "global::" + typeName + "<" + string.Join(", ", argNames) + ">";
        }

        private static string Escape(string value)
        {
            return value == null ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class BindClassInfo
        {
            public Type Type;
            public readonly List<FieldBindInfo> Fields = new List<FieldBindInfo>();
            public readonly List<EventBindInfo> Events = new List<EventBindInfo>();
            public bool HasAnyBinding => Fields.Count > 0 || Events.Count > 0;
        }

        private sealed class FieldBindInfo
        {
            public string FieldName;
            public Type FieldType;
            public string Path;
        }

        private sealed class EventBindInfo
        {
            public EventKind Kind;
            public string MethodName;
            public string Path;
            public bool IsEndEdit;
        }

        private enum EventKind
        {
            Button,
            Toggle,
            Slider,
            Scrollbar,
            Input,
            Dropdown,
            ScrollRect
        }
    }
}
#endif
