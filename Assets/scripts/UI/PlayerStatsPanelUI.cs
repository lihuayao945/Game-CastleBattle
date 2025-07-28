using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 玩家数据面板UI管理器，负责强化数据和单位数据的显示切换
/// </summary>
public class PlayerStatsPanelUI : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private GameObject playerStatsPanel; // 主数据面板
    [SerializeField] private GameObject upgradeDataPanel; // 强化数据子面板
    [SerializeField] private GameObject unitDataPanel;    // 单位数据子面板

    [Header("Buttons")]
    [SerializeField] private Button upgradeDataButton; // 切换到强化数据面板的按钮
    [SerializeField] private Button unitDataButton;    // 切换到单位数据面板的按钮

    public bool IsPanelActive => playerStatsPanel.activeInHierarchy;

    private void Awake()
    {
        // 确保主面板初始是隐藏的
        playerStatsPanel.SetActive(false);

        // 绑定按钮事件
        upgradeDataButton.onClick.AddListener(() => ShowSubPanel(upgradeDataPanel));
        unitDataButton.onClick.AddListener(() => ShowSubPanel(unitDataPanel));
    }

    private void Start()
    {
        // 默认显示强化数据面板
        ShowSubPanel(upgradeDataPanel);
    }

    /// <summary>
    /// 切换数据面板的显示/隐藏状态
    /// </summary>
    public void TogglePanel()
    {
        playerStatsPanel.SetActive(!playerStatsPanel.activeInHierarchy);
        // 当面板显示时，默认切换到强化数据面板
        if (playerStatsPanel.activeInHierarchy)
        {
            ShowSubPanel(upgradeDataPanel);
        }
    }

    /// <summary>
    /// 显示指定的子面板，隐藏其他子面板
    /// </summary>
    /// <param name="panelToShow">要显示的子面板</param>
    private void ShowSubPanel(GameObject panelToShow)
    {
        // 激活/禁用子面板
        upgradeDataPanel.SetActive(panelToShow == upgradeDataPanel);
        unitDataPanel.SetActive(panelToShow == unitDataPanel);

        // 根据当前显示的面板来设置按钮的状态和视觉效果
        if (panelToShow == upgradeDataPanel)
        {
            // 强化数据面板激活时
            upgradeDataButton.interactable = false; // 强化按钮失效
            unitDataButton.interactable = true;    // 单位按钮启用
            // 如果需要更精细的视觉效果（例如强制高亮/按下状态，而非Disabled Color），可能需要手动设置 button.image.color 或 button.spriteState。
            // 但通常情况下，Button组件的Disabled Color/Sprite足以表达"选中且不可点击"的状态。
        }
        else if (panelToShow == unitDataPanel)
        {
            // 单位数据面板激活时
            unitDataButton.interactable = false;    // 单位按钮失效
            upgradeDataButton.interactable = true; // 强化按钮启用
        }
        // 确保面板被激活后，按钮的视觉状态立即更新
        LayoutRebuilder.ForceRebuildLayoutImmediate(upgradeDataButton.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(unitDataButton.GetComponent<RectTransform>());
    }

    /// <summary>
    /// 隐藏所有子面板 (可能不需要，因为ShowSubPanel会处理)
    /// </summary>
    public void HideAllSubPanels()
    {
        upgradeDataPanel.SetActive(false);
        unitDataPanel.SetActive(false);
    }
} 