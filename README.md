# UIBinder Unity UGUI 自动绑定工具，支持字段绑定，方法绑定
  -  [MenuItem(Tools/UIBinder/Generate Binding Code)]
  -  代码生成依赖 partial，UI脚本需要手动改为改为partial
## 使用说明
  1.字段绑定
    使用 [AutoBind] 标记字段。
    
    [AutoBind("btnBack")]
    private Button btnStart;
    
    [AutoBind("UIBinderTest/btnBack")]
    private Button btnBack;
  
    [AutoBind(nameof(btnBack))]
    private Button btnBack;
  
    如果 [AutoBind] 不填写路径，会默认使用字段名查找' nameof(btnBack) '：  
    
    [AutoBind]
    private Button btnBack;

    如果方法里绑定了按钮，可以完全不写字段：
    
    [Auto.Button("BtnStart")]
    private void OnClickStart() { }

  2.方法绑定
    - Button
    方法必须无参数。  
    
    [Auto.Button("BtnStart")]
    private void OnClickStart() { }
    
    等价写法：    
    
    [OnClick("BtnStart")]
    private void OnClickStart() { }
    
    - Toggle
    方法必须有一个 bool 参数。  
    
    [Auto.Toggle("TogMusic")]
    private void OnMusicChanged(bool isOn) { }
    
    - Slider
    方法必须有一个 float 参数。  
    
    [Auto.Slider("SliderVolume")]
    private void OnVolumeChanged(float value) { }
    
    - Scrollbar
    方法必须有一个 float 参数。
    
    [Auto.Scrollbar("ScrollbarProgress")]
    private void OnProgressChanged(float value) { }
    
    - InputField / TMP_InputField
    方法必须有一个 string 参数。
    默认 isEndEdit = true，表示结束编辑时触发。 
    
    [Auto.Input("InputName")]
    private void OnNameEndEdit(string value) { }
    
    如果需要每次输入变化都触发：
    
    [Auto.Input("InputName", isEndEdit: false)]
    private void OnNameChanged(string value) { }
    
    - Dropdown / TMP_Dropdown
    方法必须有一个 int 参数。  
    
    [Auto.Dropdown("DropdownQuality")]
    private void OnQualityChanged(int index) { }
    
    - ScrollRect / 自定义 ScrollView
    方法必须有一个 Vector2 参数。
    
    [Auto.Scroll("RoleList")]
    private void OnRoleListScroll(Vector2 position) { }

