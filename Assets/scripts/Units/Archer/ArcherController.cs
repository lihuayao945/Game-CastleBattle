using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ArcherController : Unit
{
    // 弓箭手类型是固定的，直接返回Archer
    public override UnitType Type => UnitType.Archer;

    [Header("弓手属性")]
    [SerializeField] private float detectionRange; // 检测范围
    [SerializeField] private float attackCooldown = 1.5f; // 攻击冷却时间
    [SerializeField] private float targetSwitchCooldown = 2f; // 目标切换冷却时间
    [SerializeField] private float targetLostTime = 1f; // 目标丢失判定时间
    [SerializeField] private bool useFullCombo; // 是否使用完整的两段连击
    [SerializeField] private bool usePiercingArrows; // 是否使用穿透箭矢

    [Header("攻击序列参数")]
    [SerializeField] private int[] attackSkillIndices = new int[] { 0, 1 }; // 普通攻击序列对应的技能索引
    [SerializeField] private int[] piercingAttackSkillIndices = new int[] { 2, 3 }; // 穿透攻击序列对应的技能索引
    [SerializeField] private float attackStepInterval = 0.1f; // 每段攻击之间的间隔
    [SerializeField] private float attackSequenceCooldown = 1.5f; // 两段攻击后的冷却

    [Header("流体寻路参数")]
    [SerializeField] private float flowFieldUpdateInterval = 0.1f;
    [SerializeField] private float obstacleAvoidanceRadius = 0.6f; // 避障检测半径
    [SerializeField] private float avoidanceUpdateInterval = 0.05f; // 避障更新间隔，减少GC压力

    [Header("攻击参数")]
    [SerializeField] private float attackBoxLength; // 攻击判定框长度

    // 引用 MinionBaseDataSO 来获取基础属性
    [SerializeField] private MinionBaseDataSO minionDataSO; // 新增字段

    // GC优化：缓存数组和LayerMask，避免频繁内存分配和GetComponent调用
    private Collider2D[] cachedColliders = new Collider2D[20]; // 预分配固定大小数组
    private Unit[] cachedUnits = new Unit[20]; // 缓存Unit组件，避免重复GetComponent
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
    private FlowField currentFlowField;
    private float lastFlowFieldUpdateTime;
    private bool isAttackStepPlaying = false;

    private const string ATTACK_TRIGGER_1 = "AttackTrigger1";
    private const string ATTACK_TRIGGER_2 = "AttackTrigger2";
    private const string IS_RUNNING_PARAM = "IsRunning";
    private const string DEATH_TRIGGER = "Death";

    private float lastTargetSeenTime; // 最后一次看到目标的时间
    private bool isTargetLost = false; // 目标是否丢失

    private Coroutine attackSequenceCoroutine;

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
            Debug.LogError("ArcherController: Animator component not found!");
            enabled = false;
        }
        if (skillManager == null)
        {
            Debug.LogError("ArcherController: CharacterSkillManager component not found!");
            enabled = false;
        }
        forwardDir = (faction == Faction.Left) ? Vector2.right : Vector2.left;

        // 从 MinionBaseDataSO 初始化基础属性 (如果存在)
        if (minionDataSO != null)
        {
            SetUnitData(minionDataSO);
            // 设置弓箭手特有的属性
            detectionRange = minionDataSO.baseDetectionRange;
            attackBoxLength = minionDataSO.baseAttackRange;
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

        // 初始化特定小兵数值和布尔强化
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(faction);
            if (factionManager != null)
            {
                // 应用弓箭手特定的攻击范围强化
                attackBoxLength = (attackBoxLength + factionManager.archerAttackRangeAdditive) * factionManager.archerAttackRangeMultiplier; 

                // 初始化布尔强化
                useFullCombo = factionManager.archerFullComboUnlocked; 
                usePiercingArrows = factionManager.archerPiercingArrowsUnlocked; 
            }
        }
    }

    protected override void Update()
    {
        base.Update();
        if (IsDead || isStunned) return;

        // 目标有效性即时检测 - GC优化：减少重复GetComponent调用
        Unit targetUnit = null;
        if (currentTarget != null)
        {
            targetUnit = currentTarget.GetComponent<Unit>();
            if (targetUnit == null || targetUnit.IsDead || !currentTarget.gameObject.activeInHierarchy)
            {
                currentTarget = null;
                currentFlowField = null;
                StopAttackSequence();
                targetUnit = null; // 重置缓存的Unit引用
            }
        }

        // 如果当前有目标且目标还活着，继续处理当前目标
        if (currentTarget != null && targetUnit != null && !targetUnit.IsDead)
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
        // GC优化：使用缓存数组避免内存分配，缓存Unit组件避免重复GetComponent
        cachedColliderCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            detectionRange,
            cachedColliders,
            unitLayerMask
        );

        float minDistance = float.MaxValue;
        Transform closest = null;
        Transform closestInRange = null;
        float minDistanceInRange = float.MaxValue;

        // 批量获取Unit组件，避免重复GetComponent调用
        for (int i = 0; i < cachedColliderCount; i++)
        {
            Collider2D col = cachedColliders[i];
            if (col == null) continue;

            // 缓存Unit组件，避免重复GetComponent
            Unit unit = col.GetComponent<Unit>();
            cachedUnits[i] = unit;

            if (unit != null && unit != this && IsEnemy(unit) && !unit.IsDead)
            {
                float distance = Vector2.Distance(transform.position, col.transform.position);

                // 检查是否在攻击范围内
                if (IsTargetInAttackRange(col.transform))
                {
                    if (distance < minDistanceInRange)
                    {
                        minDistanceInRange = distance;
                        closestInRange = col.transform;
                    }
                }
                // 如果不在攻击范围内，记录最近的目标
                else if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = col.transform;
                }
            }
        }

        // 优先返回攻击范围内的目标
        return closestInRange != null ? closestInRange : closest;
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

        // 更新流场
        if (Time.time - lastFlowFieldUpdateTime > flowFieldUpdateInterval)
        {
            UpdateFlowField(currentTarget);
            lastFlowFieldUpdateTime = Time.time;
        }

        // 如果目标在攻击范围内，直接攻击
        if (IsTargetInAttackRange(currentTarget))
        {
            if (isStunned) // 新增：眩晕时不能攻击
            {
                StopAttackSequence();
                animator.SetBool(IS_RUNNING_PARAM, false);
                rb.velocity = Vector2.zero;
                return;
            }
            // 检查是否可以开始新的攻击序列
            if (Time.time - lastAttackTime >= attackCooldown && attackSequenceCoroutine == null)
            {
                lastAttackTime = Time.time;
                attackSequenceCoroutine = StartCoroutine(AttackSequence());
            }
            animator.SetBool(IS_RUNNING_PARAM, false);
            rb.velocity = Vector2.zero;
            return;
        }

        // 如果目标不在攻击范围内，计算移动位置
        Vector2 targetPos = currentTarget.position;
        Vector2 currentPos = transform.position;
        Vector2 movePos = currentPos;

        // 计算X方向上的理想距离（攻击范围的95%）
        float idealXDistance = attackBoxLength * 0.95f;
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
        StopAttackSequence();
        currentTarget = null;
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
        Vector2 forwardDirection = faction == Faction.Left ? Vector2.right : Vector2.left;
        MoveTo((Vector2)transform.position + forwardDirection * 100f);
        animator.SetBool(IS_RUNNING_PARAM, true);
    }

    private void StopAttackSequence()
    {
        if (attackSequenceCoroutine != null)
        {
            StopCoroutine(attackSequenceCoroutine);
            attackSequenceCoroutine = null;
            isAttackStepPlaying = false;
        }
    }

    private IEnumerator AttackSequence()
    {
        while (currentTarget != null && IsTargetInAttackRange(currentTarget) && !IsDead && !isStunned) // 在循环条件中添加isStunned检查
        {
            // 第一段攻击
            isAttackStepPlaying = true;
            animator.SetTrigger(ATTACK_TRIGGER_1);
            yield return new WaitUntil(() => !isAttackStepPlaying || isStunned); // 等待动画结束或被眩晕
            
            // 检查目标是否仍然有效或是否被眩晕
            if (currentTarget == null || !IsTargetInAttackRange(currentTarget) || currentTarget.GetComponent<Unit>()?.IsDead == true || isStunned)
            {
                StopAttackSequence();
                yield break;
            }
            
            // 如果使用完整连击，执行第二段攻击
            if (useFullCombo)
            {
                // 等待攻击间隔
                yield return new WaitForSeconds(attackStepInterval);
                isAttackStepPlaying = true;
                animator.SetTrigger(ATTACK_TRIGGER_2);
                yield return new WaitUntil(() => !isAttackStepPlaying || isStunned);
                if (isStunned) // 动画过程中被眩晕，中断当前攻击序列
                {
                    StopAttackSequence();
                    yield break;
                }
            }
            
            // 等待攻击序列冷却
            yield return new WaitForSeconds(attackSequenceCooldown);
        }
        
        // 清理状态
        StopAttackSequence(); // 确保协程最终停止
    }

    // 这个方法由动画事件调用
    public void TriggerAttackDamage(int attackSkillIndex)
    {
        if (IsDead || skillManager == null || currentTarget == null || isStunned) return; // 新增：眩晕时不能造成伤害
        
        // 根据是否使用穿透箭矢选择对应的技能索引数组
        int[] currentSkillIndices = usePiercingArrows ? piercingAttackSkillIndices : attackSkillIndices;
        
        // 确保 CharacterSkillManager 中有对应索引的技能
        if (attackSkillIndex < 0 || attackSkillIndex >= currentSkillIndices.Length)
        {
            Debug.LogError($"ArcherController: Invalid attackSkillIndex {attackSkillIndex}. Max is {currentSkillIndices.Length - 1}");
            return;
        }
        
        // 使用对应的技能索引激活技能
        skillManager.ActivateSkill(currentSkillIndices[attackSkillIndex]);
    }

    // 这个方法由每个攻击动画结束时的动画事件调用
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
        float castleWidth = collider.bounds.size.x;
        float castleHeight = collider.bounds.size.y;
        
        // 计算理想的射击位置（弓箭手应该站在攻击范围的边缘）
        float idealDistance = attackBoxLength * 0.9f; // 在攻击范围的90%处
        float castleX;
        
        // 根据阵营确定城堡的边界和弓箭手应该站的位置
        if (faction == Faction.Left)
        {
            // 左方阵营攻击右边的城堡，站在城堡左侧
            castleX = collider.bounds.min.x - idealDistance;
        }
        else
        {
            // 右方阵营攻击左边的城堡，站在城堡右侧
            castleX = collider.bounds.max.x + idealDistance;
        }
        
        // 在城堡高度范围内选择一个位置，尽量接近弓箭手当前的Y坐标
        float archerY = Mathf.Clamp(transform.position.y, 
                                   castleCenter.y - castleHeight / 2f + 0.5f, 
                                   castleCenter.y + castleHeight / 2f - 0.5f);
        
        // 添加小的随机偏移，避免所有弓箭手都挤在同一位置
        float randomYOffset = Random.Range(-0.5f, 0.5f);
        archerY += randomYOffset;
        
        // 确保Y坐标仍然在城堡高度范围内
        archerY = Mathf.Clamp(archerY, 
                             castleCenter.y - castleHeight / 2f + 0.5f, 
                             castleCenter.y + castleHeight / 2f - 0.5f);
        
        // 检查位置是否被阻挡
        Vector2 attackPos = new Vector2(castleX, archerY);
        if (!IsPositionBlocked(attackPos))
        {
            return attackPos;
        }
        
        // 如果位置被阻挡，尝试在Y轴上寻找可用的位置
        float[] yOffsets = { 0.7f, -0.7f, 1.4f, -1.4f, 2.1f, -2.1f };
        foreach (float offset in yOffsets)
        {
            Vector2 tryPos = new Vector2(castleX, archerY + offset);
            // 确保位置仍在城堡高度范围内
            if (tryPos.y >= castleCenter.y - castleHeight / 2f + 0.5f && 
                tryPos.y <= castleCenter.y + castleHeight / 2f - 0.5f &&
                !IsPositionBlocked(tryPos))
            {
                return tryPos;
            }
        }
        
        // 如果所有位置都被阻挡，尝试稍微远离一点
        float[] distanceOffsets = { 0.5f, 1f, 1.5f };
        foreach (float distOffset in distanceOffsets)
        {
            float adjustedX = faction == Faction.Left ? 
                castleX - distOffset : castleX + distOffset;
                
            Vector2 tryPos = new Vector2(adjustedX, archerY);
            if (!IsPositionBlocked(tryPos))
            {
                return tryPos;
            }
            
            // 同时尝试不同的Y偏移
            foreach (float yOffset in yOffsets)
            {
                tryPos = new Vector2(adjustedX, archerY + yOffset);
                // 确保位置仍在城堡高度范围内
                if (tryPos.y >= castleCenter.y - castleHeight / 2f + 0.5f && 
                    tryPos.y <= castleCenter.y + castleHeight / 2f - 0.5f &&
                    !IsPositionBlocked(tryPos))
                {
                    return tryPos;
                }
            }
        }
        
        // 如果所有尝试都失败，返回原始计算的位置
        return new Vector2(castleX, archerY);
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
        float distanceToTarget = Vector2.Distance(transform.position, targetPos);

        // 如果已经在攻击范围内，不需要移动
        if (IsTargetInAttackRange(target))
        {
            return false;
        }

        // 计算最佳攻击位置（在攻击范围边缘）
        Vector2 directionToTarget = (targetPos - (Vector2)transform.position).normalized;
        attackPos = targetPos - directionToTarget * (attackBoxLength * 0.8f); // 保持在攻击范围的80%处

        // 检查位置是否被阻挡
        if (!IsPositionBlocked(attackPos))
        {
            return true;
        }

        // 如果位置被阻挡，尝试在Y轴上寻找可用的位置
        float[] yOffsets = { 0.5f, -0.5f, 1f, -1f, 1.5f, -1.5f };
        foreach (float offset in yOffsets)
        {
            Vector2 tryPos = attackPos + new Vector2(0, offset);
            if (!IsPositionBlocked(tryPos))
            {
                attackPos = tryPos;
                return true;
            }
        }

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

        // 直接应用移动
        // 修复帧率问题：使用Time.fixedDeltaTime确保与物理系统同步
        Vector2 newPos = currentPos + finalDirection * currentMoveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPos);
    }

    private void UseSkill(int skillIndex)
    {
        if (IsDead || skillManager == null || currentTarget == null || isStunned) return; // 新增：眩晕时不能使用技能
        
        // 确保 CharacterSkillManager 中有对应索引的技能
        if (skillIndex < 0 || skillIndex >= skillManager.skills.Length)
        {
            Debug.LogError($"ArcherController: Invalid skillIndex {skillIndex}. Max is {skillManager.skills.Length - 1}");
            return;
        }
        skillManager.ActivateSkill(skillIndex);
    }

    void OnDrawGizmosSelected()
    {
        // 绘制攻击范围
        Vector2 facing = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        
        // 绘制射线
        Gizmos.color = Color.red;
        Vector2 rayStart = (Vector2)transform.position;
        Vector2 rayEnd = rayStart + facing * attackBoxLength;
        
        // 绘制射线
        Gizmos.DrawLine(rayStart, rayEnd);
        
        // 绘制射线起点（红色圆点）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(rayStart, 0.1f);
        
        // 绘制射线终点（红色圆点）
        Gizmos.DrawWireSphere(rayEnd, 0.1f);
        
        // 绘制射线方向指示（箭头）
        Vector2 arrowSize = new Vector2(0.2f, 0.2f);
        Vector2 arrowPos = rayStart + facing * (attackBoxLength * 0.5f);
        Vector2 arrowDir = facing;
        Vector2 arrowRight = new Vector2(-arrowDir.y, arrowDir.x);
        
        Gizmos.DrawLine(arrowPos, arrowPos + arrowDir * arrowSize.x - arrowRight * arrowSize.y);
        Gizmos.DrawLine(arrowPos, arrowPos + arrowDir * arrowSize.x + arrowRight * arrowSize.y);

        // 绘制检测范围（蓝色圆圈）
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 绘制动态避障范围（黄色圆圈）
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

    private bool IsTargetInAttackRange(Transform target)
    {
        if (target == null) return false;

        // 获取朝向方向
        Vector2 facing = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        
        // 射线起点
        Vector2 rayStart = (Vector2)transform.position;
        
        // 检查目标是否是城堡
        bool isCastle = target.CompareTag(faction == Faction.Left ? "RightCastle" : "LeftCastle");
        
        if (isCastle)
        {
            // 对城堡使用特殊的检测逻辑
            BoxCollider2D castleCollider = target.GetComponent<BoxCollider2D>();
            if (castleCollider != null)
            {
                // 计算到城堡边界的距离
                float distanceToCastle;
                if (faction == Faction.Left)
                {
                    // 左方阵营攻击右边的城堡
                    distanceToCastle = castleCollider.bounds.min.x - transform.position.x;
                }
                else
                {
                    // 右方阵营攻击左边的城堡
                    distanceToCastle = transform.position.x - castleCollider.bounds.max.x;
                }
                
                // 检查城堡是否在攻击范围内
                return distanceToCastle <= attackBoxLength && distanceToCastle > 0;
            }
        }
        
        // 对普通单位使用射线检测
        RaycastHit2D[] hits = Physics2D.RaycastAll(rayStart, facing, attackBoxLength, LayerMask.GetMask("Unit"));
        
        // 检查所有被射线击中的单位
        foreach (RaycastHit2D hit in hits)
        {
            // 如果击中目标，直接返回true
            if (hit.collider.gameObject == target.gameObject)
            {
                return true;
            }
        }
        
        return false;
    }

    // 实现Unit基类中的虚方法，在被眩晕时重置动画为待机状态
    protected override void ResetAnimationToIdle()
    {
        if (animator != null)
        {
            animator.SetBool(IS_RUNNING_PARAM, false);
            //Debug.Log($"{gameObject.name} 被眩晕，重置为待机动画");
        }
    }
} 