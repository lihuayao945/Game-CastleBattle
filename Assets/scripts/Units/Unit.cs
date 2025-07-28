using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// 单位基类 - 所有可交互单位（英雄、小兵、城堡）的基类
/// </summary>
public abstract class Unit : MonoBehaviour
{
    // 阵营枚举
    public enum Faction
    {
        Left,   // 左方阵营
        Right   // 右方阵营
    }

    // 单位类型枚举
    public enum UnitType
    {
        None,           // 未指定类型
        Knight,         // 骑士
        Necromancer,    // 死灵法师
        Soldier,        // 士兵
        Lancer,         // 长枪兵
        Archer,         // 弓箭手
        Priest,         // 牧师
        Mage,           // 法师
        LeftCastle,     // 左方城堡
        RightCastle,     // 右方城堡
        SwordMaster    // 剑术大师
    }

    // 添加抽象属性，改名为Type避免与枚举类型冲突
    public abstract UnitType Type { get; }

    // 单位所属阵营
    [SerializeField] public Faction faction;
    
    // 最大生命值
    [SerializeField] public float maxHealth = 100;
    
    // 当前生命值
    [SerializeField] public float currentHealth;
    public bool IsDead { get; protected set; } = false;
    
    // 是否处于眩晕状态
    [SerializeField] public bool isStunned = false;
    
    // 眩晕剩余时间
    private float stunTimeRemaining = 0f;
    
    // 基础移动速度
    [SerializeField] public float baseMoveSpeed = 5f;
    
    // 基础防御力
    [SerializeField] public float baseDefense = 1f;
    
    // 当前移动速度
    public float currentMoveSpeed;
    
    // 当前防御力
    public float currentDefense;
    
    // 速度修改器
    public float speedModifier = 1f;
    
    // 防御力修改器
    private float defenseModifier = 1f;
    
    // 事件
    public UnityEvent<float> OnStunned;
    public UnityEvent OnStunEnded;
    public UnityEvent OnDeath;
    public UnityEvent<float> OnDamaged;
    public UnityEvent<float, float> OnHealthUpdated; // 新增：血量更新事件 (currentHealth, maxHealth)
    
    // 是否显示伤害特效
    public virtual bool ShowDamageEffect => true;
    
    // 阵营颜色
    private static readonly Color LEFT_FACTION_TINT = new Color(0.2f, 0.4f, 1f, 1f); // 深蓝色
    private static readonly Color RIGHT_FACTION_TINT = new Color(1f, 0.2f, 0.2f, 1f); // 深红色

    // 边缘发光材质
    [SerializeField] private Material outlineMaterial;
    private Material originalMaterial;
    private SpriteRenderer spriteRenderer;
    
    // 获取单位阵营
    public Faction GetFaction() => faction;
    
    // 判断目标是否为友军
    public bool IsAlly(Unit other)
    {
        return other != null && faction == other.faction;
    }
    
    // 判断目标是否为敌军
    public bool IsEnemy(Unit other)
    {
        return other != null && faction != other.faction;
    }
    
    [Header("受伤效果")]
    [SerializeField] protected float hitFlashDuration = 0.05f;    // 受伤闪烁持续时间
    [SerializeField] protected Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 0.9f);  // 受伤闪烁颜色（淡红色，半透明）
    protected Color originalColor;                               // 原始颜色
    private Coroutine currentHitFlashCoroutine;                 // 当前正在运行的闪烁协程

    // 原始基础属性值（未经过任何强化）
    protected float originalMaxHealth;
    protected float originalMoveSpeed;
    protected float originalDefense;

    public UnityEvent<Vector3> OnResurrectionStarted; // 新增：复活开始事件 (复活点位置)
    public UnityEvent OnResurrectionCompleted; // 新增：复活完成事件

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            // 保存原始材质和颜色
            originalMaterial = spriteRenderer.material;
            originalColor = spriteRenderer.color;

            // 如果没有设置outlineMaterial，尝试从Resources加载
            if (outlineMaterial == null)
            {
                outlineMaterial = Resources.Load<Material>("Materials/UnitOutline");
                if (outlineMaterial == null)
                {
                    //Debug.LogWarning($"Unit {gameObject.name}: Could not load UnitOutline material from Resources/Materials/UnitOutline");
                }
            }
        }

        // 保存原始基础属性值
        originalMaxHealth = maxHealth;
        originalMoveSpeed = baseMoveSpeed;
        originalDefense = baseDefense;
        // originalDamage = baseDamage; // 如果有基础伤害，在这里保存
    }

    protected virtual void Start()
    {
        // 初始化当前移动速度（修复游戏开始时移动速度为0的问题）
        currentMoveSpeed = baseMoveSpeed;

        // 初始化事件
        if (OnDeath == null)
            OnDeath = new UnityEvent();

        // 设置阵营边缘颜色
        if (spriteRenderer != null)
        {
            // 创建材质实例
            Material instanceMaterial = new Material(Shader.Find("Custom/SpriteOutline"));
            // 复制原始材质的纹理
            instanceMaterial.mainTexture = spriteRenderer.sprite.texture;
            // 设置边缘颜色
            Color factionColor = faction == Faction.Left ? LEFT_FACTION_TINT : RIGHT_FACTION_TINT;
            instanceMaterial.SetColor("_OutlineColor", factionColor);
            // 设置边缘大小
            instanceMaterial.SetFloat("_OutlineSize", 0.3f);
            // 应用材质
            spriteRenderer.material = instanceMaterial;
            // 保存实例材质
            originalMaterial = instanceMaterial;
        }

        // 初始化时调用一次属性重新计算
        RecalculateStats();

        // 注册到渲染优化系统
        RegisterForRenderingOptimization();

        // 注册到GameManager的清理系统
        RegisterToGameManager();
    }

    protected virtual void OnEnable()
    {
        // 对象池的对象在激活时也需要注册
        if (Application.isPlaying)
        {
            RegisterForRenderingOptimization();
        }
    }


    
    // 受到伤害
    public virtual void TakeDamage(float damage)
    {
        if (IsDead || !gameObject.activeInHierarchy) return;

        // 计算实际伤害
        float actualDamage = Mathf.Max(1f, damage - currentDefense);
        //Debug.Log($"[伤害应用] {gameObject.name} 受到伤害: 原始={damage}, 防御={currentDefense}, 实际={actualDamage}");
        
        currentHealth -= actualDamage;

        // 只有在ShowDamageEffect为true且GameObject激活时才显示受伤效果
        if (ShowDamageEffect && gameObject.activeInHierarchy)
        {
            currentHitFlashCoroutine = StartCoroutine(HitFlashEffect());
        }

        // 触发受伤事件
        OnDamaged?.Invoke(actualDamage);
        OnHealthUpdated?.Invoke(currentHealth, maxHealth); // 触发血量更新事件

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            if (this is HeroUnit)
            {
                // 英雄死亡交由角色控制器处理，不直接调用 Die()
                return;
            }
            Die();
        }
    }
    
    // 受到治疗
    public virtual void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthUpdated?.Invoke(currentHealth, maxHealth); // 触发血量更新事件
    }
    
    // 眩晕
    public virtual void Stun(float duration)
    {
        isStunned = true;
        stunTimeRemaining = duration;
        OnStunned?.Invoke(duration);
        
        // 新增：重置动画状态为待机
        ResetAnimationToIdle();
    }
    
    // 新增：重置动画状态为待机的虚拟方法
    protected virtual void ResetAnimationToIdle()
    {
        // 基类不做具体实现，由子类覆盖
    }
    
    // 移除眩晕
    public virtual void RemoveStun()
    {
        isStunned = false;
        stunTimeRemaining = 0f;
        OnStunEnded?.Invoke();
    }
    
    // 应用速度修改器
    public virtual void ApplySpeedModifier(float modifier)
    {
        speedModifier *= modifier; // 恢复这一行，直接修改速度修改器
        currentMoveSpeed = baseMoveSpeed * speedModifier; // 恢复这一行，直接更新当前速度
        
        // 触发事件以通知其他组件速度已被修改
        //Debug.Log($"{gameObject.name} 速度被修改为 {currentMoveSpeed}（原始速度：{baseMoveSpeed}，修改系数：{speedModifier}）");
    }
    
    // 移除速度修改器
    public virtual void RemoveSpeedModifier(float modifier)
    {
        // 修复：当移除减速效果时，如果modifier是0.5，则应该除以0.5（即乘以2）才能恢复
        // 但为了防止除零错误，先检查modifier是否为0
        if (Mathf.Approximately(modifier, 0f))
        {
            //Debug.LogWarning($"{gameObject.name} 尝试移除速度修改器时，修改器值为0，操作被忽略");
            return;
        }
        
        speedModifier /= modifier; // 除以减速因子，相当于乘以其倒数
        currentMoveSpeed = baseMoveSpeed * speedModifier; // 恢复这一行，直接更新当前速度
        
        // 触发事件以通知其他组件速度已被修改
        //Debug.Log($"{gameObject.name} 速度恢复为 {currentMoveSpeed}（恢复系数：{1/modifier}）");
        
        // 重新计算属性，确保强化系统的增益被正确应用
        RecalculateStats();
    }
    
    // 应用防御力修改器
    public virtual void ApplyDefenseModifier(float modifier)
    {
        defenseModifier *= modifier; // 恢复这一行，直接修改防御修改器
        currentDefense = baseDefense * defenseModifier; // 恢复这一行，直接更新当前防御
        
        // 触发事件以通知其他组件防御已被修改
        //Debug.Log($"{gameObject.name} 防御力被修改为 {currentDefense}（原始防御力：{baseDefense}，修改系数：{defenseModifier}）");
    }
    
    // 移除防御力修改器
    public virtual void RemoveDefenseModifier(float modifier)
    {
        // 修复：当移除减防效果时，如果modifier是0.5，则应该除以0.5（即乘以2）才能恢复
        // 但为了防止除零错误，先检查modifier是否为0
        if (Mathf.Approximately(modifier, 0f))
        {
            //Debug.LogWarning($"{gameObject.name} 尝试移除防御力修改器时，修改器值为0，操作被忽略");
            return;
        }
        
        defenseModifier /= modifier; // 除以减防因子，相当于乘以其倒数
        currentDefense = baseDefense * defenseModifier; // 恢复这一行，直接更新当前防御
        
        // 触发事件以通知其他组件防御已被修改
        //Debug.Log($"{gameObject.name} 防御力恢复为 {currentDefense}（恢复系数：{1/modifier}）");
        
        // 重新计算属性，确保强化系统的增益被正确应用
        RecalculateStats();
    }

    /// <summary>
    /// 根据当前所有累计的强化，重新计算单位的各项属性。
/// 此方法应在任何强化添加或移除时被调用。
    /// </summary>
public void RecalculateStats()
{
    if (GlobalGameUpgrades.Instance == null)
    {
        //Debug.LogError($"Unit {gameObject.name}: GlobalGameUpgrades Instance is null, cannot recalculate stats.");
        return;
    }

    FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(faction);
    if (factionManager == null)
    {
        //Debug.LogError($"Unit {gameObject.name}: 无法获取 {faction} 阵营的强化管理器，无法重新计算属性。");
        return;
    }

    UnitModifiers modifiers = factionManager.GetUnitModifier(Type);

    // 重新计算最大生命值
    float oldMaxHealth = maxHealth; // 保存旧的最大生命值
    float newMaxHealth = (originalMaxHealth + modifiers.healthAdditive) * modifiers.healthMultiplier;
    if (newMaxHealth != oldMaxHealth)
    {
        // 如果最大生命值发生变化，调整当前生命值
        float healthRatio = currentHealth / oldMaxHealth; // 计算当前生命值比例基于旧的最大血量
        maxHealth = newMaxHealth;
        currentHealth = maxHealth * healthRatio; // 根据新最大生命值按比例调整当前生命值
        // Debug.Log($"重新计算 {name} (阵营: {faction}, 类型: {Type}) 生命值: {originalMaxHealth} -> {maxHealth} (加法: {modifiers.healthAdditive}, 乘法: {modifiers.healthMultiplier}).");
    }

    // 重新计算移动速度
    currentMoveSpeed = (originalMoveSpeed + modifiers.moveSpeedAdditive) * modifiers.moveSpeedMultiplier;
    // Debug.Log($"重新计算 {name} (阵营: {faction}, 类型: {Type}) 移动速度: {originalMoveSpeed} -> {currentMoveSpeed} (加法: {modifiers.moveSpeedAdditive}, 乘法: {modifiers.moveSpeedMultiplier}).");

    // 重新计算防御力
    currentDefense = (originalDefense + modifiers.defenseAdditive) * modifiers.defenseMultiplier;
    // Debug.Log($"重新计算 {name} (阵营: {faction}, 类型: {Type}) 防御力: {originalDefense} -> {currentDefense} (加法: {modifiers.defenseAdditive}, 乘法: {modifiers.defenseMultiplier}).");

    // TODO: 如果有伤害属性，也在这里重新计算

    // 触发血量更新事件 (即使maxHealth没有变化，也确保更新UI)
    OnHealthUpdated?.Invoke(currentHealth, maxHealth);
}

/// <summary>
/// 应用单个强化效果到单位属性。此方法将不再直接修改单位属性，而是修改FactionUpgradeManager中的数据，并触发RecalculateStats()
/// </summary>
public virtual void ApplySingleUpgrade(UpgradeDataSO.UnitAttributeType attributeType, float additiveValue, float multiplicativeValue)
{
        if (GlobalGameUpgrades.Instance == null)
        {
        //Debug.LogError($"Unit {gameObject.name}: GlobalGameUpgrades Instance is null, cannot apply single upgrade.");
        return;
    }

    FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(faction);
    if (factionManager == null)
    {
        //Debug.LogError($"Unit {gameObject.name}: 无法获取 {faction} 阵营的强化管理器，无法应用单个强化。");
        return;
    }

    // 将新的强化值应用到FactionUpgradeManager中的Modifier，然后重新计算Stats
    switch (attributeType)
    {
        case UpgradeDataSO.UnitAttributeType.Health:
            if (additiveValue != 0) factionManager.GetUnitModifier(Type).healthAdditive += additiveValue;
            if (multiplicativeValue != 0) {
                UnitModifiers modifier = factionManager.GetUnitModifier(Type);
                modifier.healthMultiplier = 1 + ((modifier.healthMultiplier - 1) + multiplicativeValue);
            }
            break;
        case UpgradeDataSO.UnitAttributeType.MoveSpeed:
            if (additiveValue != 0) factionManager.GetUnitModifier(Type).moveSpeedAdditive += additiveValue;
            if (multiplicativeValue != 0) {
                UnitModifiers modifier = factionManager.GetUnitModifier(Type);
                modifier.moveSpeedMultiplier = 1 + ((modifier.moveSpeedMultiplier - 1) + multiplicativeValue);
            }
            break;
        case UpgradeDataSO.UnitAttributeType.Defense:
            if (additiveValue != 0) factionManager.GetUnitModifier(Type).defenseAdditive += additiveValue;
            if (multiplicativeValue != 0) {
                UnitModifiers modifier = factionManager.GetUnitModifier(Type);
                modifier.defenseMultiplier = 1 + ((modifier.defenseMultiplier - 1) + multiplicativeValue);
            }
            break;
        case UpgradeDataSO.UnitAttributeType.Damage:
            if (additiveValue != 0) factionManager.GetUnitModifier(Type).damageAdditive += additiveValue;
            if (multiplicativeValue != 0) {
                UnitModifiers modifier = factionManager.GetUnitModifier(Type);
                modifier.damageMultiplier = 1 + ((modifier.damageMultiplier - 1) + multiplicativeValue);
            }
            break;
    }

    // 应用强化后，重新计算单位的所有属性
    RecalculateStats();
    }
    
    // 死亡
    protected virtual void Die()
    {
        if (IsDead) return;
        IsDead = true; // 设置为死亡状态
        OnDeath?.Invoke(); // 触发死亡事件
        PerformDeathActions(); // 新增：触发死亡动画和其他死亡相关动作
    }

    /// <summary>
    /// 执行单位死亡后的特定动作，如播放死亡动画。
    /// 这个方法由 Die() 调用。
    /// </summary>
    protected virtual void PerformDeathActions()
    {
        // 具体的死亡动画播放逻辑应该在子类中实现
        // 例如：animator.SetTrigger(DEATH_TRIGGER);
        // 不再在这里设置 IsDead 和 OnDeath，因为 Die() 方法已经处理
    }

    /// <summary>
    /// 复活单位，重置其状态和属性。
    /// </summary>
    public virtual void Respawn()
    {
        IsDead = false; // 不再是死亡状态
        currentHealth = maxHealth; // 恢复满血
        isStunned = false; // 解除眩晕
        stunTimeRemaining = 0f; // 重置眩晕计时
        
        // 重置速度修改器和当前速度
        speedModifier = 1f;
        currentMoveSpeed = baseMoveSpeed;
        
        // 重置防御修改器和当前防御
        defenseModifier = 1f;
        currentDefense = baseDefense;

        // 重新激活SpriteRenderer和Collider，如果它们在死亡时被禁用
        // 使用 GetComponentsInChildren 可以找到对象自身和所有子对象上的组件
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>(true); // 包含非激活的子物体
        foreach (SpriteRenderer sr in srs)
        {
            if (sr != null) sr.enabled = true;
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true); // 包含非激活的子物体
        foreach (Collider2D unitCollider in colliders)
        {
            if (unitCollider != null) unitCollider.enabled = true;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = true; // 重新启用物理模拟

        OnHealthUpdated?.Invoke(currentHealth, maxHealth); // 更新血条UI
        OnResurrectionCompleted?.Invoke(); // 触发复活完成事件
    }

    // 更新眩晕状态
    protected virtual void Update()
    {
        if (isStunned)
        {
            stunTimeRemaining -= Time.deltaTime;
            if (stunTimeRemaining <= 0)
            {
                RemoveStun();
            }
        }
    }

    protected virtual IEnumerator HitFlashEffect()
    {
        if (spriteRenderer == null) yield break;

        // 如果已经有闪烁效果在运行，先停止它
        if (currentHitFlashCoroutine != null)
        {
            StopCoroutine(currentHitFlashCoroutine);
            currentHitFlashCoroutine = null; // 确保置空
        }

        // 保存原始颜色（如果还没有保存）
        if (originalColor == default(Color))
        {
            originalColor = spriteRenderer.color;
        }

        // 设置受伤颜色，但保持alpha值不变
        Color hitColor = hitFlashColor;
        hitColor.a = originalColor.a;
        spriteRenderer.color = hitColor;

        // 等待指定时间
        yield return new WaitForSeconds(hitFlashDuration);

        // 恢复原始颜色
        spriteRenderer.color = originalColor;
        
        // 清除当前协程引用
        currentHitFlashCoroutine = null;
    }

    // 在OnDisable中确保颜色恢复 (这里修正了重复的OnDisable方法)
    protected virtual void OnDisable()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
            // 确保使用正确的材质
            if (spriteRenderer.material != originalMaterial)
            {
                spriteRenderer.material = originalMaterial;
            }
        }

        // 停止所有正在运行的协程，当对象被禁用时
        if (currentHitFlashCoroutine != null)
        {
            StopCoroutine(currentHitFlashCoroutine);
            currentHitFlashCoroutine = null; // 确保置空
        }

        // 对象被禁用时自动注销渲染优化（对象池回收时）
        if (Application.isPlaying)
        {
            UnregisterFromRenderingOptimization();
            UnregisterFromGameManager();
        }
    }

    // 获取当前移动速度
    public float GetCurrentMoveSpeed()
    {
        return currentMoveSpeed * speedModifier;
    }

    // 获取当前防御力
    public float GetCurrentDefense()
    {
        return currentDefense * defenseModifier;
    }

    // 设置速度修改器 (用于外部直接设置，例如减速效果)
    public void SetSpeedModifier(float modifier)
    {
        speedModifier = modifier;
        currentMoveSpeed = baseMoveSpeed * speedModifier;
    }

    // 设置防御力修改器 (用于外部直接设置，例如防御力降低效果)
    public void SetDefenseModifier(float modifier)
    {
        defenseModifier = modifier;
        currentDefense = baseDefense * defenseModifier;
    }

    /// <summary>
    /// 设置单位数据SO并初始化基础属性
    /// </summary>
    /// <param name="baseData">BaseUnitDataSO, HeroBaseDataSO 或 MinionBaseDataSO</param>
    public void SetUnitData(BaseUnitDataSO baseData)
    {
        if (baseData == null)
        {
            //Debug.LogError($"Unit {gameObject.name}: Attempting to set null unit data!");
            return;
        }

        // Debug.Log($"Setting unit data for {gameObject.name} from {baseData.name}");
        // Debug.Log($"Before - maxHealth: {maxHealth}, baseMoveSpeed: {baseMoveSpeed}, baseDefense: {baseDefense}");

        // 保存原始基础属性值
        originalMaxHealth = baseData.baseMaxHealth;
        originalMoveSpeed = baseData.baseMoveSpeed;
        originalDefense = baseData.baseDefense;

        // 设置当前属性值
        maxHealth = originalMaxHealth;
        baseMoveSpeed = originalMoveSpeed;
        baseDefense = originalDefense;

        // 初始化当前属性
        currentHealth = maxHealth;  // 总是设置为最大生命值
        currentMoveSpeed = baseMoveSpeed;
        currentDefense = baseDefense;

        // 重置修改器
        speedModifier = 1f;
        defenseModifier = 1f;

        // Debug.Log($"After - maxHealth: {maxHealth}, baseMoveSpeed: {baseMoveSpeed}, baseDefense: {baseDefense}");

        // 触发血量更新事件
        OnHealthUpdated?.Invoke(currentHealth, maxHealth);
    }

    // 新增：外部安全设置死亡状态的方法
    public void MarkAsDead()
    {
        if (!IsDead)
        {
            IsDead = true;
            OnDeath?.Invoke();
        }
    }

    /// <summary>
    /// 重置单位状态（用于对象池重用）
    /// </summary>
    public virtual void ResetUnit()
    {
        // 重置生命值
        currentHealth = maxHealth;

        // 重置状态
        IsDead = false;
        isStunned = false;
        stunTimeRemaining = 0f;

        // 重置速度和防御修改器
        speedModifier = 1f;
        defenseModifier = 1f;

        // 重新计算当前移动速度和防御力
        currentMoveSpeed = baseMoveSpeed;
        currentDefense = baseDefense;

        // 重新激活组件
        EnableAllComponents();

        // 重置动画状态
        ResetAnimationState();

        // 重新注册到渲染优化系统（对象池重用时需要）
        RegisterForRenderingOptimization();

        // 强制重新创建小地图图标（对象池重用时需要）
        ForceRecreateMinimapIcon();

        // 触发血量更新事件
        OnHealthUpdated?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// 强制重新创建小地图图标
    /// </summary>
    private void ForceRecreateMinimapIcon()
    {
        MagicBattle.MinimapIcon minimapIcon = GetComponent<MagicBattle.MinimapIcon>();
        if (minimapIcon != null)
        {
            // 强制重新创建图标
            minimapIcon.ForceCreateIcon();
        }
    }

    /// <summary>
    /// 启用所有组件
    /// </summary>
    protected virtual void EnableAllComponents()
    {
        // 重新激活渲染器（但不包括小地图图标的渲染器）
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sr in srs)
        {
            if (sr != null && !IsMinimapRenderer(sr))
            {
                sr.enabled = true; // 先启用，渲染优化系统会在后续控制
            }
        }

        // 重新激活碰撞体
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D col in colliders)
        {
            if (col != null) col.enabled = true;
        }

        // 重新激活刚体
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
            rb.velocity = Vector2.zero; // 重置速度
        }
    }

    /// <summary>
    /// 检查是否是小地图渲染器
    /// </summary>
    /// <param name="renderer">要检查的渲染器</param>
    /// <returns>是否是小地图渲染器</returns>
    private bool IsMinimapRenderer(SpriteRenderer renderer)
    {
        if (renderer == null) return false;

        // 检查对象名称
        if (renderer.gameObject.name.Contains("MinimapIcon") ||
            renderer.gameObject.name.Contains("Minimap"))
        {
            return true;
        }

        // 检查是否有MinimapIcon组件
        if (renderer.GetComponent<MagicBattle.MinimapIcon>() != null)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 重置动画状态
    /// </summary>
    protected virtual void ResetAnimationState()
    {
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            // 重置到默认状态
            animator.Play("IDLE", 0, 0f);
            animator.SetBool("IsRunning", false);
            // 可以根据需要重置其他动画参数
        }
    }

    /// <summary>
    /// 返回对象池的方法
    /// </summary>
    public virtual void ReturnToPool()
    {
        // 英雄单位不进入对象池
        if (CompareTag("Hero")) return;

        // 从渲染优化系统中注销
        UnregisterFromRenderingOptimization();

        // 如果对象池管理器存在，返回对象池
        if (MagicBattle.UnitPoolManager.Instance != null)
        {
            MagicBattle.UnitPoolManager.Instance.ReturnToPool(gameObject);
        }
        else
        {
            // 对象池不存在，直接销毁
            Destroy(gameObject);
        }
    }

    // 渲染优化相关
    private bool isRegisteredForOptimization = false;

    /// <summary>
    /// 注册到渲染优化系统
    /// </summary>
    private void RegisterForRenderingOptimization()
    {
        // 只有激活的对象才注册
        if (isRegisteredForOptimization || !gameObject.activeInHierarchy) return;

        // 城堡不参与渲染优化（静态、数量少、总是需要可见）
        if (CompareTag("LeftCastle") || CompareTag("RightCastle"))
        {
            return;
        }

        MagicBattle.ViewportRenderingOptimizer optimizer = MagicBattle.ViewportRenderingOptimizer.Instance;
        if (optimizer != null)
        {
            // 优化器已初始化，直接注册
            optimizer.RegisterUnit(this);
            isRegisteredForOptimization = true;
        }
        else
        {
            // 优化器未初始化，启动延迟注册
            StartCoroutine(DelayedRegisterForRenderingOptimization());
        }
    }

    /// <summary>
    /// 延迟注册到渲染优化系统（用于场景中预存在的单位）
    /// </summary>
    private IEnumerator DelayedRegisterForRenderingOptimization()
    {
        // 等待优化器初始化，最多等待5秒
        float waitTime = 0f;
        while (MagicBattle.ViewportRenderingOptimizer.Instance == null && waitTime < 5f)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        // 如果优化器已初始化且单位仍有效，则注册
        if (MagicBattle.ViewportRenderingOptimizer.Instance != null &&
            this != null && gameObject.activeInHierarchy && !isRegisteredForOptimization)
        {
            MagicBattle.ViewportRenderingOptimizer.Instance.RegisterUnit(this);
            isRegisteredForOptimization = true;
        }
    }

    /// <summary>
    /// 从渲染优化系统中注销
    /// </summary>
    private void UnregisterFromRenderingOptimization()
    {
        // 城堡不参与渲染优化，无需注销
        if (CompareTag("LeftCastle") || CompareTag("RightCastle"))
        {
            return;
        }

        if (!isRegisteredForOptimization) return;

        MagicBattle.ViewportRenderingOptimizer optimizer = MagicBattle.ViewportRenderingOptimizer.Instance;
        if (optimizer != null)
        {
            optimizer.UnregisterUnit(this);
        }

        // 无论优化器是否存在，都重置注册状态
        isRegisteredForOptimization = false;
    }

    protected virtual void OnDestroy()
    {
        // 从渲染优化系统中注销
        UnregisterFromRenderingOptimization();

        // 从GameManager清理系统中注销
        UnregisterFromGameManager();
    }

    /// <summary>
    /// 注册到GameManager的清理系统
    /// </summary>
    private void RegisterToGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterUnit(this);
        }
    }

    /// <summary>
    /// 从GameManager的清理系统中注销
    /// </summary>
    private void UnregisterFromGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterUnit(this);
        }
    }
}