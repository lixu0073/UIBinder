namespace Game.UIBinderAdapters
{
    using System;
    using GuiSolution.UIBinder;
    using UnityEngine;

    public sealed class ScrollViewAdapter : IUIBindEventAdapter
    {
        public Type CallbackType => typeof(Action<Vector2>);

        public bool CanHandle(Component component)
        {
            return component is GuiSolution.ScrollView.ScrollView;
        }

        public void AddListener(Component component, Delegate callback, object option = null)
        {
            var view = component as GuiSolution.ScrollView.ScrollView;
            if (view == null || callback == null) return;

            var act = callback as Action<Vector2>;
            if (act == null) return;

            view.onValueChanged.RemoveListener(act.Invoke);
            view.onValueChanged.AddListener(act.Invoke);
        }

        public void RemoveListener(Component component, Delegate callback, object option = null)
        {
            var view = component as GuiSolution.ScrollView.ScrollView;
            if (view == null || callback == null) return;

            var act = callback as Action<Vector2>;
            if (act == null) return;

            view.onValueChanged.RemoveListener(act.Invoke);
        }
    }

    public static class UIBindAdapterRegister
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            UIBindEventAdapterRegistry.Register(new ScrollViewAdapter());
        }
    }
}