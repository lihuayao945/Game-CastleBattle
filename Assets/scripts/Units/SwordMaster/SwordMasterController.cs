using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SwordMasterController : Unit
{
    // 剑术大师类型是固定的，直接返回SwordMaster
    public override UnitType Type => UnitType.SwordMaster;

    [Header("剑术大师属性")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float targetSwitchCooldown = 2f; // 目标切换冷却时间
    [SerializeField] private float targetLostTime = 1f; // 目标丢失判定时间

    [Header("流体寻路参数")]
    [SerializeField] private float flowFieldUpdateInterval = 0.1f;
    [SerializeField] private float attackPositionOffset = 0.60f;// 攻击位置偏移
    [SerializeField] private float obstacleAvoidanceRadius = 0.6f; // 避障检测半径

    // 引用 MinionBaseDataSO 来获取基础属性
    [SerializeField] private MinionBaseDataSO minionDataSO; // 基础属性数据

    // 处理无法到达目标的参数
    private float targetApproachStartTime = 0f;
    private float targetApproachTimeout = 0.2f; // 降低为0.5秒超时，与士兵控制器保持一致
    private HashSet<Transform> unreachableTargets = new HashSet<Transform>();
    private float unreachableTargetResetTime = 5f; // 5秒后重置不可达目标列表
    private float lastUnreachableResetTime = 0f;

    private Animator animator;
    private CharacterSkillManager skillManager;
    private Transform currentTarget;
    private float lastTargetSwitchTime = -Mathf.Infinity;
    private int attackCombo = 0; // 0: 第一段攻击, 1: 第二段攻击
    private Rigidbody2D rb;
    private Vector2 forwardDir;
    private FlowField currentFlowField;
    private float lastFlowFieldUpdateTime;
    private Vector2? currentAttackPosition;
    private bool isMovingToFront = true; // 是否正在前往目标前方

    // 攻击区域参数
    private float attackBoxWidth = 1.2f;
    private float attackBoxHeight = 1.2f;
    private float attackBoxOffset = 0.6f;

    private const string ATTACK_TRIGGER_1 = "AttackTrigger1"; // 第一段攻击动画触发器
    private const string ATTACK_TRIGGER_2 = "AttackTrigger2"; // 第二段攻击动画触发器
    private const string IS_RUNNING_PARAM = "IsRunning";
    private const string DEATH_TRIGGER = "Death";

    private float attackStepInterval = 0.1f; // 每段攻击之间的间隔
    private float attackSequenceCooldown = 1.0f; // 攻击序列后的冷却
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
            Debug.LogError("SwordMasterController: Animator component not found!");
            enabled = false;
        }
        if (skillManager == null)
        {
            Debug.LogError("SwordMasterController: CharacterSkillManager component not found!");
            enabled = false;
        }
        forwardDir = (faction == Faction.Left) ? Vector2.right : Vector2.left;

        // 从 MinionBaseDataSO 初始化基础属性 (如果存在)
        if (minionDataSO != null)
        {
            SetUnitData(minionDataSO); // 使用SetUnitData来初始化基础属性
            attackSequenceCooldown = minionDataSO.baseAttackCooldown;
        }
        
        // 初始化不可达目标重置时间
        lastUnreachableResetTime = Time.time;
    }

    protected override void Start()
    {
        base.Start(); // 调用Unit基类的Start方法，它会处理通用单位强化
    }

    protected override void Update()
    {
        base.Update();
        if (IsDead || isStunned) return;
        
        // 定期重置不可达目标列表
        if (Time.time - lastUnreachableResetTime > unreachableTargetResetTime)
        {
            unreachableTargets.Clear();
            lastUnreachableResetTime = Time.time;
        }
        
        // 目标有效性即时检测
        if (currentTarget != null)
        {
            Unit targetUnit = currentTarget.GetComponent<Unit>();
            if (targetUnit == null || targetUnit.IsDead || !currentTarget.gameObject.activeInHierarchy)
            {
                currentTarget = null;
                currentAttackPosition = null;
                currentFlowField = null;
                StopAttackSequence();
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
    
    // 寻找最近的敌人，排除不可达目标
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
                
            Unit unit = col.GetComponent<Unit>();
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
    
    // 处理当前目标
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
            if (isStunned) // 眩晕时不能攻击
            {
                StopAttackSequence();
                animator.SetBool(IS_RUNNING_PARAM, false);
                rb.velocity = Vector2.zero;
                return;
            }

            if (attackSequenceCoroutine == null)
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
    
    // 移动到攻击位置
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

        // 检测周围单位
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
                
                // 计算避让方向（远离其他单位）
                avoidanceDir += -toUnit.normalized;
                count++;
            }
            
            if (count > 0)
            {
                avoidanceDir /= count;
                // 将避让方向与期望方向混合
                desiredDir = (desiredDir + avoidanceDir).normalized;
            }
        }

        // 直接应用移动，不使用平滑插值
        // 修复帧率问题：使用Time.fixedDeltaTime确保与物理系统同步
        Vector2 newPos = currentPos + desiredDir * currentMoveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPos);
    }
    
    // 处理没有目标的情况
    private void HandleNoTarget()
    {
        StopAttackSequence();
        currentTarget = null;
        currentAttackPosition = null;
        currentFlowField = null;
        
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
        }
    }

    // 攻击序列协程 - 剑术大师的两段攻击
    private IEnumerator AttackSequenceCoroutine()
    {
        while (currentTarget != null && IsTargetInAttackBox(currentTarget) && !IsDead && !isStunned)
        {
            // 剑术大师只有两段攻击
            int maxCombo = 2;
            for (attackCombo = 0; attackCombo < maxCombo; attackCombo++)
            {
                if (currentTarget == null || !IsTargetInAttackBox(currentTarget) || currentTarget.GetComponent<Unit>()?.IsDead == true || isStunned)
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
        if (IsDead || isStunned) return; // 眩晕时不能触发动画
        switch (attackCombo)
        {
            case 0:
                animator.SetTrigger(ATTACK_TRIGGER_1);
                break;
            case 1:
                animator.SetTrigger(ATTACK_TRIGGER_2);
                break;
        }
    }

    // 这个方法由动画事件调用
    // attackSkillIndex 对应 CharacterSkillManager 中的技能列表索引
    public void TriggerAttackDamage(int attackSkillIndex)
    {
        if (IsDead || skillManager == null || currentTarget == null || isStunned) return; // 眩晕时不能造成伤害
        
        // 确保 CharacterSkillManager 中有对应索引的技能
        if (attackSkillIndex < 0 || attackSkillIndex >= skillManager.skills.Length)
        {
            Debug.LogError($"SwordMasterController: Invalid attackSkillIndex {attackSkillIndex}. Max is {skillManager.skills.Length - 1}");
            return;
        }
        skillManager.ActivateSkill(attackSkillIndex);
    }

    // 这个方法由每个攻击动画结束时的动画事件调用
    public void OnAttackAnimationEnd()
    {
        if (IsDead) return;
        isAttackStepPlaying = false;
        Debug.Log("攻击动画结束事件触发");
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
        // 销毁交给死亡动画最后一帧的动画事件DestroySelf()
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
        
        Collider2D targetCollider = target.GetComponent<Collider2D>();
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
            currentFlowField = FlowFieldManager.Instance.GenerateFlowField(target.position, detectionRange);
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
                
                // 根据剑术大师阵营，选择城堡的左侧或右侧
                float castleX = (faction == Faction.Left) ? castleBounds.min.x : castleBounds.max.x;
                
                // 在城堡高度范围内选择一个随机Y位置，避免所有剑术大师都挤在同一点
                float minY = castleBounds.min.y + 0.5f;
                float maxY = castleBounds.max.y - 0.5f;
                float targetY = Mathf.Clamp(transform.position.y, minY, maxY);
                
                // 如果单位数量多，可以在Y轴上添加一些随机偏移
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
        // 检查是否有其他单位（包括友军和敌军）在目标位置
        Collider2D[] colliders = Physics2D.OverlapCircleAll(position, obstacleAvoidanceRadius*0.5f, LayerMask.GetMask("Unit"));
        foreach (var col in colliders)
        {
            // 忽略自己
            if (col.gameObject == gameObject) continue;
            // 忽略当前目标
            if (currentTarget != null && col.gameObject == currentTarget.gameObject) continue;
            return true;
        }
        return false;
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