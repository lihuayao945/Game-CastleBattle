using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SoldierController : Unit
{
    // 士兵类型是固定的，直接返回Soldier
    public override UnitType Type => UnitType.Soldier;

    [Header("士兵属性")]
    [SerializeField] private float detectionRange = 7f;
    [SerializeField] private float targetSwitchCooldown = 2f; // 目标切换冷却时间
    [SerializeField] private bool useFullCombo; // 是否使用完整的三段连击
    [SerializeField] private bool canBlock; // 新增：是否可以格挡
    [SerializeField] private float blockChance = 0.2f; // 新增：格挡几率
    [SerializeField] private float targetLostTime = 1f; // 目标丢失判定时间

    [Header("流体寻路参数")]
    [SerializeField] private float flowFieldUpdateInterval = 0.1f;
    [SerializeField] private float attackPositionOffset = 0.60f;// 攻击位置偏移
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

    // GC优化：缓存当前目标的Unit组件，避免重复GetComponent
    private Unit cachedTargetUnit = null;

    // GC优化：缓存敌人检测时的Unit组件
    private Dictionary<Transform, Unit> cachedEnemyUnits = new Dictionary<Transform, Unit>();

    // GC优化：缓存目标的Collider2D组件
    private Dictionary<Transform, Collider2D> cachedTargetColliders = new Dictionary<Transform, Collider2D>();

    // 新增：处理无法到达目标的参数
    private float targetApproachStartTime = 0f;
    private float targetApproachTimeout = 0.5f; // 降低为0.8秒超时，原为1.5秒
    private HashSet<Transform> unreachableTargets = new HashSet<Transform>();
    private float unreachableTargetResetTime = 5f; // 5秒后重置不可达目标列表
    private float lastUnreachableResetTime = 0f;

    private Animator animator;
    private CharacterSkillManager skillManager;
    private Transform currentTarget;
    private float lastTargetSwitchTime = -Mathf.Infinity;
    private int attackCombo = 0; // 0: Attack1, 1: Attack2, 2: Attack3
    private Rigidbody2D rb;
    private Vector2 forwardDir;
    private FlowField currentFlowField;
    private float lastFlowFieldUpdateTime;
    private Vector2? currentAttackPosition;
    private bool isMovingToFront = true; // 是否正在前往目标前方

    // 攻击区域参数（可根据实际技能配置调整）
    private float attackBoxWidth = 1.0f;
    private float attackBoxHeight = 1.0f;
    private float attackBoxOffset = 0.5f;

    private const string ATTACK_TRIGGER_1 = "AttackTrigger1";
    private const string ATTACK_TRIGGER_2 = "AttackTrigger2";
    private const string ATTACK_TRIGGER_3 = "AttackTrigger3";
    private const string IS_RUNNING_PARAM = "IsRunning";
    private const string DEATH_TRIGGER = "Death";

    private float attackStepInterval = 0.1f; // 每段攻击之间的间隔
    private float attackSequenceCooldown = 1.5f; // 三段攻击后的冷却
    private Coroutine attackSequenceCoroutine;
    private bool isAttackStepPlaying = false;

    private float lastTargetSeenTime; // 最后一次看到目标的时间
    private bool isTargetLost = false; // 目标是否丢失

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
            Debug.LogError("SoldierController: Animator component not found!");
            enabled = false;
        }
        if (skillManager == null)
        {
            Debug.LogError("SoldierController: CharacterSkillManager component not found!");
            enabled = false;
        }
        // forwardDir将在UpdateForwardDirection中动态设置

        // 从 MinionBaseDataSO 初始化基础属性 (如果存在)
        if (minionDataSO != null)
        {
            SetUnitData(minionDataSO); // 使用SetUnitData来初始化基础属性
            attackSequenceCooldown = minionDataSO.baseAttackCooldown;
        }
        
        // 初始化不可达目标重置时间
        lastUnreachableResetTime = Time.time;

        // GC优化：初始化LayerMask缓存，避免重复计算
        if (unitLayerMask == -1)
        {
            unitLayerMask = LayerMask.GetMask("Unit");
        }
    }

    protected override void Start()
    {
        base.Start(); // 调用Unit基类的Start方法，它现在会处理通用单位强化

        // 初始化特定小兵布尔强化
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(faction);
            if (factionManager != null)
            {
                useFullCombo = factionManager.soldierFullComboUnlocked; // 初始化布尔强化
                canBlock = factionManager.soldierBlockUnlocked; // 新增：初始化格挡强化
            }
        }
    }

    protected override void Update()
    {
        base.Update();
        if (IsDead || isStunned) return;

        // 确保forwardDir正确设置
        UpdateForwardDirection();

        // 新增：定期重置不可达目标列表
        if (Time.time - lastUnreachableResetTime > unreachableTargetResetTime)
        {
            unreachableTargets.Clear();
            lastUnreachableResetTime = Time.time;

            // GC优化：定期清理缓存组件，避免内存泄漏
            cachedEnemyUnits.Clear();
            cachedTargetColliders.Clear();
        }

        // 目标有效性即时检测
        if (currentTarget != null)
        {
            // GC优化：缓存目标Unit组件，避免重复GetComponent
            if (cachedTargetUnit == null || cachedTargetUnit.transform != currentTarget)
            {
                cachedTargetUnit = currentTarget.GetComponent<Unit>();
            }

            if (cachedTargetUnit == null || cachedTargetUnit.IsDead || !currentTarget.gameObject.activeInHierarchy)
            {
                currentTarget = null;
                cachedTargetUnit = null;
                currentAttackPosition = null;
                currentFlowField = null;
                StopAttackSequence();
            }
        }

        // 如果当前有目标且目标还活着，继续处理当前目标
        if (currentTarget != null)
        {
            // 使用已缓存的targetUnit，避免重复GetComponent
            if (cachedTargetUnit != null && !cachedTargetUnit.IsDead)
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
                        cachedTargetUnit = null;
                        lastTargetSwitchTime = Time.time;
                    }
                }
            }
        }

        // 寻找新目标，排除已知不可达的目标
        Transform enemy = FindClosestEnemyExcluding();
        if (enemy != null && Time.time - lastTargetSwitchTime >= targetSwitchCooldown)
        {
            // 检查新目标是否比当前目标更近
            if (currentTarget == null ||
                Vector2.Distance(transform.position, enemy.position) <
                Vector2.Distance(transform.position, currentTarget.position))
            {
                currentTarget = enemy;
                cachedTargetUnit = null; // 清除缓存，下次会重新获取
                lastTargetSwitchTime = Time.time;
                lastTargetSeenTime = Time.time;
                isTargetLost = false;
                targetApproachStartTime = Time.time; // 重置接近计时器
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

    // 修复帧率问题：将移动逻辑移到FixedUpdate中
    void FixedUpdate()
    {
        // 移动逻辑在FixedUpdate中处理，确保与物理系统同步
        // 其他逻辑仍在Update中处理
    }

    private Transform FindClosestEnemyExcluding()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectionRange);
        float minDistance = float.MaxValue;
        Transform closest = null;
        Transform closestInRange = null;
        float minDistanceInRange = float.MaxValue;

        foreach (var col in colliders)
        {
            // 如果这个目标已经被标记为不可达，跳过它
            if (unreachableTargets.Contains(col.transform))
                continue;
                
            // GC优化：缓存Unit组件，避免重复GetComponent
            if (!cachedEnemyUnits.TryGetValue(col.transform, out Unit unit))
            {
                unit = col.GetComponent<Unit>();
                if (unit != null)
                {
                    cachedEnemyUnits[col.transform] = unit;
                }
            }

            if (unit != null && unit != this && IsEnemy(unit) && !unit.IsDead)
            {
                float distance = Vector2.Distance(transform.position, col.transform.position);
                
                // 检查是否在攻击判定框内
                if (IsTargetInAttackBox(col.transform))
                {
                    if (distance < minDistanceInRange)
                    {
                        minDistanceInRange = distance;
                        closestInRange = col.transform;
                    }
                }
                // 如果不在攻击判定框内，记录最近的目标
                else if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = col.transform;
                }
            }
        }

        // 优先返回攻击判定框内的目标
        return closestInRange != null ? closestInRange : closest;
    }

    private void HandleCurrentTarget()
    {
        FaceTarget(currentTarget.position);

        // 更新流场
        if (Time.time - lastFlowFieldUpdateTime > flowFieldUpdateInterval)
        {
            UpdateFlowField(currentTarget);
            lastFlowFieldUpdateTime = Time.time;
        }

        // 检查目标是否是城堡
        bool isCastle = currentTarget.CompareTag(faction == Faction.Left ? "RightCastle" : "LeftCastle");

        // 检查目标是否在攻击判定框内
        if (IsTargetInAttackBox(currentTarget))
        {
            if (isStunned) // 新增：眩晕时不能攻击
            {
                StopAttackSequence();
                animator.SetBool(IS_RUNNING_PARAM, false);
                rb.velocity = Vector2.zero;
                return;
            }

            if (attackSequenceCoroutine == null && gameObject.activeInHierarchy)
            {
                attackSequenceCoroutine = StartCoroutine(AttackSequenceCoroutine());
            }
            animator.SetBool(IS_RUNNING_PARAM, false);
            rb.velocity = Vector2.zero;
            return;
        }

        // 如果目标不在攻击判定框内，尝试获取攻击位置
        if (TryGetAttackPosition(currentTarget, out Vector2 attackPos))
        {
            targetApproachStartTime = Time.time; // 重置接近计时器
            currentAttackPosition = attackPos;
            MoveToAttackPosition(attackPos);
            animator.SetBool(IS_RUNNING_PARAM, true);
        }
        else
        {
            // 检查是否超时
            if (Time.time - targetApproachStartTime > targetApproachTimeout)
            {
                // 如果是城堡且无法接近，不要将其标记为不可达
                if (!isCastle && currentTarget != null)
                {
                    unreachableTargets.Add(currentTarget);
                }
                
                // 寻找新目标，排除已知不可达的目标
                Transform newTarget = FindClosestEnemyExcluding();
                if (newTarget != null && newTarget != currentTarget)
                {
                    currentTarget = newTarget;
                    lastTargetSwitchTime = Time.time;
                    targetApproachStartTime = Time.time; // 重置接近计时器
                }
                else if (isCastle)
                {
                    // 如果当前目标是城堡且无法接近，尝试随机移动一下再尝试
                    Vector2 randomOffset = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * 1.5f;
                    MoveTo((Vector2)transform.position + randomOffset);
                    animator.SetBool(IS_RUNNING_PARAM, true);
                    targetApproachStartTime = Time.time; // 重置接近计时器
                }
                else
                {
                    // 如果找不到可达的目标，尝试前往城堡
                    currentTarget = null;
                    currentAttackPosition = null;
                    lastTargetSwitchTime = Time.time;
                }
            }
        }
    }

    /// <summary>
    /// 更新前进方向，确保阵营设置正确
    /// </summary>
    private void UpdateForwardDirection()
    {
        forwardDir = (faction == Faction.Left) ? Vector2.right : Vector2.left;
    }

    private void HandleNoTarget()
    {
        StopAttackSequence();
        currentTarget = null;
        currentAttackPosition = null;
        currentFlowField = null;

        // 重置避障缓存，避免影响无目标时的移动
        cachedAvoidanceDirection = Vector2.zero;
        lastAvoidanceUpdateTime = 0f;

        // 没目标时直接设置朝向
        if (faction == Faction.Left)
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        // 简单直接：朝对方方向移动
        MoveTo((Vector2)transform.position + forwardDir * 100f);
        animator.SetBool(IS_RUNNING_PARAM, true);
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

    private void StopAttackSequence()
    {
        if (attackSequenceCoroutine != null)
        {
            StopCoroutine(attackSequenceCoroutine);
            attackSequenceCoroutine = null;
            isAttackStepPlaying = false;
            attackCombo = 0;
            // Debug.Log("StopAttackSequence"); // 移除冗余日志
        }
    }

    private IEnumerator AttackSequenceCoroutine()
    {
        while (currentTarget != null && IsTargetInAttackBox(currentTarget) && !IsDead && !isStunned) // 在循环条件中添加isStunned检查
        {
            int maxCombo = useFullCombo ? 3 : 2; // 根据攻击模式决定最大连击数
            for (attackCombo = 0; attackCombo < maxCombo; attackCombo++)
            {
                // GC优化：使用缓存的Unit组件，避免重复GetComponent
                if (currentTarget == null || !IsTargetInAttackBox(currentTarget) || cachedTargetUnit?.IsDead == true || isStunned) // 在循环内部添加isStunned检查
                {
                    attackCombo = 0;
                    StopAttackSequence(); // 确保停止协程
                    yield break;
                }
                isAttackStepPlaying = true;
                TriggerAttackAnimation();
                yield return new WaitUntil(() => !isAttackStepPlaying || isStunned); // 等待动画结束或被眩晕
                if (isStunned) // 动画过程中被眩晕，中断当前攻击序列
                {
                    attackCombo = 0;
                    StopAttackSequence();
                    yield break;
                }
                yield return new WaitForSeconds(attackStepInterval);
            }
            attackCombo = 0;
            yield return new WaitForSeconds(attackSequenceCooldown);
        }
        attackCombo = 0;
        StopAttackSequence(); // 确保协程最终停止
    }

    private void TriggerAttackAnimation()
    {
        if (IsDead || isStunned) return; // 新增：眩晕时不能触发动画
        switch (attackCombo)
        {
            case 0:
                animator.SetTrigger(ATTACK_TRIGGER_1);
                break;
            case 1:
                animator.SetTrigger(ATTACK_TRIGGER_2);
                break;
            case 2:
                animator.SetTrigger(ATTACK_TRIGGER_3);
                break;
        }
    }

    // 这个方法由动画事件调用
    // attackSkillIndex 对应 CharacterSkillManager 中的技能列表索引
    public void TriggerAttackDamage(int attackSkillIndex)
    {
        if (IsDead || skillManager == null || currentTarget == null || isStunned) return; // 新增：眩晕时不能造成伤害
        
        // 确保 CharacterSkillManager 中有对应索引的技能
        if (attackSkillIndex < 0 || attackSkillIndex >= skillManager.skills.Length)
        {
            Debug.LogError($"SoldierController: Invalid attackSkillIndex {attackSkillIndex}. Max is {skillManager.skills.Length - 1}");
            return;
        }
        skillManager.ActivateSkill(attackSkillIndex);
    }

    // 这个方法由每个攻击动画结束时的动画事件调用
    public void OnAttackAnimationEnd()
    {
        if (IsDead) return;
        isAttackStepPlaying = false;
        // Debug.Log("Attack2动画事件触发");
    }
    
    protected override void PerformDeathActions()
    {
        base.PerformDeathActions();

        // 首先停止所有协程，避免冲突
        StopAllCoroutines();

        if (animator != null)
        {
            animator.SetTrigger(DEATH_TRIGGER);
        }

        // 禁用碰撞体和刚体，但不立即销毁
        DisableComponentsForDeath();

        // 开始死亡处理协程
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(HandleDeathSequence());
        }
        else
        {
            // 如果对象已经非激活，直接返回对象池
            ReturnToPool();
        }
    }

    /// <summary>
    /// 禁用死亡时的组件
    /// </summary>
    private void DisableComponentsForDeath()
    {
        // 禁用碰撞体
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in colliders)
        {
            if (col != null) col.enabled = false;
        }

        // 禁用刚体物理模拟
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;
    }

    /// <summary>
    /// 死亡序列处理
    /// </summary>
    private IEnumerator HandleDeathSequence()
    {
        // 等待死亡动画播放完成
        yield return new WaitForSeconds(GetDeathAnimationDuration());

        // 渐隐效果（可选）
        yield return StartCoroutine(FadeOut());

        // 返回对象池或销毁
        ReturnToPool();
    }

    /// <summary>
    /// 获取死亡动画持续时间
    /// </summary>
    private float GetDeathAnimationDuration()
    {
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("Death"))
            {
                return stateInfo.length;
            }
        }
        return 1.0f; // 默认1秒
    }

    /// <summary>
    /// 渐隐效果
    /// </summary>
    private IEnumerator FadeOut()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        float fadeTime = 0.3f;
        float elapsedTime = 0f;
        Color originalColor = sr.color;

        while (elapsedTime < fadeTime)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeTime);
            sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        // 确保完全透明
        sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
    }

    public void DestroySelf()
    {
        // 停止所有协程，立即返回池
        StopAllCoroutines();
        ReturnToPool();
    }

    // 检查目标是否在攻击矩形区域内
    private bool IsTargetInAttackBox(Transform target)
    {
        Vector2 facing = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        Vector2 boxCenter = (Vector2)transform.position + facing * attackBoxOffset;
        Vector2 boxSize = new Vector2(attackBoxWidth, attackBoxHeight);
        
        // GC优化：缓存Collider2D组件，避免重复GetComponent
        if (!cachedTargetColliders.TryGetValue(target, out Collider2D targetCollider))
        {
            targetCollider = target.GetComponent<Collider2D>();
            if (targetCollider != null)
            {
                cachedTargetColliders[target] = targetCollider;
            }
        }
        if (targetCollider == null) return false;

        Bounds targetBounds = targetCollider.bounds;
        
        Vector2 boxMin = boxCenter - boxSize / 2;
        Vector2 boxMax = boxCenter + boxSize / 2;
        
        bool xOverlap = targetBounds.min.x <= boxMax.x && targetBounds.max.x >= boxMin.x;
        bool yOverlap = targetBounds.min.y <= boxMax.y && targetBounds.max.y >= boxMin.y;
        
        if (xOverlap && yOverlap)
        {
            // X方向检查50%范围
            float attackRangeX = boxSize.x * 0.5f;
            float attackBoxMinX = boxCenter.x - attackRangeX / 2;
            float attackBoxMaxX = boxCenter.x + attackRangeX / 2;
            bool inXRange = targetBounds.min.x <= attackBoxMaxX && targetBounds.max.x >= attackBoxMinX;

            // Y方向检查50%范围
            float attackRangeY = boxSize.y * 0.5f;
            float attackBoxMinY = boxCenter.y - attackRangeY / 2;
            float attackBoxMaxY = boxCenter.y + attackRangeY / 2;
            bool inYRange = targetBounds.min.y <= attackBoxMaxY && targetBounds.max.y >= attackBoxMinY;

            // 需要同时满足X和Y的范围要求
            return inXRange && inYRange;
        }
        
        return false;
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
        float soldierY = Mathf.Clamp(transform.position.y, castleCenter.y - castleHeight / 2f, castleCenter.y + castleHeight / 2f);
        return new Vector2(castleX, soldierY);
    }

    private void UpdateFlowField(Transform target)
    {
        if (currentFlowField == null)
        {
            currentFlowField = FlowFieldManager.Instance.GenerateFlowField(target.position, 7f);
        }
    }

    private bool TryGetAttackPosition(Transform target, out Vector2 attackPos)
    {
        attackPos = Vector2.zero;
        Vector2 targetPos = target.position;
        
        // 检查目标是否是城堡
        bool isCastle = target.CompareTag(faction == Faction.Left ? "RightCastle" : "LeftCastle");
        
        if (isCastle)
        {
            // 针对城堡的特殊处理
            BoxCollider2D castleCollider = target.GetComponent<BoxCollider2D>();
            if (castleCollider != null)
            {
                // 获取城堡的边界
                Bounds castleBounds = castleCollider.bounds;
                
                // 根据士兵阵营，选择城堡的左侧或右侧
                float castleX = (faction == Faction.Left) ? castleBounds.min.x : castleBounds.max.x;
                
                // 在城堡高度范围内选择一个随机Y位置，避免所有士兵都挤在同一点
                float minY = castleBounds.min.y + 0.5f;
                float maxY = castleBounds.max.y - 0.5f;
                float targetY = Mathf.Clamp(transform.position.y, minY, maxY);
                
                // 如果士兵数量多，可以在Y轴上添加一些随机偏移
                float randomYOffset = Random.Range(-0.5f, 0.5f);
                targetY = Mathf.Clamp(targetY + randomYOffset, minY, maxY);
                
                // 设置攻击位置
                attackPos = new Vector2(castleX, targetY);
                
                // 检查位置是否被阻挡
                if (!IsPositionBlocked(attackPos))
                {
                    return true;
                }
                
                // 如果位置被阻挡，尝试稍微上下移动
                for (float yOffset = 0.5f; yOffset <= 1.5f; yOffset += 0.5f)
                {
                    // 尝试向上
                    Vector2 upPos = new Vector2(castleX, targetY + yOffset);
                    if (!IsPositionBlocked(upPos))
                    {
                        attackPos = upPos;
                        return true;
                    }
                    
                    // 尝试向下
                    Vector2 downPos = new Vector2(castleX, targetY - yOffset);
                    if (!IsPositionBlocked(downPos))
                    {
                        attackPos = downPos;
                        return true;
                    }
                }
                
                // 所有尝试都失败，返回false
                return false;
            }
            
            // 如果没有碰撞体，使用默认方法
            Vector2? castleFront = GetCastleFrontPoint();
            if (castleFront.HasValue)
            {
                attackPos = castleFront.Value;
                return !IsPositionBlocked(attackPos);
            }
            
            return false;
        }
        
        // 非城堡目标使用原来的逻辑
        Vector2 targetFacing = target.localScale.x > 0 ? Vector2.right : Vector2.left;
        Vector2 myFacing = transform.localScale.x > 0 ? Vector2.right : Vector2.left;

        // 优先尝试目标前方位置
        if (isMovingToFront)
        {
            Vector2 frontPos = targetPos + targetFacing * attackPositionOffset;
            if (!IsPositionBlocked(frontPos))
            {
                attackPos = frontPos;
                return true;
            }
            // 如果前方被阻挡，尝试后方
            isMovingToFront = false;
        }

        // 尝试目标后方位置
        Vector2 backPos = targetPos - targetFacing * attackPositionOffset;
        if (!IsPositionBlocked(backPos))
        {
            attackPos = backPos;
            return true;
        }

        // 如果前后都无法到达，重置状态
        isMovingToFront = true;
        return false;
    }

    private bool IsPositionBlocked(Vector2 position)
    {
        // GC优化：使用缓存数组避免内存分配
        cachedColliderCount = Physics2D.OverlapCircleNonAlloc(
            position,
            obstacleAvoidanceRadius * 0.5f,
            cachedColliders,
            unitLayerMask
        );

        for (int i = 0; i < cachedColliderCount; i++)
        {
            Collider2D col = cachedColliders[i];
            if (col == null) continue;

            // 忽略自己
            if (col.gameObject == gameObject) continue;
            // 忽略当前目标
            if (currentTarget != null && col.gameObject == currentTarget.gameObject) continue;
            return true;
        }
        return false;
    }

    private void MoveToAttackPosition(Vector2 targetPos)
    {
        Vector2 currentPos = transform.position;
        Vector2 desiredDir;

        // 获取基础移动方向（流场或直接朝向目标）
        if (currentFlowField != null)
        {
            Vector2 flowDir = currentFlowField.GetFlowDirection(currentPos);
            if (flowDir != Vector2.zero)
            {
                desiredDir = flowDir;
            }
            else
            {
                desiredDir = (targetPos - currentPos).normalized;
            }
        }
        else
        {
            desiredDir = (targetPos - currentPos).normalized;
        }

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

        // 直接应用移动，不使用平滑插值
        // 修复帧率问题：使用Time.fixedDeltaTime确保与物理系统同步
        Vector2 newPos = currentPos + finalDirection * currentMoveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPos);
    }

    void OnDrawGizmosSelected()
    {
        // 绘制攻击范围
        Gizmos.color = Color.red;
        float width = attackBoxWidth;
        float height = attackBoxHeight;
        float offset = attackBoxOffset;
        Vector2 facing = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        Vector2 boxCenter = (Vector2)transform.position + facing * offset;
        Gizmos.DrawWireCube(boxCenter, new Vector3(width, height, 0));

        // 绘制检测范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 绘制避障范围
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, obstacleAvoidanceRadius);
    }

    /// <summary>
    /// 设置是否使用完整连击
    /// </summary>
    /// <param name="value">是否启用完整连击</param>
    public void SetFullCombo(bool value)
    {
        this.useFullCombo = value;
    }

    public override void TakeDamage(float damage)
    {
        if (canBlock && Random.value < blockChance)
        {
            // Debug.Log($"{gameObject.name} 成功格挡了一次攻击！");
            // 可以在这里触发一个格挡特效或音效
            // 例如: EffectManager.Instance.SpawnEffect("BlockEffect", transform.position);
            return; // 格挡成功，不承受伤害
        }

        base.TakeDamage(damage); // 调用基类的TakeDamage处理伤害
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