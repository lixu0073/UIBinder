namespace GuiSolution.UIBinder
{
    using System;
    using System.Collections.Generic;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// UI绑定 -> 委托适配，继承IUIBindEventAdapter实现
    /// </summary>

    public interface IUIBindEventAdapter
    {
        Type CallbackType { get; }

        bool CanHandle(Component component);

        void AddListener(Component component, Delegate callback, object option = null);

        void RemoveListener(Component component, Delegate callback, object option = null);
    }

    public enum InputBindMode
    {
        EndEdit,
        ValueChanged
    }

    public static class UIBindEventAdapterRegistry
    {
        private static readonly List<IUIBindEventAdapter> adapters = new List<IUIBindEventAdapter>();

        private static readonly Dictionary<Type, List<IUIBindEventAdapter>> type2Adapters = new Dictionary<Type, List<IUIBindEventAdapter>>();

        static UIBindEventAdapterRegistry()
        {
            RegisterDefaultAdapters();
        }

        public static void Register(IUIBindEventAdapter adapter)
        {
            if (adapter == null) return;
            if (adapters.Contains(adapter)) return;

            adapters.Add(adapter);

            Type callbackType = adapter.CallbackType;
            if (callbackType == null) return;

            if (!type2Adapters.TryGetValue(callbackType, out List<IUIBindEventAdapter> list))
            {
                list = new List<IUIBindEventAdapter>();
                type2Adapters.Add(callbackType, list);
            }

            list.Add(adapter);
        }

        private static void RegisterDefaultAdapters()
        {
            Register(new ButtonAdapter());
            Register(new ToggleAdapter());
            Register(new SliderAdapter());
            Register(new ScrollbarAdapter());
            Register(new InputFieldAdapter());
            Register(new TMPInputFieldAdapter());
            Register(new DropdownAdapter());
            Register(new TMPDropdownAdapter());
            Register(new ScrollRectAdapter());
        }

        #region TryBind
        public static bool TryBind<T>(Transform target, Action<T> callback, out Component boundComponent, out IUIBindEventAdapter boundAdapter)
        {
            return TryBindDelegate(target, callback, out boundComponent, out boundAdapter);
        }

        public static bool TryBind<T>(Transform target, Action<T> callback, out Component boundComponent, out IUIBindEventAdapter boundAdapter, object option = null)
        {
            return TryBindDelegate(target, callback, out boundComponent, out boundAdapter, option);
        }

        public static bool TryBind(Transform target, Action callback, out Component boundComponent, out IUIBindEventAdapter boundAdapter, object option = null)
        {
            return TryBindDelegate(target, callback, out boundComponent, out boundAdapter, option);
        }

        private static bool TryBindDelegate(Transform target, Delegate callback, out Component boundComponent, out IUIBindEventAdapter boundAdapter, object option = null)
        {
            boundComponent = null;
            boundAdapter = null;

            if (target == null || callback == null) return false;

            Type callbackType = callback.GetType();
            if (!type2Adapters.TryGetValue(callbackType, out List<IUIBindEventAdapter> matchedAdapters))
            {
                return false;
            }

            Component[] components = target.GetComponents<Component>();

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null) continue;

                for (int j = matchedAdapters.Count - 1; j >= 0; j--)
                {
                    IUIBindEventAdapter adapter = matchedAdapters[j];

                    if (!adapter.CanHandle(component)) continue;

                    adapter.RemoveListener(component, callback, option);
                    adapter.AddListener(component, callback, option);

                    boundComponent = component;
                    boundAdapter = adapter;
                    return true;
                }
            }

            return false;
        }
        #endregion


        private sealed class ButtonAdapter : IUIBindEventAdapter
        {
            public Type CallbackType => typeof(Action);

            public bool CanHandle(Component component)
            {
                return component is Button;
            }

            public void AddListener(Component component, Delegate callback, object option = null)
            {
                ((Button)component).onClick.AddListener(((Action)callback).Invoke);
            }

            public void RemoveListener(Component component, Delegate callback, object option = null)
            {
                ((Button)component).onClick.RemoveListener(((Action)callback).Invoke);
            }
        }

        private sealed class ToggleAdapter : IUIBindEventAdapter
        {
            public Type CallbackType => typeof(Action<bool>);

            public bool CanHandle(Component component)
            {
                return component is Toggle;
            }

            public void AddListener(Component component, Delegate callback, object option = null)
            {
                ((Toggle)component).onValueChanged.AddListener(((Action<bool>)callback).Invoke);
            }

            public void RemoveListener(Component component, Delegate callback, object option = null)
            {
                ((Toggle)component).onValueChanged.RemoveListener(((Action<bool>)callback).Invoke);
            }
        }

        private sealed class SliderAdapter : IUIBindEventAdapter
        {
            public Type CallbackType => typeof(Action<float>);

            public bool CanHandle(Component component)
            {
                return component is Slider;
            }

            public void AddListener(Component component, Delegate callback, object option = null)
            {
                ((Slider)component).onValueChanged.AddListener(((Action<float>)callback).Invoke);
            }

            public void RemoveListener(Component component, Delegate callback, object option = null)
            {
                ((Slider)component).onValueChanged.RemoveListener(((Action<float>)callback).Invoke);
            }
        }

        private sealed class ScrollbarAdapter : IUIBindEventAdapter
        {
            public Type CallbackType => typeof(Action<float>);

            public bool CanHandle(Component component)
            {
                return component is Scrollbar;
            }

            public void AddListener(Component component, Delegate callback, object option = null)
            {
                ((Scrollbar)component).onValueChanged.AddListener(((Action<float>)callback).Invoke);
            }

            public void RemoveListener(Component component, Delegate callback, object option = null)
            {
                ((Scrollbar)component).onValueChanged.RemoveListener(((Action<float>)callback).Invoke);
            }
        }

        private sealed class InputFieldAdapter : IUIBindEventAdapter
        {
            public Type CallbackType => typeof(Action<string>);

            public bool CanHandle(Component component)
            {
                return component is InputField;
            }

            public void AddListener(Component component, Delegate callback, object option = null)
            {
                var input = component as InputField;
                var act = callback as Action<string>;
                if (input == null || act == null) return;

                InputBindMode mode = option is InputBindMode m ? m : InputBindMode.EndEdit;

                if (mode == InputBindMode.EndEdit)
                {
                    input.onEndEdit.AddListener(act.Invoke);
                } else
                {
                    input.onValueChanged.AddListener(act.Invoke);
                }
            }

            public void RemoveListener(Component component, Delegate callback, object option = null)
            {
                var input = component as InputField;
                var act = callback as Action<string>;
                if (input == null || act == null) return;

                InputBindMode mode = option is InputBindMode m ? m : InputBindMode.EndEdit;

                if (mode == InputBindMode.EndEdit)
                {
                    input.onEndEdit.RemoveListener(act.Invoke);
                } else
                {
                    input.onValueChanged.RemoveListener(act.Invoke);
                }
            }
        }

        private sealed class TMPInputFieldAdapter : IUIBindEventAdapter
        {
            public Type CallbackType => typeof(Action<string>);

            public bool CanHandle(Component component)
            {
                return component is TMP_InputField;
            }

            public void AddListener(Component component, Delegate callback, object option = null)
            {
                var input = component as TMP_InputField;
                var act = callback as Action<string>;
                if (input == null || act == null) return;

                InputBindMode mode = option is InputBindMode m ? m : InputBindMode.EndEdit;

                if (mode == InputBindMode.EndEdit)
                {
                    input.onEndEdit.AddListener(act.Invoke);
                } else
                {
                    input.onValueChanged.AddListener(act.Invoke);
                }
            }

            public void RemoveListener(Component component, Delegate callback, object option = null)
            {
                var input = component as TMP_InputField;
                var act = callback as Action<string>;
                if (input == null || act == null) return;

                InputBindMode mode = option is InputBindMode m ? m : InputBindMode.EndEdit;

                if (mode == InputBindMode.EndEdit)
                {
                    input.onEndEdit.RemoveListener(act.Invoke);
                } else
                {
                    input.onValueChanged.RemoveListener(act.Invoke);
                }
            }
        }

        private sealed class DropdownAdapter : IUIBindEventAdapter
        {
            public Type CallbackType => typeof(Action<int>);

            public bool CanHandle(Component component)
            {
                return component is Dropdown;
            }

            public void AddListener(Component component, Delegate callback, object option = null)
            {
                ((Dropdown)component).onValueChanged.AddListener(((Action<int>)callback).Invoke);
            }

            public void RemoveListener(Component component, Delegate callback, object option = null)
            {
                ((Dropdown)component).onValueChanged.RemoveListener(((Action<int>)callback).Invoke);
            }
        }

        private sealed class TMPDropdownAdapter : IUIBindEventAdapter
        {
            public Type CallbackType => typeof(Action<int>);

            public bool CanHandle(Component component)
            {
                return component is TMP_Dropdown;
            }

            public void AddListener(Component component, Delegate callback, object option = null)
            {
                ((TMP_Dropdown)component).onValueChanged.AddListener(((Action<int>)callback).Invoke);
            }

            public void RemoveListener(Component component, Delegate callback, object option = null)
            {
                ((TMP_Dropdown)component).onValueChanged.RemoveListener(((Action<int>)callback).Invoke);
            }
        }

        private sealed class ScrollRectAdapter : IUIBindEventAdapter
        {
            public Type CallbackType => typeof(Action<Vector2>);

            public bool CanHandle(Component component)
            {
                return component is ScrollRect;
            }

            public void AddListener(Component component, Delegate callback, object option = null)
            {
                ((ScrollRect)component).onValueChanged.AddListener(((Action<Vector2>)callback).Invoke);
            }

            public void RemoveListener(Component component, Delegate callback, object option = null)
            {
                ((ScrollRect)component).onValueChanged.RemoveListener(((Action<Vector2>)callback).Invoke);
            }
        }
    }
}
