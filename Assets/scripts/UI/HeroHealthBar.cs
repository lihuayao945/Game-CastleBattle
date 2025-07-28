using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// 英雄血条控制器
/// </summary>
public class HeroHealthBar : MonoBehaviour
{
    [Header("血条组件")]
    [SerializeField] private Image healthFill;    // 血条填充图片
    [SerializeField] private TextMeshProUGUI healthText;  // 血量文本

    [Header("血量设置")]
    [SerializeField] private float maxHealth = 100f;  // 最大血量
    private float currentHealth;  // 当前血量

    [Header("测试设置")]
    [SerializeField, Range(0f, 100f)] private float testHealth = 100f;  // 测试用血量值

    [Header("提示框设置")]
    [SerializeField, TextArea(3, 10)] private string tooltipText = "敌方英雄";  // 提示文本

    private void Awake()
    {
        // 初始化血量为满血
        currentHealth = maxHealth;
        UpdateHealthUI();
        
        // 添加鼠标事件监听
        AddPointerEvents();
    }

    private void AddPointerEvents()
    {
        // 添加EventTrigger组件
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        // 添加鼠标进入事件
        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { OnPointerEnter(); });
        trigger.triggers.Add(enterEntry);

        // 添加鼠标退出事件
        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => { OnPointerExit(); });
        trigger.triggers.Add(exitEntry);
    }

    private void OnPointerEnter()
    {
        // 显示提示框
        TooltipSystem.Show(tooltipText, false);
    }

    private void OnPointerExit()
    {
        // 隐藏提示框
        TooltipSystem.Hide();
    }

    private void OnValidate()
    {
        // 在Inspector中修改testHealth时实时更新UI
        if (healthFill != null && healthText != null)
        {
            currentHealth = testHealth;
            UpdateHealthUI();
        }
    }

    /// <summary>
    /// 设置当前血量
    /// </summary>
    /// <param name="health">目标血量</param>
    public void SetHealth(float health)
    {
        currentHealth = Mathf.Clamp(health, 0f, maxHealth);
        UpdateHealthUI();
    }

    /// <summary>
    /// 受到伤害
    /// </summary>
    /// <param name="damage">伤害值</param>
    public void TakeDamage(float damage)
    {
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        UpdateHealthUI();
    }

    /// <summary>
    /// 治疗恢复
    /// </summary>
    /// <param name="healAmount">治疗量</param>
    public void Heal(float healAmount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);
        UpdateHealthUI();
    }

    /// <summary>
    /// 更新血条UI显示
    /// </summary>
    public void UpdateHealthUI()
    {
        // 更新血条填充量
        healthFill.fillAmount = currentHealth / maxHealth;
        
        // 更新血量文本
        int healthPercentage = Mathf.RoundToInt((currentHealth / maxHealth) * 100f);
        healthText.text = $"{healthPercentage}%";
    }

    /// <summary>
    /// 设置最大血量并更新当前血量比例
    /// </summary>
    /// <param name="newMaxHealth">新的最大血量</param>
    public void SetMaxHealth(float newMaxHealth)
    {
        if (newMaxHealth <= 0) 
        {
            Debug.LogWarning("SetMaxHealth: newMaxHealth should be greater than 0.");
            return;
        }
        
        // 计算当前血量的比例
        float healthRatio = currentHealth / maxHealth;
        maxHealth = newMaxHealth;
        // 根据新的最大血量调整当前血量
        currentHealth = maxHealth * healthRatio;
        UpdateHealthUI();
    }

    /// <summary>
    /// 统一更新血条的当前血量和最大血量，并刷新UI。
    /// </summary>
    /// <param name="current">当前血量</param>
    /// <param name="max">最大血量</param>
    public void UpdateHealth(float current, float max)
    {
        if (max <= 0)
        {
            Debug.LogWarning("UpdateHealth: maxHealth should be greater than 0.");
            return;
        }

        maxHealth = max;
        currentHealth = Mathf.Clamp(current, 0f, maxHealth);
        UpdateHealthUI();
    }

    /// <summary>
    /// 获取当前血量
    /// </summary>
    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    /// <summary>
    /// 获取最大血量
    /// </summary>
    public float GetMaxHealth()
    {
        return maxHealth;
    }
} 