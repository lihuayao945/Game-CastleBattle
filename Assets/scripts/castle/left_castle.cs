using UnityEngine;
using UnityEngine.UI;
using MagicBattle;

public class left_castle : Unit
{
    public Sprite fullCitySprite; // 完整城堡图片
    public Sprite damagedCitySprite; // 轻微损坏图片
    public Sprite ruinedCitySprite; // 严重损坏图片
    private SpriteRenderer cityRenderer; // 用于显示我们的SpriteRenderer
    public Slider healthBar;  // 血条UI组件

    [SerializeField] private HeroBaseDataSO castleDataSO; // 使用HeroDataSO
    
    [Header("城堡恢复设置")]
    [SerializeField] private float healthRegenPerSecond = 10f; // 每秒恢复生命值
    [SerializeField] private bool enableRegen = true; // 是否启用生命恢复

    // 重写属性，城堡不显示伤害特效
    public override bool ShowDamageEffect => false;

    // 添加公共属性
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    // 城堡类型是固定的，直接返回LeftCastle
    public override UnitType Type => UnitType.LeftCastle;

    protected override void Awake()
    {
        base.Awake();
        
        // 从 HeroDataSO 初始化基础属性 (如果存在)
        if (castleDataSO != null)
        {
            SetUnitData(castleDataSO);
        }
    }

    protected override void Start()
    {
        // 设置阵营为左方
        faction = Faction.Left;
        
        base.Start(); // 这里会调用ApplyGlobalUnitUpgrades
        
        // 获取SpriteRenderer组件
        cityRenderer = GetComponent<SpriteRenderer>();

        // 初始化城堡为完整状态
        UpdateCityImage();

        // 初始化血条
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;  // 设置血条的最大值
            healthBar.value = currentHealth;  // 设置血条的初始值
        }
    }
    
    protected override void Update()
    {
        base.Update();
        
        // 城堡生命恢复逻辑
        if (enableRegen && !IsDead && currentHealth < maxHealth)
        {
            // 计算这一帧应该恢复的生命值
            float regenAmount = healthRegenPerSecond * Time.deltaTime;
            
            // 应用生命恢复
            currentHealth = Mathf.Min(currentHealth + regenAmount, maxHealth);
            
            // 更新血条显示
            if (healthBar != null)
            {
                healthBar.value = currentHealth;
            }
            
            // 如果生命值变化足够大，更新城堡图片
            // 通常我们会在受伤时更新图片，但恢复时我们需要检查是否跨过了阈值
            if ((currentHealth > maxHealth * 0.5f && cityRenderer.sprite != fullCitySprite) ||
                (currentHealth <= maxHealth * 0.5f && currentHealth > 0 && cityRenderer.sprite != damagedCitySprite))
            {
                UpdateCityImage();
            }
        }
    }

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        UpdateCityImage();

        // 更新血条显示
        if (healthBar != null)
        {
            healthBar.value = currentHealth;  // 根据当前血量更新血条
        }
    }

    // 根据血量更新城堡图片
    void UpdateCityImage()
    {
        if (currentHealth > maxHealth * 0.5f)
        {
            cityRenderer.sprite = fullCitySprite; // 血量大于50%时显示完整城堡
        }
        else if (currentHealth > 0)
        {
            cityRenderer.sprite = damagedCitySprite; // 血量大于0时显示轻微损坏的城堡
        }
        else
        {
            cityRenderer.sprite = ruinedCitySprite; // 血量等于0时显示完全损坏的城堡
        }
    }

    protected override void Die()
    {
        // 城堡被摧毁时的特殊处理
        UpdateCityImage(); // 确保显示完全损坏的图片
        OnDeath?.Invoke(); // 触发死亡事件，但不销毁物体
        
        // 触发游戏结束界面（玩家失败）
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGameOverUI(false);
        }
        else
        {
            Debug.LogError("UIManager.Instance is null!");
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        UpdateCityImage();
        
        // 更新血条显示
        if (healthBar != null)
        {
            healthBar.value = currentHealth;
        }
    }
    
    // 设置生命恢复速率
    public void SetHealthRegenRate(float rate)
    {
        healthRegenPerSecond = rate;
    }
    
    // 启用/禁用生命恢复
    public void EnableHealthRegen(bool enable)
    {
        enableRegen = enable;
    }
}
