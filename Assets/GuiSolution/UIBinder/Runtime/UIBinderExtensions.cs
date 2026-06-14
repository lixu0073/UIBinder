namespace GuiSolution.UIBinder
{
    using UnityEngine;

    /// <summary>
    /// MonoBehaviour 扩展：在 UI 脚本里直接 this.UIBind() / this.UIUnbind()
    /// </summary>
    public static class UIBinderExtensions
    {
        public static void UIBind(this MonoBehaviour monoOwner)
        {
            if (monoOwner == null) return;

            var env = monoOwner.GetComponent<UIBinderEnv>();
            if (env == null)
            {
                env = monoOwner.gameObject.AddComponent<UIBinderEnv>();
            }

            env.Init(monoOwner);
        }

        public static void UIUnbind(this MonoBehaviour monoOwner)
        {
            if (monoOwner == null) return;

            var env = monoOwner.GetComponent<UIBinderEnv>();
            if (env == null) return;

            env.Unbind();
        }
    }
}