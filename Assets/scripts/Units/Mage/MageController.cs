using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MageController : Unit
{
    // 法师类型是固定的，直接返回Mage
    public override UnitType Type => UnitType.Mage;

    [Header("法师属性")]
    [SerializeField] private float detectionRange = 10f; // 检测范围
    [SerializeField] private float attackCooldown = 3f; // 攻击冷却时间
    [SerializeField] private float targetSwitchCooldown = 2f; // 目标切换冷却时间
    [SerializeField] private float targetLostTime = 1f; // 目标丢失判定时间

    [Header("攻击参数")]
    [SerializeField] private float attackRange = 8f; // 攻击范围
    //private float attackRadius = 2f; // 攻击区域半径 // 将其改为私有，不再序列化，保留以防万一有其他逻辑依赖

    [Header("移动参数")]
    [SerializeField] private float obstacleAvoidanceRadius = 0.6f; // 避障检测半径
    [SerializeField] private float avoidanceUpdateInterval = 0.05f; // 避障更新间隔，减少GC压力

    // 引用 MinionBaseDataSO 来获取基础属性
    [SerializeField] private MinionBaseDataSO minionDataSO; // 新增字段

    // GC优化：缓存数组和LayerMask，避免频繁内存分配
    private Collider2D[] cachedColliders = new Collider2D[20]; // 预分配固定大小数组
    private int cachedColliderCount = 0;
    private static LayerMask unitLayerMask = -1; // 缓存LayerMask，避免重复计算
    private float lastAvoidanceUpdateTime = 0f;
    private Vector2 cachedAvoidanceDirection = Vector2.zero;

    private Animator animator;
    private CharacterSkillManager skillManager;
    private Transform currentTarget;
    private float lastAttackTime = -Mathf.Infinity;
    private float lastTargetSwitchTime = -Mathf.Infinity;
    private Rigidbody2D rb;
    private Vector2 forwardDir;

    private const string ATTACK_TRIGGER = "AttackTrigger";
    private const string IS_RUNNING_PARAM = "IsRunning";
    private const string DEATH_TRIGGER = "Death";

    private float lastTargetSeenTime; // 最后一次看到目标的时间
    private bool isTargetLost = false; // 目标是否丢失
    private bool isAttackStepPlaying = false;
    
    // 添加施法状态属性
    public bool IsCasting => isAttackStepPlaying;

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
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        int skillLayer = LayerMask.NameToLayer("Skill");
        // 设置与Skill层不碰撞
        Physics2D.IgnoreLayerCollision(gameObject.layer, skillLayer, true);
        if (animator == null)
        {
            Debug.LogError("MageController: Animator component not found!");
            enabled = false;
        }
        if (skillManager == null)
        {
            Debug.LogError("MageController: CharacterSkillManager component not found!");
            enabled = false;
        }
        forwardDir = (faction == Faction.Left) ? Vector2.right : Vector2.left;

        // 从 MinionBaseDataSO 初始化基础属性 (如果存在)
        if (minionDataSO != null)
        {
            SetUnitData(minionDataSO);
            // 设置法师特有的属性
            detectionRange = minionDataSO.baseDetectionRange;
            attackRange = minionDataSO.baseAttackRange;
        }

        // GC优化：初始化LayerMask缓存，避免重复计算
        if (unitLayerMask == -1)
        {
            unitLayerMask = LayerMask.GetMask("Unit");
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
                // 应用法师特有AOE范围强化 (现在应用于attackRange)
                attackRange = (attackRange + factionManager.mageAOERadiusAdditive) * factionManager.mageAOERadiusMultiplier;
            }
        }
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
                // Mage没有currentAttackPosition/FlowField，若有攻击协程可在此终止
            }
        }

        // 如果当前有目标且目标还活着，继续处理当前目标
        if (currentTarget != null)
        {
            Unit targetUnit = currentTarget.GetComponent<Unit>();
            if (targetUnit != null && !targetUnit.IsDead)
            {
                // 检查目标是否在检测范围内
                float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
                if (distanceToTarget <= detectionRange)
                {
                    lastTargetSeenTime = Time.time;
                    isTargetLost = false;
                    HandleCurrentTarget();
                    return;
                }
                else
                {
                    // 目标超出检测范围
                    if (!isTargetLost)
                    {
                        isTargetLost = true;
                        lastTargetSeenTime = Time.time;
                    }
                    else if (Time.time - lastTargetSeenTime > targetLostTime)
                    {
                        // 目标丢失超过指定时间，重新寻找目标
                        currentTarget = null;
                        lastTargetSwitchTime = Time.time;
                    }
                }
            }
        }

        // 寻找新目标
        Transform enemy = FindClosestEnemy();
        if (enemy != null && Time.time - lastTargetSwitchTime >= targetSwitchCooldown)
        {
            // 检查新目标是否比当前目标更近
            if (currentTarget == null || 
                Vector2.Distance(transform.position, enemy.position) < 
                Vector2.Distance(transform.position, currentTarget.position))
            {
                currentTarget = enemy;
                lastTargetSwitchTime = Time.time;
                lastTargetSeenTime = Time.time;
                isTargetLost = false;
            }
        }

        if (currentTarget != null)
        {
            HandleCurrentTarget();
        }
        else
        {
            HandleNoTarget();
        }
    }

    private Transform FindClosestEnemy()
    {
        // GC优化：使用缓存数组避免内存分配
        cachedColliderCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            detectionRange,
            cachedColliders,
            unitLayerMask
        );

        float minDistance = float.MaxValue;
        Transform closest = null;

        for (int i = 0; i < cachedColliderCount; i++)
        {
            Collider2D col = cachedColliders[i];
            if (col == null) continue;

            Unit unit = col.GetComponent<Unit>();
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

    private void HandleCurrentTarget()
    {
        FaceTarget(currentTarget.position);

        // 如果正在攻击，不允许移动
        if (isAttackStepPlaying)
        {
            animator.SetBool(IS_RUNNING_PARAM, false);
            rb.velocity = Vector2.zero;
            return;
        }

        // 如果目标在攻击范围内，直接攻击
        if (IsTargetInAttackRange(currentTarget))
        {
            if (isStunned) // 新增：眩晕时不能攻击
            {
                animator.SetBool(IS_RUNNING_PARAM, false);
                rb.velocity = Vector2.zero;
                return;
            }
            // 检查是否可以开始新的攻击
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                lastAttackTime = Time.time;
                StartAttack();
            }
            animator.SetBool(IS_RUNNING_PARAM, false);
            rb.velocity = Vector2.zero;
            return;
        }

        // 如果目标不在攻击范围内，移动到攻击范围边缘
        Vector2 targetPos = currentTarget.position;
        Vector2 currentPos = transform.position;
        Vector2 movePos = currentPos;

        // 计算X方向上的理想距离（攻击范围的95%）
        float idealXDistance = attackRange * 0.95f;
        Vector2 directionToTarget = (targetPos - currentPos).normalized;
        float currentXDistance = Mathf.Abs(targetPos.x - currentPos.x);

        // 只在距离太远时前进
        if (currentXDistance > idealXDistance)
        {
            movePos.x = targetPos.x - directionToTarget.x * idealXDistance;
        }

        // Y方向直接向目标移动
        movePos.y = targetPos.y;

        MoveToAttackPosition(movePos);
        animator.SetBool(IS_RUNNING_PARAM, true);
    }

    private void HandleNoTarget()
    {
        currentTarget = null;

        // 重置避障缓存，避免影响无目标时的移动
        cachedAvoidanceDirection = Vector2.zero;
        lastAvoidanceUpdateTime = 0f;

        // 没目标时直接设置朝向
        if (faction == Faction.Left)
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        
        // 简单直接：朝对方方向移动
        Vector2 forwardDirection = faction == Faction.Left ? Vector2.right : Vector2.left;
        MoveTo((Vector2)transform.position + forwardDirection * 100f);
        animator.SetBool(IS_RUNNING_PARAM, true);
    }

    private void StartAttack()
    {
        if (IsDead || skillManager == null || currentTarget == null || isStunned) return; // 新增：眩晕时不能开始攻击
        
        isAttackStepPlaying = true;
        animator.SetTrigger(ATTACK_TRIGGER);
    }

    // 这个方法由动画事件调用
    public void TriggerAttackDamage()
    {
        if (IsDead || skillManager == null || currentTarget == null || isStunned) return; // 新增：眩晕时不能造成伤害
        
        // 调用 SkillFactory 创建技能对象
        // 假设 SkillFactory.CreateSkill 方法返回的 GameObject 已经包含了 SkillController 和相应的行为脚本
        SkillFactory.CreateSkill(skillManager.skills[0], transform, currentTarget.position);
    }

    // 这个方法由动画事件调用
    public void OnAttackAnimationEnd()
    {
        if (IsDead) return;
        isAttackStepPlaying = false;
    }

    private void MoveTo(Vector2 targetPos)
    {
        // 修复帧率问题：使用Time.fixedDeltaTime确保与物理系统同步
        Vector2 newPos = Vector2.MoveTowards(transform.position, targetPos, currentMoveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
    }
    
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

    private Vector2? GetCastleFrontPoint()
    {
        // 查找敌方城堡
        string castleTag = faction == Faction.Left ? "RightCastle" : "LeftCastle";
        GameObject castle = GameObject.FindGameObjectWithTag(castleTag);
        if (castle == null) return null;
        BoxCollider2D collider = castle.GetComponent<BoxCollider2D>();
        if (collider == null) return (Vector2)castle.transform.position;
        Vector2 castleCenter = collider.bounds.center;
        float castleHeight = collider.bounds.size.y;
        float castleX = (faction == Faction.Left) ? collider.bounds.min.x : collider.bounds.max.x;
        float mageY = Mathf.Clamp(transform.position.y, castleCenter.y - castleHeight / 2f, castleCenter.y + castleHeight / 2f);
        return new Vector2(castleX, mageY);
    }

    private bool IsTargetInAttackRange(Transform target)
    {
        if (target == null) return false;

        // GC优化：使用缓存数组避免内存分配
        cachedColliderCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            attackRange,
            cachedColliders,
            unitLayerMask
        );

        for (int i = 0; i < cachedColliderCount; i++)
        {
            Collider2D col = cachedColliders[i];
            if (col == null) continue;

            Unit unit = col.GetComponent<Unit>();
            if (unit != null && unit != this && IsEnemy(unit) && !unit.IsDead)
            {
                return true;
            }
        }
        return false;
    }


    private void MoveToAttackPosition(Vector2 targetPos)
    {
        Vector2 currentPos = transform.position;
        Vector2 desiredDir = (targetPos - currentPos).normalized;

        // GC优化：减少避障计算频率，使用缓存
        // 但确保基础移动不受影响
        Vector2 finalDirection = desiredDir; // 默认使用基础方向

        if (Time.time - lastAvoidanceUpdateTime >= avoidanceUpdateInterval)
        {
            // 使用缓存数组检测周围单位，避免内存分配
            cachedColliderCount = Physics2D.OverlapCircleNonAlloc(
                currentPos,
                obstacleAvoidanceRadius,
                cachedColliders,
                unitLayerMask
            );

            // 计算新的避让方向
            Vector2 avoidanceDir = Vector2.zero;
            int count = 0;

            for (int i = 0; i < cachedColliderCount; i++)
            {
                Collider2D unit = cachedColliders[i];
                if (unit == null || unit.gameObject == gameObject) continue;

                Vector2 toUnit = (Vector2)unit.transform.position - currentPos;
                float distance = toUnit.magnitude;

                // 计算避让方向（远离其他单位）
                avoidanceDir += -toUnit.normalized;
                count++;
            }

            if (count > 0)
            {
                cachedAvoidanceDirection = avoidanceDir / count;
            }
            else
            {
                cachedAvoidanceDirection = Vector2.zero;
            }

            lastAvoidanceUpdateTime = Time.time;
        }

        // 应用避障：只有在需要避障时才修改方向
        if (cachedAvoidanceDirection != Vector2.zero)
        {
            finalDirection = (desiredDir + cachedAvoidanceDirection).normalized;
        }

        // 使用最终方向移动

        // 直接应用移动
        // 修复帧率问题：使用Time.fixedDeltaTime确保与物理系统同步
        Vector2 newPos = currentPos + finalDirection * currentMoveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPos);
    }

    void OnDrawGizmosSelected()
    {
        // 绘制攻击范围（红色圆圈）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // 绘制检测范围（蓝色圆圈）
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 绘制避障范围（绿色圆圈）
        Gizmos.color = Color.green;
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
        // 移除禁用刚体和碰撞体的代码，让动画事件负责销毁
    }

    public void DestroySelf()
    {
        // 停止所有协程，立即返回池
        StopAllCoroutines();
        ReturnToPool();
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