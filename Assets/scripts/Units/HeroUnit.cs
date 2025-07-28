using UnityEngine;
using System.Collections;

/// <summary>
/// 英雄单位类 - 继承自Unit基类
/// </summary>
public class HeroUnit : Unit
{
    private AnimationCtroller knightController;
    private NeController necromancerController;

    [SerializeField] private HeroBaseDataSO heroDataSO; // 新增：引用英雄基础数据SO
    [SerializeField] private HeroHealthBar assignedHealthBar; // 新增：英雄血条UI的引用

    [Header("复活设置")]
    [SerializeField] private float resurrectionTime = 30f; // 英雄复活所需时间
    private Vector3 spawnPoint; // 英雄的出生点
    private Coroutine currentResurrectionCoroutine; // 当前运行的复活协程
    [SerializeField] private Vector3 offScreenPosition = new Vector3(-1000f, -1000f, 0f); // 死亡时移动到的屏幕外位置
    
    // 新增：复活倒计时相关
    private float currentResurrectionTimeRemaining; // 当前剩余复活时间
    public float ResurrectionTimeRemaining => currentResurrectionTimeRemaining; // 公开的剩余复活时间属性
    public bool IsResurrecting { get; private set; } = false; // 是否正在复活过程中
    public System.Action<float, float> OnResurrectionTimeUpdated; // 复活时间更新事件 (当前剩余时间, 总复活时间)

    private Animator animator; // 新增：英雄自身的Animator组件引用
    private const string DEATH_TRIGGER = "Death"; // 新增：死亡动画Trigger参数名

    public override UnitType Type => heroDataSO != null ? heroDataSO.unitType : UnitType.None;

    protected override void Awake()
    {
        base.Awake(); // 只调用基类的 Awake，不做其他初始化
        animator = GetComponent<Animator>(); // 初始化Animator引用
        if (animator == null)
        {
            Debug.LogError($"HeroUnit {gameObject.name}: Animator component not found!");
        }
    }

    protected override void Start()
    {
        // 从 HeroBaseDataSO 初始化基础属性 (如果存在)
        if (heroDataSO != null)
        {
            // Debug.Log($"HeroUnit Start - Initializing {gameObject.name} with {heroDataSO.name}"); // 移除此行
            SetUnitData(heroDataSO);
        }
        else
        {
            Debug.LogError($"HeroUnit {gameObject.name}: heroDataSO is null!");
        }

        base.Start(); // 调用基类的 Start，它会应用全局单位强化
        
        // 获取对应的动画控制器
        knightController = GetComponent<AnimationCtroller>();
        necromancerController = GetComponent<NeController>();

        // 确保血条初始显示正确 (在 Game Manager 分配后调用)
        if (assignedHealthBar != null)
        {
            assignedHealthBar.UpdateHealth(currentHealth, maxHealth);
        }
    }

    private void OnEnable()
    {
        // 订阅血量更新事件
        OnHealthUpdated.AddListener(UpdateAssignedHealthBar);
    }

    protected override void OnDisable()
    {
        base.OnDisable(); // 调用基类的OnDisable
        // 取消订阅，防止内存泄漏
        OnHealthUpdated.RemoveListener(UpdateAssignedHealthBar);
    }

    /// <summary>
    /// 从外部赋值血条UI。
    /// </summary>
    /// <param name="healthBar">要绑定的HeroHealthBar实例。</param>
    public void AssignHealthBar(HeroHealthBar healthBar)
    {
        assignedHealthBar = healthBar;
        if (assignedHealthBar != null)
        {
            assignedHealthBar.UpdateHealth(currentHealth, maxHealth); // 立即更新血条显示
        }
    }

    /// <summary>
    /// 当Unit的血量更新事件触发时调用，用于更新绑定的血条UI。
    /// </summary>
    /// <param name="current">当前血量</param>
    /// <param name="max">最大血量</param>
    private void UpdateAssignedHealthBar(float current, float max)
    {
        if (assignedHealthBar != null)
        {
            assignedHealthBar.UpdateHealth(current, max);
        }
    }

    // ApplyGlobalHeroUpgrades方法已被移除，通用单位强化现在由Unit基类统一处理

    /// <summary>
    /// 重写TakeDamage方法，添加玩家英雄受伤时的屏幕抖动效果
    /// </summary>
    public override void TakeDamage(float damage)
    {
        // 先调用基类的伤害处理逻辑
        base.TakeDamage(damage);

        // 如果是玩家英雄，触发屏幕抖动
        if (IsPlayerHero())
        {
            CameraShakeManager.Instance?.TriggerShake();
        }
    }

    /// <summary>
    /// 判断是否是玩家控制的英雄
    /// </summary>
    /// <returns>true表示是玩家英雄</returns>
    private bool IsPlayerHero()
    {
        // 玩家英雄：左侧阵营 + Hero标签
        return faction == Faction.Left && CompareTag("Hero");
    }

    protected override void Update()
    {
        base.Update();

        // 新增：更新复活倒计时
        if (IsResurrecting)
        {
            currentResurrectionTimeRemaining -= Time.deltaTime;
            OnResurrectionTimeUpdated?.Invoke(currentResurrectionTimeRemaining, resurrectionTime);
        }
    }

    /// <summary>
    /// 启动复活计时，由角色控制器调用
    /// </summary>
    public void StartResurrectionCoroutine()
    {
        if (currentResurrectionCoroutine != null)
        {
            StopCoroutine(currentResurrectionCoroutine);
        }
        currentResurrectionCoroutine = StartCoroutine(ResurrectionCoroutine());
    }

    /// <summary>
    /// 英雄复活协程。
    /// </summary>
    private IEnumerator ResurrectionCoroutine()
    {
        // 初始化复活状态和倒计时
        IsResurrecting = true;
        currentResurrectionTimeRemaining = resurrectionTime;
        OnResurrectionTimeUpdated?.Invoke(currentResurrectionTimeRemaining, resurrectionTime);
        
        OnResurrectionStarted?.Invoke(spawnPoint);
        //Debug.Log($"英雄 {gameObject.name} 死亡，{resurrectionTime}秒后将在 {spawnPoint} 复活。");
        yield return new WaitForSeconds(resurrectionTime);
        
        // 复活完成，重置状态
        IsResurrecting = false;
        base.Respawn();
        transform.position = spawnPoint;
        if (knightController != null) knightController.ResetToIdle();
        if (necromancerController != null) necromancerController.ResetToIdle();
        //Debug.Log($"英雄 {gameObject.name} 已在 {spawnPoint} 复活！");
    }

    // Die 由角色控制器主导
    protected override void Die()
    {
        //Debug.LogWarning($"HeroUnit.Die() 不应被直接调用，请通过角色控制器实现死亡流程。");
    }

    /// <summary>
    /// 设置英雄的出生点，用于复活。
    /// </summary>
    /// <param name="point">出生点位置。</param>
    public void SetSpawnPoint(Vector3 point)
    {
        spawnPoint = point;
    }
    
    /// <summary>
    /// 将英雄标记为活着的状态
    /// </summary>
    public void MarkAsAlive()
    {
        IsDead = false;
        IsResurrecting = false;
        
        // 启用碰撞器和渲染器
        foreach (var collider in GetComponentsInChildren<Collider2D>())
        {
            collider.enabled = true;
        }
        
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = true;
        }
        
        // 如果有Rigidbody2D组件，启用物理模拟
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
        }
        
        //Debug.Log($"英雄 {gameObject.name} 已被标记为活着");
    }
    
    /// <summary>
    /// 重置英雄的生命值到最大
    /// </summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        OnHealthUpdated?.Invoke(currentHealth, maxHealth);
        //Debug.Log($"英雄 {gameObject.name} 生命值已重置为 {currentHealth}/{maxHealth}");
    }

    // 实现Unit基类中的虚方法，在被眩晕时重置动画为待机状态
    protected override void ResetAnimationToIdle()
    {
        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetFloat("speed", 0f);
        }
    }
} 