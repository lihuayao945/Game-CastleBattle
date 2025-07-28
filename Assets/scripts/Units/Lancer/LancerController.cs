using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LancerController : Unit
{
    // 长枪兵类型是固定的，直接返回Lancer
    public override UnitType Type => UnitType.Lancer;

    [Header("骑兵属性")]
    [SerializeField] private float detectionRange = 12f; // 检测范围改为12
    [SerializeField] private float chargeTriggerRange = 6f; // 冲锋触发距离
    [SerializeField] private float targetSwitchCooldown = 2f;
    [SerializeField] private float targetLostTime = 1f;
    [SerializeField] private float chargeSpeed; // 冲锋速度 // 修改：移除默认值，将在Start中初始化
    [SerializeField] private float chargeDirectionUpdateInterval = 0.2f; // 冲锋方向更新间隔

    [Header("流体寻路参数")]
    [SerializeField] private float flowFieldUpdateInterval = 0.1f;
    [SerializeField] private float attackPositionOffset = 0.75f; // 比士兵更远的攻击位置
    [SerializeField] private float obstacleAvoidanceRadius = 0.8f;

    // 引用 MinionBaseDataSO 来获取基础属性
    [SerializeField] private MinionBaseDataSO minionDataSO; // 新增字段

    private Animator animator;
    private CharacterSkillManager skillManager;
    private Transform currentTarget;
    private float lastTargetSwitchTime = -Mathf.Infinity;
    private int attackCombo = 0;
    private Rigidbody2D rb;
    private Vector2 forwardDir;
    private FlowField currentFlowField;
    private float lastFlowFieldUpdateTime;
    private Vector2? currentAttackPosition;
    private bool isMovingToFront = true;
    private bool isCharging = false;
    private Vector2 chargeDirection;

    // 攻击区域参数
    private float attackBoxWidth = 1.5f;
    private float attackBoxHeight = 1.5f;
    private float attackBoxOffset = 0.75f;

    private const string ATTACK_TRIGGER_1 = "AttackTrigger1";
    private const string ATTACK_TRIGGER_2 = "AttackTrigger2";
    private const string CHARGE_TRIGGER = "ChargeTrigger";
    private const string IS_CHARGING = "IsCharging";
    private const string IS_RUNNING_PARAM = "IsRunning";
    private const string DEATH_TRIGGER = "Death";

    private float attackStepInterval = 0.1f;
    private float attackSequenceCooldown = 2.0f;
    private Coroutine attackSequenceCoroutine;
    private bool isAttackStepPlaying = false;

    private float lastTargetSeenTime;
    private bool isTargetLost = false;

    private float lastChargeDirectionUpdateTime;

    private HashSet<Transform> unreachableTargets = new HashSet<Transform>(); // 记录无法接近的目标

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
        Physics2D.IgnoreLayerCollision(gameObject.layer, skillLayer, true);
        if (animator == null)
        {
            Debug.LogError("LancerController: Animator component not found!");
            enabled = false;
        }
        if (skillManager == null)
        {
            Debug.LogError("LancerController: CharacterSkillManager component not found!");
            enabled = false;
        }
        forwardDir = (faction == Faction.Left) ? Vector2.right : Vector2.left;

        // 从 MinionBaseDataSO 初始化基础属性 (如果存在)
        if (minionDataSO != null)
        {
            SetUnitData(minionDataSO);
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
                chargeSpeed = (minionDataSO != null ? minionDataSO.baseChargeSpeed : chargeSpeed); // 先从SO初始化冲锋速度
                chargeSpeed = (chargeSpeed + factionManager.lancerChargeSpeedAdditive) * factionManager.lancerChargeSpeedMultiplier; // 应用长枪兵冲锋速度强化
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
                currentAttackPosition = null;
                currentFlowField = null;
                StopAttackSequence();
            }
        }

        if (isCharging)
        {
            HandleCharging();
            return;
        }

        // 目标处理逻辑
        if (currentTarget != null)
        {
            Unit targetUnit = currentTarget.GetComponent<Unit>();
            if (targetUnit != null && !targetUnit.IsDead)
            {
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
                    if (!isTargetLost)
                    {
                        isTargetLost = true;
                        lastTargetSeenTime = Time.time;
                    }
                    else if (Time.time - lastTargetSeenTime > targetLostTime)
                    {
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

    private void HandleCharging()
    {
        if (!isCharging) return;

        // 如果目标还在，每0.2秒更新一次冲锋方向
        if (currentTarget != null && Time.time - lastChargeDirectionUpdateTime >= chargeDirectionUpdateInterval)
        {
            chargeDirection = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
            lastChargeDirectionUpdateTime = Time.time;
        }

        // 计算新位置
        Vector2 newPos = (Vector2)transform.position + chargeDirection * chargeSpeed * Time.deltaTime;

        // 检查是否会撞到边界
        if (IsPositionNearMapBoundary(newPos))
        {
            // 撞到边界时停止冲锋
            SetCharging(false, Vector2.zero);
            return;
        }

        // 冲锋移动
        rb.MovePosition(newPos);

        // 确保动画状态正确
        if (animator != null)
        {
            animator.SetBool(IS_CHARGING, true);
            animator.SetBool(IS_RUNNING_PARAM, true);
        }
    }

    public void SetCharging(bool charging, Vector2 direction)
    {
        isCharging = charging;
        if (charging)
        {
            if (isStunned) return;
            chargeDirection = direction;
            lastChargeDirectionUpdateTime = Time.time; // 初始化更新时间
            GetComponent<Collider2D>().isTrigger = true;
            
            if (animator != null)
            {
                animator.SetTrigger(CHARGE_TRIGGER);
                animator.SetBool(IS_CHARGING, true);
                animator.SetBool(IS_RUNNING_PARAM, true);
            }
        }
        else
        {
            GetComponent<Collider2D>().isTrigger = false;
            rb.velocity = Vector2.zero;
            
            if (animator != null)
            {
                animator.SetBool(IS_CHARGING, false);
                animator.SetBool(IS_RUNNING_PARAM, false);
            }

            // 如果目标还在，立即开始攻击
            if (currentTarget != null && IsTargetInAttackBox(currentTarget) && !isStunned)
            {
                if (attackSequenceCoroutine == null)
                {
                    attackSequenceCoroutine = StartCoroutine(AttackSequenceCoroutine());
                }
            }
        }
    }

    private void HandleCurrentTarget()
    {
        FaceTarget(currentTarget.position);

        // 如果正在攻击，不允许移动
        if (isAttackStepPlaying || isCharging)
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

        // 检查目标是否是城堡
        bool isCastle = currentTarget.CompareTag(faction == Faction.Left ? "RightCastle" : "LeftCastle");
        float distanceToTarget;
        
        if (isCastle)
        {
            // 如果是城堡，使用前端位置计算距离
            Vector2? castleFront = GetCastleFrontPoint();
            if (!castleFront.HasValue)
            {
                currentTarget = null;
                return;
            }
            distanceToTarget = Vector2.Distance(transform.position, castleFront.Value);
        }
        else
        {
            // 如果是普通单位，使用当前位置计算距离
            distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
        }

        // 检查目标是否是敌军
        Unit targetUnit = currentTarget.GetComponent<Unit>();
        if (targetUnit != null && IsEnemy(targetUnit))
        {
            // 检查是否可以使用冲锋
            Collider2D targetCollider = currentTarget.GetComponent<Collider2D>();
            float edgeDistance = float.MaxValue;
            if (targetCollider != null)
            {
                Vector2 myPos = (Vector2)transform.position;
                Vector2 closestPoint = targetCollider.ClosestPoint(myPos);
                edgeDistance = Vector2.Distance(myPos, closestPoint);
            }
            if (isStunned)
            {
                animator.SetBool(IS_RUNNING_PARAM, false);
                rb.velocity = Vector2.zero;
                return;
            }
            if (!isCharging && edgeDistance > chargeTriggerRange && edgeDistance <= detectionRange)
            {
                if (skillManager.skills.Length >= 3)
                {
                    Vector2 chargeDir = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
                    skillManager.ActivateSkill(2);
                    return;
                }
                else
                {
                    Debug.LogError("技能数量不足，无法激活冲锋技能");
                }
            }

            // 检查是否在攻击范围内
            if (IsTargetInAttackBox(currentTarget))
            {
                if (isStunned)
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
        }

        // 如果目标在检测范围内但不在攻击范围内，向目标移动
        if (distanceToTarget <= detectionRange)
        {
            // 如果是城堡且距离大于冲锋触发范围，继续向城堡移动
            if (isCastle && distanceToTarget > chargeTriggerRange)
            {
                Vector2? castleFront = GetCastleFrontPoint();
                if (castleFront.HasValue)
                {
                    MoveTo(castleFront.Value);
                    animator.SetBool(IS_RUNNING_PARAM, true);
                    return;
                }
            }

            // 移动到攻击位置
            if (TryGetAttackPosition(currentTarget, out Vector2 attackPos))
            {
                currentAttackPosition = attackPos;
                MoveToAttackPosition(attackPos);
                animator.SetBool(IS_RUNNING_PARAM, true);
            }
            else
            {
                // 将当前目标添加到无法接近的目标集合中
                unreachableTargets.Add(currentTarget);
                
                // 如果无法获取攻击位置，检查是否有其他可攻击的目标
                Transform newTarget = FindClosestEnemy(true); // 排除当前目标
                if (newTarget != null)
                {
                    currentTarget = newTarget;
                    lastTargetSwitchTime = Time.time;
                }
                else
                {
                    // 如果所有目标都无法接近，清空无法接近的目标集合，重新开始尝试
                    unreachableTargets.Clear();
                    currentTarget = null;
                    currentAttackPosition = null;
                    lastTargetSwitchTime = Time.time;
                }
            }
        }
        else
        {
            // 如果目标超出检测范围，重新寻找目标
            Transform newTarget = FindClosestEnemy();
            if (newTarget != null && newTarget != currentTarget)
            {
                currentTarget = newTarget;
                lastTargetSwitchTime = Time.time;
            }
            else
            {
                // 如果找不到新目标，且当前目标超出检测范围，放弃当前目标
                currentTarget = null;
                lastTargetSwitchTime = Time.time;
            }
        }
    }

    private IEnumerator AttackSequenceCoroutine()
    {
        while (currentTarget != null && IsTargetInAttackBox(currentTarget) && !IsDead && !isStunned)
        {
            for (attackCombo = 0; attackCombo < 2; attackCombo++) // 只有两段攻击
            {
                if (currentTarget == null || !IsTargetInAttackBox(currentTarget) || 
                    currentTarget.GetComponent<Unit>()?.IsDead == true || isStunned)
                {
                    attackCombo = 0;
                    StopAttackSequence();
                    yield break;
                }
                isAttackStepPlaying = true;
                TriggerAttackAnimation();
                yield return new WaitUntil(() => !isAttackStepPlaying || isStunned);
                if (isStunned)
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
        StopAttackSequence();
    }

    private void HandleNoTarget()
    {
        StopAttackSequence();
        currentTarget = null;
        currentAttackPosition = null;
        currentFlowField = null;
        unreachableTargets.Clear();
        
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

    private void TriggerAttackAnimation()
    {
        if (IsDead || isStunned) return;
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

    public void TriggerAttackDamage(int attackSkillIndex)
    {
        if (IsDead || skillManager == null || currentTarget == null || isStunned) return;
        
        if (attackSkillIndex < 0 || attackSkillIndex >= skillManager.skills.Length)
        {
            Debug.LogError($"LancerController: Invalid attackSkillIndex {attackSkillIndex}. Max is {skillManager.skills.Length - 1}");
            return;
        }
        skillManager.ActivateSkill(attackSkillIndex);
    }

    public void OnAttackAnimationEnd()
    {
        if (IsDead) return;
        isAttackStepPlaying = false;
    }
    
    protected override void PerformDeathActions()
    {
        base.PerformDeathActions();
        
        // 如果正在冲锋，确保结束冲锋状态
        if (isCharging)
        {
            isCharging = false;
            // 确保Collider不是触发器
            GetComponent<Collider2D>().isTrigger = false;
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
            if (animator != null)
            {
                animator.SetBool(IS_CHARGING, false);
                animator.SetBool(IS_RUNNING_PARAM, false);
            }
        }
        
        // 停止所有协程，包括攻击序列
        StopAllCoroutines();
        
        // 触发死亡动画
        if (animator != null)
        {
            animator.SetTrigger(DEATH_TRIGGER);
        }
    }

    public void DestroySelf()
    {
        // 停止所有协程，立即返回池
        StopAllCoroutines();
        ReturnToPool();
    }

    private bool IsTargetInAttackBox(Transform target)
    {
        if (target == null) return false;
        
        // 检查目标是否是城堡
        bool isCastle = target.CompareTag(faction == Faction.Left ? "RightCastle" : "LeftCastle");
        
        if (isCastle)
        {
            // 对城堡使用特殊的检测逻辑
            BoxCollider2D castleCollider = target.GetComponent<BoxCollider2D>();
            if (castleCollider != null)
            {
                // 获取朝向方向
                Vector2 facing = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
                
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
                // 使用攻击框的参数计算实际攻击距离
                float attackDistance = attackBoxOffset + attackBoxWidth * 0.5f;
                return distanceToCastle <= attackDistance && distanceToCastle > 0;
            }
        }
        
        // 对普通单位使用标准攻击框检测
        Vector2 facingDirection = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        Vector2 boxCenter = (Vector2)transform.position + facingDirection * attackBoxOffset;
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

            // Y方向检查60%范围
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
        float castleWidth = collider.bounds.size.x;
        float castleHeight = collider.bounds.size.y;
        
        // 计算理想的攻击位置（骑兵应该站在攻击范围的边缘）
        float idealDistance = attackBoxOffset + attackBoxWidth * 0.5f; // 攻击判定框的距离
        float castleX;
        
        // 根据阵营确定城堡的边界和骑兵应该站的位置
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
        
        // 在城堡高度范围内选择一个位置，尽量接近骑兵当前的Y坐标
        float lancerY = Mathf.Clamp(transform.position.y, 
                                  castleCenter.y - castleHeight / 2f + 0.5f, 
                                  castleCenter.y + castleHeight / 2f - 0.5f);
        
        // 添加小的随机偏移，避免所有骑兵都挤在同一位置
        float randomYOffset = Random.Range(-0.7f, 0.7f);
        lancerY += randomYOffset;
        
        // 确保Y坐标仍然在城堡高度范围内
        lancerY = Mathf.Clamp(lancerY, 
                            castleCenter.y - castleHeight / 2f + 0.5f, 
                            castleCenter.y + castleHeight / 2f - 0.5f);
        
        // 检查位置是否被阻挡
        Vector2 attackPos = new Vector2(castleX, lancerY);
        if (!IsPositionBlocked(attackPos))
        {
            return attackPos;
        }
        
        // 如果位置被阻挡，尝试在Y轴上寻找可用的位置
        float[] yOffsets = { 0.8f, -0.8f, 1.6f, -1.6f, 2.4f, -2.4f };
        foreach (float offset in yOffsets)
        {
            Vector2 tryPos = new Vector2(castleX, lancerY + offset);
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
                
            Vector2 tryPos = new Vector2(adjustedX, lancerY);
            if (!IsPositionBlocked(tryPos))
            {
                return tryPos;
            }
            
            // 同时尝试不同的Y偏移
            foreach (float yOffset in yOffsets)
            {
                tryPos = new Vector2(adjustedX, lancerY + yOffset);
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
        return new Vector2(castleX, lancerY);
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
        Vector2 targetFacing = target.localScale.x > 0 ? Vector2.right : Vector2.left;
        Vector2 myFacing = transform.localScale.x > 0 ? Vector2.right : Vector2.left;

        if (isMovingToFront)
        {
            Vector2 frontPos = targetPos + targetFacing * attackPositionOffset;
            if (!IsPositionBlocked(frontPos))
            {
                attackPos = frontPos;
                return true;
            }
            isMovingToFront = false;
        }

        Vector2 backPos = targetPos - targetFacing * attackPositionOffset;
        if (!IsPositionBlocked(backPos))
        {
            attackPos = backPos;
            return true;
        }

        isMovingToFront = true;
        return false;
    }

    private bool IsPositionBlocked(Vector2 position)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(position, obstacleAvoidanceRadius*0.5f, LayerMask.GetMask("Unit"));
        foreach (var col in colliders)
        {
            if (col.gameObject == gameObject) continue;
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
                avoidanceDir += -toUnit.normalized; // 直接用负归一化方向
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

    private Transform FindClosestEnemy(bool excludeCurrentTarget = false)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectionRange);
        float minDistance = float.MaxValue;
        Transform closest = null;
        Transform closestInRange = null;
        float minDistanceInRange = float.MaxValue;
        Transform closestUntried = null;
        float minDistanceUntried = float.MaxValue;

        foreach (var col in colliders)
        {
            // 检查是否是城堡
            bool isCastle = col.CompareTag(faction == Faction.Left ? "RightCastle" : "LeftCastle");
            
            // 检查是否是有效的目标（单位或城堡）
            Unit unit = col.GetComponent<Unit>();
            bool isValidTarget = (unit != null && unit != this && IsEnemy(unit) && !unit.IsDead) || isCastle;
            
            if (isValidTarget)
            {
                // 如果需要排除当前目标，且这个单位是当前目标，则跳过
                if (excludeCurrentTarget && col.transform == currentTarget)
                    continue;

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
                // 如果不在攻击判定框内，检查是否是未尝试过的目标
                else if (!unreachableTargets.Contains(col.transform))
                {
                    if (distance < minDistanceUntried)
                    {
                        minDistanceUntried = distance;
                        closestUntried = col.transform;
                    }
                }
                // 如果已经尝试过，记录最近的目标
                else if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = col.transform;
                }
            }
        }

        // 优先返回攻击判定框内的目标，其次返回未尝试过的目标，最后返回已尝试过的目标
        if (closestInRange != null) return closestInRange;
        if (closestUntried != null) return closestUntried;
        return closest;
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

        // 绘制冲锋触发范围
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, chargeTriggerRange);

        // 绘制避障范围
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, obstacleAvoidanceRadius);
    }

    // 实现Unit基类中的虚方法，在被眩晕时重置动画为待机状态
    protected override void ResetAnimationToIdle()
    {
        if (animator != null)
        {
            animator.SetBool(IS_RUNNING_PARAM, false);
            // 如果正在冲锋，也需要重置冲锋状态
            if (isCharging)
            {
                animator.SetBool(IS_CHARGING, false);
                isCharging = false;
            }
            // Debug.Log($"{gameObject.name} 被眩晕，重置为待机动画");
        }
    }

    /// <summary>
    /// 检查位置是否接近地图边界
    /// </summary>
    /// <param name="position">要检查的位置</param>
    /// <returns>是否接近边界</returns>
    private bool IsPositionNearMapBoundary(Vector2 position)
    {
        // 使用与NecromancerAI相同的地图边界参数
        float mapCenterX = 1.24f;
        float mapCenterY = 8.53f;
        float mapWidth = 82.6f;
        float mapHeight = 14.5f;
        float safeMargin = 1.0f;

        float mapMinX = mapCenterX - (mapWidth / 2) + safeMargin;
        float mapMaxX = mapCenterX + (mapWidth / 2) - safeMargin;
        float mapMinY = mapCenterY - (mapHeight / 2) + safeMargin;
        float mapMaxY = mapCenterY + (mapHeight / 2) - safeMargin;

        return position.x < mapMinX || position.x > mapMaxX ||
               position.y < mapMinY || position.y > mapMaxY;
    }
}