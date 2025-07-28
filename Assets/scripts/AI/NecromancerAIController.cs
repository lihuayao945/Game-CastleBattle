using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MagicBattle; // 添加MagicBattle命名空间，包含城堡相关类型

/// <summary>
/// 邪术师AI控制器，负责控制右侧邪术师英雄的移动、攻击和技能释放
/// </summary>
public class NecromancerAIController : MonoBehaviour
{
    // AI难度枚举
    public enum AIDifficulty
    {
        Normal, // 普通难度
        Hard    // 困难难度
    }

    // AI状态枚举
    public enum AIState
    {
        Advance,    // 推进状态
        Engage,     // 交战状态
        Retreat,    // 撤退状态
        Dead        // 死亡状态
    }

    // AI子状态枚举
    public enum AISubState
    {
        None,           // 无子状态
        Pathfinding,    // 寻路子状态
        MoveForward,    // 前进子状态
        Approach,       // 接近子状态
        Positioning,    // 定位子状态
        Attack,         // 攻击子状态
        FindSafePosition, // 寻找安全位置子状态
        Healing         // 治疗子状态
    }

    [Header("AI Settings")]
    [SerializeField] private AIDifficulty difficulty = AIDifficulty.Normal;
    
    [Header("State Parameters")]
    [SerializeField] private AIState currentState = AIState.Advance;
    [SerializeField] private AISubState currentSubState = AISubState.None;
    
    [Header("Decision Making")]
    [Tooltip("AI决策的反应时间，值越小AI反应越快")]
    [SerializeField] private float reactionTime = 0.5f;        // 反应时间
    
    [Header("Combat Parameters")]
    [SerializeField] private float targetEvaluationAccuracy = 0.8f; // 目标评估准确度
    //[SerializeField] private float positioningPrecision = 0.7f;    // 位置选择精确度
    [SerializeField] private float retreatThreshold = 0.3f;        // 撤退血量阈值（默认普通难度30%）
    [SerializeField] private float safeHealthThreshold = 0.6f;     // 安全血量阈值（默认普通难度60%）
    [SerializeField] private float idealCastingRange = 10f;         // 理想施法距离，调整为11以保持安全距离
    
    [Header("流场寻路参数")]
    [SerializeField] private float flowFieldUpdateInterval = 0.1f; // 流场更新间隔
    [SerializeField] private float obstacleAvoidanceRadius = 1.0f; // 避障检测半径
    [SerializeField] private float obstacleDetectionOffset = 0.3f; // 避障检测向下偏移量
    
    // 组件引用
    private HeroUnit heroUnit;                  // 英雄单位组件
    private CharacterSkillManager skillManager; // 技能管理器
    private Rigidbody2D rb;                     // 刚体组件
    private Unit currentTarget;                 // 当前目标
    
    // 状态标志
    private bool isActive = false;              // AI是否激活
    private bool isCasting = false;             // 是否正在施法
    private bool hasSafePosition = false;       // 是否有安全位置
    private Vector2 safePosition;               // 安全位置
    
    // 内部计时器
    private float decisionTimer = 0f;           // 决策计时器
    
    // 敌方英雄类型
    private HeroType enemyHeroType = HeroType.Knight;  // 默认为骑士
    
    // 回血计时器
    private float healTimer = 0f;
    
    // 英雄类型枚举
    public enum HeroType
    {
        Knight,
        Necromancer
    }
    
    // 在类变量区域添加新的位置判断阈值和冷却时间
    private float lastPositionChangeTime = 0f;        // 上次改变位置的时间
    private float positionChangeCooldown = 1.0f;      // 位置变更的冷却时间（减少到1秒，更频繁调整位置）
    private float attackPositionTolerance = 2.0f;     // 攻击位置容差，减小以提高定位精确度
    
    // 流场寻路相关字段
    private FlowField currentFlowField;               // 当前流场
    private float lastFlowFieldUpdateTime = 0f;       // 上次流场更新时间
    
    // 开局等待时间逻辑已移除
    
    // 添加防止发呆的变量
    private float lastActionTime = 0f;         // 上次行动时间
    private float idleDetectionTime = 3.0f;    // 发呆检测时间阈值（减少到3秒，更快检测到卡住状态）
    private Vector2 lastPosition;              // 上次位置
    private bool isIdleDetectionActive = false; // 是否激活发呆检测
    
    /// <summary>
    /// 初始化AI控制器
    /// </summary>
    /// <param name="difficulty">AI难度</param>
    public void Initialize(AIDifficulty difficulty)
    {
        // 设置AI难度
        this.difficulty = difficulty;
        
        // 获取组件引用
        heroUnit = GetComponent<HeroUnit>();
        skillManager = GetComponent<CharacterSkillManager>();
        rb = GetComponent<Rigidbody2D>();
        
        // 如果是困难模式，修改参数
        if (difficulty == AIDifficulty.Hard)
        {
            retreatThreshold = 0.5f;  // 50%血量撤退（困难模式）
            safeHealthThreshold = 0.8f;  // 80%血量恢复安全状态（困难模式）
            reactionTime = 0.05f; // 缩短反应时间
        }
        else
        {
            retreatThreshold = 0.3f;  // 30%血量撤退（普通模式）
            safeHealthThreshold = 0.6f;  // 60%血量恢复安全状态（普通模式）
            reactionTime = 0.3f; // 保持较长反应时间
        }
        
        // 初始化状态
        currentState = AIState.Advance;
        currentSubState = AISubState.Pathfinding;
        
        // 开局等待逻辑已移除，邪术师立即行动
        
        // 激活AI
        isActive = true;
        
        //Debug.Log($"邪术师AI初始化完成，难度：{difficulty}，撤退阈值：{retreatThreshold}，安全阈值：{safeHealthThreshold}");
        
        // 检测敌方英雄类型
        StartCoroutine(DetectEnemyHeroType());
        
        // 初始化防呆机制
        lastActionTime = Time.time;
        lastPosition = transform.position;
        isIdleDetectionActive = true;
    }
    
    private void Start()
    {
        // 获取必要组件
        heroUnit = GetComponent<HeroUnit>();
        skillManager = GetComponent<CharacterSkillManager>();
        rb = GetComponent<Rigidbody2D>();
        
        // 确保组件存在
        if (heroUnit == null)
        {
            Debug.LogError("NecromancerAIController: 无法找到HeroUnit组件");
            enabled = false;
            return;
        }
        
        if (skillManager == null)
        {
            Debug.LogError("NecromancerAIController: 无法找到CharacterSkillManager组件");
            enabled = false;
            return;
        }
        
        if (rb == null)
        {
            Debug.LogError("NecromancerAIController: 无法找到Rigidbody2D组件");
            enabled = false;
            return;
        }
        
        // 调整理想施法距离为更合理的值，减少后退频率
        idealCastingRange = 10f;
        
        // 启用发呆检测机制，防止AI卡住
        isIdleDetectionActive = true;
        lastPosition = transform.position;
        lastActionTime = Time.time;
        
        // 初始化状态
        ChangeState(AIState.Advance);
    }
    
    /// <summary>
    /// 检测敌方英雄类型
    /// </summary>
    private IEnumerator DetectEnemyHeroType()
    {
        // 等待几帧，确保所有单位都已经生成
        yield return new WaitForSeconds(1f);
        
        // 查找所有英雄单位 - 使用GameManager的注册系统
        HeroUnit[] heroes = GetRegisteredHeroes();
        
        foreach (HeroUnit hero in heroes)
        {
            // 如果是左侧阵营的英雄
            if (hero.faction == Unit.Faction.Left)
            {
                // 根据英雄类型设置
                if (hero.Type == Unit.UnitType.Necromancer)
                {
                    enemyHeroType = HeroType.Necromancer;
                    //Debug.Log("检测到敌方英雄类型：邪术师");
                }
                else
                {
                    enemyHeroType = HeroType.Knight;
                    //Debug.Log("检测到敌方英雄类型：骑士");
                }
                break;
            }
        }
    }
    
    /// <summary>
    /// 每帧更新
    /// </summary>
    private void Update()
    {
        // 如果AI未激活或游戏已暂停，不执行AI逻辑
        if (!isActive || Time.timeScale == 0)
            return;
            
        // 开局等待逻辑已移除，邪术师立即行动
        // hasWaitedAtStart 现在总是为 true
            
        // 如果英雄已死亡，切换到死亡状态
        if (heroUnit != null && heroUnit.IsDead && currentState != AIState.Dead)
        {
            ChangeState(AIState.Dead);
            return;
        }
        
        // 如果AI不处于死亡状态，但英雄单位生命值为0，触发死亡
        // 这行是新增的，确保即使在眩晕状态下也能检测死亡
        if (currentState != AIState.Dead && heroUnit != null && heroUnit.currentHealth <= 0)
        {
            Die();
            return;
        }
        
        // 决策计时器
        decisionTimer += Time.deltaTime;
        
        // 如果单位被眩晕，不执行行为逻辑
        if (heroUnit != null && heroUnit.isStunned)
        {
            // 被眩晕时停止移动
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
            return;
        }
        
        // 如果计时器达到反应时间，执行决策
        if (decisionTimer >= reactionTime)
        {
            // 执行当前状态
            // 注意：ExecuteCurrentState已经包含了对施法状态的检查
            ExecuteCurrentState();
            
            // 重置计时器
            decisionTimer = 0f;
        }
        
        // 如果正在施法，确保不移动
        if (isCasting)
        {
            // 强制停止移动
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
            return;
        }
        
        // 检查是否需要切换状态
        UpdateStateMachine();
        
        // 检测AI是否发呆
        DetectAndHandleIdleState();
        
        // 添加眩晕检测逻辑
        CheckAndHandleStunState();
    }
    
    /// <summary>
    /// 更新主状态机
    /// </summary>
    private void UpdateStateMachine()
    {
        // 如果单位已死亡，切换到死亡状态
        if (heroUnit != null && heroUnit.IsDead && currentState != AIState.Dead)
        {
            ChangeState(AIState.Dead);
            return;
        }
        
        // 如果AI不处于死亡状态，但英雄单位生命值为0，触发死亡
        if (currentState != AIState.Dead && heroUnit != null && heroUnit.currentHealth <= 0)
        {
            Die();
            return;
        }
        
        // 以下是原有的状态切换逻辑
        // 获取当前血量百分比
        float healthPercentage = heroUnit.currentHealth / heroUnit.maxHealth;
        
        switch (currentState)
        {
            case AIState.Advance:
                // 发现可攻击目标时，切换到Engage状态
                if (HasAttackableTarget())
                {
                    ChangeState(AIState.Engage);
                }
                break;
                
            case AIState.Engage:
                // 目标消失或死亡，且没有其他可攻击目标时，切换回Advance状态
                if (!HasAttackableTarget())
                {
                    ChangeState(AIState.Advance);
                }
                // 血量低于阈值时，切换到Retreat状态
                else if (healthPercentage < retreatThreshold)
                {
                    ChangeState(AIState.Retreat);
                }
                break;
                
            case AIState.Retreat:
                // 血量恢复到安全阈值，且有可攻击目标时，切换回Engage状态
                if (healthPercentage >= safeHealthThreshold && HasAttackableTarget())
                {
                    ChangeState(AIState.Engage);
                }
                // 血量恢复到安全阈值，但没有可攻击目标时，切换回Advance状态
                else if (healthPercentage >= safeHealthThreshold)
                {
                    ChangeState(AIState.Advance);
                }
                break;
                
            case AIState.Dead:
                // 死亡状态不做状态转换
                break;
        }
    }
    
    /// <summary>
    /// 执行当前状态的行为
    /// </summary>
    private void ExecuteCurrentState()
    {
        // 如果AI未激活或角色死亡，不执行行为
        if (!isActive || heroUnit.IsDead)
            return;
            
        // 如果正在施法，不执行其他行为
        if (isCasting)
            return;
            
        // 如果被眩晕，不执行行为
        if (heroUnit.isStunned)
            return;
            
        // 根据当前状态执行不同行为
        switch (currentState)
        {
            case AIState.Advance:
                ExecuteAdvanceState();
                break;
            case AIState.Engage:
                ExecuteEngageState();
                break;
            case AIState.Retreat:
                ExecuteRetreatState();
                break;
            case AIState.Dead:
                // 死亡状态不执行任何行为
                break;
        }
    }
    
    /// <summary>
    /// 切换AI状态
    /// </summary>
    /// <param name="newState">新状态</param>
    private void ChangeState(AIState newState)
    {
        // 退出当前状态
        ExitState(currentState);
        
        // 设置新状态
        currentState = newState;
        
        // 进入新状态
        EnterState(newState);
        
        // 记录状态变化
                    //Debug.Log($"AI状态变化: {currentState}");
    }
    
    /// <summary>
    /// 进入状态时的处理
    /// </summary>
    /// <param name="state">要进入的状态</param>
    private void EnterState(AIState state)
    {
        switch (state)
        {
            case AIState.Advance:
                currentSubState = AISubState.Pathfinding;
                break;
                
            case AIState.Engage:
                // 在进入交战状态时，检查与目标的距离
                if (currentTarget != null)
                {
                    float distanceToTarget = Vector2.Distance(transform.position, currentTarget.transform.position);
                    
                    // 如果距离已经合适，直接进入定位阶段，不需要接近
                    if (distanceToTarget <= idealCastingRange + 3f)
                    {
                        currentSubState = AISubState.Positioning;
                        //Debug.Log($"距离目标已经合适({distanceToTarget})，直接进入定位状态");
                    }
                    // 如果距离太近，也直接进入定位阶段，随后会后退到合适距离
                    else if (distanceToTarget < idealCastingRange - 2f)
                    {
                        currentSubState = AISubState.Positioning;
                        //Debug.Log($"距离目标太近({distanceToTarget})，直接进入定位状态并后退");
                    }
                    // 如果距离较远，才进入接近阶段
                    else
                    {
                        currentSubState = AISubState.Approach;
                        //Debug.Log($"距离目标较远({distanceToTarget})，进入接近状态");
                    }
                    
                    // 无论哪种情况，确保朝向正确
                    FaceTarget();
                }
                else
                {
                    // 如果没有目标，默认进入接近状态
                    currentSubState = AISubState.Approach;
                }
                break;
                
            case AIState.Retreat:
                currentSubState = AISubState.FindSafePosition;
                hasSafePosition = false;
                break;
                
            case AIState.Dead:
                // 死亡状态无子状态
                currentSubState = AISubState.None;
                break;
        }
    }
    
    /// <summary>
    /// 退出状态时的处理
    /// </summary>
    /// <param name="state">要退出的状态</param>
    private void ExitState(AIState state)
    {
        // 清理状态相关的变量
        switch (state)
        {
            case AIState.Retreat:
                hasSafePosition = false;
                break;
        }
    }
    
    /// <summary>
    /// 检查是否有可攻击目标
    /// </summary>
    /// <returns>是否有可攻击目标</returns>
    private bool HasAttackableTarget()
    {
        // 获取所有单位 - 使用GameManager的注册系统
        Unit[] units = GetRegisteredUnits();

        // 检查是否有可攻击目标
        foreach (Unit unit in units)
        {
            // 排除无效、死亡或友方单位
            if (unit == null || unit.IsDead || unit.GetFaction() == heroUnit.GetFaction())
                continue;

            // 检查单位是否在攻击范围内
            float distance = Vector2.Distance(heroUnit.transform.position, unit.transform.position);
            if (distance <= 20f) // 从15f改为20f，增大检测范围
            {
                return true;
            }
        }

        return false;
    }
    
    /// <summary>
    /// 寻找最佳攻击目标
    /// </summary>
    /// <returns>最佳目标</returns>
    private Unit FindBestTarget()
    {
        // 获取所有单位 - 使用GameManager的注册系统
        Unit[] units = GetRegisteredUnits();
        List<Unit> validTargets = new List<Unit>();
        
        // 过滤有效目标
        foreach (Unit unit in units)
        {
            // 排除无效、死亡或友方单位
            if (unit == null || unit.IsDead || unit.GetFaction() == heroUnit.GetFaction())
                continue;
                
            // 检查单位是否在攻击范围内
            float distance = Vector2.Distance(heroUnit.transform.position, unit.transform.position);
            if (distance > 20f) // 从15f改为20f，与HasAttackableTarget保持一致
                continue;
                
            // 添加到有效目标列表
            validTargets.Add(unit);
        }
        
        // 如果没有有效目标，返回null
        if (validTargets.Count == 0)
            return null;
            
        // 评估每个目标的价值
        List<TargetScore> targetScores = new List<TargetScore>();
        
        foreach (Unit target in validTargets)
        {
            float score = CalculateTargetScore(target);
            targetScores.Add(new TargetScore(target, score));
        }
        
        // 排序目标分数（按分数降序）
        targetScores.Sort((a, b) => b.score.CompareTo(a.score));
        
        // 根据难度决定随机选择的概率
        float randomSelectionChance = (difficulty == AIDifficulty.Normal) ? 0.2f : 0.05f;
        
        // 有一定概率选择次优目标
        if (targetScores.Count > 1 && Random.value < randomSelectionChance)
        {
            int randomIndex = Random.Range(1, Mathf.Min(3, targetScores.Count));
            return targetScores[randomIndex].unit;
        }
        
        // 返回最佳目标
        return targetScores[0].unit;
    }
    
    /// <summary>
    /// 计算目标分数，用于选择最佳攻击目标
    /// </summary>
    /// <param name="target">目标单位</param>
    /// <returns>目标评分</returns>
    private float CalculateTargetScore(Unit target)
    {
        float score = 50f; // 基础分数
        
        // 根据单位类型评分
        if (target is HeroUnit)
        {
            // 如果是英雄单位
            HeroUnit heroTarget = target as HeroUnit;
            
            // 根据敌方英雄类型调整策略
            if (enemyHeroType == HeroType.Necromancer)
            {
                // 敌方是邪术师，高优先级
                score += 60f; // 降低一些优先级，避免过于集中攻击左方英雄
                
                // 如果邪术师血量低，更高优先级
                if (heroTarget.currentHealth / heroTarget.maxHealth < 0.4f)
                {
                    score += 20f; // 降低一些优先级
                }
                
                // 如果邪术师正在施法，极高优先级
                CharacterSkillManager enemySkillManager = heroTarget.GetComponent<CharacterSkillManager>();
                if (enemySkillManager != null && enemySkillManager.IsCasting)
                {
                    score += 30f; // 降低一些优先级
                }
            }
            else if (enemyHeroType == HeroType.Knight)
            {
                // 敌方是骑士，高优先级但低于邪术师
                score += 55f; // 降低一些优先级
                
                // 如果骑士血量低，更高优先级
                if (heroTarget.currentHealth / heroTarget.maxHealth < 0.3f)
                {
                    score += 20f; // 降低一些优先级
                }
                
                // 骑士是强力单位，给予高优先级
                AnimationCtroller knightController = heroTarget.GetComponent<AnimationCtroller>();
                if (knightController != null)
                {
                    score += 25f; // 降低一些优先级
                }
            }
            else
            {
                // 未知英雄类型，使用默认评分
                score += 50f; // 降低优先级
            }
        }
        else if (target is left_castle)
        {
            // 左侧城堡，最低优先级
            score += 20f; // 大幅降低城堡的优先级，使其成为最低优先级目标
            
            // 如果城堡血量极低，稍微提高一点优先级，但仍然保持较低
            if (target.currentHealth / target.maxHealth < 0.2f)
            {
                score += 15f; // 即使城堡血量低，也只给予很低的优先级加成
            }
            
            // 如果没有其他单位防守城堡，稍微提高优先级，但仍然保持较低
            int defendersCount = CountUnitsNearPosition(target.transform.position, 10f, Unit.Faction.Left);
            if (defendersCount <= 1) // 只有城堡自己或者只有一个防守者
            {
                score += 10f; // 即使无人防守，也只给予很低的优先级加成
            }
        }
        else
        {
            // 对普通单位增加一些优先级，降低对英雄和城堡的过度关注
            switch (target.Type)
            {
                case Unit.UnitType.Mage:
                    score += 75f; // 提高普通法师的优先级
                    
                    // 如果法师正在施法，更高优先级
                    MageController mageController = target.GetComponent<MageController>();
                    if (mageController != null && mageController.IsCasting)
                    {
                        score += 25f;
                    }
                    break;
                    
                case Unit.UnitType.Archer:
                    score += 75f; // 提高弓箭手的优先级
                    break;
                    
                case Unit.UnitType.Priest:
                    score += 80f; // 提高牧师的优先级
                    
                    // 如果牧师正在治疗，更高优先级
                    PriestController priestController = target.GetComponent<PriestController>();
                    if (priestController != null && priestController.IsHealing)
                    {
                        score += 30f;
                    }
                    break;
                    
                    
                case Unit.UnitType.Lancer:
                    score += 60f; // 枪兵
                    break;
                    
                case Unit.UnitType.Soldier:
                    score += 50f; // 普通士兵
                    break;
                    
                default:
                    score += 40f; // 其他单位
                    break;
            }
        }
        
        // 根据距离评分（距离越近，分数越高）
        float distance = Vector2.Distance(transform.position, target.transform.position);
        float distanceScore = Mathf.Clamp(30f - distance * 2f, -20f, 30f);
        score += distanceScore;
        
        // 根据生命值评分（生命值越低，分数越高）
        float healthPercentage = target.currentHealth / target.maxHealth;
        float healthScore = Mathf.Lerp(30f, 0f, healthPercentage);
        score += healthScore;
        
        // 根据威胁度评分
        float threatScore = EvaluateThreatScore(target);
        score += threatScore;
        
        // 根据目标评估准确度调整分数
        if (targetEvaluationAccuracy < 1.0f)
        {
            float errorRange = (1.0f - targetEvaluationAccuracy) * 30f;
            score += Random.Range(-errorRange, errorRange);
        }
        
        return score;
    }
    
    /// <summary>
    /// 计算指定位置附近指定阵营的单位数量
    /// </summary>
    /// <param name="position">中心位置</param>
    /// <param name="radius">半径</param>
    /// <param name="faction">阵营</param>
    /// <returns>单位数量</returns>
    private int CountUnitsNearPosition(Vector2 position, float radius, Unit.Faction faction)
    {
        int count = 0;
        Unit[] units = GetRegisteredUnits();

        foreach (Unit unit in units)
        {
            if (unit.faction == faction && !unit.IsDead)
            {
                float distance = Vector2.Distance(position, unit.transform.position);
                if (distance <= radius)
                {
                    count++;
                }
            }
        }

        return count;
    }
    
    /// <summary>
    /// 评估单位威胁分数
    /// </summary>
    /// <param name="unit">单位</param>
    /// <returns>威胁分数</returns>
    private float EvaluateThreatScore(Unit unit)
    {
        // 基础威胁分数
        float threatScore = 0f;
        
        // 如果是英雄，威胁较高
        if (unit.GetType() == typeof(HeroUnit))
        {
            threatScore = 0.8f;
        }
        // 如果是小兵，根据类型评估威胁
        else if (unit.GetType() != typeof(left_castle) && unit.GetType() != typeof(right_castle))
        {
            // 根据小兵类型评估威胁（这里简化处理）
            threatScore = 0.4f;
        }
        
        return threatScore;
    }
    
    /// <summary>
    /// 目标评分结构体
    /// </summary>
    private struct TargetScore
    {
        public Unit unit;
        public float score;
        
        public TargetScore(Unit unit, float score)
        {
            this.unit = unit;
            this.score = score;
        }
    }
    
    /// <summary>
    /// 执行推进状态的行为
    /// </summary>
    private void ExecuteAdvanceState()
    {
        // 获取敌方城堡位置
        left_castle leftCastle = FindLeftCastle();
        
        if (leftCastle == null)
        {
            //Debug.LogWarning("NecromancerAIController: 无法找到敌方城堡");
            return;
        }
        
        Vector2 castlePosition = leftCastle.transform.position;
        
        // 更新流场
        UpdateFlowField(leftCastle.transform);
        
        // 根据子状态执行不同行为
        switch (currentSubState)
        {
            case AISubState.Pathfinding:
                // 检查是否有障碍物或敌人阻挡
                bool pathIsBlocked = IsPathBlocked(transform.position, castlePosition);
                
                if (pathIsBlocked)
                {
                    // 如果路径被阻挡，寻找替代路径
                    FindAlternativePath();
                }
                else
                {
                    // 路径畅通，切换到前进子状态
                    currentSubState = AISubState.MoveForward;
                }
                break;
                
            case AISubState.MoveForward:
                // 向敌方城堡移动，但保持安全距离
                MoveTowardsCastle(castlePosition);
                
                // 持续寻找可攻击目标
                FindTarget();
                break;
        }
    }
    
    /// <summary>
    /// 寻找左侧城堡
    /// </summary>
    /// <returns>左侧城堡</returns>
    private left_castle FindLeftCastle()
    {
        // 查找左侧城堡
        return FindObjectOfType<left_castle>();
    }
    
    /// <summary>
    /// 检查路径是否被阻挡
    /// </summary>
    /// <param name="start">起点</param>
    /// <param name="end">终点</param>
    /// <returns>路径是否被阻挡</returns>
    private bool IsPathBlocked(Vector2 start, Vector2 end)
    {
        // 计算方向和距离
        Vector2 direction = (end - start).normalized;
        float distance = Vector2.Distance(start, end);
        
        // 发射射线检查障碍物
        RaycastHit2D hit = Physics2D.Raycast(start, direction, distance, LayerMask.GetMask("Obstacle"));
        
        // 如果射线击中障碍物，路径被阻挡
        return hit.collider != null;
    }
    
    /// <summary>
    /// 寻找替代路径
    /// </summary>
    private void FindAlternativePath()
    {
        // 简单实现：尝试向上或向下移动一段距离，然后继续前进
        // 在实际游戏中，可能需要更复杂的寻路算法
        
        // 随机选择向上或向下
        float verticalOffset = Random.value > 0.5f ? 2f : -2f;
        
        // 移动到偏移位置
        Vector2 targetPosition = (Vector2)transform.position + new Vector2(0, verticalOffset);
        MoveToPosition(targetPosition);
        
        // 一段时间后切换回前进状态
        StartCoroutine(SwitchToMoveForwardAfterDelay(1f));
    }
    
    /// <summary>
    /// 延迟后切换到前进子状态
    /// </summary>
    /// <param name="delay">延迟时间</param>
    /// <returns>协程</returns>
    private IEnumerator SwitchToMoveForwardAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (currentState == AIState.Advance)
        {
            currentSubState = AISubState.MoveForward;
        }
    }
    
    /// <summary>
    /// 向城堡移动
    /// </summary>
    /// <param name="castlePosition">城堡位置</param>
    private void MoveTowardsCastle(Vector2 castlePosition)
    {
        // 计算理想位置（保持一定距离）
        float idealDistance = 11f; // 保持8个单位的距离
        Vector2 directionToCastle = ((Vector2)castlePosition - (Vector2)transform.position).normalized;
        Vector2 idealPosition = (Vector2)castlePosition - directionToCastle * idealDistance;
        
        // 移动到理想位置
        MoveToPosition(idealPosition);
    }
    
    /// <summary>
    /// 移动到指定位置
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    private void MoveToPosition(Vector2 targetPosition)
    {
        // 如果正在施法，不执行移动
        if (isCasting)
            return;
            
        // 计算移动方向
        Vector2 currentPos = transform.position;
        Vector2 desiredDir;
        
        // 使用流场寻路或直接移动
        if (currentFlowField != null)
        {
            // 获取流场方向
            Vector2 flowDir = currentFlowField.GetFlowDirection(currentPos);
            if (flowDir != Vector2.zero)
            {
                desiredDir = flowDir;
            }
            else
            {
                // 如果流场没有提供有效方向，使用直接方向
                desiredDir = (targetPosition - currentPos).normalized;
            }
        }
        else
        {
            // 没有流场，使用直接方向
            desiredDir = (targetPosition - currentPos).normalized;
        }
        
        // 计算带偏移的检测位置
        Vector2 detectionCenter = currentPos + new Vector2(0, -obstacleDetectionOffset);
        
        // 绘制避障检测圆形（仅在编辑器中可见）
        Debug.DrawLine(detectionCenter + Vector2.up * obstacleAvoidanceRadius, 
                       detectionCenter + Vector2.down * obstacleAvoidanceRadius, Color.yellow);
        Debug.DrawLine(detectionCenter + Vector2.left * obstacleAvoidanceRadius, 
                       detectionCenter + Vector2.right * obstacleAvoidanceRadius, Color.yellow);
        
        // 检测周围单位并避让 - 增大检测半径
        float detectionRadius = obstacleAvoidanceRadius;
        if (currentState == AIState.Retreat)
        {
            // 在撤退状态下使用更大的检测半径和更强的避障力
            detectionRadius = obstacleAvoidanceRadius * 2.0f;
            // 使用Debug.DrawLine来模拟DrawWireSphere
            Debug.DrawLine(detectionCenter + Vector2.up * detectionRadius, 
                        detectionCenter + Vector2.down * detectionRadius, Color.red);
            Debug.DrawLine(detectionCenter + Vector2.left * detectionRadius, 
                        detectionCenter + Vector2.right * detectionRadius, Color.red);
        }
        
        Collider2D[] nearbyUnits = Physics2D.OverlapCircleAll(detectionCenter, detectionRadius, 
            LayerMask.GetMask("Unit"));
        
        Vector2 avoidanceDir = Vector2.zero;
        int count = 0;
        
        foreach (Collider2D unit in nearbyUnits)
        {
            // 跳过自己
            if (unit.gameObject == gameObject) continue;
            
            // 跳过当前目标（如果在交战状态）
            if (currentState == AIState.Engage && currentTarget != null && unit.gameObject == currentTarget.gameObject) continue;
            
            // 获取单位的Unit组件
            Unit unitComponent = unit.GetComponent<Unit>();
            
            // 如果是撤退状态，检查与我们阵营相同的单位
            bool shouldAvoid = true;
            
            // 在撤退状态下，即使是友军也要避让
            if (currentState == AIState.Retreat && unitComponent != null && unitComponent.faction == heroUnit.faction)
            {
                // 这是友军，但我们在撤退状态，所以也要避让
                shouldAvoid = true;
            }
            
            if (shouldAvoid)
            {
                Vector2 toUnit = (Vector2)unit.transform.position - currentPos;
                float distance = toUnit.magnitude;
                
                // 计算避让方向（远离其他单位）
                if (distance < detectionRadius)
                {
                    // 避让力与距离成反比
                    float avoidStrength = 1.0f - (distance / detectionRadius);
                    
                    // 在撤退状态下使用更强的避让力
                    if (currentState == AIState.Retreat)
                    {
                        avoidStrength *= 2.0f;
                    }
                    
                    avoidanceDir -= toUnit.normalized * avoidStrength;
                    count++;
                    
                    // 可视化避让方向
                    Debug.DrawRay(currentPos, -toUnit.normalized * avoidStrength, Color.red);
                }
            }
        }
        
        // 如果有需要避让的单位，计算最终方向
        if (count > 0)
        {
            avoidanceDir /= count; // 平均避让方向
            
            // 结合基础方向和避让方向
            float avoidWeight = 2.5f; // 提高避障强度，从1.5提升到2.5
            if (currentState == AIState.Retreat)
            {
                avoidWeight = 3.0f; // 撤退时更强的避障权重
            }
            
            desiredDir = (desiredDir + avoidanceDir * avoidWeight).normalized;
            
            // 可视化最终方向
            Debug.DrawRay(currentPos, desiredDir * 2.0f, Color.green);
        }
        
        // 计算移动速度
        float moveSpeed = heroUnit.currentMoveSpeed;
        
        // 应用移动
        rb.velocity = desiredDir * moveSpeed;
        
        // 如果有当前目标，就面向目标（而不是移动方向）
        if (currentTarget != null)
        {
            FaceTarget();
        }
        // 只有在没有目标时，才根据移动方向设置朝向
        else
        {
            // 设置朝向（通过缩放X轴实现）
            float originalScaleX = Mathf.Abs(transform.localScale.x); // 保存原始X轴绝对值
            transform.localScale = new Vector3(
                desiredDir.x > 0 ? originalScaleX : -originalScaleX, 
                transform.localScale.y, 
                transform.localScale.z);
        }
            
        // 如果已经非常接近目标位置，停止移动
        if (Vector2.Distance(transform.position, targetPosition) < 0.5f)
        {
            rb.velocity = Vector2.zero;
        }
        
        // 根据速度设置动画参数
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetFloat("speed", rb.velocity.magnitude);
        }
    }
    
    /// <summary>
    /// 寻找目标
    /// </summary>
    private void FindTarget()
    {
        // 尝试寻找目标
        Unit target = FindBestTarget();
        
        // 如果找到目标，添加一个模拟人类反应时间的延迟
        if (target != null)
        {
            StartCoroutine(ReactToNewTarget(target));
        }
    }
    
    /// <summary>
    /// 响应发现新目标
    /// </summary>
    /// <param name="target">目标单位</param>
    private IEnumerator ReactToNewTarget(Unit target)
    {
        // 根据难度调整反应时间
        float actualReactionDelay = reactionTime * Random.Range(0.8f, 1.2f);
        
        // 等待反应时间
        yield return new WaitForSeconds(actualReactionDelay);
        
        // 如果AI仍然激活且目标有效
        if (isActive && target != null && !target.IsDead)
        {
            currentTarget = target;
            ChangeState(AIState.Engage);
        }
    }
    
    /// <summary>
    /// 执行交战状态的行为
    /// </summary>
    private void ExecuteEngageState()
    {
        // 如果正在施法，不执行其他行为
        if (isCasting)
            return;
            
        // 确保有目标
        if (currentTarget == null || currentTarget.IsDead)
        {
            // 目标无效，重新寻找目标
            currentTarget = FindBestTarget();
            
            if (currentTarget == null)
            {
                // 没有可攻击目标，切换回推进状态
                ChangeState(AIState.Advance);
                return;
            }
        }
        
        // 计算X轴和Y轴的距离分量
        float xDistanceToTarget = Mathf.Abs(transform.position.x - currentTarget.transform.position.x);
        float yDistanceToTarget = Mathf.Abs(transform.position.y - currentTarget.transform.position.y);
        
        // 直线距离用于调试日志和边界检测
        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.transform.position);
        
        // 确保朝向目标 - 不管什么状态，都应该面向目标
        FaceTarget();
        
        // Y轴对齐容差 - 近距离时允许更大的容差
        float yToleranceForAttack = xDistanceToTarget < 5.0f ? 2.5f : 1.8f;
        bool isYAxisAligned = yDistanceToTarget < yToleranceForAttack;
        
        // 检查是否接近地图边界，如果是并且距离敌人较近，优先调整位置
        bool isNearBoundary = IsNearMapBoundary(Vector2.zero);
        if (isNearBoundary && xDistanceToTarget < idealCastingRange + 5f)
        {
            // 已经接近地图边界，优先调整位置
            if (Time.time - lastPositionChangeTime > positionChangeCooldown * 0.5f)
            {
                TryAdjustPositionAtBoundary();
                lastPositionChangeTime = Time.time;
                //Debug.Log("接近地图边界，优先调整位置");
                return;
            }
            else
            {
                // 如果冷却中，尝试随机移动
                TryRandomMove();
                lastPositionChangeTime = Time.time;
                //Debug.Log("接近地图边界，位置调整冷却中，尝试随机移动");
                return;
            }
        }
        
        // 如果在相对可接受的X轴距离范围内且Y轴对齐良好，直接进入攻击状态
        if (Mathf.Abs(xDistanceToTarget - idealCastingRange) < 5.0f && 
            isYAxisAligned &&
            !IsLineOfSightBlocked(transform.position, currentTarget.transform.position))
        {
            currentSubState = AISubState.Attack;
            //Debug.Log($"X轴距离已经在可接受范围内({xDistanceToTarget:F1})，Y轴对齐良好({yDistanceToTarget:F1})，直接攻击");
            return;
        }
        
        // 根据子状态执行不同行为
        switch (currentSubState)
        {
            case AISubState.Approach:
                // 如果X轴距离太远，接近目标
                if (xDistanceToTarget > idealCastingRange + 7f)
                {
                    ApproachTarget();
                }
                // 如果X轴距离太近，也需要调整位置（后退）- 但如果接近边界则不后退
                else if (xDistanceToTarget < idealCastingRange - 5f && 
                        !IsDirectionNearMapBoundary(-((Vector2)currentTarget.transform.position - (Vector2)transform.position).normalized, 3f))
                {
                    ApproachTarget(); // ApproachTarget会处理后退逻辑
                }
                // Y轴不对齐，需要调整位置
                else if (!isYAxisAligned)
                {
                    ApproachTarget(); // 调整Y轴对齐
                }
                // X轴距离基本可接受且Y轴对齐良好，直接切换到攻击状态
                else
                {
                    currentSubState = AISubState.Attack;
                    //Debug.Log($"已接近目标至可接受X轴距离({xDistanceToTarget:F1})，Y轴对齐良好({yDistanceToTarget:F1})，直接切换到攻击状态");
                }
                break;
                
            case AISubState.Positioning:
                // 大幅减少定位频率，通常直接切换到攻击状态
                // 只有视线被阻挡时，才真正需要定位
                if (IsLineOfSightBlocked(transform.position, currentTarget.transform.position))
                {
                    // 视线被阻挡，寻找最佳施法位置
                    FindOptimalCastingPosition();
                }
                else
                {
                    // 视线没有阻挡，直接切换到攻击状态
                    currentSubState = AISubState.Attack;
                    //Debug.Log("视线没有阻挡，直接切换到攻击状态");
                }
                break;
                
            case AISubState.Attack:
                // 执行攻击行为
                ExecuteAttack();
                break;
                
            default:
                // 未知子状态，切换到接近状态
                currentSubState = AISubState.Approach;
                break;
        }
    }
    
    /// <summary>
    /// 在边界处尝试调整位置
    /// </summary>
    private void TryAdjustPositionAtBoundary()
    {
        if (currentTarget == null) return;
        
        // 获取当前位置和目标位置
        Vector2 currentPos = transform.position;
        Vector2 targetPos = currentTarget.transform.position;
        
        // 计算X轴和Y轴距离
        float xDistance = Mathf.Abs(currentPos.x - targetPos.x);
        float yDistance = Mathf.Abs(currentPos.y - targetPos.y);
        
        // 如果Y轴距离太大，尝试调整Y轴位置
        if (yDistance > 2.0f)
        {
            // 计算Y轴移动方向
            float yDir = (targetPos.y > currentPos.y) ? 1f : -1f;
            
            // 检查Y轴移动是否会导致接近边界
            if (!IsDirectionNearMapBoundary(new Vector2(0, yDir), 1.5f))
            {
                // Y轴移动不会接近边界，进行Y轴调整
                Vector2 movePos = new Vector2(currentPos.x, currentPos.y + yDir * 1.5f);
                MoveToPosition(movePos);
                //Debug.Log($"在边界处调整Y轴位置: ({movePos.x}, {movePos.y})");
                return;
            }
        }
        
        // 如果X轴距离不理想，尝试调整X轴位置
        float idealX = idealCastingRange;
        if (Mathf.Abs(xDistance - idealX) > 3.0f)
        {
            // 计算理想X轴位置
            float xAdjustment = 0;
            if (xDistance < idealX - 3.0f)
            {
                // 太近，需要后退
                xAdjustment = -2.0f * Mathf.Sign(targetPos.x - currentPos.x);
            }
            else if (xDistance > idealX + 3.0f)
            {
                // 太远，需要靠近
                xAdjustment = 2.0f * Mathf.Sign(targetPos.x - currentPos.x);
            }
            
            // 检查X轴移动是否会导致接近边界
            if (!IsDirectionNearMapBoundary(new Vector2(xAdjustment, 0).normalized, Mathf.Abs(xAdjustment)))
            {
                // X轴移动不会接近边界，进行X轴调整
                Vector2 movePos = new Vector2(currentPos.x + xAdjustment, currentPos.y);
                MoveToPosition(movePos);
                //Debug.Log($"在边界处调整X轴位置: ({movePos.x}, {movePos.y})");
                return;
            }
        }
        
        // 无法调整位置，尝试随机移动而不是直接攻击
        TryRandomMove();
        //Debug.Log("无法在边界处调整位置，尝试随机移动");
    }
    
    /// <summary>
    /// 接近目标
    /// </summary>
    private void ApproachTarget()
    {
        // 如果正在施法，不执行移动
        if (isCasting)
            return;
            
        // 确保目标不为空
        if (currentTarget == null)
        {
            // 目标为空，尝试寻找新目标
            currentTarget = FindBestTarget();
            
            if (currentTarget == null)
            {
                // 没有找到目标，切换回推进状态
                ChangeState(AIState.Advance);
                return;
            }
        }
        
        // 更新目标流场
        UpdateFlowField(currentTarget.transform);
        
        // 计算X轴和Y轴的距离分量
        float xDistanceToTarget = Mathf.Abs(transform.position.x - currentTarget.transform.position.x);
        float yDistanceToTarget = Mathf.Abs(transform.position.y - currentTarget.transform.position.y);
        
        // 直线距离用于调试日志
        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.transform.position);
        
        // 判断X轴距离是否在理想范围内
        float xDistanceTolerance = 3.0f;
        bool isInIdealXRange = Mathf.Abs(xDistanceToTarget - idealCastingRange) < xDistanceTolerance;
        
        // Y轴对齐容差 - 近距离时允许更大的容差
        float yToleranceForAttack = xDistanceToTarget < 5.0f ? 2.5f : 1.8f;
        bool isYAxisAligned = yDistanceToTarget < yToleranceForAttack;
        
        // 如果X轴距离合适且Y轴对齐良好，直接切换到攻击状态
        if (isInIdealXRange && isYAxisAligned && !IsLineOfSightBlocked(transform.position, currentTarget.transform.position))
        {
            // 已经在接近理想距离，直接切换到攻击状态
            currentSubState = AISubState.Attack;
            // 确保朝向正确的方向
            FaceTarget();
            //Debug.Log($"X轴距离已接近理想范围({xDistanceToTarget:F1})，Y轴对齐良好({yDistanceToTarget:F1})，直接切换到攻击状态");
            return;
        }
        
        // 计算从AI到目标的方向向量
        Vector2 directionToTarget = ((Vector2)currentTarget.transform.position - (Vector2)transform.position).normalized;
        Vector2 idealPosition;
        
        // 如果X轴距离太近，考虑后退
        if (xDistanceToTarget < idealCastingRange - 4.0f)
        {
            // 检查后退是否会接近地图边界
            Vector2 backDirection = -directionToTarget;
            float backDistance = 3.0f;
            bool isNearMapBoundary = IsDirectionNearMapBoundary(backDirection, backDistance);
            
            if (isNearMapBoundary)
            {
                // 已靠近边界，尝试调整位置或直接攻击
                if (isYAxisAligned)
                {
                    currentSubState = AISubState.Attack;
                    //Debug.Log("X轴距离较近且靠近地图边界，Y轴对齐良好，直接攻击");
                    return;
                }
                else
                {
                    // 尝试调整位置而不是后退
                    TryAdjustPositionAtBoundary();
                    return;
                }
            }
            else
            {
                // 不接近边界，适度后退（主要调整X轴位置）
                float xAdjustment = (idealCastingRange - xDistanceToTarget) * Mathf.Sign(transform.position.x - currentTarget.transform.position.x);
                idealPosition = new Vector2(transform.position.x + xAdjustment, transform.position.y);
                
                // 如果Y轴差距太大，也进行适当调整
                if (yDistanceToTarget > yToleranceForAttack)
                {
                    float yAdjustment = (yDistanceToTarget - yToleranceForAttack * 0.8f) * Mathf.Sign(currentTarget.transform.position.y - transform.position.y);
                    idealPosition.y += yAdjustment;
                }
                
                //Debug.Log($"X轴距离太近({xDistanceToTarget:F1})，适度后退，Y轴调整({yDistanceToTarget:F1})");
            }
        }
        // 如果X轴距离太远，向目标靠近
        else
        {
            // 计算目标周围理想施法位置（主要考虑X轴）
            float xOffset = idealCastingRange * Mathf.Sign(transform.position.x - currentTarget.transform.position.x);
            idealPosition = new Vector2(currentTarget.transform.position.x + xOffset, transform.position.y);
            
            // 如果Y轴差距太大，也进行适当调整
            if (yDistanceToTarget > yToleranceForAttack)
            {
                float yAdjustment = yDistanceToTarget * Mathf.Sign(currentTarget.transform.position.y - transform.position.y);
                // 限制Y轴调整幅度，避免过度移动
                yAdjustment = Mathf.Min(yAdjustment, 2.0f) * 0.8f;
                idealPosition.y += yAdjustment;
            }
            
            // 检查移动方向是否会导致接近边界
            Vector2 moveDirection = idealPosition - (Vector2)transform.position;
            float moveDistance = moveDirection.magnitude;
            
            if (moveDistance > 0.1f && IsDirectionNearMapBoundary(moveDirection.normalized, moveDistance))
            {
                // 移动会导致接近边界，尝试调整移动方向
                //Debug.Log("向目标移动会导致接近边界，尝试调整移动方向");
                
                // 尝试只调整Y轴位置
                if (yDistanceToTarget > yToleranceForAttack)
                {
                    float yDir = Mathf.Sign(currentTarget.transform.position.y - transform.position.y);
                    Vector2 yOnlyMove = new Vector2(0, yDir * 1.5f);
                    
                    if (!IsDirectionNearMapBoundary(yOnlyMove.normalized, yOnlyMove.magnitude))
                    {
                        // Y轴移动不会接近边界
                        idealPosition = (Vector2)transform.position + yOnlyMove;
                        //Debug.Log($"仅调整Y轴位置: ({idealPosition.x}, {idealPosition.y})");
                    }
                    else
                    {
                        // Y轴移动也会接近边界，直接切换到攻击状态
                        currentSubState = AISubState.Attack;
                        //Debug.Log("无法安全移动，直接切换到攻击状态");
                        return;
                    }
                }
                else
                {
                    // Y轴已经对齐良好，直接切换到攻击状态
                    currentSubState = AISubState.Attack;
                    //Debug.Log("无法安全移动且Y轴已对齐，直接切换到攻击状态");
                    return;
                }
            }
            else
            {
                //Debug.Log($"X轴距离太远({xDistanceToTarget:F1})，向目标靠近，Y轴调整({yDistanceToTarget:F1})");
            }
        }
        
        // 检查计算出的理想位置是否在地图边界外
        if (IsPositionOutsideMapBoundary(idealPosition))
        {
            //Debug.Log($"计算的理想位置({idealPosition.x}, {idealPosition.y})在地图边界外，尝试调整");
            
            // 如果理想位置在边界外，尝试找一个安全的位置
            Vector2 currentPos = transform.position;
            Vector2 safeDirection = Vector2.zero;
            
            // 尝试几个不同的方向，看哪个是安全的
            Vector2[] directions = {
                new Vector2(0, 1),   // 上
                new Vector2(0, -1),  // 下
                new Vector2(1, 0),   // 右
                new Vector2(-1, 0),  // 左
                new Vector2(1, 1).normalized,   // 右上
                new Vector2(1, -1).normalized,  // 右下
                new Vector2(-1, 1).normalized,  // 左上
                new Vector2(-1, -1).normalized  // 左下
            };
            
            foreach (Vector2 dir in directions)
            {
                if (!IsDirectionNearMapBoundary(dir, 1.5f))
                {
                    safeDirection = dir;
                    break;
                }
            }
            
            if (safeDirection != Vector2.zero)
            {
                // 找到安全方向，向该方向移动一小段距离
                idealPosition = currentPos + safeDirection * 1.5f;
                //Debug.Log($"找到安全移动方向，新位置: ({idealPosition.x}, {idealPosition.y})");
            }
            else
            {
                // 没有安全方向，直接切换到攻击状态
                currentSubState = AISubState.Attack;
                //Debug.Log("无法找到安全移动方向，直接切换到攻击状态");
                return;
            }
        }
        
        // 移动到理想位置
        MoveToPosition(idealPosition);
    }
    
    /// <summary>
    /// 检查位置是否接近地图边界
    /// </summary>
    /// <param name="moveDirection">移动方向和距离</param>
    /// <returns>是否接近边界</returns>
    private bool IsNearMapBoundary(Vector2 moveDirection)
    {
        // 计算移动后的位置
        Vector2 futurePosition = (Vector2)transform.position + moveDirection;
        
        // 地图边界范围 - 根据MapBoundary组件实际设置的值
        // 地图中心点位置在 (1.24, 8.53)
        // 地图宽度为 82.6，高度为 14.5
        float mapCenterX = 1.24f;
        float mapCenterY = 8.53f;
        float mapWidth = 82.6f;
        float mapHeight = 14.5f;
        
        // 计算边界坐标
        float mapMinX = mapCenterX - (mapWidth / 2);
        float mapMaxX = mapCenterX + (mapWidth / 2);
        float mapMinY = mapCenterY - (mapHeight / 2);
        float mapMaxY = mapCenterY + (mapHeight / 2);
        
        // 安全边距
        float safeMargin = 0.5f; // 减小安全边距，使AI可以更靠近边界
        
        // 检查是否接近或超出边界
        bool beyondBoundary = 
            futurePosition.x < mapMinX + safeMargin || 
            futurePosition.x > mapMaxX - safeMargin ||
            futurePosition.y < mapMinY + safeMargin || 
            futurePosition.y > mapMaxY - safeMargin;
            
        if (beyondBoundary)
        {
            //Debug.Log($"检测到接近边界: 位置({futurePosition.x}, {futurePosition.y})接近地图边界");
        }
            
        return beyondBoundary;
    }
    
    /// <summary>
    /// 检查特定方向是否接近地图边界
    /// </summary>
    /// <param name="direction">要检查的方向</param>
    /// <param name="distance">检查距离</param>
    /// <returns>指定方向是否接近边界</returns>
    private bool IsDirectionNearMapBoundary(Vector2 direction, float distance)
    {
        // 标准化方向向量
        if(direction.magnitude > 0)
            direction.Normalize();
            
        // 计算未来位置
        Vector2 futurePosition = (Vector2)transform.position + direction * distance;
        
        // 地图边界范围 - 与IsNearMapBoundary保持一致
        float mapCenterX = 1.24f;
        float mapCenterY = 8.53f;
        float mapWidth = 82.6f;
        float mapHeight = 14.5f;
        
        // 计算边界坐标
        float mapMinX = mapCenterX - (mapWidth / 2);
        float mapMaxX = mapCenterX + (mapWidth / 2);
        float mapMinY = mapCenterY - (mapHeight / 2);
        float mapMaxY = mapCenterY + (mapHeight / 2);
        
        // 安全边距
        float safeMargin = 0.5f;
        
        // 检查是否接近或超出边界
        bool beyondBoundary = 
            futurePosition.x < mapMinX + safeMargin || 
            futurePosition.x > mapMaxX - safeMargin ||
            futurePosition.y < mapMinY + safeMargin || 
            futurePosition.y > mapMaxY - safeMargin;
            
        return beyondBoundary;
    }
    
    /// <summary>
    /// 检查位置是否在地图边界外
    /// </summary>
    /// <param name="position">要检查的位置</param>
    /// <returns>是否在地图外</returns>
    private bool IsPositionOutsideMapBoundary(Vector2 position)
    {
        // 地图边界范围 - 与IsNearMapBoundary方法保持一致
        // 地图中心点位置在 (1.24, 8.53)
        // 地图宽度为 82.6，高度为 14.5
        float mapCenterX = 1.24f;
        float mapCenterY = 8.53f;
        float mapWidth = 82.6f;
        float mapHeight = 14.5f;
        
        // 计算边界坐标
        float mapMinX = mapCenterX - (mapWidth / 2);
        float mapMaxX = mapCenterX + (mapWidth / 2);
        float mapMinY = mapCenterY - (mapHeight / 2);
        float mapMaxY = mapCenterY + (mapHeight / 2);
        
        // 安全边距
        float safeMargin = 1.0f; // 安全边距改为1.0f
        
        // 检查是否超出边界
        bool outside = position.x < mapMinX + safeMargin || 
                       position.x > mapMaxX - safeMargin ||
                       position.y < mapMinY + safeMargin || 
                       position.y > mapMaxY - safeMargin;
        
        if (outside)
        {
            //Debug.Log($"位置({position.x}, {position.y})在地图边界外");
        }
        
        return outside;
    }
    
    /// <summary>
    /// 确保角色朝向目标
    /// </summary>
    private void FaceTarget()
    {
        if (currentTarget == null) return;
        
        // 获取AI和目标的水平位置差
        float horizontalDifference = currentTarget.transform.position.x - transform.position.x;
        
        // 根据水平位置差调整角色朝向
        Vector3 scale = transform.localScale;
        if (horizontalDifference < 0)
        {
            // 目标在左边，让角色朝左
            scale.x = Mathf.Abs(scale.x) * -1;
        }
        else
        {
            // 目标在右边，让角色朝右
            scale.x = Mathf.Abs(scale.x);
        }
        transform.localScale = scale;
    }
    
    /// <summary>
    /// 检查是否在良好的施法位置
    /// </summary>
    /// <returns>是否在良好位置</returns>
    private bool IsInGoodCastingPosition()
    {
        // 如果没有目标，直接返回false
        if (currentTarget == null)
            return false;
        
        // 计算与目标的距离
        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.transform.position);
        
        // 增大容差范围，减少不必要的定位
        bool isGoodDistance = Mathf.Abs(distanceToTarget - idealCastingRange) <= attackPositionTolerance;
        
        // 检查视线是否被阻挡
        bool lineOfSightClear = !IsLineOfSightBlocked(transform.position, currentTarget.transform.position);
        
        // 检查是否有足够空间施法
        bool hasEnoughSpace = HasEnoughSpaceToCast();
        
        // 检查是否有敌人在侧翼
        bool noFlankingEnemies = !IsFlankingEnemyNearby();
        
        // 当处于困难模式时，要求更严格
        if (difficulty == AIDifficulty.Hard)
        {
            // 困难模式下对位置要求更高
            return isGoodDistance && lineOfSightClear && hasEnoughSpace && noFlankingEnemies;
        }
        else
        {
            // 普通模式下只需要距离合适且视线没被阻挡
            return isGoodDistance && lineOfSightClear;
        }
    }
    
    /// <summary>
    /// 检查视线是否被阻挡
    /// </summary>
    /// <param name="start">起点</param>
    /// <param name="end">终点</param>
    /// <returns>视线是否被阻挡</returns>
    private bool IsLineOfSightBlocked(Vector2 start, Vector2 end)
    {
        // 因为没有障碍物层，直接返回false表示视线永不被阻挡
        return false;
        
        // 原代码注释掉
        // 计算方向和距离
        // Vector2 direction = (end - start).normalized;
        // float distance = Vector2.Distance(start, end);
        
        // 发射射线检查障碍物
        // RaycastHit2D hit = Physics2D.Raycast(start, direction, distance, LayerMask.GetMask("Obstacle"));
        
        // 如果射线击中障碍物，视线被阻挡
        // return hit.collider != null;
    }
    
    /// <summary>
    /// 检查是否有足够的空间施法
    /// </summary>
    /// <returns>是否有足够空间</returns>
    private bool HasEnoughSpaceToCast()
    {
        // 因为没有障碍物层，直接返回true表示总是有足够空间
        return true;
        
        // 原代码注释掉
        // 简单实现：检查周围一定范围内是否有障碍物
        // Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 2f, LayerMask.GetMask("Obstacle"));
        // return colliders.Length == 0;
    }
    
    /// <summary>
    /// 检查是否有敌人在侧翼或身后
    /// </summary>
    /// <returns>是否有侧翼敌人</returns>
    private bool IsFlankingEnemyNearby()
    {
        // 获取朝向目标的方向
        Vector2 directionToTarget = ((Vector2)currentTarget.transform.position - (Vector2)transform.position).normalized;
        
        // 检查周围的敌人
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 5f);
        
        foreach (Collider2D collider in colliders)
        {
            Unit unit = collider.GetComponent<Unit>();
            
            // 如果不是敌方单位，跳过
            if (unit == null || unit.faction == Unit.Faction.Right || unit.IsDead)
                continue;
                
            // 计算敌人方向
            Vector2 directionToEnemy = ((Vector2)unit.transform.position - (Vector2)transform.position).normalized;
            
            // 计算夹角
            float angle = Vector2.Angle(directionToTarget, directionToEnemy);
            
            // 如果敌人在侧面或后方（夹角大于90度），返回true
            if (angle > 90f)
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 获取目标位置
    /// </summary>
    /// <returns>目标位置</returns>
    private Vector2 GetTargetPosition()
    {
        // 如果有当前目标，返回目标位置
        if (currentTarget != null)
        {
            return currentTarget.transform.position;
        }
        
        // 如果没有当前目标，尝试获取敌方城堡位置
        left_castle leftCastle = FindLeftCastle();
        if (leftCastle != null)
        {
            return leftCastle.transform.position;
        }
        
        // 如果都没有，返回前方位置
        return (Vector2)transform.position + Vector2.left * 10f;
    }
    
    /// <summary>
    /// 寻找最佳施法位置
    /// </summary>
    private void FindOptimalCastingPosition()
    {
        // 获取目标位置
        Vector2 targetPosition = GetTargetPosition();
        
        // 如果没有有效的目标位置，使用前方位置
        if (targetPosition == (Vector2)transform.position + Vector2.left * 10f && currentTarget == null)
        {
            // 没有目标，切换回推进状态
            ChangeState(AIState.Advance);
            return;
        }
        
        // 计算X轴和Y轴的距离分量
        float xDistanceToTarget = Mathf.Abs(transform.position.x - targetPosition.x);
        float yDistanceToTarget = Mathf.Abs(transform.position.y - targetPosition.y);
        
        // Y轴对齐容差 - 近距离时允许更大的容差
        float yToleranceForAttack = xDistanceToTarget < 5.0f ? 2.5f : 1.8f;
        bool isYAxisAligned = yDistanceToTarget < yToleranceForAttack;
        
        // 如果当前位置已经可以接受，直接切换到攻击状态而不是寻找新位置
        if (Mathf.Abs(xDistanceToTarget - idealCastingRange) < 2.5f && 
            isYAxisAligned &&
            !IsLineOfSightBlocked(transform.position, targetPosition))
        {
            // 已经在接受范围内且视线没有阻挡，直接攻击
            currentSubState = AISubState.Attack;
            //Debug.Log($"当前位置已经可接受，X轴距离({xDistanceToTarget:F1})，Y轴对齐良好({yDistanceToTarget:F1})，直接切换到攻击状态");
            return;
        }
        
        // 创建潜在位置列表
        List<Vector2> potentialPositions = new List<Vector2>();
        
        // 设置理想施法距离
        float radius = idealCastingRange;
        
        // 添加当前位置附近的几个简单位置
        Vector2 directionToTarget = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        
        // 添加直接目标位置（目标周围理想X轴距离）
        float targetX = targetPosition.x - Mathf.Sign(targetPosition.x - transform.position.x) * radius;
        
        // 先尝试保持当前Y坐标
        Vector2 directPosition = new Vector2(targetX, transform.position.y);
        if (!IsPositionOutsideMapBoundary(directPosition))
        {
            potentialPositions.Add(directPosition);
            // 增加权重
            potentialPositions.Add(directPosition);
        }
        
        // 添加Y轴对齐的位置
        Vector2 yAlignedPosition = new Vector2(targetX, targetPosition.y);
        if (!IsPositionOutsideMapBoundary(yAlignedPosition))
        {
            potentialPositions.Add(yAlignedPosition);
            // 增加权重
            potentialPositions.Add(yAlignedPosition);
            potentialPositions.Add(yAlignedPosition); // 更高权重
        }
        
        // 添加Y轴稍微偏移的位置
        float yOffset = 1.5f;
        Vector2 yOffsetUp = new Vector2(targetX, targetPosition.y + yOffset);
        Vector2 yOffsetDown = new Vector2(targetX, targetPosition.y - yOffset);
        
        if (!IsPositionOutsideMapBoundary(yOffsetUp))
        {
            potentialPositions.Add(yOffsetUp);
        }
        
        if (!IsPositionOutsideMapBoundary(yOffsetDown))
        {
            potentialPositions.Add(yOffsetDown);
        }
        
        // 如果没有找到任何有效位置，尝试更多位置
        if (potentialPositions.Count == 0)
        {
            // 尝试不同的X轴距离
            float[] alternativeDistances = { radius * 0.7f, radius * 1.3f };
            
            foreach (float altRadius in alternativeDistances)
            {
                float altTargetX = targetPosition.x - Mathf.Sign(targetPosition.x - transform.position.x) * altRadius;
                Vector2 altPosition = new Vector2(altTargetX, targetPosition.y);
                
                if (!IsPositionOutsideMapBoundary(altPosition))
                {
                    potentialPositions.Add(altPosition);
                }
            }
        }
        
        // 如果仍然没有找到有效位置，尝试当前位置的小范围调整
        if (potentialPositions.Count == 0)
        {
            // 添加当前位置附近的点
            Vector2 currentPos = transform.position;
            potentialPositions.Add(currentPos + new Vector2(1f, 0));
            potentialPositions.Add(currentPos + new Vector2(-1f, 0));
            potentialPositions.Add(currentPos + new Vector2(0, 1f));
            potentialPositions.Add(currentPos + new Vector2(0, -1f));
        }
        
        // 评估每个位置
        Vector2 bestPosition = transform.position;
        float bestScore = -1000f;
        
        foreach (Vector2 position in potentialPositions)
        {
            // 跳过地图边界外的位置
            if (IsPositionOutsideMapBoundary(position))
                continue;
                
            // 评估位置
            float score = EvaluatePosition(position);
            
            // 更新最佳位置
            if (score > bestScore)
            {
                bestScore = score;
                bestPosition = position;
            }
        }
        
        // 移动到最佳位置
        MoveToPosition(bestPosition);
        
        // 记录位置变更时间
        lastPositionChangeTime = Time.time;
    }
    
    /// <summary>
    /// 执行攻击行为
    /// </summary>
    private void ExecuteAttack()
    {
        // 如果正在施法，不打断
        if (isCasting)
            return;
            
        // 如果没有目标，寻找目标
        if (currentTarget == null || currentTarget.IsDead)
        {
            currentTarget = FindBestTarget();
            
            if (currentTarget == null)
            {
                // 如果仍然没有目标，切换回推进状态
                ChangeState(AIState.Advance);
                return;
            }
        }
        
        // 确保朝向目标
        FaceTarget();
        
        // 计算X轴和Y轴的距离分量
        float xDistanceToTarget = Mathf.Abs(transform.position.x - currentTarget.transform.position.x);
        float yDistanceToTarget = Mathf.Abs(transform.position.y - currentTarget.transform.position.y);
        
        // 直线距离用于部分判断
        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.transform.position);
        
        // 当敌人X轴非常近时，实现"火焰骷髅 > 普通攻击 > 后退到安全距离"的优先级
        if (xDistanceToTarget < 4.0f)
        {
            // 检查Y轴对齐 - 即使在近距离也需要基本对齐
            bool closeRangeYAligned = IsTargetInAngleRange(currentTarget.transform.position, 20f);
            
            if (closeRangeYAligned)
            {
                // 近距离优先使用火焰骷髅(技能2)
                if (skillManager.IsSkillReady(2))
                {
                    //Debug.Log($"敌人X轴靠近({xDistanceToTarget:F1})，Y轴对齐良好，使用火焰骷髅");
                    UseSkill(2);
                    return;
                }
                // 其次使用普通攻击
                else if (skillManager.IsSkillReady(4))
                {
                    //Debug.Log($"敌人X轴靠近({xDistanceToTarget:F1})，Y轴对齐良好，使用普通攻击");
                    UseBasicAttack();
                    return;
                }
                // 如果火焰骷髅和普通攻击都不可用，后退到安全距离
                else
                {
                    //Debug.Log($"敌人X轴靠近({xDistanceToTarget:F1})，火焰骷髅和普通攻击都不可用，后退到安全距离");
                    
                    // 计算后退方向 - 主要在X轴上后退
                    float xAdjustment = (idealCastingRange - xDistanceToTarget) * Mathf.Sign(transform.position.x - currentTarget.transform.position.x);
                    Vector2 retreatPosition = new Vector2(transform.position.x + xAdjustment, transform.position.y);
                    
                    // 检查后退位置是否安全
                    if (!IsPositionOutsideMapBoundary(retreatPosition) && !IsLineOfSightBlocked(transform.position, retreatPosition))
                    {
                        // 移动到后退位置
                        MoveToPosition(retreatPosition);
                    }
                    else
                    {
                        // 如果后退位置不安全，尝试找一个安全的位置
                        FindOptimalCastingPosition();
                    }
                    return;
                }
            }
            else
            {
                // 即使Y轴不完全对齐，也尝试普通攻击
                if (skillManager.IsSkillReady(4) && yDistanceToTarget < 2.0f)
                {
                    //Debug.Log($"敌人X轴靠近但Y轴不完全对齐({yDistanceToTarget:F1})，尝试普通攻击");
                    UseBasicAttack();
                    return;
                }
                else
                {
                    // 如果普通攻击不可用且Y轴不对齐，后退并尝试调整位置
                    //Debug.Log($"敌人X轴靠近但Y轴不对齐({yDistanceToTarget:F1})且普通攻击不可用，后退并调整位置");
                    FindOptimalCastingPosition();
                    return;
                }
            }
        }
        
        // 决定使用哪个技能
        int skillToUse = DecideSkillToUse();
        
        // 减小X轴距离判断的容差，提高定位精确度
        float xDistanceTolerance = 4.0f; // 更严格的X轴容差
        bool isInGoodXRange = false;
        float idealRangeForSkill = GetIdealCastingRangeForSkill(skillToUse);
        
        // 对所有技能使用更宽松的X轴距离判断
        isInGoodXRange = Mathf.Abs(xDistanceToTarget - idealRangeForSkill) <= xDistanceTolerance;
        
        // Y轴对齐容差 - 远距离技能需要更严格的Y轴对齐
        float yToleranceForAttack = xDistanceToTarget < 5.0f ? 2.5f : 1.8f;
        bool isYAxisAligned = yDistanceToTarget < yToleranceForAttack;
        
        // 检查是否有视线阻挡
        bool lineOfSightBlocked = IsLineOfSightBlocked(transform.position, currentTarget.transform.position);
        
        // 检查目标是否在AI前方
        bool targetInFront = IsTargetInFront(currentTarget.transform.position);
        
        // 如果目标不在前方，简单转向而不是重新定位
        if (!targetInFront)
        {
            FaceTarget(); // 重新面向目标
        }
        
        // 如果视线没有被阻挡，尝试使用技能
        if (!lineOfSightBlocked)
        {
            if (skillToUse >= 0)
            {
                // 根据技能和距离决定是否需要严格Y轴对齐
                bool canCast = true;
                
                // 远距离技能需要更严格的Y轴对齐
                if (xDistanceToTarget > 8.0f && (skillToUse == 0 || skillToUse == 3))
                {
                    canCast = isYAxisAligned;
                }
                
                if (canCast)
                {
                    UseSkill(skillToUse);
                    //Debug.Log($"执行技能攻击：技能{skillToUse}，X轴距离{xDistanceToTarget:F1}，Y轴距离{yDistanceToTarget:F1}");
                }
                else
                {
                    // Y轴不够对齐，尝试普通攻击或近战技能
                    //Debug.Log($"目标Y轴对齐不够理想({yDistanceToTarget:F1})，尝试使用普通攻击");
                    UseBasicAttack();
                }
            }
            else
            {
                // 没有合适的技能，使用普通攻击
                UseBasicAttack();
            }
            
            // 记录最后一次位置变更时间，减少频繁移动
            lastPositionChangeTime = Time.time;
        }
        // 如果视线被阻挡，需要重新定位
        else if (lineOfSightBlocked && Time.time - lastPositionChangeTime > positionChangeCooldown)
        {
            currentSubState = AISubState.Positioning;
            // Debug.Log("视线被阻挡，需要重新定位");
        }
        // 如果X轴距离差异极大，需要重新定位
        else if (!isInGoodXRange && Mathf.Abs(xDistanceToTarget - idealRangeForSkill) > xDistanceTolerance * 2.0f 
                && Time.time - lastPositionChangeTime > positionChangeCooldown * 1.5f)
        {
            currentSubState = AISubState.Positioning;
            // Debug.Log($"X轴距离差异极大，需要重新定位：当前{xDistanceToTarget:F1}，理想{idealRangeForSkill:F1}");
        }
        // 如果Y轴对齐不好，也需要重新定位
        else if (!isYAxisAligned && Time.time - lastPositionChangeTime > positionChangeCooldown)
        {
            currentSubState = AISubState.Positioning;
            // Debug.Log($"Y轴对齐不佳({yDistanceToTarget:F1})，需要重新定位");
        }
    }
    
    /// <summary>
    /// 决定使用哪个技能
    /// </summary>
    /// <returns>技能索引</returns>
    private int DecideSkillToUse()
    {
        // 如果无法使用技能，返回普通攻击
        if (skillManager == null)
        {
            return 0; // 默认返回普通攻击
        }
        
        // 评估所有技能
        List<SkillDecision> skillDecisions = EvaluateAllSkills();
        
        // 按照价值排序
        skillDecisions.Sort((a, b) => b.value.CompareTo(a.value));
        
        // 如果有价值大于0的技能，选择最有价值的技能
        if (skillDecisions.Count > 0 && skillDecisions[0].value > 0)
        {
            // Debug.Log($"AI选择技能: {skillDecisions[0].skillIndex}, 价值: {skillDecisions[0].value}");
            return skillDecisions[0].skillIndex;
        }
        
        // 如果当前没有适合的特殊技能，使用普通攻击（即使它在冷却中，也会重置并返回）
        return 0;
    }
    
    /// <summary>
    /// 评估所有技能
    /// </summary>
    /// <returns>技能决策列表</returns>
    private List<SkillDecision> EvaluateAllSkills()
    {
        List<SkillDecision> decisions = new List<SkillDecision>();
        
        // 确保技能管理器存在
        if (skillManager == null)
            return decisions;
            
        // 技能列表：普通攻击 + 四个特殊技能
        int skillCount = skillManager.skills.Length;
        
        // 遍历所有技能（包括普通攻击和特殊技能）
        for (int i = 0; i < skillCount; i++)
        {
            // 评估技能价值
            float value = EvaluateSkillValue(i);
            
            // 如果技能有价值（大于0），添加到决策列表
            if (value > 0)
            {
                decisions.Add(new SkillDecision(i, value));
            }
            
            // 针对技能3（灵魂锁链，即U键），如果目标是英雄，提高其优先级
            if (i == 3 && currentTarget is HeroUnit)
            {
                // 克制英雄，增加优先级
                decisions.Add(new SkillDecision(i, value * 1.5f));
            }
        }
        
        return decisions;
    }
    
    /// <summary>
    /// 评估技能价值
    /// </summary>
    /// <param name="skillIndex">技能索引</param>
    /// <returns>技能价值评分</returns>
    private float EvaluateSkillValue(int skillIndex)
    {
        // 如果技能没有准备好，返回-1
        if (!skillManager.IsSkillReady(skillIndex))
            return -1f;
            
        // 获取目标
        if (currentTarget == null)
            return -1f;
            
        // 与目标的距离
        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.transform.position);
        
        // 基础价值由技能类型决定
        float baseValue = 0f;
        
        // 根据技能类型设定基础价值
        switch (skillIndex)
        {
            case 0: // 技能1（血腥之刺）
                baseValue = 70f;
                break;
            case 1: // 技能2（诅咒）
                baseValue = 80f;
                break;
            case 2: // 技能3（火焰骷髅）
                baseValue = 75f;
                break;
            case 3: // 技能4（死神降临）
                baseValue = 85f;
                break;
            case 4: // 普通攻击 - 提高基础价值
                baseValue = 65f; // 从50f提高到65f
                break;
            default:
                return -1f;
        }
        
        // 近距离时明确优先级：火焰骷髅 > 普通攻击 > 后退到安全距离
        if (distanceToTarget < 5.0f)
        {
            if (skillIndex == 2) // 火焰骷髅 - 近战最优先使用
                baseValue += 80f; // 大幅提高价值，确保最高优先级
            else if (skillIndex == 4) // 普通攻击 - 近战第二选择，提高加成
                baseValue += 70f; // 从40f提高到70f，使其更接近火焰骷髅的优先级
            else // 其他技能在近距离时降低优先级
                baseValue -= 10f;
                
            // 注意：后退到安全距离的逻辑不在技能评估中，
            // 而是在ExecuteAttack或ExecuteEngageState方法中处理
        }
        // 中距离也提高普通攻击的价值
        else if (distanceToTarget >= 5.0f && distanceToTarget < 10.0f)
        {
            if (skillIndex == 0) // 血腥之刺
                baseValue += 15f;
            else if (skillIndex == 1) // 诅咒
                baseValue += 20f;
            else if (skillIndex == 4) // 普通攻击 - 中距离也可以使用
                baseValue += 30f; // 新增中距离普通攻击的加成
        }
        
        // 远距离增加死神降临的价值
        else if (distanceToTarget >= 10.0f)
        {
            if (skillIndex == 3) // 死神降临
                baseValue += 25f;
        }

        // 检查Y轴对齐情况 - 使用不同的角度判断标准
        bool isStrictlyAligned = IsTargetInAngleRange(currentTarget.transform.position, 8f); // 严格对齐
        bool isModeratelyAligned = IsTargetInAngleRange(currentTarget.transform.position, 15f); // 中等对齐
        bool isLooselyAligned = IsTargetInAngleRange(currentTarget.transform.position, 25f); // 宽松对齐
        
        // 获取Y轴距离，用于更精确的判断
        float yDistanceToTarget = Mathf.Abs(transform.position.y - currentTarget.transform.position.y);
        
        // 对不同技能应用不同的Y轴对齐要求
        switch (skillIndex)
        {
            case 0: // 血腥之刺 - 远程直线技能，需要中等对齐
                if (!isModeratelyAligned) {
                    baseValue *= 0.3f;
                }
                if (yDistanceToTarget > 2.0f) {
                    baseValue *= 0.5f;
                }
                break;
                
            case 1: // 诅咒 - AOE技能，需要中等对齐
                if (!isModeratelyAligned) {
                    baseValue *= 0.4f;
                }
                if (yDistanceToTarget > 2.0f) {
                    baseValue *= 0.6f;
                }
                break;
                
            case 2: // 火焰骷髅 - 近战技能，需要宽松对齐即可
                if (!isLooselyAligned) {
                    baseValue *= 0.5f;
                }
                break;
                
            case 3: // 死神降临 - 远程AOE，对齐要求最低
                if (!isLooselyAligned) {
                    baseValue *= 0.7f;
                }
                break;
                
            case 4: // 普通攻击 - 需要非常严格的对齐
                if (!isStrictlyAligned) {
                    baseValue *= 0.1f;  // 更严格的惩罚
                }
                if (!isModeratelyAligned) {
                    baseValue *= 0.1f; // 如果连中等对齐都达不到，价值极低
                }
                if (yDistanceToTarget > 1.0f) {  // 降低阈值
                    baseValue *= 0.3f;  // 更严格的惩罚
                }
                break;
        }
        
        // 严重不对齐时，完全禁止使用普通攻击
        if (skillIndex == 4 && yDistanceToTarget > 1.5f) {
            baseValue = 0f;
        }
        
        return baseValue;
    }
    
    /// <summary>
    /// 检查指定区域内是否有敌方英雄
    /// </summary>
    /// <param name="position">区域中心</param>
    /// <param name="radius">区域半径</param>
    /// <returns>是否有敌方英雄</returns>
    private bool IsEnemyHeroInArea(Vector2 position, float radius)
    {
        // 查找所有英雄单位 - 使用GameManager的注册系统
        HeroUnit[] heroes = GetRegisteredHeroes();

        foreach (HeroUnit hero in heroes)
        {
            // 检查是否是敌方英雄
            if (hero.faction == Unit.Faction.Left && !hero.IsDead)
            {
                // 检查是否在区域内
                float distance = Vector2.Distance(position, hero.transform.position);
                if (distance <= radius)
                {
                    return true;
                }
            }
        }

        return false;
    }
    
    /// <summary>
    /// 技能类型枚举
    /// </summary>
    private enum SkillType
    {
        DirectDamage,  // 直接伤害
        AreaDamage,    // 范围伤害
        Debuff,        // 减益效果
        Summon,        // 召唤
        Healing,       // 治疗
        Shield         // 护盾
    }
    
    /// <summary>
    /// 技能数据类
    /// </summary>
    private class SkillData
    {
        public SkillType skillType;  // 技能类型
        public float damage;         // 伤害值
        public float areaOfEffect;   // 作用范围
        public float duration;       // 持续时间
        
        public SkillData(SkillType type, float damage, float areaOfEffect, float duration)
        {
            this.skillType = type;
            this.damage = damage;
            this.areaOfEffect = areaOfEffect;
            this.duration = duration;
        }
    }
    
    /// <summary>
    /// 检查是否被追击
    /// </summary>
    /// <returns>是否被追击</returns>
    private bool IsBeingChased()
    {
        // 获取所有单位
        Unit[] units = FindObjectsOfType<Unit>();
        int chasingEnemies = 0;
        
        // 统计追击敌人数量
        foreach (Unit unit in units)
        {
            // 检查单位是否有效
            if (unit == null || unit.IsDead || unit.faction == heroUnit.faction)
                continue;
                
            // 检查单位是否在追击范围内
            float distance = Vector2.Distance(heroUnit.transform.position, unit.transform.position);
            if (distance <= 10f) // 扩大追击检测范围到10个单位
            {
                chasingEnemies++;
                
                // 如果是英雄单位，直接认为是严重威胁
                if (unit is HeroUnit)
                {
                    chasingEnemies++; // 英雄权重更大
                }
            }
        }
        
        // 降低被追击判定阈值，更早进入防御状态
        return chasingEnemies >= 1; // 只需要1个敌人就认为被追击
    }
    
    /// <summary>
    /// 技能决策结构体
    /// </summary>
    private struct SkillDecision
    {
        public int skillIndex;
        public float value;
        
        public SkillDecision(int skillIndex, float value)
        {
            this.skillIndex = skillIndex;
            this.value = value;
        }
    }
    
    /// <summary>
    /// 使用技能
    /// </summary>
    /// <param name="skillIndex">技能索引</param>
    private void UseSkill(int skillIndex)
    {
        if (skillManager == null || currentTarget == null || heroUnit == null)
            return;
            
        // 如果目标死亡，停止施法
        if (currentTarget.IsDead)
            return;
            
        // 如果被眩晕，不能施法
        if (heroUnit.isStunned)
            return;
            
        // 设置施法状态
        isCasting = true;
        
        // 获取技能数据
        global::SkillData skillData = skillManager.GetSkillData(skillIndex);
        
        // 停止移动
        if (rb != null)
            rb.velocity = Vector2.zero;
            
        // 确保朝向正确方向 - 重要修复，确保朝向目标方向施法
        // 获取目标相对于AI的水平位置
        float horizontalDifference = currentTarget.transform.position.x - transform.position.x;
        
        // 调整角色朝向
        Vector3 scale = transform.localScale;
        if (horizontalDifference < 0)
        {
            // 目标在左边，让角色朝左
            scale.x = Mathf.Abs(scale.x) * -1;
            // Debug.Log("施法朝向: 左侧目标");
        }
        else
        {
            // 目标在右边，让角色朝右
            scale.x = Mathf.Abs(scale.x);
            // Debug.Log("施法朝向: 右侧目标");
        }
        transform.localScale = scale;
            
        // 触发动画 - 使用和NeController相同的动画触发命名
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger($"SkillTrigger{skillIndex}");
        }
        
        // 获取施法时间
        float castingTime = GetCastingTime(skillIndex);
        
        // 先启动施法时间协程，动画结束后再激活技能，确保按照设计延迟施放
        StartCoroutine(CastSkillWithDelay(skillIndex, castingTime));
    }
    
    /// <summary>
    /// 延迟激活技能，确保按照设计的施法时间延迟施放
    /// </summary>
    /// <param name="skillIndex">技能索引</param>
    /// <param name="castingTime">施法时间</param>
    /// <returns>协程</returns>
    private IEnumerator CastSkillWithDelay(int skillIndex, float castingTime)
    {
        // 保存开始施法时的目标引用
        Unit skillTarget = currentTarget;
        
        // 等待施法时间
        yield return new WaitForSeconds(castingTime);
        
        // 只检查施法者状态，不检查目标位置或Y轴对齐
        if (heroUnit.isStunned || heroUnit.IsDead)
        {
            isCasting = false;
            yield break;
        }
        
        // 无条件激活技能，完全不考虑目标移动或Y轴对齐情况
        skillManager.ActivateSkill(skillIndex);
                    //Debug.Log($"释放技能 {skillIndex} - 忽略目标移动和Y轴对齐");
        
        // 结束施法状态
        isCasting = false;
    }
    
    /// <summary>
    /// 获取技能生成距离
    /// </summary>
    /// <param name="skillIndex">技能索引</param>
    /// <returns>生成距离</returns>
    private float GetSkillGenerationDistance(int skillIndex)
    {
        // 根据技能索引返回不同的生成距离
        // 基于设计文档中指定的值
        // 索引0-3对应邪术师的技能1-4，索引4对应普通攻击
        switch (skillIndex)
        {
            case 0: return 11f;    // 技能1 (死亡之云) - 生成距离11
            case 1: return 10f;    // 技能2 (腐蚀地带) - 生成距离10
            case 2: return 13f;    // 技能3 (灵魂锁链) - 生成距离匹配理想施法距离
            case 3: return 13f;    // 技能4 (死亡风暴) - 生成距离13
            case 4: return 13f;    // 普通攻击 - 生成距离匹配理想施法距离
            default: return 0f;
        }
    }
    
    /// <summary>
    /// 获取指定技能的理想施法距离
    /// </summary>
    /// <param name="skillIndex">技能索引</param>
    /// <returns>理想施法距离</returns>
    private float GetIdealCastingRangeForSkill(int skillIndex)
    {
        // 根据技能索引返回不同的理想施法距离，调整为更合理的值
        // 索引0-3对应邪术师的技能1-4，索引4对应普通攻击
        switch (skillIndex)
        {
            case 0: return 11f;     // 技能1 (血腥之刺) 
            case 1: return 10f;     // 技能2 (诅咒) -
            case 2: return 8f;    // 技能3 (火焰骷髅)
            case 3: return 12f;    // 技能4 (死神降临) 
            case 4: return 10f;    // 普通攻击 
            default: return idealCastingRange;
        }
    }
    
    /// <summary>
    /// 获取技能施法时间
    /// </summary>
    /// <param name="skillIndex">技能索引</param>
    /// <returns>施法时间</returns>
    private float GetCastingTime(int skillIndex)
    {
        // 根据技能索引返回指定的施法时间（基于设计文档）
        // 索引0-3对应邪术师的技能1-4，索引4对应普通攻击
        switch (skillIndex)
        {
            case 0: return 1.0f;  // 技能1 (死亡之云) - 1秒
            case 1: return 1.5f;  // 技能2 (腐蚀地带) - 1.5秒
            case 2: return 2.0f;  // 技能3 (灵魂锁链) - 2秒
            case 3: return 3.0f;  // 技能4 (死亡风暴) - 3秒
            case 4: return 0.6f;  // 普通攻击 - 0.6秒
            default: return 1.0f;
        }
    }
    
    /// <summary>
    /// 使用普通攻击
    /// </summary>
    private void UseBasicAttack()
    {
        if (skillManager == null || currentTarget == null || heroUnit == null)
            return;
            
        // 如果目标死亡，停止攻击
        if (currentTarget.IsDead)
            return;
            
        // 如果被眩晕，不能攻击
        if (heroUnit.isStunned)
            return;
            
        // 设置施法状态
        isCasting = true;
        
        // 停止移动
        if (rb != null)
            rb.velocity = Vector2.zero;
            
        // 确保朝向正确方向
        // 获取目标相对于AI的水平位置
        float horizontalDifference = currentTarget.transform.position.x - transform.position.x;
        
        // 调整角色朝向
        Vector3 scale = transform.localScale;
        if (horizontalDifference < 0)
        {
            // 目标在左边，让角色朝左
            scale.x = Mathf.Abs(scale.x) * -1;
            // Debug.Log("普攻朝向: 左侧目标");
        }
        else
        {
            // 目标在右边，让角色朝右
            scale.x = Mathf.Abs(scale.x);
            // Debug.Log("普攻朝向: 右侧目标");
        }
        transform.localScale = scale;
            
        // 触发动画 - 使用和NeController相同的动画触发命名
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("SkillTrigger4"); // 使用索引4对应普通攻击
        }
        
        // 获取施法时间 - 普通攻击设置为1秒
        float castingTime = 0.5f;
        
        // 先启动施法时间协程，动画结束后再激活技能，确保按照设计延迟施放
        StartCoroutine(UseBasicAttackWithDelay(castingTime));
    }
    
    /// <summary>
    /// 延迟激活普通攻击，确保按照设计的施法时间延迟施放
    /// </summary>
    /// <param name="castingTime">施法时间</param>
    /// <returns>协程</returns>
    private IEnumerator UseBasicAttackWithDelay(float castingTime)
    {
        // 保存开始施法时的目标引用
        Unit attackTarget = currentTarget;
        
        // 等待施法时间
        yield return new WaitForSeconds(castingTime);
        
        // 只检查施法者状态，不检查目标位置或Y轴对齐
        if (heroUnit.isStunned || heroUnit.IsDead)
        {
            isCasting = false;
            yield break;
        }
        
        // 无条件激活普通攻击，完全不考虑目标移动或Y轴对齐情况
        skillManager.ActivateSkill(4); // 普通攻击是索引4
        //Debug.Log("释放普通攻击 - 忽略目标移动和Y轴对齐");
        
        // 结束施法状态
        isCasting = false;
    }
    
    /// <summary>
    /// 执行撤退状态的行为
    /// </summary>
    private void ExecuteRetreatState()
    {
        // 根据子状态执行不同行为
        switch (currentSubState)
        {
            case AISubState.FindSafePosition:
                // 如果没有安全位置，寻找一个
                if (!hasSafePosition)
                {
                    FindSafePosition();
                }
                else
                {
                    // 已找到安全位置，切换到治疗子状态
                    currentSubState = AISubState.Healing;
                }
                break;
                
            case AISubState.Healing:
                // 向安全位置移动
                MoveToSafePosition();
                
                // 如果被追击，更积极地使用技能阻止追击者，但要确保技能能命中
                if (IsBeingChased())
                {
                    // 获取最近的追击者作为目标
                    Unit nearestChaser = GetNearestChaser();
                    if (nearestChaser != null)
                    {
                        // 保存当前目标
                        Unit originalTarget = currentTarget;
                        currentTarget = nearestChaser;
                        
                        // 确保朝向目标
                        FaceTarget();
                        
                        // 检查目标是否在适当的角度范围内（-10到10度）
                        bool isInAngleRange = IsTargetInAngleRange(currentTarget.transform.position, 10f);
                        
                        if (isInAngleRange)
                        {
                            // 优先使用灵魂锁链（技能3/U）进行眩晕控制
                            if (skillManager.IsSkillReady(2)) 
                            {
                                                                //Debug.Log("撤退时使用骷髅眩晕追击者");
                                UseSkill(2);
                                currentTarget = originalTarget; // 恢复原始目标
                                return; // 施法后不执行其他操作
                            }
                            // 其次使用腐蚀地带（技能2/L）减速敌人
                            else if (skillManager.IsSkillReady(1))
                            {
                                // Debug.Log("撤退时使用诅咒减速追击者");
                                UseSkill(1);
                                currentTarget = originalTarget; // 恢复原始目标
                                return; // 施法后不执行其他操作
                            }
                        }
                        else
                        {
                            // Debug.Log("撤退时敌人不在适当的角度范围内，不使用技能");
                            currentTarget = originalTarget; // 恢复原始目标
                        }
                    }
                }
                
                // 检查是否可以安全地攻击敌人
                if (CanSafelyAttackWhileRetreating())
                {
                    ExecuteOpportunisticAttack();
                }
                break;
        }
    }
    
    /// <summary>
    /// 寻找安全位置
    /// </summary>
    private void FindSafePosition()
    {
        // 直接使用右侧出生点的已知坐标
        Vector2 spawnPosition = new Vector2(40.27f, 6.96f);
        
        // 在出生点周围添加一些随机偏移，避免所有邪术师都堆在同一个点
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 3f; // 3单位半径的随机偏移
            
        safePosition = spawnPosition + offset;
        //Debug.Log("使用右侧出生点附近作为安全位置: " + safePosition);
        
        // 标记已找到安全位置
        hasSafePosition = true;
    }
    
    /// <summary>
    /// 寻找右侧城堡
    /// </summary>
    /// <returns>右侧城堡</returns>
    private right_castle FindRightCastle()
    {
        // 查找右侧城堡
        return FindObjectOfType<right_castle>();
    }
    
    /// <summary>
    /// 移动到安全位置
    /// </summary>
    private void MoveToSafePosition()
    {
        // 确保流场寻路逻辑在所有移动阶段都可用
        // 更新流场，保障每次移动都使用最新的流场数据
        // 撤退时使用更频繁的流场更新
        float retreatFlowUpdateInterval = flowFieldUpdateInterval * 0.5f; // 减半更新间隔，更频繁更新
        
        if (Time.time - lastFlowFieldUpdateTime > retreatFlowUpdateInterval)
        {
            if (FlowFieldManager.Instance != null)
            {
                // 使用统一大小的流场
                currentFlowField = FlowFieldManager.Instance.GenerateFlowField(safePosition, 20f);
                lastFlowFieldUpdateTime = Time.time;
                // Debug.Log($"撤退时更新流场寻路数据，目标位置: {safePosition}");
            }
        }
        
        // 移动到安全位置，使用流场寻路
        MoveToPosition(safePosition);
        
        // 如果已经非常接近安全位置，停止移动并开始自我恢复
        if (Vector2.Distance(transform.position, safePosition) < 1.5f)
        {
            // 停止移动
            if (rb != null)
        {
            rb.velocity = Vector2.zero;
            }
            
            // 治疗逻辑 - 更直接的每秒固定回血实现
            HealInSafeZone();
        }
        else
        {
            // 不在安全区域，重置治疗计时器
            healTimer = 0f;
        }
    }
    
    /// <summary>
    /// 在安全区域内治疗
    /// </summary>
    private void HealInSafeZone()
    {
        // 只有未死亡且血量未满时才需要回血
        if (heroUnit == null || heroUnit.IsDead || heroUnit.currentHealth >= heroUnit.maxHealth)
        {
            healTimer = 0f;
            return;
        }
        
        // 完全废弃Time.deltaTime计时方式，直接使用Time.time比较
        // 计算当前时间和上次回血时间的差值
        float currentTime = Time.time;
        
        // 如果没有初始化上次回血时间，设置为当前时间减去一个完整间隔
        if (healTimer <= 0)
        {
            healTimer = currentTime;
            return;
        }
        
        // 固定0.25秒回血一次，确保更快的回血频率
        float healInterval = 0.25f;
        
        // 检查是否达到回血间隔
        if (currentTime - healTimer >= healInterval)
        {
            // 根据难度确定每次回血量（每0.25秒回一次）
            float healAmount = difficulty == AIDifficulty.Hard ? 2.5f : 1.25f; // 10/秒或5/秒
            
            // 记录治疗前的血量
            float oldHealth = heroUnit.currentHealth;
            
            // 直接修改血量
            heroUnit.currentHealth = Mathf.Min(heroUnit.currentHealth + healAmount, heroUnit.maxHealth);
            
            // 手动触发血量更新事件
            heroUnit.OnHealthUpdated?.Invoke(heroUnit.currentHealth, heroUnit.maxHealth);
            
            // 计算实际回复的血量
            float actualHeal = heroUnit.currentHealth - oldHealth;
            
            // 只有实际回血时才输出日志
            if (actualHeal > 0)
            {
                float perSecondRate = healAmount * (1f / healInterval);
                // Debug.Log($"邪术师回血：+{actualHeal:F1}点生命值 (每秒{perSecondRate:F1}点)，当前生命值：{heroUnit.currentHealth:F1}/{heroUnit.maxHealth}");
            }
            
            // 更新上次回血时间
            healTimer = currentTime;
        }
    }
    
    /// <summary>
    /// 检查是否可以在撤退时安全攻击
    /// </summary>
    /// <returns>是否可以安全攻击</returns>
    private bool CanSafelyAttackWhileRetreating()
    {
        // 如果正在施法，不能安全攻击
        if (isCasting)
            return false;
            
        // 寻找可能的目标
        Unit target = FindBestTarget();
        
        // 如果没有目标，不能攻击
        if (target == null)
            return false;
            
        // 计算与目标的距离
        float distanceToTarget = Vector2.Distance(transform.position, target.transform.position);
        
        // 如果距离太近，不安全
        if (distanceToTarget < 5f)
            return false;
            
        // 如果有技能冷却完成，可以安全攻击
        for (int i = 0; i < 5; i++)
        {
            if (skillManager.IsSkillReady(i))
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 执行机会性攻击
    /// </summary>
    private void ExecuteOpportunisticAttack()
    {
        // 寻找最佳目标
        Unit target = FindBestTarget();
        
        // 如果找到目标，临时将其设为当前目标
        if (target != null)
        {
            Unit originalTarget = currentTarget;
            currentTarget = target;
            
            // 确保朝向目标
            FaceTarget();
            
            // 检查目标是否在适当的角度范围内（-10到10度）
            if (IsTargetInAngleRange(currentTarget.transform.position, 10f))
            {
            // 决定使用哪个技能
            int skillIndex = DecideSkillToUse();
            
            // 使用技能
            if (skillIndex >= 0)
            {
                    // Debug.Log($"撤退时执行机会性攻击，使用技能{skillIndex}");
                UseSkill(skillIndex);
                }
            }
            else
            {
                // Debug.Log("机会性攻击目标不在角度范围内，放弃攻击");
            }
            
            // 恢复原始目标
            currentTarget = originalTarget;
        }
    }
    
    /// <summary>
    /// 设置AI激活状态
    /// </summary>
    /// <param name="active">是否激活</param>
    public void SetActive(bool active)
    {
        isActive = active;
    }

    /// <summary>
    /// 获取最佳攻击目标
    /// </summary>
    /// <returns>最佳攻击目标</returns>
    private Unit GetBestTarget()
    {
        // 获取所有单位 - 使用GameManager的注册系统
        Unit[] units = GetRegisteredUnits();
        List<Unit> validTargets = new List<Unit>();

        // 过滤有效目标
        foreach (Unit unit in units)
        {
            // 排除无效、死亡或友方单位
            if (unit == null || unit.IsDead || unit.GetFaction() == heroUnit.GetFaction())
                continue;

            // 检查单位是否在攻击范围内
            float distance = Vector2.Distance(heroUnit.transform.position, unit.transform.position);
            if (distance > 15f)
                continue;

            // 添加到有效目标列表
            validTargets.Add(unit);
        }

        // 如果没有有效目标，检查敌方城堡
        if (validTargets.Count == 0)
        {
            // 在注册的单位中查找左侧城堡
            foreach (Unit unit in units)
            {
                if (unit is left_castle leftCastle && leftCastle.faction != heroUnit.faction &&
                    !leftCastle.IsDead &&
                    Vector2.Distance(heroUnit.transform.position, leftCastle.transform.position) <= 20f)
                {
                    return leftCastle;
                }
            }
            return null;
        }
        
        // 评估每个目标的价值
        Unit bestTarget = null;
        float bestScore = float.MinValue;
        
        foreach (Unit target in validTargets)
        {
            float score = CalculateTargetScore(target);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = target;
            }
        }
        
        return bestTarget;
    }
    
    /// <summary>
    /// 检查是否可以攻击敌方城堡
    /// </summary>
    /// <returns>是否可以攻击敌方城堡</returns>
    private bool CheckCanAttackEnemyCastle()
    {
        // 获取敌方城堡
        left_castle enemyCastle = FindObjectOfType<left_castle>();
        if (enemyCastle == null || enemyCastle.faction == heroUnit.faction || enemyCastle.IsDead)
            return false;
            
        // 检查城堡是否在攻击范围内
        float distance = Vector2.Distance(heroUnit.transform.position, enemyCastle.transform.position);
        if (distance > 15f)
            return false;
            
        // 检查是否有足够的友军支援 - 要求更多友军才考虑攻击城堡
        int friendlyCount = 0;
        int enemyCount = 0;
        
        Unit[] units = FindObjectsOfType<Unit>();
        foreach (Unit unit in units)
        {
            if (unit == null || unit.IsDead || unit == heroUnit)
                continue;
                
            // 计算单位与城堡的距离
            float unitToCastleDistance = Vector2.Distance(unit.transform.position, enemyCastle.transform.position);
            if (unitToCastleDistance <= 10f) // 在城堡附近的单位
            {
                if (unit.faction == heroUnit.faction)
                    friendlyCount++;
                else
                    enemyCount++;
            }
        }
        
        // 只有当友军数量显著多于敌军数量，且没有其他更好的目标时才考虑攻击城堡
        // 增加了更严格的条件，降低攻击城堡的可能性
        return friendlyCount > enemyCount * 2 && friendlyCount >= 3;
    }
    
    /// <summary>
    /// 统计友军单位数量
    /// </summary>
    /// <returns>友军数量</returns>
    private int CountFriendlyUnits()
    {
        int count = 0;
        
        // 获取所有单位
        Unit[] units = FindObjectsOfType<Unit>();
        
        // 统计友军数量
        foreach (Unit unit in units)
        {
            // 检查单位是否有效
            if (unit == null || unit.IsDead || unit == heroUnit)
                continue;
                
            // 检查单位是否为友军
            if (unit.faction == heroUnit.faction)
            {
                count++;
            }
        }
        
        return count;
    }

    /// <summary>
    /// 评估位置分数
    /// </summary>
    /// <param name="position">位置</param>
    /// <returns>位置分数</returns>
    private float EvaluatePosition(Vector2 position)
    {
        // 基础分数
        float score = 0f;
        
        // 如果没有目标，返回0分
        if (currentTarget == null)
            return 0f;
        
        // 获取目标位置
        Vector2 targetPosition = currentTarget.transform.position;
        
        // 计算与目标的距离
        float distanceToTarget = Vector2.Distance(position, targetPosition);
        
        // 计算理想施法距离的偏差（越接近理想距离，分数越高）
        float distanceDeviation = Mathf.Abs(distanceToTarget - idealCastingRange);
        
        // 基于距离给分 - 越接近理想距离，分数越高
        score += (10f - Mathf.Min(distanceDeviation, 10f)) * 10f;
        
        // 检查位置是否与目标形成正确的朝向关系（位置和目标在彼此前方）
        bool goodAlignment = (position.x < targetPosition.x && targetPosition.x < transform.position.x) || 
                             (position.x > targetPosition.x && targetPosition.x > transform.position.x);
        if (goodAlignment)
        {
            score += 30f; // 对于能够保持正确朝向的位置给予额外加分
        }
        
        // 检查视线是否被阻挡
        if (IsLineOfSightBlocked(position, targetPosition))
        {
            score -= 100f; // 视线被阻挡是个严重问题
        }
        
        // 检查是否接近地图边界
        if (IsNearMapBoundary(Vector2.zero))
        {
            score -= 50f; // 靠近边界不是好主意
        }
        
        // 检查位置是否足够空旷，便于施法
        if (!HasEnoughSpaceToCast())
        {
            score -= 30f;
        }
        
        // 检查周围敌军数量 - 避免被包围
        int enemiesNearby = CountEnemiesNearTarget(position, 5f);
        if (enemiesNearby > 1)
        {
            score -= enemiesNearby * 10f; // 周围敌人越多，风险越高
        }
        
        // 检查我方单位数量 - 靠近友军通常较安全
        int friendliesNearby = CountUnitsNearPosition(position, 5f, Unit.Faction.Right);
        score += friendliesNearby * 5f;
        
        return score;
    }

    /// <summary>
    /// 计算目标位置附近敌方单位的数量
    /// </summary>
    /// <param name="position">目标位置</param>
    /// <param name="radius">检测半径</param>
    /// <returns>敌方单位数量</returns>
    private int CountEnemiesNearTarget(Vector2 position, float radius)
    {
        int count = 0;
        Unit[] units = FindObjectsOfType<Unit>();
        
        foreach (Unit unit in units)
        {
            if (unit.faction != heroUnit.faction && !unit.IsDead)
            {
                float distance = Vector2.Distance(position, unit.transform.position);
                if (distance <= radius)
                {
                    count++;
                }
            }
        }
        
        return count;
    }

    /// <summary>
    /// 将全局SkillData转换为内部SkillData类型
    /// </summary>
    /// <param name="globalSkillData">全局技能数据</param>
    /// <returns>内部技能数据</returns>
    private SkillData ConvertToInternalSkillData(global::SkillData globalSkillData)
    {
        // 根据技能效果类型确定内部技能类型
        SkillType internalType = SkillType.DirectDamage; // 默认为直接伤害
        
        // 根据全局技能的效果类型和行为类型确定内部技能类型
        if (globalSkillData.effectType == global::SkillEffectType.Damage)
        {
            if (globalSkillData.behaviorType == global::SkillBehaviorType.AreaEffect ||
                globalSkillData.behaviorType == global::SkillBehaviorType.DelayedDamageArea)
            {
                internalType = SkillType.AreaDamage;
            }
            else
            {
                internalType = SkillType.DirectDamage;
            }
        }
        else if (globalSkillData.effectType == global::SkillEffectType.Heal)
        {
            internalType = SkillType.Healing;
        }
        else if (globalSkillData.effectType == global::SkillEffectType.Stun)
        {
            internalType = SkillType.Debuff;
        }
        
        // 创建并返回内部SkillData
        float damage = (globalSkillData.effectType == global::SkillEffectType.Damage) ? globalSkillData.effectValue : 0;
        float areaOfEffect = (globalSkillData.areaShape == global::EffectShape.Circle) ? globalSkillData.areaSize.x : 
                            Mathf.Max(globalSkillData.areaSize.x, globalSkillData.areaSize.y);
        float duration = globalSkillData.areaDuration;
        
        return new SkillData(internalType, damage, areaOfEffect, duration);
    }

    /// <summary>
    /// 死亡方法 - 使用与普通邪术师相同的逻辑
    /// </summary>
    private void Die()
    {
        if (heroUnit == null || heroUnit.IsDead) return;
        
        // 标记为死亡
        heroUnit.MarkAsDead();
        
        // 播放死亡动画
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("DeathTrigger");
            
            // 无法在运行时添加动画事件，使用协程来估计动画结束时间
            float estimatedDeathAnimTime = 1.5f; // 估计死亡动画时长为1.5秒
            StartCoroutine(HandleDeathAnimationEnd(estimatedDeathAnimTime));
        }
        else
        {
            // 如果没有动画器，直接调用死亡后的处理
            Debug.LogWarning("邪术师AI没有动画器，直接处理死亡");
            HandleDeath();
        }
        
        // 切换到死亡状态
        ChangeState(AIState.Dead);
        
        // 禁止移动和施法
        isCasting = true; // 防止继续攻击
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }
        
        Debug.Log("邪术师AI已死亡");
    }
    
    /// <summary>
    /// 等待死亡动画结束后处理
    /// </summary>
    /// <param name="animationDuration">估计的动画持续时间</param>
    /// <returns>协程</returns>
    private IEnumerator HandleDeathAnimationEnd(float animationDuration)
    {
        Debug.Log($"等待死亡动画播放完成，估计时长: {animationDuration}秒");
        yield return new WaitForSeconds(animationDuration);
        
        // 动画播放完成后处理死亡
        HandleDeath();
    }
    
    /// <summary>
    /// 处理死亡相关逻辑
    /// </summary>
    private void HandleDeath()
    {
        Debug.Log("邪术师AI死亡动画结束，准备重生过程");
        
        // 在销毁对象前，先设置重生
        ScheduleRespawn();
        
        // 销毁当前实例
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Unity动画事件：死亡动画结束事件处理
    /// 注意：这个方法可能不会被调用，因为动画事件需要在编辑器中设置
    /// </summary>
    public void OnDeathAnimationEnd()
    {
        // 如果动画事件被正确设置并调用了这个方法，直接处理死亡
        HandleDeath();
    }
    
    /// <summary>
    /// 安排重生过程
    /// </summary>
    private void ScheduleRespawn()
    {
        // 直接使用AIManager处理重生，而不创建额外对象
        AIManager aiManager = FindObjectOfType<AIManager>();
        
        if (aiManager != null)
        {
            // 使用AI管理器处理重生
            aiManager.ScheduleNecromancerRespawn(30f, (int)difficulty);
            // Debug.Log("通过AIManager安排邪术师AI在30秒后重生");
        }
        else
        {
            Debug.LogWarning("无法找到AIManager，邪术师无法重生");
        }
    }
    
    /// <summary>
    /// 检查目标是否在AI的前方
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    /// <returns>目标是否在前方</returns>
    private bool IsTargetInFront(Vector2 targetPosition)
    {
        // 获取AI当前朝向（基于localScale.x）
        bool facingRight = transform.localScale.x > 0;
        
        // 目标相对于AI的水平位置
        float horizontalDifference = targetPosition.x - transform.position.x;
        
        // 如果AI朝右，目标必须在右边；如果AI朝左，目标必须在左边
        return (facingRight && horizontalDifference > 0) || (!facingRight && horizontalDifference < 0);
    }
        
        /// <summary>
    /// 更新流场
        /// </summary>
    /// <param name="target">目标</param>
    private void UpdateFlowField(Transform target)
    {
        // 检查是否需要更新流场
        if (Time.time - lastFlowFieldUpdateTime < flowFieldUpdateInterval)
            return;
            
        // 检查FlowFieldManager是否存在
        if (FlowFieldManager.Instance == null)
            return;
            
                        // 更新流场
        currentFlowField = FlowFieldManager.Instance.GenerateFlowField(target.position, 20f); // 20单位半径（统一流场大小）
        lastFlowFieldUpdateTime = Time.time;
        }
        
        /// <summary>
    /// 获取最近的追击者
        /// </summary>
    /// <returns>最近的敌方单位</returns>
    private Unit GetNearestChaser()
    {
        // 获取所有单位 - 使用GameManager的注册系统
        Unit[] units = GetRegisteredUnits();
        Unit nearestUnit = null;
        float minDistance = float.MaxValue;
        
        // 寻找最近的敌人
        foreach (Unit unit in units)
        {
            // 检查单位是否有效
            if (unit == null || unit.IsDead || unit.faction == heroUnit.faction)
                continue;
                
            // 检查单位是否在追击范围内
            float distance = Vector2.Distance(heroUnit.transform.position, unit.transform.position);
            if (distance <= 10f && distance < minDistance) // 追击检测范围内的最近单位
            {
                minDistance = distance;
                nearestUnit = unit;
            }
        }
        
        return nearestUnit;
        }
        
            /// <summary>
    /// 检查目标是否在前方的角度范围内
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    /// <param name="maxAngle">最大角度（单侧，度）</param>
    /// <returns>是否在角度范围内</returns>
    private bool IsTargetInFrontAngle(Vector2 targetPosition, float maxAngle = 10f)
    {
        // 获取AI当前朝向
        Vector2 forwardDirection = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        
        // 计算到目标的方向
        Vector2 directionToTarget = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        
        // 计算角度
        float angle = Vector2.SignedAngle(forwardDirection, directionToTarget);
        
        // 检查是否在角度范围内
        return Mathf.Abs(angle) <= maxAngle;
    }

    /// <summary>
    /// 在Unity编辑器中显示调试用的可视化Gizmos
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 绘制避障检测范围
        Gizmos.color = Color.yellow;
        Vector3 detectionCenter = transform.position + new Vector3(0, -obstacleDetectionOffset, 0);
        Gizmos.DrawWireSphere(detectionCenter, obstacleAvoidanceRadius);
        
        // 绘制撤退安全位置（如果存在）
        if (hasSafePosition)
        {
            // 如果当前在撤退状态，使用绿色显示并连线
            if (currentState == AIState.Retreat)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(safePosition, 1.5f);
                Gizmos.DrawLine(transform.position, safePosition);
                
                // 绘制回血范围
                Gizmos.color = new Color(0, 1, 0, 0.3f); // 半透明绿色
                Gizmos.DrawSphere(safePosition, 1.5f);
            }
            else
            {
                // 如果不在撤退状态，只用淡绿色显示安全位置
                Gizmos.color = new Color(0, 0.5f, 0, 0.5f);
                Gizmos.DrawWireSphere(safePosition, 1.5f);
            }
        }
        
        // 显示出生点位置（使用硬编码坐标）
        Vector3 spawnPosition = new Vector3(40.27f, 6.96f, 0f);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(spawnPosition, 3.0f);
        Gizmos.DrawWireCube(spawnPosition, new Vector3(0.5f, 0.5f, 0f));
        
        // 绘制施法理想距离
        if (currentTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, idealCastingRange);
        }
    }

    /// <summary>
    /// 检查目标是否在指定角度范围内
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    /// <param name="maxAngle">最大角度</param>
    /// <returns>是否在角度范围内</returns>
    private bool IsTargetInAngleRange(Vector2 targetPosition, float maxAngle)
    {
        // 获取AI当前朝向
        Vector2 forwardDirection = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        
        // 计算到目标的方向
        Vector2 directionToTarget = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        
        // 计算角度
        float angle = Vector2.SignedAngle(forwardDirection, directionToTarget);
        
        // 计算Y轴高度差值
        float heightDifference = Mathf.Abs(targetPosition.y - transform.position.y);
        
        // 获取距离
        float distance = Vector2.Distance(transform.position, targetPosition);
        
        // Y轴对齐判断 - 根据距离动态调整容忍度
        bool isYAligned = false;
        
        // 根据距离动态调整Y轴容忍度
        if (distance <= 4.0f)
        {
            // 近距离时，容忍度适中
            isYAligned = heightDifference <= 2.0f;
        }
        else if (distance <= 7.0f)
        {
            // 中近距离，中等容忍度
            isYAligned = heightDifference <= 1.5f;
        }
        else if (distance <= 10.0f)
        {
            // 中等距离，较低容忍度
            isYAligned = heightDifference <= 1.4f;
        }
        else
        {
            // 远距离时(>10单位)，使用1.0f的固定值，要求更严格的对齐
            isYAligned = heightDifference <= 1.2f;
        }
        
        // 检查是否在角度范围内且Y轴对齐
        return Mathf.Abs(angle) <= maxAngle && isYAligned;
    }

    /// <summary>
    /// 检测并处理AI发呆状态
    /// </summary>
    private void DetectAndHandleIdleState()
    {
        // 如果AI未激活、死亡或被眩晕，不检测发呆
        if (!isActive || heroUnit.IsDead || heroUnit.isStunned || !isIdleDetectionActive)
            return;
            
        // 检查是否有行动（移动或攻击）
        bool hasMoved = Vector2.Distance(transform.position, lastPosition) > 0.1f;
        
        if (hasMoved || isCasting)
        {
            // 如果有移动或正在施法，更新最后行动时间
            lastActionTime = Time.time;
            lastPosition = transform.position;
        }
        else if (Time.time - lastActionTime > idleDetectionTime)
        {
            // 如果长时间没有行动，执行防呆措施
            HandleIdleState();
            lastActionTime = Time.time; // 重置计时器
        }
    }
    
    /// <summary>
    /// 处理AI发呆状态
    /// </summary>
    private void HandleIdleState()
    {
        // Debug.Log("检测到AI发呆，执行防呆措施");
        
        // 根据当前状态采取不同的防呆措施
        switch (currentState)
        {
            case AIState.Advance:
                // 在推进状态发呆，强制寻找新目标
                FindTarget();
                
                // 如果仍然没有目标，尝试直接移动到敌方城堡
                if (currentTarget == null)
                {
                    left_castle enemyCastle = FindLeftCastle();
                    if (enemyCastle != null)
                    {
                        // Debug.Log("AI发呆且没有目标，直接移动到敌方城堡");
                        MoveTowardsCastle(enemyCastle.transform.position);
                    }
                }
                break;
                
            case AIState.Engage:
                // 在交战状态发呆，可能是卡在边界或无法接近目标
                
                // 首先检查目标是否有效
                if (currentTarget == null || currentTarget.IsDead)
                {
                    // 目标无效，寻找新目标
                    FindTarget();
                    break;
                }
                
                // 检查是否在边界附近
                if (IsNearMapBoundary(Vector2.zero))
                {
                    // 在边界附近发呆，尝试随机移动一小段距离
                    TryRandomMove();
                }
                else
                {
                    // 不在边界但发呆，可能是目标不可达，切换到其他目标
                    Unit newTarget = FindBestTarget();
                    if (newTarget != null && newTarget != currentTarget)
                    {
                        // Debug.Log("AI发呆，切换到新目标");
                        currentTarget = newTarget;
                    }
                    else
                    {
                        // 如果没有其他目标，尝试随机移动
                        TryRandomMove();
                    }
                }
                break;
                
            case AIState.Retreat:
                // 在撤退状态发呆，可能是找不到安全位置
                // 强制移动到己方城堡方向
                right_castle friendlyCastle = FindRightCastle();
                if (friendlyCastle != null)
                {
                    // Debug.Log("AI在撤退状态发呆，强制移动到己方城堡方向");
                    MoveToPosition(friendlyCastle.transform.position);
                }
                else
                {
                    // 如果找不到己方城堡，尝试随机移动
                    TryRandomMove();
                }
                break;
                
            default:
                break;
        }
    }
    
    /// <summary>
    /// 尝试随机移动以打破发呆状态
    /// </summary>
    private void TryRandomMove()
    {
        // Debug.Log("AI尝试随机移动以打破发呆状态");
        
        // 随机选择一个方向
        Vector2[] directions = {
            new Vector2(0, 1),   // 上
            new Vector2(0, -1),  // 下
            new Vector2(1, 0),   // 右
            new Vector2(-1, 0),  // 左
            new Vector2(1, 1).normalized,   // 右上
            new Vector2(1, -1).normalized,  // 右下
            new Vector2(-1, 1).normalized,  // 左上
            new Vector2(-1, -1).normalized  // 左下
        };
        
        // 随机打乱方向数组
        for (int i = 0; i < directions.Length; i++)
        {
            int randomIndex = Random.Range(i, directions.Length);
            Vector2 temp = directions[i];
            directions[i] = directions[randomIndex];
            directions[randomIndex] = temp;
        }
        
        // 优先考虑Y轴移动方向，帮助调整Y轴对齐
        if (currentTarget != null)
        {
            float yDiff = currentTarget.transform.position.y - transform.position.y;
            if (Mathf.Abs(yDiff) > 1.0f)
            {
                Vector2 yDirection = new Vector2(0, Mathf.Sign(yDiff));
                if (!IsDirectionNearMapBoundary(yDirection, 2.0f))
                {
                    // 优先向目标Y轴方向移动
                    Vector2 movePosition = (Vector2)transform.position + yDirection * 2.0f;
                    MoveToPosition(movePosition);
                    Debug.Log($"优先向目标Y轴方向移动: ({movePosition.x}, {movePosition.y})");
                    return;
                }
            }
        }
        
        // 尝试每个方向，直到找到一个不会导致接近边界的方向
        foreach (Vector2 dir in directions)
        {
            if (!IsDirectionNearMapBoundary(dir, 2.0f))
            {
                // 找到安全方向，向该方向移动
                Vector2 movePosition = (Vector2)transform.position + dir * 2.0f;
                MoveToPosition(movePosition);
                Debug.Log($"随机移动到: ({movePosition.x}, {movePosition.y})");
                return;
            }
        }
    }

    /// <summary>
    /// 检查是否有任何技能准备好
    /// </summary>
    /// <returns>是否有任何技能准备好</returns>
    private bool IsAnySkillReady()
    {
        if (skillManager == null)
            return false;
            
        // 检查所有技能是否有准备好的
        for (int i = 0; i < 4; i++)
        {
            if (skillManager.IsSkillReady(i))
                return true;
        }
        
        // 检查普通攻击是否准备好
        return skillManager.IsSkillReady(4);
    }

    // 新增方法：检测和处理眩晕状态
    private void CheckAndHandleStunState()
    {
        // 如果AI控制的英雄被眩晕，确保动画设置为待机状态
        if (heroUnit != null && heroUnit.isStunned)
        {
            Animator animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetFloat("speed", 0f);
                // 确保任何其他动画参数也被重置
                rb.velocity = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// 获取注册的英雄单位（避免FindObjectsOfType）
    /// </summary>
    private HeroUnit[] GetRegisteredHeroes()
    {
        if (GameManager.Instance == null) return new HeroUnit[0];

        var heroes = new List<HeroUnit>();
        foreach (Unit unit in GameManager.Instance.GetRegisteredUnits())
        {
            if (unit is HeroUnit hero)
            {
                heroes.Add(hero);
            }
        }
        return heroes.ToArray();
    }

    /// <summary>
    /// 获取注册的所有单位（避免FindObjectsOfType）
    /// </summary>
    private Unit[] GetRegisteredUnits()
    {
        if (GameManager.Instance == null) return new Unit[0];

        var registeredUnits = GameManager.Instance.GetRegisteredUnits();
        var unitsArray = new Unit[registeredUnits.Count];
        registeredUnits.CopyTo(unitsArray);
        return unitsArray;
    }
}
