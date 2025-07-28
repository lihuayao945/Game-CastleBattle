using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 角色技能管理器 - 管理角色的技能列表和冷却时间
/// </summary>
public class CharacterSkillManager : MonoBehaviour
{
    [Tooltip("技能数据列表")]
    public SkillData[] skills;

    // 技能冷却计时器
    private Dictionary<int, float> cooldownTimers = new Dictionary<int, float>();
    
    // 施法状态跟踪
    private bool isCasting = false;
    
    // 当前施法的技能索引
    private int currentCastingSkillIndex = -1;
    
    // 施法状态属性
    public bool IsCasting => isCasting;

    void Update()
    {
        // 更新所有技能的冷却时间
        UpdateCooldowns();
    }

    /// <summary>
    /// 激活指定索引的技能
    /// </summary>
    /// <param name="index">技能索引</param>
    public void ActivateSkill(int index)
    {
        // 检查游戏是否暂停
        if (GameManager.Instance.IsPaused) return;
        
        // 检查技能是否可用
        if (!IsSkillReady(index)) return;
        if (index < 0 || index >= skills.Length) return;

        // 获取技能数据
        SkillData skill = skills[index];
        
        // 计算技能生成位置
        Vector3 spawnPos = CalculateSpawnPosition(skill);
        
        // 调试当前角色朝向
        Vector2 facingDirection = DirectionHelper.GetFacingDirection(transform);
        //Debug.Log($"激活技能: {index}, 角色朝向: {facingDirection}, 生成位置: {spawnPos}");
        
        // 设置施法状态
        isCasting = true;
        currentCastingSkillIndex = index;
        
        // 使用技能工厂创建技能
        SkillFactory.CreateSkill(skill, transform, spawnPos);
        
        // 获取施法者单位组件以确定阵营
        Unit casterUnit = GetComponent<Unit>();
        if (casterUnit != null)
        {
            // 使用考虑强化后的冷却时间
            float finalCooldown = skill.GetFinalCooldown(casterUnit.faction);
            StartCooldown(index, finalCooldown);
            //Debug.Log($"技能{index}使用了强化后的冷却时间: {finalCooldown}秒 (原始: {skill.cooldownTime}秒)");
            
            // 延迟重置施法状态
            StartCoroutine(ResetCastingState(skill.castingTime));
        }
        else
        {
            // 如果找不到Unit组件，使用原始冷却时间
            StartCooldown(index, skill.cooldownTime);
            
            // 延迟重置施法状态
            StartCoroutine(ResetCastingState(skill.castingTime));
        }
    }
    
    /// <summary>
    /// 重置施法状态的协程
    /// </summary>
    private IEnumerator ResetCastingState(float delay)
    {
        yield return new WaitForSeconds(delay);
        isCasting = false;
        currentCastingSkillIndex = -1;
    }

    /// <summary>
    /// 获取技能数据
    /// </summary>
    /// <param name="index">技能索引</param>
    /// <returns>技能数据</returns>
    public SkillData GetSkillData(int index)
    {
        if (index >= 0 && index < skills.Length)
        {
            return skills[index];
        }
        return null;
    }
    
    /// <summary>
    /// 获取技能冷却百分比
    /// </summary>
    /// <param name="index">技能索引</param>
    /// <returns>冷却百分比（0-1，0表示冷却完成）</returns>
    public float GetCooldownPercentage(int index)
    {
        if (index < 0 || index >= skills.Length) return 0;
        
        if (!cooldownTimers.ContainsKey(index) || cooldownTimers[index] <= 0)
        {
            return 0f; // 冷却完成
        }
        
        // 获取技能的总冷却时间
        float totalCooldown = skills[index].cooldownTime;
        
        // 获取当前剩余冷却时间
        float remainingCooldown = cooldownTimers[index];
        
        // 计算冷却百分比
        return Mathf.Clamp01(remainingCooldown / totalCooldown);
    }

    /// <summary>
    /// 更新所有技能的冷却时间
    /// </summary>
    private void UpdateCooldowns()
    {
        List<int> keys = new List<int>(cooldownTimers.Keys);
        foreach (int index in keys)
        {
            if (cooldownTimers[index] > 0)
            {
                cooldownTimers[index] -= Time.deltaTime;
            }
        }
    }

    /// <summary>
    /// 检查技能是否准备就绪（冷却完成）
    /// </summary>
    /// <param name="index">技能索引</param>
    /// <returns>技能是否可用</returns>
    public bool IsSkillReady(int index)
    {
        if (index < 0 || index >= skills.Length) return false;
        return !cooldownTimers.ContainsKey(index) || cooldownTimers[index] <= 0;
    }

    /// <summary>
    /// 开始技能冷却
    /// </summary>
    /// <param name="index">技能索引</param>
    /// <param name="cooldown">冷却时间</param>
    private void StartCooldown(int index, float cooldown)
    {
        if (cooldownTimers.ContainsKey(index))
        {
            cooldownTimers[index] = cooldown;
        }
        else
        {
            cooldownTimers.Add(index, cooldown);
        }
    }

    /// <summary>
    /// 计算技能生成位置
    /// </summary>
    /// <param name="skill">技能数据</param>
    /// <returns>生成位置</returns>
    private Vector3 CalculateSpawnPosition(SkillData skill)
    {
        Vector3 basePos = transform.position;
        Vector2 direction = DirectionHelper.GetFacingDirection(transform);

        // 根据偏移类型计算生成位置
        return skill.spawnOffsetType switch
        {
            SpawnOffsetType.Forward =>
                basePos + (Vector3)(direction * skill.spawnDistance),
            SpawnOffsetType.Custom =>
                basePos + skill.followOffset,
            _ => basePos
        };
    }

    /// <summary>
    /// 在指定位置激活技能
    /// </summary>
    /// <param name="index">技能索引</param>
    /// <param name="targetPosition">目标位置</param>
    public void ActivateSkillAtPosition(int index, Vector2 targetPosition)
    {
        // 检查游戏是否暂停
        if (GameManager.Instance.IsPaused) return;
        
        // 检查技能是否可用
        if (!IsSkillReady(index)) return;
        if (index < 0 || index >= skills.Length) return;

        // 获取技能数据
        SkillData skill = skills[index];
        
        // 使用指定的目标位置作为生成位置
        Vector3 spawnPos = targetPosition;
        
        // 调试信息
        Vector2 facingDirection = DirectionHelper.GetFacingDirection(transform);
        //Debug.Log($"在指定位置激活技能: {index}, 角色朝向: {facingDirection}, 目标位置: {targetPosition}");
        
        // 设置施法状态
        isCasting = true;
        currentCastingSkillIndex = index;
        
        // 使用技能工厂创建技能
        SkillFactory.CreateSkill(skill, transform, spawnPos);
        
        // 获取施法者单位组件以确定阵营
        Unit casterUnit = GetComponent<Unit>();
        if (casterUnit != null)
        {
            // 使用考虑强化后的冷却时间
            float finalCooldown = skill.GetFinalCooldown(casterUnit.faction);
            StartCooldown(index, finalCooldown);
            
            // 延迟重置施法状态
            StartCoroutine(ResetCastingState(skill.castingTime));
        }
        else
        {
            // 如果找不到Unit组件，使用原始冷却时间
            StartCooldown(index, skill.cooldownTime);
            
            // 延迟重置施法状态
            StartCoroutine(ResetCastingState(skill.castingTime));
        }
    }
}