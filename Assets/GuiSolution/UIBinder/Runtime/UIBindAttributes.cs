namespace GuiSolution.UIBinder
{
    using System;

    /// <summary>
    /// 仅用作别名，方便在代码中使用 [Auto.Button("BtnStart")] 这种形式来绑定事件方法
    /// </summary>
    public static class Auto
    {
        [AttributeUsage(AttributeTargets.Method)]
        public class ButtonAttribute : OnClickAttribute
        {
            public ButtonAttribute(string name) : base(name) { }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class ToggleAttribute : OnToggleChangedAttribute
        {
            public ToggleAttribute(string name) : base(name) { }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class SliderAttribute : OnSliderChangedAttribute
        {
            public SliderAttribute(string name) : base(name) { }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class ScrollbarAttribute : OnScrollbarChangedAttribute
        {
            public ScrollbarAttribute(string name) : base(name) { }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class InputAttribute : OnInputChangedAttribute
        {
            public InputAttribute(string name, bool isEndEdit = true) : base(name, isEndEdit) { }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class DropdownAttribute : OnDropdownChangedAttribute
        {
            public DropdownAttribute(string name) : base(name) { }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class ScrollAttribute : OnScrollRectChangedAttribute
        {
            public ScrollAttribute(string name) : base(name) { }
        }
    }

    /// <summary>
    /// [AutoBind("")] 自动识别绑定，作用于字段，支持多种 UGUI 组件
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class AutoBindAttribute : Attribute
    {
        public string TransformPath { get; } // 子路径
        public AutoBindAttribute(string transformPath = "") => TransformPath = transformPath;
    }

    /// <summary>
    /// Button 绑定，作用于方法，必须无参数
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class OnClickAttribute : Attribute
    {
        public string ButtonName { get; }
        public OnClickAttribute(string buttonName) => ButtonName = buttonName;
    }

    /// <summary>
    /// Toggle 绑定，作用于方法，必须带有一个 bool 参数（表示当前开关的实时状态）
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class OnToggleChangedAttribute : Attribute
    {
        public string ToggleName { get; }
        public OnToggleChangedAttribute(string toggleName) => ToggleName = toggleName;
    }

    /// <summary>
    /// Slider 绑定，作用于方法，必须带有一个 float 参数（表示当前滑动条的实时数值）
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class OnSliderChangedAttribute : Attribute
    {
        public string SliderName { get; }
        public OnSliderChangedAttribute(string sliderName) => SliderName = sliderName;
    }

    /// <summary>
    /// Scrollbar 绑定，作用于方法，必须带有一个 float 参数（表示当前滚动条的实时数值）
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class OnScrollbarChangedAttribute : Attribute
    {
        public string ScrollbarName { get; }
        public OnScrollbarChangedAttribute(string scrollbarName)
        {
            ScrollbarName = scrollbarName;
        }
    }

    /// <summary>
    /// InputField/TMP_InputField 绑定，作用于方法，必须带有一个 string 参数（表示当前输入框的文本内容）
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class OnInputChangedAttribute : Attribute
    {
        public string InputName { get; }
        public bool IsEndEdit { get; } // true 表示在按下回车或失去焦点时触发，false 表示每打一个字都触发
        public OnInputChangedAttribute(string inputName, bool isEndEdit = true)
        {
            InputName = inputName;
            IsEndEdit = isEndEdit;
        }
    }

    /// <summary>
    /// Dropdown/TMP_Dropdown 绑定，作用于方法，必须带有一个 int 参数（表示当前选项的索引）
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class OnDropdownChangedAttribute : System.Attribute
    {
        public string DropdownName { get; }
        public OnDropdownChangedAttribute(string dropdownName) => DropdownName = dropdownName;
    }

    /// <summary>
    /// ScrollRect 绑定，作用于方法，必须带有一个 Vector2 参数（表示当前列表滚动的位置）
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class OnScrollRectChangedAttribute : Attribute
    {
        public string ScrollRectName { get; }

        public OnScrollRectChangedAttribute(string scrollRectName)
        {
            ScrollRectName = scrollRectName;
        }
    }
}
