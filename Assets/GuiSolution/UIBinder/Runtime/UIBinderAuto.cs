namespace GuiSolution.UIBinder
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using TMPro;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.UI;

    public class UIBinderAuto
    {
        [Serializable]
        public enum BindComponetType
        {
            None,

            // 字段绑定
            Component,
            Graphic,
            MaskableGraphic,
            Text,
            TMP_Text,
            TextMeshProUGUI,
            Image,
            RawImage,
            RectTransform,
            CanvasGroup,

            // 事件绑定
            Button,
            Toggle,
            Slider,
            Scrollbar,
            InputField,
            TMP_InputField,
            Dropdown,
            TMP_Dropdown,
            ScrollRect,

            // 外部重写的UGUI
            Adapter
        }

        [Serializable]
        public struct BindingInfo
        {
            public BindComponetType bindComponentType;

            public string displayName;
            public string gameObjectName;
            public string componentTypeName;
            public string callbackMethodName;

            public Component componentInstance;
        }

        private readonly List<(Button btn, UnityAction act)> _buttonBindings = new List<(Button, UnityAction)>();
        private readonly List<(Toggle tog, UnityAction<bool> act)> _toggleBindings = new List<(Toggle, UnityAction<bool>)>();
        private readonly List<(Slider sld, UnityAction<float> act)> _sliderBindings = new List<(Slider, UnityAction<float>)>();
        private readonly List<(Scrollbar sbr, UnityAction<float> act)> _scrollbarBindings = new List<(Scrollbar, UnityAction<float>)>();
        private readonly List<(InputField field, UnityAction<string> act, bool isEndEdit)> _inputBindings = new List<(InputField, UnityAction<string>, bool)>();
        private readonly List<(TMP_InputField field, UnityAction<string> act, bool isEndEdit)> _tmpInputBindings = new List<(TMP_InputField, UnityAction<string>, bool)>();
        private readonly List<(Dropdown dp, UnityAction<int> act)> _dropdownBindings = new List<(Dropdown, UnityAction<int>)>();
        private readonly List<(TMP_Dropdown dp, UnityAction<int> act)> _tmpDropdownBindings = new List<(TMP_Dropdown, UnityAction<int>)>();
        private readonly List<(ScrollRect srt, UnityAction<Vector2> act)> _scrollRectBindings = new List<(ScrollRect, UnityAction<Vector2>)>();
        private readonly List<(Component component, IUIBindEventAdapter adapter, Delegate callback, object option)> _adapterBindings = new List<(Component, IUIBindEventAdapter, Delegate, object)>();

        private readonly Transform _rootTransform;

#if UNITY_EDITOR
        public List<BindingInfo> _bindingsInfo { get; } = new List<BindingInfo>();
#endif

        // 反射缓存
        private static readonly Dictionary<Type, TypeBindMeta> TypeMetaCache = new Dictionary<Type, TypeBindMeta>(64);

        private sealed class TypeBindMeta
        {
            public readonly List<FieldBindMeta> Fields = new List<FieldBindMeta>();
            public readonly List<MethodBindMeta> Methods = new List<MethodBindMeta>();
        }

        private sealed class FieldBindMeta
        {
            public FieldInfo Field;
            public AutoBindAttribute Attribute;
        }

        private sealed class MethodBindMeta
        {
            public MethodInfo Method;
            public Attribute Attribute;
        }

        public UIBinderAuto(Transform rootTransform)
        {
            _rootTransform = rootTransform;
        }

        #region ------------------ Auto Bind UGUI Components ------------------

        private void Bind(Button button, Action callback)
        {
            if (button == null || callback == null) return;

            UnityAction unityCallback = callback.Invoke;

            button.onClick.RemoveListener(unityCallback);
            button.onClick.AddListener(unityCallback);

            _buttonBindings.Add((button, unityCallback));

            AddBindingsInfo(BindComponetType.Button, button.gameObject.name, callback.Method.Name, button);
        }

        private void Bind(Toggle toggle, Action<bool> callback)
        {
            if (toggle == null || callback == null) return;

            UnityAction<bool> unityCallback = callback.Invoke;

            toggle.onValueChanged.RemoveListener(unityCallback);
            toggle.onValueChanged.AddListener(unityCallback);

            _toggleBindings.Add((toggle, unityCallback));

            AddBindingsInfo(BindComponetType.Toggle, toggle.gameObject.name, callback.Method.Name, toggle);
        }

        private void Bind(Slider slider, Action<float> callback)
        {
            if (slider == null || callback == null) return;

            UnityAction<float> unityCallback = callback.Invoke;

            slider.onValueChanged.RemoveListener(unityCallback);
            slider.onValueChanged.AddListener(unityCallback);

            _sliderBindings.Add((slider, unityCallback));

            AddBindingsInfo(BindComponetType.Slider, slider.gameObject.name, callback.Method.Name, slider);
        }

        private void Bind(Scrollbar scrollbar, Action<float> callback)
        {
            if (scrollbar == null || callback == null) return;

            UnityAction<float> unityCallback = callback.Invoke;

            scrollbar.onValueChanged.RemoveListener(unityCallback);
            scrollbar.onValueChanged.AddListener(unityCallback);

            _scrollbarBindings.Add((scrollbar, unityCallback));

            AddBindingsInfo(BindComponetType.Scrollbar, scrollbar.gameObject.name, callback.Method.Name, scrollbar);
        }

        private void Bind(InputField inputField, Action<string> callback, bool isEndEdit = true)
        {
            if (inputField == null || callback == null) return;

            UnityAction<string> unityCallback = callback.Invoke;

            if (isEndEdit)
            {
                inputField.onEndEdit.RemoveListener(unityCallback);
                inputField.onEndEdit.AddListener(unityCallback);
            } else
            {
                inputField.onValueChanged.RemoveListener(unityCallback);
                inputField.onValueChanged.AddListener(unityCallback);
            }

            _inputBindings.Add((inputField, unityCallback, isEndEdit));

            AddBindingsInfo(BindComponetType.InputField, inputField.gameObject.name, callback.Method.Name, inputField);
        }

        private void Bind(TMP_InputField inputField, Action<string> callback, bool isEndEdit = true)
        {
            if (inputField == null || callback == null) return;

            UnityAction<string> unityCallback = callback.Invoke;

            if (isEndEdit)
            {
                inputField.onEndEdit.RemoveListener(unityCallback);
                inputField.onEndEdit.AddListener(unityCallback);
            } else
            {
                inputField.onValueChanged.RemoveListener(unityCallback);
                inputField.onValueChanged.AddListener(unityCallback);
            }

            _tmpInputBindings.Add((inputField, unityCallback, isEndEdit));

            AddBindingsInfo(BindComponetType.TMP_InputField, inputField.gameObject.name, callback.Method.Name, inputField);
        }

        private void Bind(Dropdown dropdown, Action<int> callback)
        {
            if (dropdown == null || callback == null) return;

            UnityAction<int> unityCallback = callback.Invoke;

            dropdown.onValueChanged.RemoveListener(unityCallback);
            dropdown.onValueChanged.AddListener(unityCallback);

            _dropdownBindings.Add((dropdown, unityCallback));

            AddBindingsInfo(BindComponetType.Dropdown, dropdown.gameObject.name, callback.Method.Name, dropdown);
        }

        private void Bind(TMP_Dropdown dropdown, Action<int> callback)
        {
            if (dropdown == null || callback == null) return;

            UnityAction<int> unityCallback = callback.Invoke;

            dropdown.onValueChanged.RemoveListener(unityCallback);
            dropdown.onValueChanged.AddListener(unityCallback);

            _tmpDropdownBindings.Add((dropdown, unityCallback));

            AddBindingsInfo(BindComponetType.TMP_Dropdown, dropdown.gameObject.name, callback.Method.Name, dropdown);
        }

        private void Bind(ScrollRect scrollRect, Action<Vector2> callback)
        {
            if (scrollRect == null || callback == null) return;

            UnityAction<Vector2> unityCallback = callback.Invoke;

            scrollRect.onValueChanged.RemoveListener(unityCallback);
            scrollRect.onValueChanged.AddListener(unityCallback);

            _scrollRectBindings.Add((scrollRect, unityCallback));

            AddBindingsInfo(BindComponetType.ScrollRect, scrollRect.gameObject.name, callback.Method.Name, scrollRect);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void AddBindingsInfo(BindComponetType type, string goName, string memberName, Component comp)
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(memberName) && memberName.Contains("<"))
            {
                memberName = "#<Lambda>";
            }

            string componentTypeName = comp != null ? comp.GetType().Name : "null";
            var displayName = IsFieldBinding(type) ? $"{goName}" : $"{goName} | {memberName}";

            _bindingsInfo.Add(new BindingInfo {
                bindComponentType = type,
                gameObjectName = goName,
                componentTypeName = componentTypeName,
                callbackMethodName = memberName,
                componentInstance = comp,
                displayName = displayName
            });
#endif
        }

        #endregion

        #region ------------------ Manual Bind ------------------

        public void Bind(string transformPath, Action callback)
        {
            if (callback == null) return;

            Transform targetNode = FindTargetNodeFallback(transformPath, "ManualBinding", callback.Method.Name);
            if (targetNode == null) return;

            if (UIBindEventAdapterRegistry.TryBind(targetNode, callback, out Component component, out IUIBindEventAdapter adapter))
            {
                _adapterBindings.Add((component, adapter, callback, null));
                return;
            }

            Debug.LogError($"<color=lime>[UIAutoBind]</color> GameObject '{targetNode.name}' Missing registered Action adapter!");
        }

        public void Bind(string transformPath, Action<bool> callback)
        {
            if (callback == null) return;

            Transform targetNode = FindTargetNodeFallback(transformPath, "ManualBinding", callback.Method.Name);
            if (targetNode == null) return;

            if (UIBindEventAdapterRegistry.TryBind(targetNode, callback, out Component component, out IUIBindEventAdapter adapter))
            {
                _adapterBindings.Add((component, adapter, callback, null));
                return;
            }

            Debug.LogError($"<color=lime>[UIAutoBind]</color> GameObject '{targetNode.name}' Missing registered Action<bool> adapter!");
        }

        public void Bind(string transformPath, Action<float> callback)
        {
            if (callback == null) return;

            Transform targetNode = FindTargetNodeFallback(transformPath, "ManualBinding", callback.Method.Name);
            if (targetNode == null) return;

            if (UIBindEventAdapterRegistry.TryBind(targetNode, callback, out Component component, out IUIBindEventAdapter adapter))
            {
                _adapterBindings.Add((component, adapter, callback, null));
                return;
            }

            Debug.LogError($"<color=lime>[UIAutoBind]</color> GameObject '{targetNode.name}' Missing registered Action<float> adapter!");
        }

        public void Bind(string transformPath, Action<int> callback)
        {
            if (callback == null) return;

            Transform targetNode = FindTargetNodeFallback(transformPath, "ManualBinding", callback.Method.Name);
            if (targetNode == null) return;

            if (UIBindEventAdapterRegistry.TryBind(targetNode, callback, out Component component, out IUIBindEventAdapter adapter))
            {
                _adapterBindings.Add((component, adapter, callback, null));
                return;
            }

            Debug.LogError($"<color=lime>[UIAutoBind]</color> GameObject '{targetNode.name}' Missing registered Action<int> adapter!");
        }

        public void Bind(string transformPath, Action<string> callback, bool isEndEdit = true)
        {
            if (callback == null) return;

            Transform targetNode = FindTargetNodeFallback(transformPath, "ManualBinding", callback.Method.Name);
            if (targetNode == null) return;

            InputBindMode mode = isEndEdit ? InputBindMode.EndEdit : InputBindMode.ValueChanged;
            if (UIBindEventAdapterRegistry.TryBind(targetNode, callback, out Component component, out IUIBindEventAdapter adapter, mode))
            {
                _adapterBindings.Add((component, adapter, callback, mode));
                return;
            }

            Debug.LogError(
                $"<color=lime>[UIAutoBind]</color> GameObject '{targetNode.name}' Missing registered Action<string> adapter!");
        }

        public void Bind(string transformPath, Action<Vector2> callback)
        {
            if (callback == null) return;

            Transform targetNode = FindTargetNodeFallback(transformPath, "ManualBinding", callback.Method.Name);
            if (targetNode == null) return;

            if (UIBindEventAdapterRegistry.TryBind(targetNode, callback, out Component component, out IUIBindEventAdapter adapter))
            {
                _adapterBindings.Add((component, adapter, callback, null));
                return;
            }

            Debug.LogError($"<color=lime>[UIAutoBind]</color> GameObject '{targetNode.name}' Missing registered Action<Vector2> adapter!");
        }

        #endregion

        #region ------------------ Auto Inject ------------------

        public void AutoInject(object targetOwner)
        {
            if (targetOwner == null) return;

            Type type = targetOwner.GetType();
            TypeBindMeta meta = GetOrCreateTypeMeta(type);

            AutoInjectFields(targetOwner, type, meta);
            AutoInjectMethods(targetOwner, type, meta);
        }

        private void AutoInjectFields(object targetOwner, Type ownerType, TypeBindMeta meta)
        {
            for (int i = 0; i < meta.Fields.Count; i++)
            {
                FieldBindMeta fieldMeta = meta.Fields[i];

                FieldInfo field = fieldMeta.Field;
                AutoBindAttribute autoBindAttr = fieldMeta.Attribute;
                string pathOrName = string.IsNullOrEmpty(autoBindAttr.TransformPath) ? field.Name : autoBindAttr.TransformPath;

                Transform targetNode = FindTargetNodeFallback(pathOrName, ownerType.Name, field.Name, field.FieldType);
                if (targetNode == null) continue;

                Component comp = targetNode.GetComponent(field.FieldType);

                UnityEngine.Assertions.Assert.IsNotNull(comp,
                    $"<color=lime>[UIAutoBind]</color> {ownerType.Name}.{field.Name}: Failed to bind component '{field.FieldType.Name}'" +
                    $" - GameObject '{targetNode.name}' (path: '{pathOrName}') does not have the required component."
                );

                if (comp == null) continue;

                field.SetValue(targetOwner, comp);

                BindComponetType bindType = GetBindComponentType(comp);
                if (IsFieldBinding(bindType))
                {
                    AddBindingsInfo(bindType, targetNode.gameObject.name, field.Name, comp);
                }
            }
        }

        public static bool IsFieldBinding(BindComponetType type)
        {
            switch (type)
            {
                // 这些没有事件绑定，字段绑定需要显示
                case BindComponetType.Graphic:
                case BindComponetType.MaskableGraphic:
                case BindComponetType.Text:
                case BindComponetType.TMP_Text:
                case BindComponetType.TextMeshProUGUI:
                case BindComponetType.Image:
                case BindComponetType.RawImage:
                case BindComponetType.RectTransform:
                case BindComponetType.CanvasGroup:
                case BindComponetType.Component:
                    return true;

                default:
                    return false;
            }
        }

        private static BindComponetType GetBindComponentType(Component comp) => comp switch {
            TextMeshProUGUI => BindComponetType.TextMeshProUGUI,
            TMP_Text => BindComponetType.TMP_Text,
            Text => BindComponetType.Text,
            Image => BindComponetType.Image,
            RawImage => BindComponetType.RawImage,
            MaskableGraphic => BindComponetType.MaskableGraphic,
            Graphic => BindComponetType.Graphic,
            RectTransform => BindComponetType.RectTransform,
            CanvasGroup => BindComponetType.CanvasGroup,

            Button => BindComponetType.Button,
            Toggle => BindComponetType.Toggle,
            Slider => BindComponetType.Slider,
            Scrollbar => BindComponetType.Scrollbar,
            TMP_InputField => BindComponetType.TMP_InputField,
            InputField => BindComponetType.InputField,
            TMP_Dropdown => BindComponetType.TMP_Dropdown,
            Dropdown => BindComponetType.Dropdown,
            ScrollRect => BindComponetType.ScrollRect,
            null => BindComponetType.None,
            _ => BindComponetType.Component
        };

        private void AutoInjectMethods(object targetOwner, Type ownerType, TypeBindMeta meta)
        {
            for (int i = 0; i < meta.Methods.Count; i++)
            {
                MethodBindMeta methodMeta = meta.Methods[i];

                MethodInfo method = methodMeta.Method;
                Attribute attr = methodMeta.Attribute;

                try
                {
                    switch (attr)
                    {
                        case OnClickAttribute clickAttr:
                            var btn = FindComponentInChildren<Button>(clickAttr.ButtonName, ownerType.Name, method.Name);
                            if (btn != null)
                            {
                                var del = (Action)Delegate.CreateDelegate(typeof(Action), targetOwner, method);
                                Bind(btn, del);
                            }
                            break;
                        case OnToggleChangedAttribute toggleAttr:
                            var tog = FindComponentInChildren<Toggle>(toggleAttr.ToggleName, ownerType.Name, method.Name);
                            if (tog != null)
                            {
                                var del = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), targetOwner, method);
                                Bind(tog, del);
                            }
                            break;
                        case OnSliderChangedAttribute sliderAttr:
                            var sld = FindComponentInChildren<Slider>(sliderAttr.SliderName, ownerType.Name, method.Name);
                            if (sld != null)
                            {
                                var del = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), targetOwner, method);
                                Bind(sld, del);
                            }
                            break;
                        case OnScrollbarChangedAttribute scrollbarAttr:
                            var sbr = FindComponentInChildren<Scrollbar>(scrollbarAttr.ScrollbarName, ownerType.Name, method.Name);
                            if (sbr != null)
                            {
                                var del = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), targetOwner, method);
                                Bind(sbr, del);
                            }
                            break;
                        case OnInputChangedAttribute inputAttr:
                            BindInputMethod(targetOwner, ownerType, method, inputAttr);
                            break;
                        case OnDropdownChangedAttribute dropdownAttr:
                            BindDropdownMethod(targetOwner, ownerType, method, dropdownAttr);
                            break;
                        case OnScrollRectChangedAttribute scrollRectAttr:
                            var srt = FindComponentInChildren<ScrollRect>(scrollRectAttr.ScrollRectName, ownerType.Name, method.Name);
                            if (srt != null)
                            {
                                var del = (Action<Vector2>)Delegate.CreateDelegate(typeof(Action<Vector2>), targetOwner, method);
                                Bind(srt, del);
                            }
                            break;
                    }
                } catch (Exception ex)
                {
                    Debug.LogError($"<color=lime>[UIAutoBind]</color> Class: {ownerType.Name} | Method: {method.Name} | " +
                        $"Delegate create or bind failed.\n{ex}");
                }
            }
        }

        private void BindInputMethod(object targetOwner, Type ownerType, MethodInfo method, OnInputChangedAttribute inputAttr)
        {
            Transform targetNode = FindTargetNodeFallback( inputAttr.InputName, ownerType.Name, method.Name, typeof(InputField), typeof(TMP_InputField));
            if (targetNode == null) return;

            var oldComp = targetNode.GetComponent<InputField>();
            if (oldComp != null)
            {
                var del = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), targetOwner, method);
                Bind(oldComp, del, inputAttr.IsEndEdit);
                return;
            }

            var tmpComp = targetNode.GetComponent<TMP_InputField>();

            UnityEngine.Assertions.Assert.IsNotNull( tmpComp,
                $"<color=lime>[UIAutoBind]</color> Class: {ownerType.Name} | Method: {method.Name} | " +
                $"Message: Neither InputField nor TMP_InputField was found on GameObject '{targetNode.name}'!");

            if (tmpComp != null)
            {
                var del = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), targetOwner, method);
                Bind(tmpComp, del, inputAttr.IsEndEdit);
            }
        }

        private void BindDropdownMethod(object targetOwner, Type ownerType, MethodInfo method, OnDropdownChangedAttribute dropdownAttr)
        {
            Transform targetNode = FindTargetNodeFallback( dropdownAttr.DropdownName, ownerType.Name, method.Name, typeof(Dropdown), typeof(TMP_Dropdown));
            if (targetNode == null) return;

            var oldComp = targetNode.GetComponent<Dropdown>();
            if (oldComp != null)
            {
                var del = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), targetOwner, method);
                Bind(oldComp, del);
                return;
            }

            var tmpComp = targetNode.GetComponent<TMP_Dropdown>();

            UnityEngine.Assertions.Assert.IsNotNull( tmpComp,
                $"<color=lime>[UIAutoBind]</color> Class: {ownerType.Name} | Method: {method.Name} | " +
                $"Message: Neither Dropdown nor TMP_Dropdown was found on GameObject '{targetNode.name}'!");

            if (tmpComp != null)
            {
                var del = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), targetOwner, method);
                Bind(tmpComp, del);
            }
        }

        #endregion

        #region ------------------ Type Meta Cache ------------------

        private static TypeBindMeta GetOrCreateTypeMeta(Type type)
        {
            if (TypeMetaCache.TryGetValue(type, out TypeBindMeta meta))
            {
                return meta;
            }

            meta = new TypeBindMeta();

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo[] fields = type.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                var attr = field.GetCustomAttribute<AutoBindAttribute>();

                if (attr != null)
                {
                    meta.Fields.Add(new FieldBindMeta {
                        Field = field,
                        Attribute = attr
                    });
                }
            }

            MethodInfo[] methods = type.GetMethods(flags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];

                AddMethodMeta<OnClickAttribute>(meta, method);
                AddMethodMeta<OnToggleChangedAttribute>(meta, method);
                AddMethodMeta<OnSliderChangedAttribute>(meta, method);
                AddMethodMeta<OnScrollbarChangedAttribute>(meta, method);
                AddMethodMeta<OnInputChangedAttribute>(meta, method);
                AddMethodMeta<OnDropdownChangedAttribute>(meta, method);
                AddMethodMeta<OnScrollRectChangedAttribute>(meta, method);
            }

            TypeMetaCache.Add(type, meta);
            return meta;
        }

        private static void AddMethodMeta<T>(TypeBindMeta meta, MethodInfo method) where T : Attribute
        {
            var attr = method.GetCustomAttribute<T>();
            if (attr == null) return;

            meta.Methods.Add(new MethodBindMeta { Method = method, Attribute = attr });
        }

        public static void ClearTypeMetaCache()
        {
            TypeMetaCache.Clear();
        }

        #endregion

        #region ------------------ Transform Search ------------------

        private Transform FindTargetNodeFallback( string pathOrName, string className, string memberName, params Type[] expectedComponentTypes)
        {
            return UIBindGeneratedUtil.FindAutoBindTarget( _rootTransform, pathOrName, className, memberName, expectedComponentTypes);
        }

        private T FindComponentInChildren<T>(string objName, string className, string memberName) where T : Component
        {
            Transform target = FindTargetNodeFallback(objName, className, memberName, typeof(T));
            if (target == null) return null;

            return target.GetComponent<T>();
        }

        #endregion

        #region ------------------ Destroy / Unbind ------------------

        public void ClearAndUnbindAll(bool clearCache = false)
        {
            for (int i = 0; i < _buttonBindings.Count; i++)
            {
                var item = _buttonBindings[i];
                if (item.btn != null)
                {
                    item.btn.onClick.RemoveListener(item.act);
                }
            }

            for (int i = 0; i < _toggleBindings.Count; i++)
            {
                var item = _toggleBindings[i];
                if (item.tog != null)
                {
                    item.tog.onValueChanged.RemoveListener(item.act);
                }
            }

            for (int i = 0; i < _sliderBindings.Count; i++)
            {
                var item = _sliderBindings[i];
                if (item.sld != null)
                {
                    item.sld.onValueChanged.RemoveListener(item.act);
                }
            }

            for (int i = 0; i < _scrollbarBindings.Count; i++)
            {
                var item = _scrollbarBindings[i];
                if (item.sbr != null)
                {
                    item.sbr.onValueChanged.RemoveListener(item.act);
                }
            }

            for (int i = 0; i < _inputBindings.Count; i++)
            {
                var item = _inputBindings[i];
                if (item.field == null) continue;

                if (item.isEndEdit)
                {
                    item.field.onEndEdit.RemoveListener(item.act);
                } else
                {
                    item.field.onValueChanged.RemoveListener(item.act);
                }
            }

            for (int i = 0; i < _tmpInputBindings.Count; i++)
            {
                var item = _tmpInputBindings[i];
                if (item.field == null) continue;

                if (item.isEndEdit)
                {
                    item.field.onEndEdit.RemoveListener(item.act);
                } else
                {
                    item.field.onValueChanged.RemoveListener(item.act);
                }
            }

            for (int i = 0; i < _dropdownBindings.Count; i++)
            {
                var item = _dropdownBindings[i];
                if (item.dp != null)
                {
                    item.dp.onValueChanged.RemoveListener(item.act);
                }
            }

            for (int i = 0; i < _tmpDropdownBindings.Count; i++)
            {
                var item = _tmpDropdownBindings[i];
                if (item.dp != null)
                {
                    item.dp.onValueChanged.RemoveListener(item.act);
                }
            }

            for (int i = 0; i < _scrollRectBindings.Count; i++)
            {
                var item = _scrollRectBindings[i];
                if (item.srt != null)
                {
                    item.srt.onValueChanged.RemoveListener(item.act);
                }
            }

            for (int i = 0; i < _adapterBindings.Count; i++)
            {
                var item = _adapterBindings[i];

                if (item.component != null &&
                    item.adapter != null &&
                    item.callback != null)
                {
                    item.adapter.RemoveListener(item.component, item.callback, item.option);
                }
            }

            _adapterBindings.Clear();
            _buttonBindings.Clear();
            _toggleBindings.Clear();
            _sliderBindings.Clear();
            _scrollbarBindings.Clear();
            _inputBindings.Clear();
            _tmpInputBindings.Clear();
            _dropdownBindings.Clear();
            _tmpDropdownBindings.Clear();
            _scrollRectBindings.Clear();

#if UNITY_EDITOR
            _bindingsInfo.Clear();
#endif

            if (clearCache)
            {
                UIBindGeneratedUtil.ClearCache(_rootTransform);
            }
        }

        #endregion
    }
}
