using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using MagicBattle;  // 添加MagicBattle命名空间引用

public class CastleHealthUI : MonoBehaviour
{
    [Header("左城堡血条")]
    [SerializeField] private Image leftHealthBarImage;
    [SerializeField] private TextMeshProUGUI leftHealthText;
    [SerializeField] private left_castle leftCastle;
    [SerializeField] private string leftCastleTooltip = "左城堡血量";  // 左城堡提示文本

    [Header("右城堡血条")]
    [SerializeField] private Image rightHealthBarImage;
    [SerializeField] private TextMeshProUGUI rightHealthText;
    [SerializeField] private right_castle rightCastle;
    [SerializeField] private string rightCastleTooltip = "右城堡血量";  // 右城堡提示文本

    [Header("左城堡血条图片")]
    [SerializeField] private Sprite leftHealth100;  // 100%血量
    [SerializeField] private Sprite leftHealth80;   // 80%血量
    [SerializeField] private Sprite leftHealth60;   // 60%血量
    [SerializeField] private Sprite leftHealth40;   // 40%血量
    [SerializeField] private Sprite leftHealth20;   // 20%血量
    [SerializeField] private Sprite leftHealth10;   // 10%血量

    [Header("右城堡血条图片")]
    [SerializeField] private Sprite rightHealth100;  // 100%血量
    [SerializeField] private Sprite rightHealth80;   // 80%血量
    [SerializeField] private Sprite rightHealth60;   // 60%血量
    [SerializeField] private Sprite rightHealth40;   // 40%血量
    [SerializeField] private Sprite rightHealth20;   // 20%血量
    [SerializeField] private Sprite rightHealth10;   // 10%血量

    private void Start()
    {
        // 确保所有引用都已设置
        if (leftHealthBarImage == null || leftHealthText == null || leftCastle == null ||
            rightHealthBarImage == null || rightHealthText == null || rightCastle == null)
        {
            Debug.LogError("CastleHealthUI: 请设置所有必要的引用！");
            return;
        }

        // 添加鼠标悬停事件
        AddPointerEvents(leftHealthBarImage.gameObject, leftCastleTooltip);
        AddPointerEvents(rightHealthBarImage.gameObject, rightCastleTooltip);

        // 初始化血条显示
        UpdateLeftHealthBar();
        UpdateRightHealthBar();
    }

    private void AddPointerEvents(GameObject obj, string tooltipText)
    {
        // 添加EventTrigger组件
        EventTrigger trigger = obj.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = obj.AddComponent<EventTrigger>();
        }

        // 添加鼠标进入事件
        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { OnPointerEnter(tooltipText); });
        trigger.triggers.Add(enterEntry);

        // 添加鼠标退出事件
        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => { OnPointerExit(); });
        trigger.triggers.Add(exitEntry);
    }

    private void OnPointerEnter(string tooltipText)
    {
        // 显示提示框
        TooltipSystem.Show(tooltipText, false);
    }

    private void OnPointerExit()
    {
        // 隐藏提示框
        TooltipSystem.Hide();
    }

    private void Update()
    {
        // 实时更新血条
        UpdateLeftHealthBar();
        UpdateRightHealthBar();
    }

    private void UpdateLeftHealthBar()
    {
        if (leftCastle == null) return;

        float healthPercentage = (float)leftCastle.CurrentHealth / leftCastle.MaxHealth * 100f;
        UpdateHealthBar(leftHealthBarImage, leftHealthText, healthPercentage, (int)leftCastle.CurrentHealth, (int)leftCastle.MaxHealth, true);
    }

    private void UpdateRightHealthBar()
    {
        if (rightCastle == null) return;

        float healthPercentage = (float)rightCastle.CurrentHealth / rightCastle.MaxHealth * 100f;
        UpdateHealthBar(rightHealthBarImage, rightHealthText, healthPercentage, (int)rightCastle.CurrentHealth, (int)rightCastle.MaxHealth, false);
    }

    private void UpdateHealthBar(Image healthBarImage, TextMeshProUGUI healthText, float healthPercentage, int currentHealth, int maxHealth, bool isLeftCastle)
    {
        // 更新血条图片
        if (healthPercentage <= 0)
        {
            healthBarImage.sprite = null;  // 血量为0时清空图片
        }
        else if (healthPercentage >= 100)
        {
            healthBarImage.sprite = isLeftCastle ? leftHealth100 : rightHealth100;
        }
        else if (healthPercentage >= 80)
        {
            healthBarImage.sprite = isLeftCastle ? leftHealth80 : rightHealth80;
        }
        else if (healthPercentage >= 60)
        {
            healthBarImage.sprite = isLeftCastle ? leftHealth60 : rightHealth60;
        }
        else if (healthPercentage >= 40)
        {
            healthBarImage.sprite = isLeftCastle ? leftHealth40 : rightHealth40;
        }
        else if (healthPercentage >= 20)
        {
            healthBarImage.sprite = isLeftCastle ? leftHealth20 : rightHealth20;
        }
        else
        {
            healthBarImage.sprite = isLeftCastle ? leftHealth10 : rightHealth10;
        }

        // 更新血量文本，右侧城堡显示顺序相反
        healthText.text = isLeftCastle ? $"{currentHealth}/{maxHealth}" : $"{maxHealth}/{currentHealth}";
    }
} 