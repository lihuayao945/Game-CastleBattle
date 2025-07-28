using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // 需要使用 LINQ 进行更方便的集合操作

/// <summary>
/// 牧师控制器 - 寻找受伤友军并在自身周围区域施放治愈技能
/// </summary>
public class PriestController : Unit
{
    // 牧师类型是固定的，直接返回Priest
    public override UnitType Type => UnitType.Priest;

    [Header("牧师属性")]
    [SerializeField] private float detectionRange = 8f; // 检测范围内友军
    [SerializeField] private float healCooldown = 4f; // 治愈冷却时间
    [SerializeField] private float targetSwitchCooldown = 2f; // 目标切换冷却时间 (在没有受伤友军时寻找)
    //[SerializeField] private float targetLockTime = 3f; // 目标锁定时间 (针对同一个受伤友军 - 用于决定何时重新寻找)
    [SerializeField] private float targetLostTime = 1f; // 目标丢失判定时间 (受伤友军治愈完成或离开范围)

    [Header("治愈区域参数")]
    [SerializeField] private float healAreaRadius = 4f; // 治愈区域的半径 (对应 SkillData 中的 areaSize.x)
    [SerializeField] private float minHealthPercentageToHeal = 0.95f; // 低于此生命百分比的友军才会被视为受伤目标

    // 引用 MinionBaseDataSO 来获取基础属性
    [SerializeField] private MinionBaseDataSO minionDataSO; // 新增字段

    // 添加移动和避障参数
    [Header("移动参数")]
    [SerializeField] private float obstacleAvoidanceRadius = 0.6f; // 避障检测半径

    private Animator animator;
    private CharacterSkillManager skillManager; // 用于获取技能数据和触发技能
    private Transform currentTarget; // 当前决定是否施法的参考目标 (最受伤友军)
    private float lastHealTime = -Mathf.Infinity; // 上次治愈时间
    private float lastTargetSwitchTime = -Mathf.Infinity; // 上次切换目标时间
    private bool isCastingHeal = false; // 是否正在施法治愈
    private Rigidbody2D rb;
    private Vector2 forwardDir; // 单位朝向
    
    // 添加治疗状态属性
    public bool IsHealing => isCastingHeal;

    private const string HEAL_TRIGGER = "HealTrigger"; // 治愈动画Trigger参数名 (假设有)
    private const string IS_RUNNING_PARAM = "IsRunning"; // 跑步动画参数名
    private const string DEATH_TRIGGER = "Death"; // 死亡动画参数名

    private float lastTargetSeenTime; // 最后一次看到目标的时间
    private bool isTargetLost = false; // 目标是否丢失 (治愈完成或离开范围)

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
        skillManager = GetComponent<CharacterSkillManager>();
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // 假设牧师原地施法
        }
        // 假设技能层不需要与单位层碰撞
        int skillLayer = LayerMask.NameToLayer("Skill");
        if (skillLayer != -1) // 检查层是否存在
        {
             Physics2D.IgnoreLayerCollision(gameObject.layer, skillLayer, true);
        }


        if (animator == null) Debug.LogError("PriestController: Animator component not found!");
        if (skillManager == null) Debug.LogError("PriestController: CharacterSkillManager component not found!");

        forwardDir = (faction == Faction.Left) ? Vector2.right : Vector2.left;

        // 从 MinionBaseDataSO 初始化基础属性 (如果存在)
        if (minionDataSO != null)
        {
            maxHealth = minionDataSO.baseMaxHealth;
            baseMoveSpeed = minionDataSO.baseMoveSpeed;
            baseDefense = minionDataSO.baseDefense;
            detectionRange = minionDataSO.baseDetectionRange; 
            healAreaRadius = minionDataSO.baseHealAreaRadius;
        }
    }

    protected override void Start()
    {
        base.Start(); // 调用Unit基类的Start方法，它现在会处理通用单位强化

        // 初始化特定小兵数值强化
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(faction);
            if (factionManager != null)
            {
                // 应用牧师特有治疗量强化
                // 注意：这里healAreaRadius在Awake中已从minionDataSO.baseHealAreaRadius初始化
                healAreaRadius = (healAreaRadius + factionManager.priestHealAmountAdditive) * factionManager.priestHealAmountMultiplier;
            }
        }
        
        // 牧师初始待机或寻找目标
        animator.SetBool(IS_RUNNING_PARAM, false);
        rb.velocity = Vector2.zero;
    }


    protected override void Update()
    {
        base.Update();
        if (IsDead || isStunned) return;

        // 目标有效性即时检测
        if (currentTarget != null)
        {
            Unit targetUnit = currentTarget.GetComponent<Unit>();
            if (targetUnit == null || targetUnit.IsDead || !currentTarget.gameObject.activeInHierarchy)
            {
                currentTarget = null;
            }
        }

        // 如果正在施法，不执行其他逻辑
        if (isCastingHeal)
        {
            // 施法期间保持待机动画和不动
            animator.SetBool(IS_RUNNING_PARAM, false);
            rb.velocity = Vector2.zero;
            return;
        }

        // --- 寻找和处理目标逻辑 ---

        // 只有当没有当前目标或者达到目标切换冷却时间时才寻找新目标
        if (currentTarget == null || Time.time - lastTargetSwitchTime >= targetSwitchCooldown)
        {
             Transform injuredAlly = FindMostInjuredAlly();
             if (injuredAlly != null)
             {
                 // 找到受伤友军，设置为当前参考目标
                 currentTarget = injuredAlly;
                 lastTargetSwitchTime = Time.time;
                 lastTargetSeenTime = Time.time;
                 isTargetLost = false;
                //  Debug.Log($"Priest '{gameObject.name}' found new target: '{currentTarget.name}'");
             }
             else
             {
                // 没有找到受伤友军，清除当前目标（如果存在）
                if (currentTarget != null)
                {
                    currentTarget = null;
                    lastTargetSwitchTime = Time.time; // 允许立即寻找
                    // Debug.Log($"Priest '{gameObject.name}': No injured allies found. Clearing target.");
                }
             }
        }

        // 如果当前有参考目标
        if (currentTarget != null)
        {
            Unit targetUnit = currentTarget.GetComponent<Unit>();
            // 检查参考目标是否仍然是友军、活着且生命值低于阈值，并且在检测范围内
            if (targetUnit != null && IsAlly(targetUnit) && !targetUnit.IsDead && targetUnit.currentHealth < targetUnit.maxHealth * minHealthPercentageToHeal)
            {
                 float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
                 if (distanceToTarget <= detectionRange)
                 {
                     // 目标有效且在检测范围内
                     lastTargetSeenTime = Time.time;
                     isTargetLost = false;
                     HandleCurrentTarget(); // 决定是否施法或移动
                     return;
                 }
            }
            // 如果当前参考目标不再有效（已满血，死亡，非友军）或超出检测范围
            // ### 注意：目标满血现在也被视为无效目标
            else
            {
                 if (!isTargetLost) // 第一次丢失目标
                 {
                     isTargetLost = true;
                     lastTargetSeenTime = Time.time;
                    //  Debug.Log($"Priest '{gameObject.name}': Target '{currentTarget.name}' lost (Status/Range/Full Health). Starting lost timer.");
                 }
                 else if (Time.time - lastTargetSeenTime > targetLostTime)
                 {
                     // 目标丢失超过指定时间，清除当前目标，等待寻找新目标
                     currentTarget = null;
                     lastTargetSwitchTime = Time.time; // 允许立即寻找新目标
                    //  Debug.Log($"Priest '{gameObject.name}': Target lost timer expired. Clearing target.");
                 }
            }
        }

        // 如果没有目标或目标丢失，检查检测范围内是否有敌军。
        // 只有在既没有受伤友军也没有敌军的情况下，才向城堡移动。
        if (currentTarget == null)
        {
             Transform nearbyEnemy = FindClosestEnemy(); // 查找最近的敌军

             if (nearbyEnemy == null) // 如果检测范围内没有敌军
             {
                 HandleNoTarget(); // 向城堡移动或待机 (取决于 HandleNoTarget 的内部逻辑)
             }
             else
             {
                 // 检测范围内有敌军，保持待机状态，等待友军受伤或敌军进入治疗范围 (如果未来有攻击敌人的逻辑)
                 animator.SetBool(IS_RUNNING_PARAM, false);
                 rb.velocity = Vector2.zero;
                 FaceTarget((Vector2)transform.position + forwardDir); // 可选：面朝前进方向
                 // 可以添加 Debug.Log 来确认进入了这个状态
                 // Debug.Log($"Priest '{gameObject.name}': No injured allies, but found nearby enemy '{nearbyEnemy.name}'. Staying put.");
             }
        }
    }

    /// <summary>
    /// 寻找范围内生命值最低（最受伤）的友军
    /// </summary>
    /// <returns>最受伤友军的Transform，如果没有符合条件的友军则返回null</returns>
    private Transform FindMostInjuredAlly()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectionRange, LayerMask.GetMask("Unit"));
        
        // 过滤出符合条件的友军：活着、生命值低于阈值、且是友军，排除城堡
        var injuredAllies = colliders
            .Select(col => col.GetComponent<Unit>())
            // 使用公开的 currentHealth 和 maxHealth 进行过滤和排序
            .Where(unit => unit != null 
                && unit != this 
                && IsAlly(unit) 
                && !unit.IsDead 
                && unit.currentHealth < unit.maxHealth * minHealthPercentageToHeal
                && unit.Type != UnitType.LeftCastle && unit.Type != UnitType.RightCastle) // 排除城堡类型
            .ToList();

        if (injuredAllies.Count == 0)
        {
            return null; // 没有符合条件的友军
        }

        // 找到生命值最低的友军并返回其Transform
        Unit mostInjured = injuredAllies.OrderBy(unit => unit.currentHealth).First();

        return mostInjured.transform;
    }


    private void HandleCurrentTarget()
    {
        // 有效的参考目标存在，决定是否施放治愈区域或移动
        FaceTarget(currentTarget.position); // 面朝目标方向

        // 检查最受伤友军是否在治愈区域半径范围内 (以自身为中心)
        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
        if (distanceToTarget <= healAreaRadius) // 使用治愈区域半径判断
        {
            if (isStunned) // 新增：眩晕时不能施法
            {
                animator.SetBool(IS_RUNNING_PARAM, false);
                rb.velocity = Vector2.zero;
                return;
            }
             // 目标在治愈范围内，检查冷却，然后施法
            if (Time.time - lastHealTime >= healCooldown)
            {
                lastHealTime = Time.time;
                StartHeal(); // 触发治愈技能施法
            }
            // 在施法范围内，停止移动，保持待机
            animator.SetBool(IS_RUNNING_PARAM, false);
            rb.velocity = Vector2.zero;
            return;
        }

        // 如果目标在检测范围内但不在治愈范围内，移动到治愈区域的边缘附近
        Vector2 targetPos = currentTarget.position;
        Vector2 currentPos = transform.position;
        
        // 计算目标位置相对于牧师的方向
        Vector2 directionToTarget = (targetPos - currentPos).normalized;
        
        // 计算一个期望的移动位置，使其与目标距离为 healAreaRadius
        // 确保移动位置在目标和牧师之间，位于治愈范围边缘
        Vector2 movePos = targetPos - directionToTarget * healAreaRadius;

        // 调用移动方法（包含避障）
        MoveToHealPosition(movePos);
        // animator.SetBool(IS_RUNNING_PARAM, true); // 播放跑步动画 - 放到MoveToHealPosition内部控制
    }

    private void HandleNoTarget()
    {
        currentTarget = null; // 确保没有目标
        
        // 没目标时直接设置朝向
        if (faction == Faction.Left)
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        
        // 面朝前进方向
        //FaceTarget((Vector2)transform.position + forwardDir);
        
        // 尝试获取敌方城堡前点并移动过去
        Vector2? castleFront = GetCastleFrontPoint();
        if (castleFront.HasValue)
        {
            Vector2 targetCastleApproachPos = castleFront.Value - forwardDir * detectionRange;
            MoveTo(targetCastleApproachPos);
        }
        else
        {
            MoveTo((Vector2)transform.position + forwardDir * 100f);
        }
        animator.SetBool(IS_RUNNING_PARAM, true);
    }

    /// <summary>
    /// 触发治愈技能 (通过动画事件调用)
    /// </summary>
    public void TriggerHealSkill()
    {
        if (IsDead || skillManager == null || currentTarget == null || isStunned) // 新增：眩晕时不能触发技能
        {
             return;
        }
        
        // 假设治愈技能数据是 skillManager.skills 列表中的第一个 (或根据索引查找)
        // 并且这个 SkillData 配置了 SkillBehaviorType.AreaEffect, SkillEffectType.Heal, AreaShape.Circle
        // Area Size.x 应该设置为 healAreaRadius (技能数据中的半径)
        // Effect Value 是治愈量，Heal Effect Prefab 是治愈特效 (在目标位置实例化)，Effect Prefab 是区域视觉特效 (由 SkillFactory 管理)

        // 调用 SkillFactory 创建技能对象，对于目标位置区域治愈，生成位置是目标友军的位置
        if (skillManager.skills.Length > 0)
        {
            // 将技能生成位置改为 currentTarget.position
            SkillFactory.CreateSkill(skillManager.skills[0], transform, currentTarget.position); 
        }
        else
        {
            // Debug.LogError($"Priest '{gameObject.name}': SkillManager has no skills assigned!");
        }
    }

    /// <summary>
    /// 开始治愈施法 (由控制器逻辑调用)
    /// </summary>
    private void StartHeal()
    {
        if (IsDead || skillManager == null || isCastingHeal || isStunned) return; // 新增：眩晕时不能开始施法
        
        isCastingHeal = true;
        animator.SetTrigger(HEAL_TRIGGER); // 播放治愈动画
        // 动画事件TriggerHealSkill() 会在动画的合适帧被调用
    }

    // 这个方法由治愈动画结束事件调用
    public void OnHealAnimationEnd()
    {
        if (IsDead) return;
        isCastingHeal = false;
        // 动画结束后，可以再次检查目标或进入冷却等待
    }

    /// <summary>
    /// 移动到指定位置，并包含简单的避障
    /// </summary>
    private void MoveToHealPosition(Vector2 targetPos)
    {
        Vector2 currentPos = transform.position;
        Vector2 desiredDir = (targetPos - currentPos).normalized;

        // 如果已经非常接近目标位置，停止移动
        if (Vector2.Distance(currentPos, targetPos) < 0.1f) // 设置一个阈值
        {
             animator.SetBool(IS_RUNNING_PARAM, false);
             rb.velocity = Vector2.zero;
             return;
        }

        // 检测周围单位进行避障
        Collider2D[] nearbyUnits = Physics2D.OverlapCircleAll(currentPos, obstacleAvoidanceRadius, 
            LayerMask.GetMask("Unit"));
        
        // 如果有其他单位，计算避让方向
        if (nearbyUnits.Length > 0)
        {
            Vector2 avoidanceDir = Vector2.zero;
            int count = 0;
            
            foreach (Collider2D unit in nearbyUnits)
            {
                // 跳过自己
                if (unit.gameObject == gameObject) continue;
                
                Vector2 toUnit = (Vector2)unit.transform.position - currentPos;
                float distance = toUnit.magnitude;
                
                // 计算避让方向（远离其他单位），距离越近，避让权重越高 (简单的反比实现)
                if (distance < obstacleAvoidanceRadius)
                {
                     float weight = 1f / (distance + 0.001f); // 避免除以零
                     avoidanceDir += -toUnit.normalized * weight;
                     count++;
                }
            }
            
            if (count > 0)
            {
                // 将避让方向与期望方向混合，避让权重可以调整
                // 这里的混合方式可以根据需要调整，例如线性混合或加权混合
                desiredDir = (desiredDir + avoidanceDir.normalized * 0.5f).normalized; // 示例：避让方向权重0.5
            }
        }

        // 应用移动速度
        // 修复帧率问题：使用Time.fixedDeltaTime确保与物理系统同步
        Vector2 movement = desiredDir * currentMoveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(currentPos + movement);

        // 设置跑步动画状态 - 只有实际移动时才播放跑步动画
        animator.SetBool(IS_RUNNING_PARAM, true);
    }

    // 判断目标是否在治愈区域半径范围内 (用于决定是否施法) - 方法名改为 IsTargetInHealArea
    private bool IsTargetInHealArea(Transform target)
    {
        if (target == null) return false;
        // 检查目标是否在以牧师为中心， healAreaRadius 为半径的范围内
        return Vector2.Distance(transform.position, target.position) <= healAreaRadius;
    }

    // 面朝目标 (如果需要) - 方法名保留 FaceTarget
    private void FaceTarget(Vector3 targetPosition)
    {
        if (targetPosition.x > transform.position.x)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else if (targetPosition.x < transform.position.x)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    // Unit 基类中的 IsEnemy 方法已存在，这里不需要重新实现
    // Unit 基类中的 IsAlly 方法已存在，这里不需要重新实现

    /// <summary>
    /// 获取敌方城堡前方的点，用于单位在没有其他目标时移动。
    /// </summary>
    /// <returns>敌方城堡前方的世界坐标点，如果找不到城堡则返回 null。</returns>
    private Vector2? GetCastleFrontPoint()
    {
        // 查找敌方城堡 (假设敌方城堡有特定的Tag)
        string castleTag = faction == Faction.Left ? "RightCastle" : "LeftCastle";
        GameObject castle = GameObject.FindGameObjectWithTag(castleTag);
        if (castle == null)
        {
            // Debug.LogWarning($"Priest '{gameObject.name}': Could not find enemy castle with tag '{castleTag}'.");
            return null;
        }

        BoxCollider2D collider = castle.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
             // 如果城堡没有 BoxCollider2D，直接返回城堡的位置
            //  Debug.LogWarning($"Priest '{gameObject.name}': Enemy castle missing BoxCollider2D. Returning castle position.");
             return (Vector2)castle.transform.position;
        }

        Vector2 castleCenter = collider.bounds.center;
        float castleHeight = collider.bounds.size.y;
        
        // 根据阵营确定城堡前方的X坐标
        float castleX = (faction == Faction.Left) ? collider.bounds.min.x : collider.bounds.max.x;
        
        // 将单位的Y位置限制在城堡的高度范围内，以避免移动到城堡上方或下方太远
        float priestY = Mathf.Clamp(transform.position.y, castleCenter.y - castleHeight / 2f, castleCenter.y + castleHeight / 2f);
        
        return new Vector2(castleX, priestY);
    }

    // OnDrawGizmosSelected 用于在编辑器中显示范围
    void OnDrawGizmosSelected()
    {
        // 绘制检测范围（蓝色圆圈）
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // 绘制治愈区域范围（绿色圆圈）
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healAreaRadius);

        // 绘制避障范围（黄色圆圈）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, obstacleAvoidanceRadius);
    }

    protected override void PerformDeathActions()
    {
        base.PerformDeathActions();

        // 首先停止所有协程，避免冲突
        StopAllCoroutines();

        if (animator != null)
        {
            // 假设死亡动画播放完成后销毁
            animator.SetTrigger(DEATH_TRIGGER);
        }
    }

    // 可能由死亡动画事件调用
    public void DestroySelf()
    {
        // 停止所有协程，立即返回池
        StopAllCoroutines();
        ReturnToPool();
    }

    /// <summary>
    /// 将单位移动到指定的世界坐标点。
    /// </summary>
    /// <param name="targetPos">目标世界坐标点。</param>
    private void MoveTo(Vector2 targetPos)
    {
        // 使用 MoveTowards 平滑移动，速度由 currentMoveSpeed 控制
        // 修复帧率问题：使用Time.fixedDeltaTime确保与物理系统同步
        Vector2 newPos = Vector2.MoveTowards(transform.position, targetPos, currentMoveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
    }

    /// <summary>
    /// 寻找范围内最近的敌军。
    /// </summary>
    /// <returns>最近敌军的Transform，如果没有敌军则返回null。</returns>
    private Transform FindClosestEnemy()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectionRange, LayerMask.GetMask("Unit"));
        float minDistance = float.MaxValue;
        Transform closest = null;

        foreach (var col in colliders)
        {
            Unit unit = col.GetComponent<Unit>();
            // 检查是否是敌军且未死亡
            if (unit != null && unit != this && IsEnemy(unit) && !unit.IsDead)
            {
                float distance = Vector2.Distance(transform.position, col.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = col.transform;
                }
            }
        }

        return closest;
    }

    // 实现Unit基类中的虚方法，在被眩晕时重置动画为待机状态
    protected override void ResetAnimationToIdle()
    {
        if (animator != null)
        {
            animator.SetBool(IS_RUNNING_PARAM, false);
            // Debug.Log($"{gameObject.name} 被眩晕，重置为待机动画");
        }
    }
} 