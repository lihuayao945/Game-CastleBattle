using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using MagicBattle; // 添加命名空间引用

public class UpgradeSelectionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject upgradeSelectionPanel; // 主面板
    [SerializeField] private Transform cardsContainer; // 卡片容器 (Horizontal/GridLayoutGroup父级)
    [SerializeField] private UpgradeCardUI upgradeCardPrefab; // 强化卡片Prefab

    [Header("Settings")]
    [SerializeField] private int numberOfChoices = 3; // 每次提供3个选择

    [Header("Testing")]
    [SerializeField] private UpgradeDataSO forcedFirstUpgrade; // 用于测试的强制第一个强化选项
    [SerializeField] private bool enableTestMode = false; // 是否启用测试模式

    private List<UpgradeDataSO> allAvailableUpgrades; // 所有UpgradeDataSO资产

    // 事件：当强化选择UI被显示/隐藏时通知GameManager
    public static event System.Action<bool> OnUpgradePanelVisibilityChanged;

    private void Awake()
    {
        // 确保面板初始状态是隐藏的
        upgradeSelectionPanel.SetActive(false);

        // 加载所有UpgradeDataSO资产到allAvailableUpgrades
        Object[] loadedAssets = Resources.LoadAll("UpgradeData");

        if (loadedAssets != null && loadedAssets.Length > 0)
        {
            foreach (var asset in loadedAssets)
            {
            }
        }

        allAvailableUpgrades = Resources.LoadAll<UpgradeDataSO>("UpgradeData").ToList();

        if (allAvailableUpgrades == null || allAvailableUpgrades.Count == 0)
        {
            //Debug.LogError("Awake中未找到任何 UpgradeDataSO 资产。请确保它们在 Resources/UpgradeData 文件夹下。");
            return;
        }
    }

    private void Start()
    {
        // Start方法现在是空的，因为加载逻辑已移到Awake
    }

    /// <summary>
    /// 显示强化选择面板并填充选项。
    /// </summary>
    /// <param name="playerFaction">当前玩家所属的阵营。</param>
    public void ShowUpgradeSelection(Unit.Faction playerFaction)
    {
        // 移除重复的加载和null检查，因为Awake中已经处理
        if (allAvailableUpgrades == null || allAvailableUpgrades.Count == 0)
        {
            //Debug.LogError("allAvailableUpgrades仍然为null或空，无法显示强化选择。");
            return;
        }

        // 确保面板是激活的
        upgradeSelectionPanel.SetActive(true);
        
        // 确保暂停面板是隐藏的
        var uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.HidePause();
        }
        
        // 通知游戏管理器UI已显示
        OnUpgradePanelVisibilityChanged?.Invoke(true);

        // 清除之前的卡片
        foreach (Transform child in cardsContainer)
        {
            Destroy(child.gameObject);
        }

        // 过滤和选择强化
        // 1. 获取玩家阵营的FactionUpgradeManager
        FactionUpgradeManager playerFactionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(playerFaction);

        if (playerFactionManager == null)
        {
            //Debug.LogError($"无法获取{playerFaction}阵营的FactionUpgradeManager");
            return;
        }

        // 2. 获取当前阵营的英雄类型
        Unit.UnitType currentHeroType = Unit.UnitType.Knight; // 默认值设为Knight
        MagicBattle.HeroType? heroTypeEnum = null;
        bool foundHeroInScene = false;
        
        // 查找当前阵营的英雄单位
        HeroUnit[] heroes = FindObjectsOfType<HeroUnit>();
        foreach (var hero in heroes)
        {
            if (hero.faction == playerFaction)
            {
                currentHeroType = hero.Type;
                foundHeroInScene = true;
                //Debug.Log($"在场景中找到阵营 {playerFaction} 的英雄，类型: {currentHeroType}");
                break;
            }
        }
        
        // 获取选中的英雄索引，用于确定英雄类型
        int selectedHeroIndex = PlayerPrefs.GetInt("SelectedHeroIndex", 0);
        heroTypeEnum = (MagicBattle.HeroType)selectedHeroIndex;
        
        // 如果场景中找到英雄，但与PlayerPrefs中的英雄类型不匹配，优先使用场景中的英雄类型
        if (foundHeroInScene)
        {
            // 将UnitType映射回HeroType
            if (currentHeroType == Unit.UnitType.Knight)
            {
                heroTypeEnum = MagicBattle.HeroType.Knight;
            }
            else if (currentHeroType == Unit.UnitType.Necromancer)
            {
                heroTypeEnum = MagicBattle.HeroType.Necromancer;
            }
        }
        
        //Debug.Log($"当前阵营 {playerFaction} 的英雄类型: {currentHeroType}，HeroType枚举: {heroTypeEnum}，是否在场景中找到: {foundHeroInScene}");
        
        // 3. 过滤出满足前置条件且尚未激活的强化，并且与当前英雄相关
        //Debug.Log($"开始过滤强化，总数: {allAvailableUpgrades.Count}");
        
        List<UpgradeDataSO> eligibleUpgrades = new List<UpgradeDataSO>();
        
        foreach (var upgrade in allAvailableUpgrades)
        {
            if (upgrade == null) continue;
            
            bool hasRequirements = playerFactionManager.HasAllRequiredUpgrades(upgrade.requiredUpgrades);
            bool isNotActive = !playerFactionManager.activeUpgrades.Contains(upgrade);
            bool isApplicable = IsUpgradeApplicableForHero(upgrade, currentHeroType, heroTypeEnum);
            
            //Debug.Log($"强化 '{upgrade.upgradeName}' 过滤结果: 满足前置条件={hasRequirements}, 未激活={isNotActive}, 适用于当前英雄={isApplicable}");
            
            if (hasRequirements && isNotActive && isApplicable)
            {
                eligibleUpgrades.Add(upgrade);
            }
        }
        
        //Debug.Log($"过滤后的可用强化数量: {eligibleUpgrades.Count}");
        
        // 打印所有可用强化的名称，帮助调试
        foreach (var upgrade in eligibleUpgrades)
        {
            //Debug.Log($"可用强化: {upgrade.upgradeName}, 类型: {upgrade.type}, 目标单位类型: {upgrade.targetUnitType}");
        }

        if (eligibleUpgrades.Count == 0)
        {
            //Debug.LogWarning("没有可用的强化选项了！");
            HideUpgradeSelection(); // 如果没有可选强化，直接隐藏UI
            return;
        }

        // 随机选择 numberOfChoices 个强化
        List<UpgradeDataSO> chosenUpgrades = new List<UpgradeDataSO>();
        
        // 测试模式：如果启用测试模式且指定了强制第一个强化，则添加它
        if (enableTestMode && forcedFirstUpgrade != null)
        {
            // 检查强制强化是否满足条件且未被激活
            bool isForcedUpgradeEligible = playerFactionManager.HasAllRequiredUpgrades(forcedFirstUpgrade.requiredUpgrades) && 
                                          !playerFactionManager.activeUpgrades.Contains(forcedFirstUpgrade);
            
            if (isForcedUpgradeEligible)
            {
                chosenUpgrades.Add(forcedFirstUpgrade);
                //Debug.Log($"[测试模式] 强制添加强化: {forcedFirstUpgrade.upgradeName}");
                
                // 从可选列表中移除已选的强制强化
                eligibleUpgrades.Remove(forcedFirstUpgrade);
            }
            else
            {
                //Debug.LogWarning($"[测试模式] 强制强化 {forcedFirstUpgrade.upgradeName} 不满足条件或已被激活，无法添加。");
            }
        }
        
        // 使用一个副本进行选择，以防 eligibleUpgrades 数量不足 numberOfChoices
        List<UpgradeDataSO> tempEligibleUpgrades = new List<UpgradeDataSO>(eligibleUpgrades);

        // 计算还需要选择的强化数量
        int remainingChoices = numberOfChoices - chosenUpgrades.Count;
        
        for (int i = 0; i < remainingChoices; i++)
        {
            if (tempEligibleUpgrades.Count == 0) break; // 如果可选强化不够，就提前结束

            int randomIndex = Random.Range(0, tempEligibleUpgrades.Count);
            chosenUpgrades.Add(tempEligibleUpgrades[randomIndex]);
            tempEligibleUpgrades.RemoveAt(randomIndex); // 移除已选，避免重复
        }

        // 实例化并设置卡片UI
        foreach (UpgradeDataSO upgrade in chosenUpgrades)
        {
            UpgradeCardUI card = Instantiate(upgradeCardPrefab, cardsContainer);
            card.Setup(upgrade);

            // 假设 Button 组件挂载在 UpgradeCardUI 预制体下的名为 "Faction" 的子物体上
            Button cardButton = card.transform.Find("Faction")?.GetComponent<Button>();
            if (cardButton != null)
            {
                // 绑定点击事件，当卡片被点击时调用OnUpgradeCardClicked
                cardButton.onClick.AddListener(() => OnUpgradeCardClicked(upgrade, playerFaction));

                // 确保Faction GameObject (包含Button和Target Graphic) 在其兄弟元素中渲染在最上层
                cardButton.transform.SetAsLastSibling();
            }
            else
            {
                //Debug.LogError($"无法在强化卡片预制体 {upgradeCardPrefab.name} 的 'Faction' 子物体上找到 Button 组件。请检查层级结构。");
            }
        }
    }

    /// <summary>
    /// 处理强化卡片被点击的逻辑。
    /// </summary>
    /// <param name="selectedUpgrade">被选择的强化数据。</param>
    /// <param name="playerFaction">玩家阵营。</param>
    private void OnUpgradeCardClicked(UpgradeDataSO selectedUpgrade, Unit.Faction playerFaction)
    {
        selectedUpgrade.ApplyUpgrade(playerFaction); // 应用选择的强化
        HideUpgradeSelection(); // 隐藏UI
    }

    /// <summary>
    /// 隐藏强化选择面板。
    /// </summary>
    public void HideUpgradeSelection()
    {
        upgradeSelectionPanel.SetActive(false);
        // 通知游戏管理器UI已隐藏
        OnUpgradePanelVisibilityChanged?.Invoke(false);
    }

    /// <summary>
    /// 设置测试模式的强制第一个强化选项
    /// </summary>
    /// <param name="upgrade">要强制作为第一个选项的强化</param>
    /// <param name="enable">是否启用测试模式</param>
    public void SetForcedFirstUpgrade(UpgradeDataSO upgrade, bool enable = true)
    {
        forcedFirstUpgrade = upgrade;
        enableTestMode = enable;
        //Debug.Log($"[测试模式] 设置强制第一个强化为: {(upgrade != null ? upgrade.upgradeName : "无")}，测试模式: {(enable ? "启用" : "禁用")}");
    }
    
    /// <summary>
    /// 判断一个强化是否适用于当前英雄
    /// </summary>
    /// <param name="upgrade">要检查的强化</param>
    /// <param name="currentHeroType">当前英雄的UnitType</param>
    /// <param name="heroTypeEnum">当前英雄的HeroType枚举</param>
    /// <returns>如果强化适用于当前英雄，返回true</returns>
    private bool IsUpgradeForCurrentHero(UpgradeDataSO upgrade, Unit.UnitType currentHeroType, MagicBattle.HeroType? heroTypeEnum)
    {
        //Debug.Log($"详细检查强化 '{upgrade.upgradeName}' 是否适用于当前英雄 - 目标单位: {upgrade.targetUnitType}, 当前英雄: {currentHeroType}, 英雄枚举: {heroTypeEnum}");
        
        // 如果目标单位类型是当前英雄类型，则适用
        if (upgrade.targetUnitType == currentHeroType)
        {
            //Debug.Log($"强化 '{upgrade.upgradeName}' 目标单位类型与当前英雄类型匹配: {currentHeroType}");
            return true;
        }
            
        // 特殊处理：将HeroType枚举映射到UnitType
        // Knight -> Knight, Necromancer -> Necromancer
        if (heroTypeEnum.HasValue)
        {
            switch (heroTypeEnum.Value)
            {
                case MagicBattle.HeroType.Knight:
                    if (upgrade.targetUnitType == Unit.UnitType.Knight)
                    {
                        //Debug.Log($"强化 '{upgrade.upgradeName}' 目标单位类型与英雄枚举匹配: Knight");
                        return true;
                    }
                    break;
                case MagicBattle.HeroType.Necromancer:
                    if (upgrade.targetUnitType == Unit.UnitType.Necromancer)
                    {
                        //Debug.Log($"强化 '{upgrade.upgradeName}' 目标单位类型与英雄枚举匹配: Necromancer");
                        return true;
                    }
                    break;
            }
        }
        
        // 检查targetSkill是否为当前英雄的技能
        if (upgrade.targetSkill != null)
        {
            // 获取当前英雄数据
            string heroDataPath = heroTypeEnum == MagicBattle.HeroType.Knight ? "Heroes/Knight" : "Heroes/Necromancer";
            MagicBattle.HeroDataSO heroData = Resources.Load<MagicBattle.HeroDataSO>(heroDataPath);
            
            //Debug.Log($"检查强化 '{upgrade.upgradeName}' 的目标技能 '{upgrade.targetSkill.name}' 是否属于英雄 {heroTypeEnum}，加载英雄数据: {heroDataPath}");
            
            if (heroData != null)
            {
                // 检查技能名称是否匹配当前英雄的任何技能
                string targetSkillName = upgrade.targetSkill.name;
                foreach (var skill in heroData.skills)
                {
                    //Debug.Log($"比较技能: 目标 '{targetSkillName}' vs 英雄技能 '{skill.name}'");
                    if (skill.name == targetSkillName)
                    {
                        //Debug.Log($"强化 '{upgrade.upgradeName}' 的目标技能 '{targetSkillName}' 匹配英雄 {heroTypeEnum} 的技能");
                        return true;
                    }
                }
                //Debug.Log($"强化 '{upgrade.upgradeName}' 的目标技能 '{targetSkillName}' 不匹配英雄 {heroTypeEnum} 的任何技能");
            }
            else
            {
                //Debug.LogWarning($"无法加载英雄数据: {heroDataPath}");
                return false;
            }
        }
        
        //Debug.Log($"强化 '{upgrade.upgradeName}' 不适用于当前英雄");
        return false;
    }
    
    /// <summary>
    /// 判断一个强化是否适用于当前英雄（包括所有类型的强化）
    /// </summary>
    /// <param name="upgrade">要检查的强化</param>
    /// <param name="currentHeroType">当前英雄的UnitType</param>
    /// <param name="heroTypeEnum">当前英雄的HeroType枚举</param>
    /// <returns>如果强化适用于当前英雄，返回true</returns>
    private bool IsUpgradeApplicableForHero(UpgradeDataSO upgrade, Unit.UnitType currentHeroType, MagicBattle.HeroType? heroTypeEnum)
    {
        // 获取当前英雄的UnitType
        Unit.UnitType heroUnitType = Unit.UnitType.Knight; // 默认为骑士
        if (heroTypeEnum.HasValue)
        {
            switch (heroTypeEnum.Value)
            {
                case MagicBattle.HeroType.Knight:
                    heroUnitType = Unit.UnitType.Knight;
                    break;
                case MagicBattle.HeroType.Necromancer:
                    heroUnitType = Unit.UnitType.Necromancer;
                    break;
            }
        }
        
        //Debug.Log($"检查强化 '{upgrade.upgradeName}' 是否适用于英雄 - 强化类型: {upgrade.type}, 目标单位: {upgrade.targetUnitType}, 当前英雄: {heroUnitType}/{currentHeroType}");
        
        // 检查强化类型
        switch (upgrade.type)
        {
            case UpgradeDataSO.UpgradeType.SpecificHeroSkill:
                // 英雄特定技能强化
                bool isApplicable = IsUpgradeForCurrentHero(upgrade, currentHeroType, heroTypeEnum);
                //Debug.Log($"英雄特定技能强化 '{upgrade.upgradeName}' 适用性: {isApplicable}");
                return isApplicable;
                
            case UpgradeDataSO.UpgradeType.UnitAttribute:
                // 单位属性强化，检查目标单位类型
                // 如果是英雄单位类型，必须匹配当前英雄
                if (upgrade.targetUnitType == Unit.UnitType.Knight || 
                    upgrade.targetUnitType == Unit.UnitType.Necromancer)
                {
                    // 如果强化的目标是英雄单位，但不是当前英雄类型，则不适用
                    if (upgrade.targetUnitType != heroUnitType && 
                        upgrade.targetUnitType != currentHeroType)
                    {
                        //Debug.Log($"过滤掉英雄强化: {upgrade.upgradeName}，目标单位类型: {upgrade.targetUnitType}，当前英雄类型: {heroUnitType}/{currentHeroType}");
                        return false;
                    }
                    //Debug.Log($"英雄属性强化 '{upgrade.upgradeName}' 匹配当前英雄类型: {heroUnitType}/{currentHeroType}，允许显示");
                }
                else
                {
                    //Debug.Log($"非英雄单位强化 '{upgrade.upgradeName}'，目标单位类型: {upgrade.targetUnitType}，允许显示");
                }
                // 如果是小兵单位类型，无需匹配当前英雄，所有小兵强化都可用
                return true;
                
            // 金币相关强化
            case UpgradeDataSO.UpgradeType.GoldGenerationRate:
            case UpgradeDataSO.UpgradeType.GoldInstantGain:
            case UpgradeDataSO.UpgradeType.GoldRandomGain:
            case UpgradeDataSO.UpgradeType.GoldSacrificeGain:
            case UpgradeDataSO.UpgradeType.MinionSpawnCost:
            case UpgradeDataSO.UpgradeType.SwordMasterUnlock:
                // 金币相关强化和剑术大师解锁是通用强化，适用于所有英雄
                //Debug.Log($"金币相关或剑术大师解锁强化 '{upgrade.upgradeName}'，允许显示");
                return true;
                
            // 小兵特定强化
            case UpgradeDataSO.UpgradeType.SoldierBlockUnlock:
            case UpgradeDataSO.UpgradeType.SoldierFullComboUnlock:
            case UpgradeDataSO.UpgradeType.ArcherPiercingArrowsUnlock:
            case UpgradeDataSO.UpgradeType.ArcherFullComboUnlock:
            case UpgradeDataSO.UpgradeType.LancerChargeSpeed:
            case UpgradeDataSO.UpgradeType.ArcherAttackRange:
            case UpgradeDataSO.UpgradeType.PriestHealAmount:
            case UpgradeDataSO.UpgradeType.MageAOERadius:
            case UpgradeDataSO.UpgradeType.SpecificMinionSkill:
                // 小兵相关的强化，无论选择哪个英雄都应该可用
                //Debug.Log($"小兵相关强化 '{upgrade.upgradeName}'，允许显示");
                return true;
                
            default:
                // 未明确处理的强化类型，打印日志并默认返回true
                //Debug.LogWarning($"未明确处理的强化类型: {upgrade.type}，默认允许显示");
                return true;
        }
    }
} 