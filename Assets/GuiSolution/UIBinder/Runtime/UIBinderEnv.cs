namespace GuiSolution.UIBinder
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
#endif

#if UNITY_EDITOR
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class BindingListLabelAttribute : PropertyAttribute
    {
        public string TargetFieldName { get; }

        public BindingListLabelAttribute(string targetFieldName)
        {
            TargetFieldName = targetFieldName;
        }
    }
#endif

    [DefaultExecutionOrder(-5000)]
    [DisallowMultipleComponent]
    public class UIBinderEnv : MonoBehaviour
    {
        [Tooltip("可手动指定绑定目标。不填则自动查找同 GameObject 上的 UI 脚本。")]
        [SerializeField] private MonoBehaviour targetOwner;

#if UNITY_EDITOR
        [Header("运行时绑定快照")]
        [BindingListLabel("gameObjectName")]
        [SerializeField] private List<UIBinderAuto.BindingInfo> activeBindings = new List<UIBinderAuto.BindingInfo>();
#endif

        private UIBinderAuto _cachedBinder;
        private object _cachedOwner;
        private bool _isInitialized;

        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private void Awake()
        {
            if (_isInitialized) return;

            AutoBindOwner();
        }

        private void AutoBindOwner()
        {
            if (_isInitialized) return;

            MonoBehaviour owner = FindOwner();
            if (owner != null) Init(owner);
            else Debug.LogWarning($"<color=lime>[UIBinderEnv]</color> {gameObject.name} no bindable UI script found.", this);
        }

        private MonoBehaviour FindOwner()
        {
            if (targetOwner != null && targetOwner != this) return targetOwner;

            MonoBehaviour[] monos = GetComponents<MonoBehaviour>();

            foreach (MonoBehaviour mono in monos)
            {
                if (mono == null || mono == this) continue;

                if (mono is IUIBindGenerated)
                {
                    targetOwner = mono;
                    return mono;
                }
            }

            foreach (MonoBehaviour mono in monos)
            {
                if (mono == null || mono == this) continue;

                if (HasUIBinderAttribute(mono.GetType()))
                {
                    targetOwner = mono;
                    return mono;
                }
            }

            return null;
        }

        private static bool HasUIBinderAttribute(Type type)
        {
            FieldInfo[] fields = type.GetFields(Flags);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].GetCustomAttribute<AutoBindAttribute>() != null)
                {
                    return true;
                }
            }

            MethodInfo[] methods = type.GetMethods(Flags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];

                if (method.GetCustomAttribute<OnClickAttribute>() != null) return true;
                if (method.GetCustomAttribute<OnToggleChangedAttribute>() != null) return true;
                if (method.GetCustomAttribute<OnSliderChangedAttribute>() != null) return true;
                if (method.GetCustomAttribute<OnScrollbarChangedAttribute>() != null) return true;
                if (method.GetCustomAttribute<OnInputChangedAttribute>() != null) return true;
                if (method.GetCustomAttribute<OnDropdownChangedAttribute>() != null) return true;
                if (method.GetCustomAttribute<OnScrollRectChangedAttribute>() != null) return true;
            }

            return false;
        }

        [ContextMenu("Refresh Bindings (Runtime)")]
        public void RefreshBindings()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("<color=lime>[UIBinderEnv]</color> runtime refresh only！");
                return;
            }

            if (_cachedOwner == null)
            {
                MonoBehaviour owner = FindOwner();

                if (owner == null)
                {
                    Debug.LogError($"<color=lime>[UIBinderEnv]</color> {gameObject.name} refresh failed: no bindable owner found.", this);
                    return;
                }

                Init(owner);
                return;
            }

            if (_cachedOwner is IUIBindGenerated generated)
            {
                generated.UIUnbindGenerated();
                ClearOwnerRootCache();
                generated.UIBindGenerated();

                _isInitialized = true;
                SyncGeneratedSnapshot(_cachedOwner);

                Debug.Log($"<color=lime>[UIBinderEnv]</color> {gameObject.name} rebind by generated code.", this);
                return;
            }

            if (_cachedOwner is MonoBehaviour monoOwner)
            {
                if (_cachedBinder == null)
                {
                    _cachedBinder = new UIBinderAuto(monoOwner.transform);
                }

                _cachedBinder.ClearAndUnbindAll();
                _cachedBinder.AutoInject(_cachedOwner);

                _isInitialized = true;
                SyncBindSnapshot();

                Debug.Log($"<color=lime>[UIBinderEnv]</color> {gameObject.name} rebind by reflection fallback.", this);
            }
        }

        public void Init(object owner)
        {
            if (owner == null) return;

            if (_isInitialized && ReferenceEquals(_cachedOwner, owner))
            {
                return;
            }

            if (_isInitialized)
            {
                Unbind(false);
            }

            _cachedOwner = owner;

            if (_cachedOwner is IUIBindGenerated generated)
            {
                generated.UIBindGenerated();

                _isInitialized = true;
                SyncGeneratedSnapshot(owner);
                return;
            }

            if (_cachedOwner is MonoBehaviour monoOwner)
            {
                if (_cachedBinder == null)
                {
                    _cachedBinder = new UIBinderAuto(monoOwner.transform);
                }

                _cachedBinder.AutoInject(_cachedOwner);

                _isInitialized = true;
                SyncBindSnapshot();
            }
        }

        public void Init(UIBinderAuto binder, object owner)
        {
            _cachedBinder = binder;
            Init(owner);
        }

        private void Unbind(bool clearCache)
        {
            if (!_isInitialized) return;

            if (_cachedOwner is IUIBindGenerated generated)
            {
                generated.UIUnbindGenerated();

                if (clearCache)
                {
                    ClearOwnerRootCache();
                }
            } else
            {
                _cachedBinder?.ClearAndUnbindAll(clearCache);
            }

            _isInitialized = false;

            ClearActiveBindingsEditor();
        }
         
        public void Unbind()
        {
            Unbind(false);
        }

        private void ClearOwnerRootCache()
        {
            if (_cachedOwner is MonoBehaviour monoOwner)
            {
                UIBindGeneratedUtil.ClearCache(monoOwner);
            }
        }

        private void SyncGeneratedSnapshot(object owner)
        {
#if UNITY_EDITOR
            activeBindings.Clear();

            activeBindings.Add(new UIBinderAuto.BindingInfo {
                bindComponentType = UIBinderAuto.BindComponetType.None,
                gameObjectName = "[GeneratedCode] " + owner.GetType().Name,
                callbackMethodName = "UIBindGenerated",
                componentInstance = this
            });

            EditorUtility.SetDirty(this);
#endif
        }

        public void SyncBindSnapshot()
        {
#if UNITY_EDITOR
            activeBindings.Clear();

            if (_cachedBinder == null || _cachedBinder._bindingsInfo == null)
            {
                return;
            }

            foreach (UIBinderAuto.BindingInfo info in _cachedBinder._bindingsInfo)
            {
                activeBindings.Add(info);
            }

            EditorUtility.SetDirty(this);
#endif
        }

        private void ClearActiveBindingsEditor()
        {
#if UNITY_EDITOR
            activeBindings.Clear();
#endif
        }

        private void OnDestroy()
        {
            Unbind(true);

            _cachedBinder = null;
            _cachedOwner = null;
            ClearActiveBindingsEditor();
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(BindingListLabelAttribute))]
    public class BindingListLabelDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (IsGeneratedCodeItem(property))
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (IsGeneratedCodeItem(property))
            {
                DrawGeneratedCodeItem(position, property);
                return;
            }

            BindingListLabelAttribute targetAttr = (BindingListLabelAttribute)attribute;
            SerializedProperty targetField = property.FindPropertyRelative(targetAttr.TargetFieldName);

            if (targetField != null)
            {
                string goName = targetField.stringValue;

                SerializedProperty typeField = property.FindPropertyRelative("bindComponentType");
                SerializedProperty methodField = property.FindPropertyRelative("callbackMethodName");

                string typeStr = "[Unknown]";

                if (typeField != null)
                {
                    string enumName = typeField.enumDisplayNames[typeField.enumValueIndex];
                    typeStr = $"[{enumName}]";
                }

                string methodStr = methodField != null
                    ? $" -> {methodField.stringValue}()"
                    : "";

                label.text = string.IsNullOrEmpty(goName)
                    ? "Empty Node"
                    : $"{typeStr}  {goName}{methodStr}";
            }

            EditorGUI.PropertyField(position, property, label, true);
        }

        private static bool IsGeneratedCodeItem(SerializedProperty property)
        {
            SerializedProperty typeField = property.FindPropertyRelative("bindComponentType");
            SerializedProperty nameField = property.FindPropertyRelative("gameObjectName");

            if (typeField == null || nameField == null)
            {
                return false;
            }

            string enumName = typeField.enumDisplayNames[typeField.enumValueIndex];
            string goName = nameField.stringValue;

            return enumName == "None" && goName.StartsWith("[GeneratedCode]");
        }

        private static void DrawGeneratedCodeItem(Rect position, SerializedProperty property)
        {
            SerializedProperty nameField = property.FindPropertyRelative("gameObjectName");
            SerializedProperty methodField = property.FindPropertyRelative("callbackMethodName");

            string goName = nameField != null ? nameField.stringValue : "[GeneratedCode]";
            string methodName = methodField != null ? methodField.stringValue : "UIBindGenerated";

            string text = $"{goName} -> {methodName}()";

            EditorGUI.LabelField(position, text, EditorStyles.boldLabel);
        }
    }
#endif
}
