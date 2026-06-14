namespace GuiSolution.ScrollView
{
    using System;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    [CustomEditor(typeof(ScrollView), true)]
    public class ScrollViewEditor : Editor
    {
        private const string bgPath = "UI/Skin/Background.psd";
        private const string spritePath = "UI/Skin/UISprite.psd";
        private const string maskPath = "UI/Skin/UIMask.psd";

        private static Color panelColor = new Color(1f, 1f, 1f, 0.392f);
        private static Color defaultSelectableColor = new Color(1f, 1f, 1f, 1f);
        private static Vector2 thinElementSize = new Vector2(160f, 20f);
        private static Action<GameObject, MenuCommand> PlaceUIElementRoot;

        private SerializedProperty itemTemplate;
        private SerializedProperty poolSize;
        private SerializedProperty defaultItemSize;
        private SerializedProperty layoutType;

        private SerializedProperty content;
        private SerializedProperty viewport;
        private SerializedProperty horizontal;
        private SerializedProperty vertical;
        private SerializedProperty movementType;
        private SerializedProperty elasticity;

        private SerializedProperty horizontalScrollbar;
        private SerializedProperty verticalScrollbar;
        private SerializedProperty horizontalScrollbarVisibility;
        private SerializedProperty verticalScrollbarVisibility;
        private SerializedProperty horizontalScrollbarSpacing;
        private SerializedProperty verticalScrollbarSpacing;

        private SerializedProperty reverseScrollDirection;
        private SerializedProperty inertiaMode;
        private SerializedProperty customDeceleration;
        private SerializedProperty customStopVelocity;
        private SerializedProperty customMaxVelocity;
        private SerializedProperty customSlowVelocity;
        private SerializedProperty customSlowDamping;
        private SerializedProperty customPixelSnap;
        private SerializedProperty customElasticity;
        private SerializedProperty dragSensitivity;
        private SerializedProperty dragElasticLimit;
        private SerializedProperty wheelSensitivity;
        private SerializedProperty wheelInertiaMultiplier;

        private GUIStyle cachedCaption;

        private GUIStyle caption
        {
            get
            {
                if (this.cachedCaption == null)
                {
                    this.cachedCaption = new GUIStyle
                    {
                        richText = true,
                        alignment = TextAnchor.MiddleCenter,
                    };
                }

                return this.cachedCaption;
            }
        }

        protected virtual void OnEnable()
        {
            this.itemTemplate = this.serializedObject.FindProperty("itemTemplate");
            this.poolSize = this.serializedObject.FindProperty("poolSize");
            this.defaultItemSize = this.serializedObject.FindProperty("defaultItemSize");
            this.layoutType = this.serializedObject.FindProperty("layoutType");

            this.content = this.serializedObject.FindProperty("m_Content");
            this.viewport = this.serializedObject.FindProperty("m_Viewport");
            this.horizontal = this.serializedObject.FindProperty("m_Horizontal");
            this.vertical = this.serializedObject.FindProperty("m_Vertical");
            this.movementType = this.serializedObject.FindProperty("m_MovementType");
            this.elasticity = this.serializedObject.FindProperty("m_Elasticity");

            this.horizontalScrollbar = this.serializedObject.FindProperty("m_HorizontalScrollbar");
            this.verticalScrollbar = this.serializedObject.FindProperty("m_VerticalScrollbar");
            this.horizontalScrollbarVisibility = this.serializedObject.FindProperty("m_HorizontalScrollbarVisibility");
            this.verticalScrollbarVisibility = this.serializedObject.FindProperty("m_VerticalScrollbarVisibility");
            this.horizontalScrollbarSpacing = this.serializedObject.FindProperty("m_HorizontalScrollbarSpacing");
            this.verticalScrollbarSpacing = this.serializedObject.FindProperty("m_VerticalScrollbarSpacing");

            this.reverseScrollDirection = this.serializedObject.FindProperty("reverseScrollDirection");
            this.inertiaMode = this.serializedObject.FindProperty("inertiaMode");
            this.customDeceleration = this.serializedObject.FindProperty("customDeceleration");
            this.customStopVelocity = this.serializedObject.FindProperty("customStopVelocity");
            this.customMaxVelocity = this.serializedObject.FindProperty("customMaxVelocity");
            this.customSlowVelocity = this.serializedObject.FindProperty("customSlowVelocity");
            this.customSlowDamping = this.serializedObject.FindProperty("customSlowDamping");
            this.customPixelSnap = this.serializedObject.FindProperty("customPixelSnap");
            this.customElasticity = this.serializedObject.FindProperty("customElasticity");
            this.dragSensitivity = this.serializedObject.FindProperty("dragSensitivity");
            this.dragElasticLimit = this.serializedObject.FindProperty("dragElasticLimit");
            this.wheelSensitivity = this.serializedObject.FindProperty("wheelSensitivity");
            this.wheelInertiaMultiplier = this.serializedObject.FindProperty("wheelInertiaMultiplier");
        }

        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("<b>ScrollView</b>", this.caption);
            EditorGUILayout.Space(5);
            this.DrawConfigInfo();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("<b>ScrollRect 必要配置</b>", this.caption);
            EditorGUILayout.Space(5);
            this.DrawScrollRectNecessaryInfo();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("<b>Custom Inertia</b>", this.caption);
            EditorGUILayout.Space(5);
            this.DrawCustomInertiaInfo();
            EditorGUILayout.EndVertical();

            this.serializedObject.ApplyModifiedProperties();
        }

        protected virtual void DrawConfigInfo()
        {
            this.DrawProperty(this.itemTemplate);
            this.DrawProperty(this.poolSize);
            this.DrawProperty(this.defaultItemSize);

            if (this.layoutType != null)
            {
                this.layoutType.intValue = (int)(ScrollView.ItemLayoutType)EditorGUILayout.EnumPopup(
                    "layoutType",
                    (ScrollView.ItemLayoutType)this.layoutType.intValue);
            }
        }
         
        private void DrawScrollRectNecessaryInfo()
        {
            this.DrawProperty(this.content);
            this.DrawProperty(this.viewport);

            EditorGUILayout.Space();

            this.DrawProperty(this.horizontal);
            this.DrawProperty(this.vertical);
            this.DrawProperty(this.movementType);

            if (this.movementType != null &&
                this.movementType.enumValueIndex == (int)ScrollRect.MovementType.Elastic)
            {
                this.DrawProperty(this.elasticity);
            }

            EditorGUILayout.Space();
            this.DrawProperty(this.horizontalScrollbar);
            this.DrawProperty(this.verticalScrollbar);

            if (this.horizontalScrollbar != null && this.horizontalScrollbar.objectReferenceValue != null)
            {
                this.DrawProperty(this.horizontalScrollbarVisibility);
                this.DrawProperty(this.horizontalScrollbarSpacing);
            }

            if (this.verticalScrollbar != null && this.verticalScrollbar.objectReferenceValue != null)
            {
                this.DrawProperty(this.verticalScrollbarVisibility);
                this.DrawProperty(this.verticalScrollbarSpacing);
            }
        }

        private void DrawCustomInertiaInfo()
        {
            this.DrawProperty(this.reverseScrollDirection);
            EditorGUILayout.Space();

            this.DrawProperty(this.inertiaMode);

            var customMode = this.inertiaMode != null && this.inertiaMode.enumValueIndex == 1;

            if (!customMode)
            {
                EditorGUILayout.HelpBox("Native 模式使用 Unity ScrollRect 原生惯性。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Custom 模式下，Unity 原生 Inertia / Deceleration Rate 会被关闭，由这里的参数控制。",
                MessageType.Info);

            EditorGUI.indentLevel++;

            this.DrawProperty(this.customDeceleration);
            this.DrawProperty(this.customStopVelocity);
            this.DrawProperty(this.customMaxVelocity);
            this.DrawProperty(this.customSlowVelocity);
            this.DrawProperty(this.customSlowDamping);
            this.DrawProperty(this.customPixelSnap);

            EditorGUILayout.Space();

            if (this.movementType != null && this.movementType.enumValueIndex == (int)ScrollRect.MovementType.Elastic)
            {
                this.DrawProperty(this.customElasticity);
            }


            EditorGUILayout.Space();
            this.DrawProperty(this.dragSensitivity);
            this.DrawProperty(this.dragElasticLimit);
            this.DrawProperty(this.wheelSensitivity);
            this.DrawProperty(this.wheelInertiaMultiplier);

            EditorGUI.indentLevel--;
        }

        protected void DrawProperty(SerializedProperty property)
        {
            if (property != null)
            {
                EditorGUILayout.PropertyField(property);
            }
        }

        #region 创建scroll view
        [MenuItem("GameObject/UI/ScrollView", false, 90)]
        private static void AddScrollViewEx(MenuCommand menuCommand)
        {
            InternalAddScrollView<ScrollView>(menuCommand);
        }

        protected static void InternalAddScrollView<T>(MenuCommand menuCommand)
            where T : ScrollView
        {
            GetPrivateMethodByReflection();

            GameObject root = CreateUIElementRoot(typeof(T).Name, new Vector2(200, 200));
            PlaceUIElementRoot?.Invoke(root, menuCommand);

            GameObject viewport = CreateUIObject("Viewport", root);
            GameObject content = CreateUIObject("Content", viewport);

            var parent = menuCommand.context as GameObject;
            if (parent != null)
            {
                root.transform.SetParent(parent.transform, false);
            }

            Selection.activeGameObject = root;

            GameObject hScrollbar = CreateScrollbar();
            hScrollbar.name = "Scrollbar Horizontal";
            hScrollbar.transform.SetParent(root.transform, false);

            RectTransform hScrollbarRT = hScrollbar.GetComponent<RectTransform>();
            hScrollbarRT.anchorMin = Vector2.zero;
            hScrollbarRT.anchorMax = Vector2.right;
            hScrollbarRT.pivot = Vector2.zero;
            hScrollbarRT.sizeDelta = new Vector2(0, hScrollbarRT.sizeDelta.y);

            GameObject vScrollbar = CreateScrollbar();
            vScrollbar.name = "Scrollbar Vertical";
            vScrollbar.transform.SetParent(root.transform, false);
            vScrollbar.GetComponent<Scrollbar>().SetDirection(Scrollbar.Direction.BottomToTop, true);

            RectTransform vScrollbarRT = vScrollbar.GetComponent<RectTransform>();
            vScrollbarRT.anchorMin = Vector2.right;
            vScrollbarRT.anchorMax = Vector2.one;
            vScrollbarRT.pivot = Vector2.one;
            vScrollbarRT.sizeDelta = new Vector2(vScrollbarRT.sizeDelta.x, 0);

            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.pivot = Vector2.up;

            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.up;
            contentRect.anchorMax = Vector2.one;
            contentRect.sizeDelta = new Vector2(0, 300);
            contentRect.pivot = Vector2.up;

            ScrollView scrollRect = root.AddComponent<T>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontalScrollbar = hScrollbar.GetComponent<Scrollbar>();
            scrollRect.verticalScrollbar = vScrollbar.GetComponent<Scrollbar>();
            scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.horizontalScrollbarSpacing = -3;
            scrollRect.verticalScrollbarSpacing = -3;

            Image rootImage = root.AddComponent<Image>();
            rootImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(bgPath);
            rootImage.type = Image.Type.Sliced;
            rootImage.color = panelColor;

            Mask viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(maskPath);
            viewportImage.type = Image.Type.Sliced;
        }

        private static GameObject CreateScrollbar()
        {
            GameObject scrollbarRoot = CreateUIElementRoot("Scrollbar", thinElementSize);
            GameObject sliderArea = CreateUIObject("Sliding Area", scrollbarRoot);
            GameObject handle = CreateUIObject("Handle", sliderArea);

            Image bgImage = scrollbarRoot.AddComponent<Image>();
            bgImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(bgPath);
            bgImage.type = Image.Type.Sliced;
            bgImage.color = defaultSelectableColor;

            Image handleImage = handle.AddComponent<Image>();
            handleImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(spritePath);
            handleImage.type = Image.Type.Sliced;
            handleImage.color = defaultSelectableColor;

            RectTransform sliderAreaRect = sliderArea.GetComponent<RectTransform>();
            sliderAreaRect.sizeDelta = new Vector2(-20, -20);
            sliderAreaRect.anchorMin = Vector2.zero;
            sliderAreaRect.anchorMax = Vector2.one;

            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 20);

            Scrollbar scrollbar = scrollbarRoot.AddComponent<Scrollbar>();
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;
            SetDefaultColorTransitionValues(scrollbar);

            return scrollbarRoot;
        }

        private static GameObject CreateUIElementRoot(string name, Vector2 size)
        {
            var child = new GameObject(name);
            RectTransform rectTransform = child.AddComponent<RectTransform>();
            rectTransform.sizeDelta = size;
            return child;
        }

        private static GameObject CreateUIObject(string name, GameObject parent)
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            SetParentAndAlign(go, parent);
            return go;
        }

        private static void SetParentAndAlign(GameObject child, GameObject parent)
        {
            if (parent == null)
            {
                return;
            }

            child.transform.SetParent(parent.transform, false);
            SetLayerRecursively(child, parent.layer);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            Transform t = go.transform;

            for (var i = 0; i < t.childCount; i++)
            {
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
            }
        }

        private static void SetDefaultColorTransitionValues(Selectable slider)
        {
            ColorBlock colors = slider.colors;
            colors.highlightedColor = new Color(0.882f, 0.882f, 0.882f);
            colors.pressedColor = new Color(0.698f, 0.698f, 0.698f);
            colors.disabledColor = new Color(0.521f, 0.521f, 0.521f);
            slider.colors = colors;
        }

        private static void GetPrivateMethodByReflection()
        {
            if (PlaceUIElementRoot != null)
            {
                return;
            }

            Assembly uiEditorAssembly = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (var i = 0; i < assemblies.Length; i++)
            {
                if (assemblies[i].GetName().Name == "UnityEditor.UI")
                {
                    uiEditorAssembly = assemblies[i];
                    break;
                }
            }

            if (uiEditorAssembly == null)
            {
                return;
            }

            Type menuOptionType = uiEditorAssembly.GetType("UnityEditor.UI.MenuOptions");

            if (menuOptionType == null)
            {
                return;
            }

            MethodInfo miPlaceUIElementRoot = menuOptionType.GetMethod(
                "PlaceUIElementRoot",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (miPlaceUIElementRoot == null)
            {
                return;
            }

            PlaceUIElementRoot = Delegate.CreateDelegate(
                typeof(Action<GameObject, MenuCommand>),
                miPlaceUIElementRoot) as Action<GameObject, MenuCommand>;
        }
        #endregion
    }
} 
