using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; // Required for IPointerEnterHandler and IPointerExitHandler
using System;

/// <summary>
/// 单个强化条目的UI控制器，处理图标、名称显示及悬停事件
/// </summary>
public class UpgradeItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] public Image upgradeIcon; // 强化图标
    [SerializeField] public TextMeshProUGUI upgradeNameText; // 强化名称文本

    // 存储强化描述，用于传递给Tooltip
    public string UpgradeDescription { get; private set; }

    // 定义事件，以便UpgradeDisplayManager可以订阅
    public event Action<string, Vector2> OnShowUpgradeTooltip;
    public event Action OnHideUpgradeTooltip;

    /// <summary>
    /// 初始化强化条目UI
    /// </summary>
    /// <param name="iconSprite">强化图标</param>
    /// <param name="name">强化名称</param>
    /// <param name="description">强化描述</param>
    public void Initialize(Sprite iconSprite, string name, string description)
    {
        if (upgradeIcon != null)
        {
            upgradeIcon.sprite = iconSprite;
        }
        if (upgradeNameText != null)
        {
            upgradeNameText.text = name;
        }
        UpgradeDescription = description;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnShowUpgradeTooltip?.Invoke(UpgradeDescription, Input.mousePosition);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnHideUpgradeTooltip?.Invoke();
    }
} 