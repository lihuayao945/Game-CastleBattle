using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 强化数据 - 用于配置肉鸽强化的ScriptableObject
/// 可以在Unity编辑器中配置不同强化参数
/// </summary>
[CreateAssetMenu(fileName = "NewUpgrade", menuName = "Game/Upgrade Data")]
public class UpgradeDataSO : ScriptableObject
{
    [Header("UI Display")]
    public string upgradeName;
    [TextArea(3, 5)] // 多行文本输入
    public string description;
    public Sprite icon; // 强化图标

    [Header("Upgrade Logic")]
    public UpgradeType type;
    public UpgradeValueType valueType;
    public float value;
    public bool booleanValue;

    [Header("单位属性强化配置 (当类型为UnitAttribute时使用)")]
    [SerializeField] public Unit.UnitType targetUnitType; // 新增：要强化的单位类型
    [SerializeField] public UnitAttributeType unitAttributeType; // 新增：要强化的单位属性

    [Header("前置强化条件")]
    public List<UpgradeDataSO> requiredUpgrades = new List<UpgradeDataSO>(); // 新增：需要的前置强化

    [Header("特定技能强化配置 (当类型为SpecificSkill时使用)")]
    [SerializeField] public SkillData targetSkill; // 新增：要强化的特定技能
    [SerializeField] public SkillData.SkillPropertyType skillPropertyToModify; // 新增：要修改的技能属性

    [Header("肉鸽元素条件")]
    public List<UpgradeDataSO> requiredPrerequisites; // 需要的前置强化

    [Header("随机金币范围 (当类型为GoldRandomGain时使用)")]
    [SerializeField] public float minGoldAmount = 50f;
    [SerializeField] public float maxGoldAmount = 150f;

    public enum UpgradeType
    {
        // 金币与资源
        GoldGenerationRate,             // 金币增长速度
        MinionSpawnCost,                // 小兵召唤费用
        GoldInstantGain,                // 新增：立即获得金币（固定数量）
        GoldRandomGain,                 // 新增：立即获得随机数量的金币
        GoldSacrificeGain,              // 新增：牺牲城堡血量获得金币和生成速度

        // 单位属性强化 (英雄或小兵的通用属性，可指定UnitType)
        UnitAttribute, // 新增：通用单位属性强化

        // 特定小兵强化 (布尔型，兵种特有功能)
        SoldierBlockUnlock,             // 士兵解锁格挡反击 (布尔型)
        SoldierFullComboUnlock,         // 士兵解锁完整三段连击 (布尔型)
        ArcherPiercingArrowsUnlock,     // 弓箭手解锁穿透箭矢 (布尔型)
        ArcherFullComboUnlock,          // 弓箭手解锁完整两段连击 (布尔型)

        // 特定小兵强化 (数值型，兵种特有属性)
        LancerChargeSpeed,              // 长枪兵冲锋速度
        ArcherAttackRange,              // 弓箭手攻击范围
        PriestHealAmount,               // 祭司治疗量
        MageAOERadius,                  // 法师AOE范围

        SpecificHeroSkill, // 新增：针对英雄的特定技能强化
        SpecificMinionSkill, // 新增：针对小兵的特定技能强化 (如果小兵有多种技能)
        
        SwordMasterUnlock, // 新增：解锁剑术大师兵种
    }

    // 新增枚举：定义强化值的应用方式
    public enum UpgradeValueType
    {
        Additive,       // 加法强化 (例如：+5 生命值)
        Multiplicative, // 乘法强化 (例如：*1.1 伤害)
        Boolean         // 布尔强化 (例如：解锁功能)
    }

    // 新增枚举：指定要修改的单位属性类型
    public enum UnitAttributeType
    {
        None,
        Health,
        Damage,
        MoveSpeed,
        Defense,
        DetectionRange,
        // ... 其他可被强化的单位属性
    }

    /// <summary>
    /// 应用强化效果。这个方法会修改全局强化数据。
    /// </summary>
    /// <param name="targetFaction">指定强化应用于哪个阵营。</param>
    public virtual void ApplyUpgrade(Unit.Faction targetFaction)
    {
        //Debug.Log($"应用强化: {upgradeName} 类型: {type}, 值类型: {valueType}, 数值: {value} 到阵营: {targetFaction}");

        if (GlobalGameUpgrades.Instance == null)
        {
            //Debug.LogError("GlobalGameUpgrades Instance is null!");
            return;
        }

        FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(targetFaction);
        if (factionManager == null)
        {
            //Debug.LogError($"无法获取 {targetFaction} 阵营的强化管理器！");
            return;
        }

        switch (type)
        {
            case UpgradeType.GoldGenerationRate:
                // 金币生成率通常是全局的，不分阵营，但如果设计上金币属于特定阵营，则需要使用 factionManager
                // 这里假设金币生成率是全局的，为了演示，暂不修改 GlobalGameUpgrades.Instance.goldGenerationRateAdditive
                if (valueType == UpgradeValueType.Additive) factionManager.goldGenerationRateAdditive += value;
                else if (valueType == UpgradeValueType.Multiplicative) factionManager.goldGenerationRateMultiplier *= (1 + value);
                break;
            case UpgradeType.MinionSpawnCost:
                if (valueType == UpgradeValueType.Additive) factionManager.minionCostAdditive += value; // 减少费用，所以加负值
                else if (valueType == UpgradeValueType.Multiplicative) factionManager.minionCostMultiplier *= (1 - value); // 减少费用，所以乘小于1的值
                break;
                
            // 新增：立即获得固定金币
            case UpgradeType.GoldInstantGain:
                if (valueType == UpgradeValueType.Additive)
                {
                    // 添加固定数量的金币
                    factionManager.AddGold(value);
                    //Debug.Log($"[金币强化] {targetFaction} 阵营立即获得 {value} 金币");
                }
                break;
                
            // 新增：立即获得随机金币
            case UpgradeType.GoldRandomGain:
                if (valueType == UpgradeValueType.Additive)
                {
                    // 在设定的范围内随机生成金币数量，使用偏向低值的分布
                    float randomValue = Random.value;
                    
                    // 使用立方函数使分布更加偏向低值（低金币概率更高）
                    // 立方函数比平方根函数更加偏向低值
                    float normalizedValue = Mathf.Pow(randomValue, 3);
                    
                    // 调整范围，使高金额更加稀有
                    float randomGold = Mathf.Lerp(minGoldAmount, maxGoldAmount, normalizedValue);
                    
                    // 四舍五入到整数
                    randomGold = Mathf.Round(randomGold);
                    
                    factionManager.AddGold(randomGold);
                    //Debug.Log($"[金币强化] {targetFaction} 阵营立即获得随机金币 {randomGold}，范围 {minGoldAmount}-{maxGoldAmount}");
                }
                break;
                
            // 新增：牺牲城堡血量获得金币和生成速度
            case UpgradeType.GoldSacrificeGain:
                if (valueType == UpgradeValueType.Additive)
                {
                    // 找到对应阵营的城堡
                    Unit.UnitType castleType = targetFaction == Unit.Faction.Left ? Unit.UnitType.LeftCastle : Unit.UnitType.RightCastle;
                    Unit[] allUnits = GameObject.FindObjectsOfType<Unit>();
                    Unit castle = null;
                    
                    foreach (Unit unit in allUnits)
                    {
                        // 确保只找到对应阵营的城堡
                        if (unit.Type == castleType && unit.GetFaction() == targetFaction)
                        {
                            castle = unit;
                            break;
                        }
                    }
                    
                    if (castle == null)
                    {
                        //Debug.LogWarning($"无法找到{targetFaction}阵营的城堡，牺牲强化未生效");
                        return;
                    }
                    
                    // 扣除城堡50%当前血量
                    float sacrificeAmount = castle.currentHealth * 0.5f;
                    castle.TakeDamage(sacrificeAmount);
                    
                    // 获得金币和金币生成速度提升
                    float goldGain = value; // 直接使用value作为获得的金币数量
                    factionManager.AddGold(goldGain);
                    
                    // 增加金币生成速度（加法和乘法都增加一些）
                    float addRateGain = 4f;
                    float multRateGain = 0.1f; // 10%
                    factionManager.goldGenerationRateAdditive += addRateGain;
                    factionManager.goldGenerationRateMultiplier *= (1 + multRateGain);
                    
                    //Debug.Log($"[金币强化] {targetFaction} 阵营牺牲城堡{sacrificeAmount}血量，获得{goldGain}金币");
                    //Debug.Log($"[金币强化] {targetFaction} 阵营金币生成速度 +{addRateGain}，乘以{1 + multRateGain}");
                }
                break;

            case UpgradeType.UnitAttribute:
                if (targetUnitType == Unit.UnitType.None || unitAttributeType == UnitAttributeType.None)
                {
                    //Debug.LogWarning($"UpgradeDataSO {upgradeName} is a UnitAttribute upgrade but targetUnitType or unitAttributeType is None!");
                    return;
                }
                factionManager.AddUnitModifier(targetUnitType, valueType, value, unitAttributeType, targetFaction);
                break;

            // 特定小兵强化 (布尔型)
            case UpgradeType.SoldierBlockUnlock:
                if (valueType == UpgradeValueType.Boolean)
                {
                    //Debug.Log($"[DEBUG] 设置 soldierBlockUnlocked，之前值: {factionManager.soldierBlockUnlocked}，新值: {booleanValue}");
                    factionManager.soldierBlockUnlocked = booleanValue;
                    //Debug.Log($"[DEBUG] 设置后的值: {factionManager.soldierBlockUnlocked}");
                }
                break;
            case UpgradeType.SoldierFullComboUnlock:
                if (valueType == UpgradeValueType.Boolean)
                {
                    //Debug.Log($"[DEBUG] 设置 soldierFullComboUnlocked，之前值: {factionManager.soldierFullComboUnlocked}，新值: {booleanValue}");
                    factionManager.soldierFullComboUnlocked = booleanValue;
                    //Debug.Log($"[DEBUG] 设置后的值: {factionManager.soldierFullComboUnlocked}");
                }
                break;
            case UpgradeType.ArcherPiercingArrowsUnlock:
                if (valueType == UpgradeValueType.Boolean)
                {
                    //Debug.Log($"[DEBUG] 设置 archerPiercingArrowsUnlocked，之前值: {factionManager.archerPiercingArrowsUnlocked}，新值: {booleanValue}");
                    factionManager.archerPiercingArrowsUnlocked = booleanValue;
                    //Debug.Log($"[DEBUG] 设置后的值: {factionManager.archerPiercingArrowsUnlocked}");
                }
                break;
            case UpgradeType.ArcherFullComboUnlock:
                if (valueType == UpgradeValueType.Boolean)
                {
                    //Debug.Log($"[DEBUG] 设置 archerFullComboUnlocked，之前值: {factionManager.archerFullComboUnlocked}，新值: {booleanValue}");
                    factionManager.archerFullComboUnlocked = booleanValue;
                    //Debug.Log($"[DEBUG] 设置后的值: {factionManager.archerFullComboUnlocked}");
                }
                break;

            // 特定小兵强化 (数值型)
            case UpgradeType.LancerChargeSpeed:
                if (valueType == UpgradeValueType.Additive) factionManager.lancerChargeSpeedAdditive += value;
                else if (valueType == UpgradeValueType.Multiplicative) factionManager.lancerChargeSpeedMultiplier *= (1 + value);
                break;
            case UpgradeType.ArcherAttackRange:
                if (valueType == UpgradeValueType.Additive) factionManager.archerAttackRangeAdditive += value;
                else if (valueType == UpgradeValueType.Multiplicative) factionManager.archerAttackRangeMultiplier *= (1 + value);
                break;
            case UpgradeType.PriestHealAmount:
                if (valueType == UpgradeValueType.Additive) factionManager.priestHealAmountAdditive += value;
                else if (valueType == UpgradeValueType.Multiplicative) factionManager.priestHealAmountMultiplier *= (1 + value);
                break;
            case UpgradeType.MageAOERadius:
                if (valueType == UpgradeValueType.Additive) factionManager.mageAOERadiusAdditive += value;
                else if (valueType == UpgradeValueType.Multiplicative) factionManager.mageAOERadiusMultiplier *= (1 + value);
                break;
                
            case UpgradeType.SwordMasterUnlock:
                if (valueType == UpgradeValueType.Boolean)
                {
                    //Debug.Log($"[DEBUG] 设置 swordMasterUnlocked，阵营: {targetFaction}，之前值: {factionManager.swordMasterUnlocked}，新值: {booleanValue}");
                    factionManager.swordMasterUnlocked = booleanValue;
                    //Debug.Log($"[DEBUG] 设置后的值: {factionManager.swordMasterUnlocked}");
                    
                    // 强制刷新所有UI
                    MinionSpawnUI[] allUIs = FindObjectsOfType<MinionSpawnUI>();
                    foreach (var ui in allUIs)
                    {
                        ui.RefreshSwordMasterUI();
                    }
                }
                break;

            case UpgradeType.SpecificHeroSkill:
            case UpgradeType.SpecificMinionSkill:
                if (targetSkill == null)
                {
                    //Debug.LogWarning($"UpgradeDataSO {upgradeName} is a specific skill upgrade but targetSkill is null!");
                    return;
                }
                factionManager.AddSkillModifier(targetSkill, valueType, value, skillPropertyToModify);
                break;
        }
        factionManager.AddActiveUpgrade(this); // 将当前强化添加到特定阵营的已激活列表
        //Debug.Log($"[DEBUG] 强化 {upgradeName} 已添加到阵营 {targetFaction} 的激活列表，当前激活强化数量: {factionManager.activeUpgrades.Count}");
    }

    /// <summary>
    /// 应用单位属性强化到所有当前活跃的单位。
    /// </summary>
    /// <param name="faction">要更新单位的阵营。</param>
    private void ApplyUnitAttributeUpgrade(Unit.Faction faction)
    {
        // 查找所有场景中存在的Unit实例
        Unit[] activeUnits = FindObjectsOfType<Unit>();
        foreach (Unit unit in activeUnits)
        {
            // 只更新指定阵营和类型的单位
            if (unit.faction == faction && unit.Type == targetUnitType)
            {
                // 根据强化类型应用相应的值
                if (valueType == UpgradeValueType.Additive)
                {
                    unit.ApplySingleUpgrade(unitAttributeType, value, 0);
                }
                else if (valueType == UpgradeValueType.Multiplicative)
                {
                    unit.ApplySingleUpgrade(unitAttributeType, 0, value);
                }
                //Debug.Log($"更新 {faction} 阵营单位 {unit.name} 的属性: {unitAttributeType}, 值类型: {valueType}, 值: {value}");
            }
        }
    }
} 