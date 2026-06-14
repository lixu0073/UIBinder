namespace GuiSolution.ScrollView
{
    using UnityEditor;

    [CustomEditor(typeof(ScrollViewEx))]
    public class ScrollViewExEditor : ScrollViewEditor
    {
        private SerializedProperty pageSize;

        protected override void OnEnable()
        {
            base.OnEnable();
            this.pageSize = this.serializedObject.FindProperty("pageSize");
        }

        protected override void DrawConfigInfo()
        {
            base.DrawConfigInfo();
            EditorGUILayout.PropertyField(this.pageSize);
        }

        [MenuItem("GameObject/UI/ScrollViewEx", false, 90)]
        private static void AddScrollViewEx(MenuCommand menuCommand)
        {
            InternalAddScrollView<ScrollViewEx>(menuCommand);
        }
    }
}
