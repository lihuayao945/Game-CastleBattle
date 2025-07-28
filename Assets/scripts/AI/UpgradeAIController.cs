using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MagicBattle;

/// <summary>
/// 强化选择AI控制器，负责控制右侧强化的选择
/// </summary>
public class UpgradeAIController : MonoBehaviour
{
    // AI难度
    private NecromancerAIController.AIDifficulty difficulty;
    
    // 状态标志
    private bool isActive = false;
    
    // 已选择的强化路线
    private List<string> selectedUpgradePaths = new List<string>();
    
    // 当前可用的强化选项
    private List<UpgradeDataSO> currentUpgradeOptions = new List<UpgradeDataSO>();
    
    // 右侧阵营的强化管理器
    private FactionUpgradeManager rightFactionUpgrades;
    
    // 强化计时器
    private float upgradeTimer = 0f;
    [SerializeField] private float timeToNextUpgrade = 30f; // 每60秒触发一次强化选择
    
    /// <summary>
    /// 初始化强化选择AI
    /// </summary>
    /// <param name="difficulty">AI难度</param>
    public void Initialize(NecromancerAIController.AIDifficulty difficulty)
    {
        this.difficulty = difficulty;
        
        // 激活AI
        isActive = true;
        
        // 获取右侧阵营的强化管理器
        rightFactionUpgrades = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Right);
        if (rightFactionUpgrades == null)
        {
            Debug.LogError("UpgradeAIController: 无法获取右侧阵营的FactionUpgradeManager");
        }
        
        // 重置强化计时器
        upgradeTimer = 0f;
    }
    
    private void Update()
    {
        // 如果AI未激活或游戏暂停，不执行AI逻辑
        if (!isActive || Time.timeScale == 0)
            return;
            
        // 强化选择计时
        upgradeTimer += Time.deltaTime;
        if (upgradeTimer >= timeToNextUpgrade)
        {
            upgradeTimer = 0f; // 重置计时器
            GenerateAndSelectRightSideUpgrades();
        }
    }
    
    /// <summary>
    /// 为右侧阵营生成强化选项并选择
    /// </summary>
    private void GenerateAndSelectRightSideUpgrades()
    {
        StartCoroutine(GenerateAndSelectRightSideUpgradesCoroutine());
    }
    
    /// <summary>
    /// 为右侧阵营生成强化选项并选择的协程
    /// </summary>
    private IEnumerator GenerateAndSelectRightSideUpgradesCoroutine()
    {
        // 等待一帧，确保所有系统都已初始化
        yield return null;
        
        // 清空当前选项列表
        currentUpgradeOptions.Clear();
        
        // 加载所有可用的强化数据
        UpgradeDataSO[] allUpgrades = Resources.LoadAll<UpgradeDataSO>("UpgradeData");
        
        // 查找右侧阵营的英雄类型
        HeroType rightHeroType = HeroType.Necromancer; // 默认为邪术师
        
        // 查找场景中的右侧英雄
        HeroUnit[] heroes = FindObjectsOfType<HeroUnit>();
        foreach (HeroUnit hero in heroes)
        {
            if (hero.faction == Unit.Faction.Right)
            {
                // 使用Unit.Type属性而不是CompareTag
                if (hero.Type == Unit.UnitType.Knight)
                {
                    rightHeroType = HeroType.Knight;
                }
                else
                {
                    rightHeroType = HeroType.Necromancer;
                }
                break;
            }
        }
        
        // 过滤出满足条件的强化
        List<UpgradeDataSO> eligibleUpgrades = new List<UpgradeDataSO>();
        
        foreach (UpgradeDataSO upgrade in allUpgrades)
        {
            if (upgrade == null) continue;
            
            // 检查前置条件
            bool hasRequirements = rightFactionUpgrades.HasAllRequiredUpgrades(upgrade.requiredUpgrades);
            
            // 检查是否已激活
            bool isNotActive = !rightFactionUpgrades.activeUpgrades.Contains(upgrade);
            
            // 检查是否适用于当前英雄
            bool isApplicable = IsUpgradeApplicableForHero(upgrade, rightHeroType);
            
            if (hasRequirements && isNotActive && isApplicable)
            {
                eligibleUpgrades.Add(upgrade);
            }
        }
        
        if (eligibleUpgrades.Count == 0)
        {
            //Debug.LogWarning("UpgradeAIController: 没有可用的强化选项");
            yield break;
        }
        
        // 随机选择3个强化选项（或更少，如果可用的不足3个）
        int numOptions = Mathf.Min(3, eligibleUpgrades.Count);
        
        for (int i = 0; i < numOptions; i++)
        {
            if (eligibleUpgrades.Count == 0) break;
            
            int randomIndex = Random.Range(0, eligibleUpgrades.Count);
            currentUpgradeOptions.Add(eligibleUpgrades[randomIndex]);
            eligibleUpgrades.RemoveAt(randomIndex);
        }
        
        // 添加一个短暂延迟，模拟AI"思考"时间
        float delay = (difficulty == NecromancerAIController.AIDifficulty.Normal) ? 
                      Random.Range(0f, 1.0f) : Random.Range(0f, 0.5f);
                      
        yield return new WaitForSeconds(delay);
        
        // 选择最佳强化并应用
        if (currentUpgradeOptions.Count > 0)
        {
            UpgradeDataSO bestUpgrade = SelectBestUpgrade(currentUpgradeOptions);
            if (bestUpgrade != null)
            {
                // 直接应用强化
                bestUpgrade.ApplyUpgrade(Unit.Faction.Right);
                //Debug.Log($"AI选择强化：{bestUpgrade.upgradeName}");
                
                // 特殊处理剑术大师解锁强化
                if (bestUpgrade.type == UpgradeDataSO.UpgradeType.SwordMasterUnlock)
                {
                    // 确保剑术大师解锁状态被正确设置
                    if (rightFactionUpgrades != null)
                    {
                        rightFactionUpgrades.swordMasterUnlocked = true;
                        //Debug.Log("AI选择了剑术大师解锁，强制设置swordMasterUnlocked = true");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 判断强化是否适用于指定英雄类型
    /// </summary>
    private bool IsUpgradeApplicableForHero(UpgradeDataSO upgrade, HeroType heroType)
    {
        // 获取英雄的UnitType
        Unit.UnitType heroUnitType = (heroType == HeroType.Knight) ? Unit.UnitType.Knight : Unit.UnitType.Necromancer;
        
        // 检查强化类型
        switch (upgrade.type)
        {
            case UpgradeDataSO.UpgradeType.SpecificHeroSkill:
                // 英雄特定技能强化，必须匹配当前英雄
                if (upgrade.targetUnitType == heroUnitType)
                {
                    return true;
                }
                
                // 检查目标技能是否属于当前英雄
                if (upgrade.targetSkill != null)
                {
                    string heroDataPath = (heroType == HeroType.Knight) ? "Heroes/Knight" : "Heroes/Necromancer";
                    HeroDataSO heroData = Resources.Load<HeroDataSO>(heroDataPath);
                    
                    if (heroData != null)
                    {
                        foreach (var skill in heroData.skills)
                        {
                            if (skill.name == upgrade.targetSkill.name)
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
                
            case UpgradeDataSO.UpgradeType.UnitAttribute:
                // 单位属性强化，检查目标单位类型
                if (upgrade.targetUnitType == Unit.UnitType.Knight || 
                    upgrade.targetUnitType == Unit.UnitType.Necromancer)
                {
                    // 如果是英雄单位类型，必须匹配当前英雄
                    return upgrade.targetUnitType == heroUnitType;
                }
                // 如果是小兵单位类型，所有小兵强化都可用
                return true;
                
            // 金币相关强化和通用强化，适用于所有英雄
            case UpgradeDataSO.UpgradeType.GoldGenerationRate:
            case UpgradeDataSO.UpgradeType.GoldInstantGain:
            case UpgradeDataSO.UpgradeType.GoldRandomGain:
            case UpgradeDataSO.UpgradeType.GoldSacrificeGain:
            case UpgradeDataSO.UpgradeType.MinionSpawnCost:
            case UpgradeDataSO.UpgradeType.SwordMasterUnlock:
                return true;
                
            // 小兵相关强化，适用于所有英雄
            case UpgradeDataSO.UpgradeType.SoldierBlockUnlock:
            case UpgradeDataSO.UpgradeType.SoldierFullComboUnlock:
            case UpgradeDataSO.UpgradeType.ArcherPiercingArrowsUnlock:
            case UpgradeDataSO.UpgradeType.ArcherFullComboUnlock:
            case UpgradeDataSO.UpgradeType.LancerChargeSpeed:
            case UpgradeDataSO.UpgradeType.ArcherAttackRange:
            case UpgradeDataSO.UpgradeType.PriestHealAmount:
            case UpgradeDataSO.UpgradeType.MageAOERadius:
            case UpgradeDataSO.UpgradeType.SpecificMinionSkill:
                return true;
                
            default:
                return true;
        }
    }
    
    /// <summary>
    /// 根据名称查找强化数据
    /// </summary>
    /// <param name="upgradeName">强化名称</param>
    /// <returns>强化数据</returns>
    private UpgradeDataSO FindUpgradeByName(string upgradeName)
    {
        // 从Resources加载所有强化数据
        UpgradeDataSO[] allUpgrades = Resources.LoadAll<UpgradeDataSO>("UpgradeData");
        
        // 查找匹配名称的强化
        foreach (UpgradeDataSO upgrade in allUpgrades)
        {
            if (upgrade.upgradeName == upgradeName)
            {
                return upgrade;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 选择最佳强化
    /// </summary>
    /// <param name="availableUpgrades">可用强化列表</param>
    /// <returns>最佳强化</returns>
    private UpgradeDataSO SelectBestUpgrade(List<UpgradeDataSO> availableUpgrades)
    {
        // 如果只有一个选项，直接返回
        if (availableUpgrades.Count == 1)
            return availableUpgrades[0];
            
        // 评估每个强化
        List<UpgradeEvaluation> evaluations = new List<UpgradeEvaluation>();
        
        foreach (UpgradeDataSO upgrade in availableUpgrades)
        {
            float score = EvaluateUpgrade(upgrade);
            evaluations.Add(new UpgradeEvaluation(upgrade, score));
        }
        
        // 排序评估结果（按分数降序）
        evaluations.Sort((a, b) => b.score.CompareTo(a.score));
        
        // 根据难度决定随机选择的概率
        float randomSelectionChance = (difficulty == NecromancerAIController.AIDifficulty.Normal) ? 0.3f : 0.1f;
        
        // 有一定概率选择次优强化
        if (evaluations.Count > 1 && Random.value < randomSelectionChance)
        {
            int randomIndex = Random.Range(1, evaluations.Count);
            return evaluations[randomIndex].upgrade;
        }
        
        // 返回最佳强化
        return evaluations[0].upgrade;
    }
    
    /// <summary>
    /// 评估强化价值
    /// </summary>
    /// <param name="upgrade">强化</param>
    /// <returns>强化价值</returns>
    private float EvaluateUpgrade(UpgradeDataSO upgrade)
    {
        float score = 0f;
        
        // 基础分数
        score += 50f;
        
        // 根据强化类型评分
        switch (upgrade.type)
        {
            case UpgradeDataSO.UpgradeType.UnitAttribute:
                // 根据属性类型评分
                if (upgrade.unitAttributeType == UpgradeDataSO.UnitAttributeType.Damage)
                {
                    score += 80f;
                }
                else if (upgrade.unitAttributeType == UpgradeDataSO.UnitAttributeType.Health)
                {
                    score += 60f;
                }
                else if (upgrade.unitAttributeType == UpgradeDataSO.UnitAttributeType.MoveSpeed)
                {
                    score += 50f;
                }
                else if (upgrade.unitAttributeType == UpgradeDataSO.UnitAttributeType.Defense)
                {
                    score += 70f;
                }
                break;
                
            case UpgradeDataSO.UpgradeType.GoldGenerationRate:
                score += 120f;
                break;
                
            // case UpgradeDataSO.UpgradeType.MinionSpawnCost:
            //     score += 85f;
            //     break;
                
            case UpgradeDataSO.UpgradeType.GoldInstantGain:
                score += 90f;
                break;
                
            case UpgradeDataSO.UpgradeType.GoldRandomGain:
                score += 85f;
                break;
                
            case UpgradeDataSO.UpgradeType.SwordMasterUnlock:
                score += 20f;
                break;
                
            case UpgradeDataSO.UpgradeType.SpecificHeroSkill:
                score += 80f;
                break;
                
            case UpgradeDataSO.UpgradeType.SpecificMinionSkill:
                score += 75f;
                break;
                
            // 小兵特定强化
            case UpgradeDataSO.UpgradeType.SoldierBlockUnlock:
            case UpgradeDataSO.UpgradeType.SoldierFullComboUnlock:
                score += 85f;
                break;
                
            case UpgradeDataSO.UpgradeType.ArcherPiercingArrowsUnlock:
            case UpgradeDataSO.UpgradeType.ArcherFullComboUnlock:
                score += 80f;
                break;
                
            case UpgradeDataSO.UpgradeType.LancerChargeSpeed:
            case UpgradeDataSO.UpgradeType.ArcherAttackRange:
            case UpgradeDataSO.UpgradeType.PriestHealAmount:
            case UpgradeDataSO.UpgradeType.MageAOERadius:
                score += 70f;
                break;
        }
        
        // 根据AI难度调整评分
        if (difficulty == NecromancerAIController.AIDifficulty.Normal)
        {
            // 普通难度下，增加随机性
            score *= Random.Range(0.8f, 1.2f);
        }
        else
        {
            // 困难难度下，更倾向于选择金币生成、小兵成本减少和士兵相关强化
            if (upgrade.type == UpgradeDataSO.UpgradeType.GoldGenerationRate || 
                upgrade.type == UpgradeDataSO.UpgradeType.MinionSpawnCost)
            {
                score *= 1.3f;
            }
            
            // 困难模式下提高士兵相关强化的权重
            if (upgrade.type == UpgradeDataSO.UpgradeType.SoldierBlockUnlock || 
                upgrade.type == UpgradeDataSO.UpgradeType.SoldierFullComboUnlock)
            {
                score *= 1.4f;
            }
            
            // 困难模式下提高单位属性中伤害和防御的权重
            if (upgrade.type == UpgradeDataSO.UpgradeType.UnitAttribute)
            {
                if (upgrade.unitAttributeType == UpgradeDataSO.UnitAttributeType.Damage ||
                    upgrade.unitAttributeType == UpgradeDataSO.UnitAttributeType.Defense)
                {
                    score *= 1.2f;
                }
            }
        }
        
        return score;
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
    /// 设置AI难度
    /// </summary>
    /// <param name="difficulty">难度</param>
    public void SetDifficulty(NecromancerAIController.AIDifficulty difficulty)
    {
        this.difficulty = difficulty;
    }
    
    /// <summary>
    /// 强化评估结构体
    /// </summary>
    private struct UpgradeEvaluation
    {
        public UpgradeDataSO upgrade;
        public float score;
        
        public UpgradeEvaluation(UpgradeDataSO upgrade, float score)
        {
            this.upgrade = upgrade;
            this.score = score;
        }
    }
    
    /// <summary>
    /// 英雄类型枚举
    /// </summary>
    public enum HeroType
    {
        Knight,
        Necromancer
    }
}
