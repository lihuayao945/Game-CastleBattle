using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeCardUI : MonoBehaviour
{
    [SerializeField] private Image upgradeIcon;
    [SerializeField] private TextMeshProUGUI upgradeNameText;
    [SerializeField] private TextMeshProUGUI upgradeDescriptionText;

    /// <summary>
    /// 设置卡片的显示内容。
    /// </summary>
    /// <param name="upgradeData">要显示的强化数据。</param>
    public void Setup(UpgradeDataSO upgradeData)
    {
        if (upgradeData.icon != null)
        {
            upgradeIcon.sprite = upgradeData.icon;
        }
        else
        {
            upgradeIcon.sprite = null; // 清除旧图标，或者设置为默认空白
        }
        upgradeNameText.text = upgradeData.upgradeName;
        upgradeDescriptionText.text = upgradeData.description;
    }
} 