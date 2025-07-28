using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 封装一个阵营的所有全局强化数据和相关逻辑。
/// </summary>
public class FactionUpgradeManager : MonoBehaviour
{
    [Header("阵营")]
    public Unit.Faction faction;

    // 金币与资源
    public float goldGenerationRateAdditive = 0f;
    public float goldGenerationRateMultiplier = 1f;
    public float minionCostAdditive = 0f; // 默认0，减少费用为负值
    public float minionCostMultiplier = 1f; // 默认1，减少费用为小于1的值

    // 新增：当前金币数量
    [Header("金币")]
    public float currentGold = 0f;
    private float goldGenerationTimer = 0f;
    public float baseGoldGenerationRate = 1f; // 基础金币生成速度（每秒）
    public float goldGenerationInterval = 1f; // 金币生成间隔（秒）

    // 金币变化事件
    public System.Action<float> OnGoldChanged;

    // 单位通用属性强化 (按UnitType区分)
    private Dictionary<Unit.UnitType, UnitModifiers> unitModifiers = new Dictionary<Unit.UnitType, UnitModifiers>();

    // 小兵特定强化 (布尔型)
    [Header("特定小兵布尔型强化")]
    public bool soldierFullComboUnlocked = false;
    public bool soldierBlockUnlocked = false; // 新增：士兵格挡解锁
    public bool archerFullComboUnlocked = false; // 新增弓兵完整连击
    public bool archerPiercingArrowsUnlocked = false; // 弓兵穿透箭
    
    public bool _swordMasterUnlocked = false;
    public bool swordMasterUnlocked 
    { 
        get { return _swordMasterUnlocked; }
        set 
        { 
            if (_swordMasterUnlocked != value)
            {
                _swordMasterUnlocked = value;
                // 触发事件通知UI更新
                if (OnSwordMasterUnlockChanged != null)
                {
                    OnSwordMasterUnlockChanged(value);
                }
            }
        }
    }
    
    // 剑术大师解锁状态改变事件
    public System.Action<bool> OnSwordMasterUnlockChanged;

    // 小兵特定强化 (数值型)
    [Header("特定小兵数值型强化")]
    public float lancerChargeSpeedAdditive = 0f;
    public float lancerChargeSpeedMultiplier = 1f;
    public float archerAttackRangeAdditive = 0f;
    public float archerAttackRangeMultiplier = 1f;
    public float priestHealAmountAdditive = 0f;
    public float priestHealAmountMultiplier = 1f;
    public float mageAOERadiusAdditive = 0f;
    public float mageAOERadiusMultiplier = 1f;

    // 技能特定强化
    private Dictionary<SkillData, SkillModifiers> skillModifiers = new Dictionary<SkillData, SkillModifiers>();

    // 已激活的肉鸽强化列表
    public List<UpgradeDataSO> activeUpgrades = new List<UpgradeDataSO>();

    // 新增：添加金币方法
    public void AddGold(float amount)
    {
        currentGold += amount;
        // 触发金币变化事件
        OnGoldChanged?.Invoke(currentGold);
        //Debug.Log($"[金币系统] {faction} 阵营添加 {amount} 金币，当前总额: {currentGold}");
    }

    // 新增：消费金币方法
    public bool SpendGold(float amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            // 触发金币变化事件
            OnGoldChanged?.Invoke(currentGold);
            //Debug.Log($"[金币系统] {faction} 阵营消费 {amount} 金币，当前总额: {currentGold}");
            return true;
        }
        else
        {
            //Debug.Log($"[金币系统] {faction} 阵营金币不足，无法消费 {amount} 金币");
            return false;
        }
    }

    // 新增：获取当前金币数量
    public float GetCurrentGold()
    {
        return currentGold;
    }

    // 新增：获取当前金币生成速度
    public float GetCurrentGoldGenerationRate()
    {
        // 金币生成计算公式：(基础生成+加法金币数量和)*金币速率倍速
        return (baseGoldGenerationRate + goldGenerationRateAdditive) * goldGenerationRateMultiplier;
    }

    // 新增：更新方法，处理金币自动生成
    private void Update()
    {
        // 金币自动生成
        goldGenerationTimer += Time.deltaTime;
        if (goldGenerationTimer >= goldGenerationInterval)
        {
            goldGenerationTimer = 0f;
            float currentRate = GetCurrentGoldGenerationRate();
            float goldToGenerate = currentRate * goldGenerationInterval;
            if (goldToGenerate > 0)
            {
                AddGold(goldToGenerate);
                
                // // 每隔一段时间输出一次当前的金币生成速率
                // if (Time.frameCount % 300 == 0) // 大约每5秒输出一次（假设60帧/秒）
                // {
                //     //Debug.Log($"[金币系统] {faction} 阵营当前金币生成速率: {currentRate}/秒 (基础:{baseGoldGenerationRate} + 加法:{goldGenerationRateAdditive}) * 乘法:{goldGenerationRateMultiplier}");
                // }
            }
        }
    }

    public void ResetUpgrades()
    {
        // 重置金币与资源
        goldGenerationRateAdditive = 0f;
        goldGenerationRateMultiplier = 1f;
        minionCostAdditive = 0f;
        minionCostMultiplier = 1f;
        // 新增：重置金币（可选，取决于是否要在每次重置时清空金币）
        // currentGold = 0f;
        // goldGenerationTimer = 0f;

        // 重置小兵特定强化 (布尔型)
        soldierFullComboUnlocked = false;
        soldierBlockUnlocked = false;
        archerFullComboUnlocked = false;
        archerPiercingArrowsUnlocked = false;
        
        // 使用临时变量保存当前值，避免重置时触发不必要的事件
        bool oldValue = _swordMasterUnlocked;
        _swordMasterUnlocked = false;
        
        // 如果之前是true，现在是false，手动触发事件
        if (oldValue && OnSwordMasterUnlockChanged != null)
        {
            OnSwordMasterUnlockChanged(false);
        }

        // 重置小兵特定强化 (数值型)
        lancerChargeSpeedAdditive = 0f;
        lancerChargeSpeedMultiplier = 1f;
        archerAttackRangeAdditive = 0f;
        archerAttackRangeMultiplier = 1f;
        priestHealAmountAdditive = 0f;
        priestHealAmountMultiplier = 1f;
        mageAOERadiusAdditive = 0f;
        mageAOERadiusMultiplier = 1f;
        
        // 重置字典
        skillModifiers.Clear();
        unitModifiers.Clear();
        activeUpgrades.Clear();
        
        //Debug.Log($"阵营 {faction} 的所有强化已重置。");
    }

    /// <summary>
    /// 获取特定单位类型的强化修改器。
    /// </summary>
    public UnitModifiers GetUnitModifier(Unit.UnitType unitType)
    {
        // 如果单位类型是None，返回默认修改器
        if (unitType == Unit.UnitType.None)
        {
            return new UnitModifiers();
        }

        // 如果字典中不存在该单位类型的修改器，先创建一个
        if (!unitModifiers.ContainsKey(unitType))
        {
            unitModifiers[unitType] = new UnitModifiers();
        }

        return unitModifiers[unitType];
    }

    /// <summary>
    /// 添加或更新特定单位类型的通用属性强化。
    /// </summary>
    public void AddUnitModifier(Unit.UnitType targetUnitType, UpgradeDataSO.UpgradeValueType valueType, float value, UpgradeDataSO.UnitAttributeType attributeType, Unit.Faction faction)
    {
        if (!unitModifiers.ContainsKey(targetUnitType))
        {
            unitModifiers[targetUnitType] = new UnitModifiers();
        }

        // 获取当前场景中所有相关类型的单位
        // Unit[] allUnits = GameObject.FindObjectsOfType<Unit>();
        // List<Unit> targetUnits = new List<Unit>();
        // foreach (Unit unit in allUnits)
        // {
        //     // 增加阵营检查，确保只对正确阵营的、存活的单位应用强化
        //     if (unit.Type == targetUnitType && unit.faction == faction && !unit.IsDead)
        //     {
        //         targetUnits.Add(unit);
        //     }
        // }

        // 记录修改前的值
        float previousAdditive = 0f;
        float previousMultiplier = 1f;
        UnitModifiers currentModifiers = unitModifiers[targetUnitType];

        // 根据属性类型获取之前的值
        switch (attributeType)
        {
            case UpgradeDataSO.UnitAttributeType.Health:
                previousAdditive = currentModifiers.healthAdditive;
                previousMultiplier = currentModifiers.healthMultiplier;
                break;
            case UpgradeDataSO.UnitAttributeType.MoveSpeed:
                previousAdditive = currentModifiers.moveSpeedAdditive;
                previousMultiplier = currentModifiers.moveSpeedMultiplier;
                break;
            case UpgradeDataSO.UnitAttributeType.Defense:
                previousAdditive = currentModifiers.defenseAdditive;
                previousMultiplier = currentModifiers.defenseMultiplier;
                break;
            case UpgradeDataSO.UnitAttributeType.Damage:
                previousAdditive = currentModifiers.damageAdditive;
                previousMultiplier = currentModifiers.damageMultiplier;
                break;
        }

        // 应用新的强化
        switch (attributeType)
        {
            case UpgradeDataSO.UnitAttributeType.Health:
                if (valueType == UpgradeDataSO.UpgradeValueType.Additive)
                {
                    currentModifiers.healthAdditive += value;
                }
                else if (valueType == UpgradeDataSO.UpgradeValueType.Multiplicative)
                {
                    // 修改：直接将百分比值加到乘法系数上，而不是相乘
                    currentModifiers.healthMultiplier = 1 + ((currentModifiers.healthMultiplier - 1) + value);
                }
                break;
            case UpgradeDataSO.UnitAttributeType.MoveSpeed:
                if (valueType == UpgradeDataSO.UpgradeValueType.Additive)
                {
                    currentModifiers.moveSpeedAdditive += value;
                }
                else if (valueType == UpgradeDataSO.UpgradeValueType.Multiplicative)
                {
                    // 修改：直接将百分比值加到乘法系数上，而不是相乘
                    currentModifiers.moveSpeedMultiplier = 1 + ((currentModifiers.moveSpeedMultiplier - 1) + value);
                }
                break;
            case UpgradeDataSO.UnitAttributeType.Defense:
                if (valueType == UpgradeDataSO.UpgradeValueType.Additive)
                {
                    currentModifiers.defenseAdditive += value;
                }
                else if (valueType == UpgradeDataSO.UpgradeValueType.Multiplicative)
                {
                    // 修改：直接将百分比值加到乘法系数上，而不是相乘
                    currentModifiers.defenseMultiplier = 1 + ((currentModifiers.defenseMultiplier - 1) + value);
                }
                break;
            case UpgradeDataSO.UnitAttributeType.Damage:
                if (valueType == UpgradeDataSO.UpgradeValueType.Additive)
                {
                    currentModifiers.damageAdditive += value;
                }
                else if (valueType == UpgradeDataSO.UpgradeValueType.Multiplicative)
                {
                    // 修改：直接将百分比值加到乘法系数上，而不是相乘
                    currentModifiers.damageMultiplier = 1 + ((currentModifiers.damageMultiplier - 1) + value);
                }
                break;
        }

        // 遍历所有单位，并为相关单位重新计算属性
        Unit[] allUnitsInScene = GameObject.FindObjectsOfType<Unit>();
        foreach (Unit unit in allUnitsInScene)
        {
            if (unit.GetFaction() == faction && unit.Type == targetUnitType && !unit.IsDead)
            {
                unit.RecalculateStats();
            }
        }

        //Debug.Log($"更新单位类型 {targetUnitType} 的 {attributeType} 强化: {valueType}, 值: {value}");
        //Debug.Log($"影响的单位数量: {allUnitsInScene.Length} (阵营: {faction})");
    }

    /// <summary>
    /// 获取特定技能的强化修改器。
    /// </summary>
    public SkillModifiers GetSkillModifier(SkillData skill)
    {
        if (skillModifiers.TryGetValue(skill, out SkillModifiers modifiers))
        {
            return modifiers;
        }
        return new SkillModifiers(); // 返回默认值，避免空引用
    }

    /// <summary>
    /// 添加或更新特定技能的强化修改器。
    /// </summary>
    public void AddSkillModifier(SkillData targetSkill, UpgradeDataSO.UpgradeValueType valueType, float value, SkillData.SkillPropertyType propertyType)
    {
        if (!skillModifiers.ContainsKey(targetSkill))
        {
            skillModifiers[targetSkill] = new SkillModifiers();
        }

        SkillModifiers currentModifiers = skillModifiers[targetSkill];

        switch (propertyType)
        {
            case SkillData.SkillPropertyType.Damage:
                if (valueType == UpgradeDataSO.UpgradeValueType.Additive) currentModifiers.damageAdditive += value;
                else if (valueType == UpgradeDataSO.UpgradeValueType.Multiplicative) currentModifiers.damageMultiplier = 1 + ((currentModifiers.damageMultiplier - 1) + value);
                break;
            case SkillData.SkillPropertyType.Heal:
                if (valueType == UpgradeDataSO.UpgradeValueType.Additive) currentModifiers.healAdditive += value;
                else if (valueType == UpgradeDataSO.UpgradeValueType.Multiplicative) currentModifiers.healMultiplier = 1 + ((currentModifiers.healMultiplier - 1) + value);
                break;
            case SkillData.SkillPropertyType.Cooldown:
                if (valueType == UpgradeDataSO.UpgradeValueType.Multiplicative) {
                    // 冷却减少是特殊情况，需要用减法而不是加法
                    float currentReduction = 1 - currentModifiers.cooldownMultiplier;
                    currentModifiers.cooldownMultiplier = 1 - (currentReduction + value);
                }
                break;
            case SkillData.SkillPropertyType.StunDuration:
                if (valueType == UpgradeDataSO.UpgradeValueType.Additive) currentModifiers.stunDurationAdditive += value;
                else if (valueType == UpgradeDataSO.UpgradeValueType.Multiplicative) currentModifiers.stunDurationMultiplier = 1 + ((currentModifiers.stunDurationMultiplier - 1) + value);
                break;
            case SkillData.SkillPropertyType.Range: // 投射物射程强化
                if (valueType == UpgradeDataSO.UpgradeValueType.Additive) currentModifiers.rangeAdditive += value;
                else if (valueType == UpgradeDataSO.UpgradeValueType.Multiplicative) currentModifiers.rangeMultiplier = 1 + ((currentModifiers.rangeMultiplier - 1) + value);
                break;
            case SkillData.SkillPropertyType.AreaSize: // 新增：区域大小强化
                if (valueType == UpgradeDataSO.UpgradeValueType.Additive) currentModifiers.areaSizeAdditive += value;
                break;
        }
        skillModifiers[targetSkill] = currentModifiers; // 结构体需要重新赋值
        // Debug.Log($"更新技能 {targetSkill.name} 的 {propertyType} 强化: {valueType}, 值: {value}");
    }

    /// <summary>
    /// 将一个强化添加到已激活列表。
    /// </summary>
    public void AddActiveUpgrade(UpgradeDataSO upgrade)
    {
        if (!activeUpgrades.Contains(upgrade))
        {
            activeUpgrades.Add(upgrade);
        }
    }

    /// <summary>
    /// 检查是否满足所有前置强化条件。
    /// </summary>
    public bool HasAllRequiredUpgrades(List<UpgradeDataSO> requiredUpgrades)
    {
        if (requiredUpgrades == null || requiredUpgrades.Count == 0)
        {
            return true; // 没有前置条件则默认满足
        }

        foreach (UpgradeDataSO requiredUpgrade in requiredUpgrades)
        {
            if (!activeUpgrades.Contains(requiredUpgrade))
            {
                return false; // 存在未满足的前置条件
            }
        }
        return true; // 所有前置条件都已满足
    }

    /// <summary>
    /// 获取所有已激活的肉鸽强化列表。
    /// </summary>
    public List<UpgradeDataSO> GetAllActiveUpgrades()
    {
        return activeUpgrades;
    }
} 