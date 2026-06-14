# UIBinder

Unity UGUI 自动绑定工具，支持字段绑定和方法绑定。

## 功能特性

* 支持字段自动绑定
* 支持方法事件自动绑定
* 支持 `UGUI`
* 支持 `TextMeshPro`
* 支持自定义组件，例如自定义 `ScrollView`
* 支持代码生成，减少运行时反射开销
* 支持通过 `UIBinderEnv` 在运行时显示绑定信息

## 生成绑定代码

菜单路径：

```text
Tools/UIBinder/Generate Binding Code
```

对应菜单声明：

```csharp
[MenuItem("Tools/UIBinder/Generate Binding Code")]
```

示例：

```csharp
public partial class UIBinderTest : MonoBehaviour
{
}
```

## UIBinderEnv

`UIBinderEnv` 是绑定环境组件。

主要作用：

* 自动调用绑定和解绑逻辑
* 运行时显示字段绑定信息
* 运行时显示方法绑定信息
* 方便查看反射绑定和代码生成绑定的实际结果

如果 UI 脚本所在物体已经挂载了 `UIBinderEnv`，可以不用手动编写下面这段代码：

```csharp
private void Start()
{
    this.UIBind();
}

private void OnDestroy()
{
    this.UIUnbind();
}
```

也就是说，下面两种方式都支持：

* 挂载 `UIBinderEnv`，由组件自动处理绑定和解绑
* 不挂载 `UIBinderEnv`，自己手动调用 `this.UIBind()` 和 `this.UIUnbind()`

推荐使用 `UIBinderEnv`，这样既能自动绑定，也能在运行时查看绑定信息。

## 使用说明

### 1. 字段绑定

使用 `[AutoBind]` 标记字段。

#### 按名称绑定

```csharp
[AutoBind("btnBack")]
private Button btnStart;
```

#### 按路径绑定

```csharp
[AutoBind("UIBinderTest/btnBack")]
private Button btnBack;
```

#### 使用 `nameof`

```csharp
[AutoBind(nameof(btnBack))]
private Button btnBack;
```

#### 省略路径

如果 `[AutoBind]` 不填写路径，会默认使用字段名查找。

```csharp
[AutoBind]
private Button btnBack;
```

等价于：

```csharp
[AutoBind(nameof(btnBack))]
private Button btnBack;
```

#### 只绑定方法，不声明字段

如果只需要绑定按钮点击事件，可以不声明字段。

```csharp
[Auto.Button("BtnStart")]
private void OnClickStart()
{
}
```

### 2. 方法绑定

#### Button

方法必须无参数。

```csharp
[Auto.Button("BtnStart")]
private void OnClickStart()
{
}
```

等价写法：

```csharp
[OnClick("BtnStart")]
private void OnClickStart()
{
}
```

#### Toggle

方法必须有一个 `bool` 参数。

```csharp
[Auto.Toggle("TogMusic")]
private void OnMusicChanged(bool isOn)
{
}
```

#### Slider

方法必须有一个 `float` 参数。

```csharp
[Auto.Slider("SliderVolume")]
private void OnVolumeChanged(float value)
{
}
```

#### Scrollbar

方法必须有一个 `float` 参数。

```csharp
[Auto.Scrollbar("ScrollbarProgress")]
private void OnProgressChanged(float value)
{
}
```

#### InputField / TMP_InputField

方法必须有一个 `string` 参数。

默认 `isEndEdit = true`，表示结束编辑时触发。

```csharp
[Auto.Input("InputName")]
private void OnNameEndEdit(string value)
{
}
```

如果需要每次输入变化时触发：

```csharp
[Auto.Input("InputName", isEndEdit: false)]
private void OnNameChanged(string value)
{
}
```

#### Dropdown / TMP_Dropdown

方法必须有一个 `int` 参数。

```csharp
[Auto.Dropdown("DropdownQuality")]
private void OnQualityChanged(int index)
{
}
```

#### ScrollRect / 自定义 ScrollView

方法必须有一个 `Vector2` 参数。

```csharp
[Auto.Scroll("RoleList")]
private void OnRoleListScroll(Vector2 position)
{
}
```

## 支持的常用组件

字段绑定支持常用 `UGUI`、`TextMeshPro` 组件以及自定义组件。

### UGUI 组件

```csharp
[AutoBind] private Button btnBack;
[AutoBind] private Toggle togMute;
[AutoBind] private Slider sliderVolume;
[AutoBind] private Scrollbar scrollViewBar;
[AutoBind] private InputField inputName;
[AutoBind] private Dropdown dropdownQuality;
[AutoBind] private ScrollRect srvBagList;
[AutoBind] private Text txtLegacy;
[AutoBind] private Image imgIcon;
[AutoBind] private RectTransform rectContentRoot;
[AutoBind] private CanvasGroup canvasGroupPanel;
```

### TextMeshPro 组件

```csharp
[AutoBind] private TMP_Text txtStatus;
[AutoBind] private TextMeshProUGUI txtTitleUGUI;
[AutoBind] private TMP_InputField inputPlayerName;
[AutoBind] private TMP_Dropdown dpLanguage;
```

### 自定义组件

```csharp
[AutoBind] private ScrollView roleList;
```

### 使用 `nameof` 绑定

```csharp
[AutoBind(nameof(txtStatus))] private TMP_Text txtStatus;
[AutoBind(nameof(txtTitleUGUI))] private TextMeshProUGUI txtTitleUGUI;
[AutoBind(nameof(txtLegacy))] private Text txtLegacy;
[AutoBind(nameof(imgIcon))] private Image imgIcon;
[AutoBind(nameof(rectContentRoot))] private RectTransform rectContentRoot;
[AutoBind(nameof(canvasGroupPanel))] private CanvasGroup canvasGroupPanel;
```

## 完整示例

### 推荐写法：挂载 UIBinderEnv

UI 脚本所在物体挂载 `UIBinderEnv` 后，不需要自己写 `Start` 和 `OnDestroy` 里的绑定代码。

方法里绑定了组件后，可以不手动声明字段，代码生成或反射会自动处理。

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using GuiSolution.ScrollView;
using GuiSolution.UIBinder;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UIBinderTest : MonoBehaviour
{
    //[AutoBind] private Button btnBack;
    //[AutoBind] private Toggle togMute;
    //[AutoBind] private Slider sliderVolume;
    //[AutoBind] private Scrollbar scrollViewBar;
    //[AutoBind] private InputField inputName;
    //[AutoBind] private TMP_InputField inputPlayerName;
    //[AutoBind] private Dropdown dropdownQuality;
    //[AutoBind] private TMP_Dropdown dpLanguage;
    //[AutoBind] private ScrollRect srvBagList;

    [AutoBind(nameof(txtStatus))] private TMP_Text txtStatus;
    [AutoBind(nameof(txtTitleUGUI))] private TextMeshProUGUI txtTitleUGUI;
    [AutoBind(nameof(txtLegacy))] private Text txtLegacy;
    [AutoBind(nameof(imgIcon))] private Image imgIcon;
    [AutoBind(nameof(rectContentRoot))] private RectTransform rectContentRoot;
    [AutoBind(nameof(canvasGroupPanel))] private CanvasGroup canvasGroupPanel;

    [AutoBind] private ScrollView roleList;

    [Auto.Button("BtnStart")]
    private void OnClickStart()
    {
    }

    [Auto.Toggle("TogMusic")]
    private void OnMusicChanged(bool isOn)
    {
    }

    [Auto.Slider("SliderVolume")]
    private void OnVolumeChanged(float value)
    {
    }

    [Auto.Scrollbar("ScrollbarProgress")]
    private void OnProgressChanged(float value)
    {
    }

    [Auto.Input("InputName")]
    private void OnNameEndEdit(string value)
    {
    }

    [Auto.Input("InputName", isEndEdit: false)]
    private void OnNameChanged(string value)
    {
    }

    [Auto.Dropdown("DropdownQuality")]
    private void OnQualityChanged(int index)
    {
    }

    [Auto.Scroll("RoleList")]
    private void OnRoleListScroll(Vector2 position)
    {
    }
}
```

## 注意事项

* 使用代码生成时，UI 脚本必须是 `partial`
* 字段名、节点名、路径需要和实际层级保持一致
* `[AutoBind]` 不填写路径时，默认使用字段名查找
* 方法绑定时，参数类型必须和组件事件类型一致
* 如果只需要绑定事件方法，可以不声明对应字段
* 如果同名节点较多，建议使用完整路径绑定，避免绑定到错误节点
* 挂载 `UIBinderEnv` 后，可以不用手动调用 `this.UIBind()` 和 `this.UIUnbind()`
* `UIBinderEnv` 同时用于运行时显示字段绑定和方法绑定信息
