using System.Collections.Generic;
using System.Text;
using GuiSolution.ScrollView;
using GuiSolution.UIBinder;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UIBinderTest : MonoBehaviour
{
    [AutoBind(nameof(btnBack))] private Button btnBack;
    [AutoBind(nameof(togMute))] private Toggle togMute;
    [AutoBind(nameof(sliderVolume))] private Slider sliderVolume;
    [AutoBind(nameof(scrollViewBar))] private Scrollbar scrollViewBar;
    [AutoBind(nameof(inputPlayerName))] private TMP_InputField inputPlayerName;
    [AutoBind(nameof(dpLanguage))] private TMP_Dropdown dpLanguage;
    [AutoBind(nameof(srvBagList))] private ScrollRect srvBagList;

    List<DefaultScrollItemData> testData = new List<DefaultScrollItemData>();

    #region ScrollView引入测试
    [AutoBind] private ScrollView roleList;

    private void Start()
    {
        roleList.SetItemCountFunc(itemCountFunc);
        roleList.SetUpdateFunc(updateFunc);
        roleList.SetItemSizeFunc(itemSizeFunc);

        for (int i = 0; i < 50; i++)
        {
            this.AddRandomData(false);
        }

        roleList.UpdateData();
    }

    public void AddRandomData(bool update = true)
    {
        var newData = new DefaultScrollItemData() {
            name = GetRandomSizeString(),
            longString = GetRandomLongText()
        };

        this.testData.Insert(UnityEngine.Random.Range(0, this.testData.Count + 1), newData);

        if (update && this.roleList != null)
        {
            this.roleList.UpdateData();
        }
    }

    [Auto.Scroll("roleList")]
    private void OnRoleListScrolled(Vector2 normalizedPos)
    {
        Debug.Log($"[Test] RoleList 滚动位置 -> X = {normalizedPos.x:F2}, Y = {normalizedPos.y:F2}");
    }
    static string GetRandomLongText()
    {
        var rand = UnityEngine.Random.Range(1, 100);
        var stringBuilder = new StringBuilder(rand + 2);
        do
        {
            stringBuilder.Append((char)UnityEngine.Random.Range('A', 'Z'));
        }
        while (--rand > 0);
        stringBuilder.AppendLine();
        return stringBuilder.ToString();
    }
    static string GetRandomSizeString()
    {
        var f = UnityEngine.Random.value;
        if (f > 0.8)
        {
            return "XXL";
        } else if (f > 0.6)
        {
            return "XL";
        } else if (f > 0.4)
        {
            return "L";
        } else if (f > 0.2)
        {
            return "M";
        } else
        {
            return "S";
        }
    }
    void updateFunc(int index, RectTransform item)
    {
        if (index < 0 || index >= this.testData.Count)
        {
            return;
        }

        DefaultScrollItemData data = this.testData[index];

        item.gameObject.SetActive(true);

        var text = item.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = $"{data.name}_{index}";
            return;
        }

        var tmpText = item.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
        {
            tmpText.text = $"{data.name}_{index}";
            return;
        }

        Debug.LogError($"Item 上找不到 Text 或 TMP_Text: {item.name}", item);
    }
    int itemCountFunc()
    {
        return this.testData.Count;
    }
    Vector2 itemSizeFunc(int index)
    {
        DefaultScrollItemData sd = this.testData[index];
        if (sd.name == "XXL")
            return new Vector2(300, 120);
        else if (sd.name == "XL")
            return new Vector2(300, 110);
        else if (sd.name == "L")
            return new Vector2(300, 100);
        else if (sd.name == "M")
            return new Vector2(300, 90);
        else // "S"
            return new Vector2(300, 80);
    }
    #endregion

    [Auto.Button("btnBack")]
    private void OnBackButtonClick()
    {
        Debug.Log("[Test] Button Clicked: 成功回退上级页面。");
    }

    [Auto.Toggle("togMute")]
    private void OnMuteStatusChanged(bool isMute)
    {
        Debug.Log($"[Test] Toggle Value Changed: 声音切换状态 -> 开关是否勾选 = {isMute}");
    }

    [OnSliderChanged("sliderVolume")]
    private void OnVolumeSliderMoved(float currentVolume)
    {
        Debug.Log($"[Test] Slider Value Changed: 实时音量调节中 -> {currentVolume:P0}");
    }

    [OnScrollbarChanged("scrollViewBar")]
    private void OnScrollbarValueMoved(float scrollValue)
    {
        if (!Application.isPlaying) return;
        Debug.Log($"[Test] Scrollbar Value Changed: 滚动条发生物理位移 -> 当前进度 = {scrollValue:P0}");
    }

    [OnInputChanged("inputPlayerName")]
    private void OnPlayerNameSubmit(string finalName)
    {
        Debug.Log($"[Test] InputField EndEdit: 玩家完成名称修改 -> 最终提交结果 = {finalName}");
    }

    [OnInputChanged("inputPlayerName", isEndEdit: false)]
    private void OnChatContentTyping(string currentText)
    {
        Debug.Log($"[Test] InputField OnValueChanged: 聊天框字符物理变更 -> 实时文字 = {currentText}");
    }

    [OnDropdownChanged("dpLanguage")]
    private void OnLanguageSelectedIndex(int index)
    {
        if (dpLanguage != null && index >= 0 && index < dpLanguage.options.Count)
        {
            string selectedLang = dpLanguage.options[index].text;
            Debug.Log($"[Test] Dropdown Value Changed: 下拉选单切线成功 -> 当前选中索引 = {index} (语言 = {selectedLang})");
        } else
        {
            Debug.LogWarning($"[Test] Dropdown 索引越界或组件未绑定成功！Index: {index}");
        }
    }

    [OnScrollRectChanged("srvBagList")]
    private void OnBagScrollViewScrolled(Vector2 normalizedPos)
    {
        Debug.Log($"[Test] ScrollRect 滚动位置 -> X = {normalizedPos.x:F2}, Y = {normalizedPos.y:F2}");
    }
}