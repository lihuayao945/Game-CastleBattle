using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 单位数据面板UI，负责显示左右两边阵营的单位数据
/// </summary>
public class UnitDataPanelUI : MonoBehaviour
{
    [System.Serializable]
    public class HeaderRow
    {
        public GameObject rowObject;            // 行的游戏对象
    }
    
    [System.Serializable]
    public class UnitInfoRow
    {
        public GameObject rowObject;            // 行的游戏对象
        
        public Image unitIcon;                  // 单位图标
        public TextMeshProUGUI unitName;        // 单位名称
        
        public TextMeshProUGUI healthText;      // 生命值文本
        public TextMeshProUGUI defenseText;     // 防御值文本
        public TextMeshProUGUI speedText;       // 移动速度文本
        public TextMeshProUGUI damageMultText;  // 伤害倍率文本
    }

    [Header("表头")]
    [SerializeField] private HeaderRow leftHeaderRow;   // 左侧表头行
    [SerializeField] private HeaderRow rightHeaderRow;  // 右侧表头行

    [Header("左侧阵营单位信息")]
    [SerializeField] private List<UnitInfoRow> leftUnitRows = new List<UnitInfoRow>();
    
    [Header("右侧阵营单位信息")]
    [SerializeField] private List<UnitInfoRow> rightUnitRows = new List<UnitInfoRow>();

    [Header("单位图标")]
    [SerializeField] private Sprite knightSprite;
    [SerializeField] private Sprite necromancerSprite;
    [SerializeField] private Sprite soldierSprite;
    [SerializeField] private Sprite archerSprite;
    [SerializeField] private Sprite lancerSprite;
    [SerializeField] private Sprite mageSprite;
    [SerializeField] private Sprite priestSprite;
    [SerializeField] private Sprite swordMasterSprite;

    // 单位数据缓存
    private Dictionary<Unit.UnitType, UnitData> leftUnitDataCache = new Dictionary<Unit.UnitType, UnitData>();
    private Dictionary<Unit.UnitType, UnitData> rightUnitDataCache = new Dictionary<Unit.UnitType, UnitData>();

    // 单位数据结构
    private class UnitData
    {
        public string name;
        public float maxHealth;
        public float defense;
        public float moveSpeed;
        public float damageMultiplier;
        public bool isUnlocked;
    }

    // 单位基础属性
    private static class UnitBaseStats
    {
        // 英雄单位
        public static readonly Dictionary<Unit.UnitType, (float health, float defense, float speed)> HeroStats = 
            new Dictionary<Unit.UnitType, (float, float, float)>
            {
                { Unit.UnitType.Knight, (100f, 4f, 7f) },
                { Unit.UnitType.Necromancer, (100f, 3f, 5f) }
            };
            
        // 小兵单位
        public static readonly Dictionary<Unit.UnitType, (float health, float defense, float speed)> MinionStats = 
            new Dictionary<Unit.UnitType, (float, float, float)>
            {
                { Unit.UnitType.Soldier, (100f, 2f, 5f) },
                { Unit.UnitType.Archer, (100f, 1f, 5f) },
                { Unit.UnitType.Lancer, (100f, 2f, 7f) },
                { Unit.UnitType.Mage, (100f, 1f, 5f) },
                { Unit.UnitType.Priest, (100f, 1f, 5f) },
                { Unit.UnitType.SwordMaster, (125f, 2f, 6f) }
            };
    }

    private void Awake()
    {
        // 检查GlobalGameUpgrades是否已初始化
        if (GlobalGameUpgrades.Instance == null)
        {
            Debug.LogWarning("GlobalGameUpgrades.Instance is null, attempting to find it in scene");
            
            // 尝试在场景中查找GlobalGameUpgrades
            GlobalGameUpgrades upgradeManager = FindObjectOfType<GlobalGameUpgrades>();
            if (upgradeManager == null)
            {
                Debug.LogError("Could not find GlobalGameUpgrades in scene. Unit data will use default values.");
            }
        }
    }

    private void OnEnable()
    {
        // 当面板启用时，更新所有单位数据
        UpdateAllUnitData();
        
        // 添加事件监听，当强化应用时更新面板
        if (GlobalGameUpgrades.Instance != null)
        {
            // 每秒定期刷新数据，确保强化效果实时显示
            InvokeRepeating("UpdateAllUnitData", 1f, 1f);
        }
    }
    
    private void OnDisable()
    {
        // 取消定期刷新
        CancelInvoke("UpdateAllUnitData");
    }

    /// <summary>
    /// 更新所有单位数据
    /// </summary>
    public void UpdateAllUnitData()
    {
        // 清空缓存
        leftUnitDataCache.Clear();
        rightUnitDataCache.Clear();

        // 从强化器中读取数据
        CollectUnitDataFromUpgrades();

        // 更新UI显示
        UpdateLeftUnitRows();
        UpdateRightUnitRows();
    }

    /// <summary>
    /// 从强化器中读取单位数据
    /// </summary>
    private void CollectUnitDataFromUpgrades()
    {
        // 获取全局强化数据
        FactionUpgradeManager leftFactionManager = null;
        FactionUpgradeManager rightFactionManager = null;

        if (GlobalGameUpgrades.Instance != null)
        {
            leftFactionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Left);
            rightFactionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Right);
        }
        else
        {
            Debug.LogWarning("GlobalGameUpgrades.Instance is null, using default values for unit data");
        }

        // 处理左侧阵营单位数据
        GenerateUnitData(leftUnitDataCache, leftFactionManager, Unit.Faction.Left);

        // 处理右侧阵营单位数据
        GenerateUnitData(rightUnitDataCache, rightFactionManager, Unit.Faction.Right);
    }

    /// <summary>
    /// 为指定阵营生成单位数据
    /// </summary>
    private void GenerateUnitData(Dictionary<Unit.UnitType, UnitData> dataCache, FactionUpgradeManager factionManager, Unit.Faction faction)
    {
        // 获取当前阵营的英雄类型
        Unit.UnitType heroType = faction == Unit.Faction.Left ? 
            GetCurrentLeftHeroType() : GetCurrentRightHeroType();
        
        // 生成英雄数据
        dataCache[heroType] = CalculateUnitData(heroType, factionManager);
        
        // 为了确保UI能够正确显示，如果当前英雄类型不是Knight或Necromancer，
        // 也生成这两种英雄的数据，但不会在UI中显示
        if (heroType != Unit.UnitType.Knight)
        {
            dataCache[Unit.UnitType.Knight] = CalculateUnitData(Unit.UnitType.Knight, factionManager);
        }
        if (heroType != Unit.UnitType.Necromancer)
        {
            dataCache[Unit.UnitType.Necromancer] = CalculateUnitData(Unit.UnitType.Necromancer, factionManager);
        }

        // 小兵单位
        dataCache[Unit.UnitType.Soldier] = CalculateUnitData(Unit.UnitType.Soldier, factionManager);
        dataCache[Unit.UnitType.Archer] = CalculateUnitData(Unit.UnitType.Archer, factionManager);
        dataCache[Unit.UnitType.Lancer] = CalculateUnitData(Unit.UnitType.Lancer, factionManager);
        dataCache[Unit.UnitType.Mage] = CalculateUnitData(Unit.UnitType.Mage, factionManager);
        dataCache[Unit.UnitType.Priest] = CalculateUnitData(Unit.UnitType.Priest, factionManager);
        
        // 剑术大师（特殊处理，需要检查是否解锁）
        UnitData swordMasterData = CalculateUnitData(Unit.UnitType.SwordMaster, factionManager);
        swordMasterData.isUnlocked = factionManager != null && factionManager.swordMasterUnlocked;
        dataCache[Unit.UnitType.SwordMaster] = swordMasterData;
    }

    /// <summary>
    /// 计算单位数据，应用强化效果
    /// </summary>
    private UnitData CalculateUnitData(Unit.UnitType type, FactionUpgradeManager factionManager)
    {
        // 从基础数据中获取
        float baseHealth = 0;
        float baseDefense = 0;
        float baseMoveSpeed = 0;

        // 根据单位类型获取基础属性
        if (type == Unit.UnitType.Knight || type == Unit.UnitType.Necromancer)
        {
            if (UnitBaseStats.HeroStats.TryGetValue(type, out var stats))
            {
                baseHealth = stats.health;
                baseDefense = stats.defense;
                baseMoveSpeed = stats.speed;
            }
        }
        else
        {
            if (UnitBaseStats.MinionStats.TryGetValue(type, out var stats))
            {
                baseHealth = stats.health;
                baseDefense = stats.defense;
                baseMoveSpeed = stats.speed;
            }
        }

        // 应用全局强化（如果有强化管理器）
        float finalHealth = baseHealth;
        float finalDefense = baseDefense;
        float finalMoveSpeed = baseMoveSpeed;
        float damageMultiplier = 1.0f; // 默认伤害倍率为1

        if (factionManager != null)
        {
            // 获取单位的修改器
            UnitModifiers modifiers = factionManager.GetUnitModifier(type);
            
            // 应用加法和乘法修改器
            finalHealth = (baseHealth + modifiers.healthAdditive) * modifiers.healthMultiplier;
            finalDefense = (baseDefense + modifiers.defenseAdditive) * modifiers.defenseMultiplier;
            finalMoveSpeed = (baseMoveSpeed + modifiers.moveSpeedAdditive) * modifiers.moveSpeedMultiplier;
            
            // 获取特定单位的伤害倍率，在默认值1的基础上加成
            damageMultiplier = GetUnitDamageMultiplier(type, factionManager);
        }

        return new UnitData
        {
            name = GetUnitName(type),
            maxHealth = finalHealth,
            defense = finalDefense,
            moveSpeed = finalMoveSpeed,
            damageMultiplier = damageMultiplier,
            isUnlocked = true // 除了剑术大师外，其他都默认解锁
        };
    }

    /// <summary>
    /// 获取单位名称
    /// </summary>
    private string GetUnitName(Unit.UnitType type)
    {
        switch (type)
        {
            case Unit.UnitType.Knight:
                return "骑士";
            case Unit.UnitType.Necromancer:
                return "邪术师";
            case Unit.UnitType.Soldier:
                return "士兵";
            case Unit.UnitType.Archer:
                return "弓箭手";
            case Unit.UnitType.Lancer:
                return "枪骑兵";
            case Unit.UnitType.Mage:
                return "法师";
            case Unit.UnitType.Priest:
                return "牧师";
            case Unit.UnitType.SwordMaster:
                return "剑术大师";
            default:
                return "未知";
        }
    }

    /// <summary>
    /// 获取单位伤害倍率
    /// </summary>
    private float GetUnitDamageMultiplier(Unit.UnitType type, FactionUpgradeManager factionManager)
    {
        if (factionManager == null) return 1.0f;

        // 获取单位的修改器
        UnitModifiers modifiers = factionManager.GetUnitModifier(type);
        
        // 返回伤害乘数，应用加法和乘法修改器
        // 由于伤害倍率的基础值是1，所以加法修改器直接加到1上
        return (1.0f + modifiers.damageAdditive) * modifiers.damageMultiplier;
    }

    /// <summary>
    /// 更新左侧单位行
    /// </summary>
    private void UpdateLeftUnitRows()
    {
        // 确保行数量足够
        if (leftUnitRows.Count < 7) return;

        // 设置英雄数据（动态获取当前选择的英雄类型）
        Unit.UnitType leftHeroType = GetCurrentLeftHeroType();
        if (leftUnitDataCache.TryGetValue(leftHeroType, out UnitData heroData))
        {
            // 根据英雄类型选择对应的图标
            Sprite heroSprite = GetHeroSprite(leftHeroType);
            UpdateUnitRow(leftUnitRows[0], heroData, heroSprite);
        }

        // 设置小兵数据，按照顺序：士兵、弓箭手、枪兵、法师、牧师
        if (leftUnitDataCache.TryGetValue(Unit.UnitType.Soldier, out UnitData soldierData))
        {
            UpdateUnitRow(leftUnitRows[1], soldierData, soldierSprite);
        }

        if (leftUnitDataCache.TryGetValue(Unit.UnitType.Archer, out UnitData archerData))
        {
            UpdateUnitRow(leftUnitRows[2], archerData, archerSprite);
        }

        if (leftUnitDataCache.TryGetValue(Unit.UnitType.Lancer, out UnitData lancerData))
        {
            UpdateUnitRow(leftUnitRows[3], lancerData, lancerSprite);
        }

        if (leftUnitDataCache.TryGetValue(Unit.UnitType.Mage, out UnitData mageData))
        {
            UpdateUnitRow(leftUnitRows[4], mageData, mageSprite);
        }

        if (leftUnitDataCache.TryGetValue(Unit.UnitType.Priest, out UnitData priestData))
        {
            UpdateUnitRow(leftUnitRows[5], priestData, priestSprite);
        }

        // 剑术大师（特殊处理，需要检查是否解锁）
        if (leftUnitDataCache.TryGetValue(Unit.UnitType.SwordMaster, out UnitData swordMasterData))
        {
            leftUnitRows[6].rowObject.SetActive(swordMasterData.isUnlocked);
            if (swordMasterData.isUnlocked)
            {
                UpdateUnitRow(leftUnitRows[6], swordMasterData, swordMasterSprite);
            }
        }
        else
        {
            leftUnitRows[6].rowObject.SetActive(false);
        }
    }

    /// <summary>
    /// 更新右侧单位行
    /// </summary>
    private void UpdateRightUnitRows()
    {
        // 确保行数量足够
        if (rightUnitRows.Count < 7) return;

        // 设置英雄数据（动态获取当前选择的英雄类型）
        Unit.UnitType rightHeroType = GetCurrentRightHeroType();
        if (rightUnitDataCache.TryGetValue(rightHeroType, out UnitData heroData))
        {
            // 根据英雄类型选择对应的图标
            Sprite heroSprite = GetHeroSprite(rightHeroType);
            UpdateUnitRow(rightUnitRows[0], heroData, heroSprite);
        }

        // 设置小兵数据，按照顺序：士兵、弓箭手、枪兵、法师、牧师
        if (rightUnitDataCache.TryGetValue(Unit.UnitType.Soldier, out UnitData soldierData))
        {
            UpdateUnitRow(rightUnitRows[1], soldierData, soldierSprite);
        }

        if (rightUnitDataCache.TryGetValue(Unit.UnitType.Archer, out UnitData archerData))
        {
            UpdateUnitRow(rightUnitRows[2], archerData, archerSprite);
        }

        if (rightUnitDataCache.TryGetValue(Unit.UnitType.Lancer, out UnitData lancerData))
        {
            UpdateUnitRow(rightUnitRows[3], lancerData, lancerSprite);
        }

        if (rightUnitDataCache.TryGetValue(Unit.UnitType.Mage, out UnitData mageData))
        {
            UpdateUnitRow(rightUnitRows[4], mageData, mageSprite);
        }

        if (rightUnitDataCache.TryGetValue(Unit.UnitType.Priest, out UnitData priestData))
        {
            UpdateUnitRow(rightUnitRows[5], priestData, priestSprite);
        }

        // 剑术大师（特殊处理，需要检查是否解锁）
        if (rightUnitDataCache.TryGetValue(Unit.UnitType.SwordMaster, out UnitData swordMasterData))
        {
            rightUnitRows[6].rowObject.SetActive(swordMasterData.isUnlocked);
            if (swordMasterData.isUnlocked)
            {
                UpdateUnitRow(rightUnitRows[6], swordMasterData, swordMasterSprite);
            }
        }
        else
        {
            rightUnitRows[6].rowObject.SetActive(false);
        }
    }

    /// <summary>
    /// 获取当前左侧阵营选择的英雄类型
    /// </summary>
    private Unit.UnitType GetCurrentLeftHeroType()
    {
        Unit.UnitType currentHeroType = Unit.UnitType.Knight; // 默认值设为Knight
        bool foundHeroInScene = false;
        
        // 首先尝试从场景中查找左侧阵营的英雄单位
        HeroUnit[] heroes = FindObjectsOfType<HeroUnit>();
        foreach (var hero in heroes)
        {
            if (hero != null && hero.faction == Unit.Faction.Left)
            {
                currentHeroType = hero.Type;
                foundHeroInScene = true;
                break;
            }
        }
        
        // 如果场景中没有找到，则从PlayerPrefs中获取选择的英雄索引
        if (!foundHeroInScene)
        {
            int selectedHeroIndex = PlayerPrefs.GetInt("SelectedHeroIndex", 0);
            currentHeroType = selectedHeroIndex == 0 ? Unit.UnitType.Knight : Unit.UnitType.Necromancer;
        }
        
        return currentHeroType;
    }

    /// <summary>
    /// 获取当前右侧阵营选择的英雄类型
    /// </summary>
    private Unit.UnitType GetCurrentRightHeroType()
    {
        Unit.UnitType currentHeroType = Unit.UnitType.Necromancer; // 默认值设为Necromancer
        
        // 首先尝试从场景中查找右侧阵营的英雄单位
        HeroUnit[] heroes = FindObjectsOfType<HeroUnit>();
        foreach (var hero in heroes)
        {
            if (hero != null && hero.faction == Unit.Faction.Right)
            {
                currentHeroType = hero.Type;
                break;
            }
        }
        
        // 如果场景中没有找到，则使用默认值（通常AI使用邪术师）
        // 已经设置了默认值，所以这里不需要额外处理
        
        return currentHeroType;
    }

    /// <summary>
    /// 根据英雄类型获取对应的图标
    /// </summary>
    private Sprite GetHeroSprite(Unit.UnitType heroType)
    {
        // 首先检查是否有直接引用的精灵图
        switch (heroType)
        {
            case Unit.UnitType.Knight:
                if (knightSprite != null) return knightSprite;
                break;
            case Unit.UnitType.Necromancer:
                if (necromancerSprite != null) return necromancerSprite;
                break;
        }
        
        // 如果没有直接引用，尝试从Resources加载
        string resourcePath = "";
        switch (heroType)
        {
            case Unit.UnitType.Knight:
                resourcePath = "Sprites/Heroes/Knight";
                break;
            case Unit.UnitType.Necromancer:
                resourcePath = "Sprites/Heroes/Necromancer";
                break;
            // 如果有更多英雄类型，在这里添加
            default:
                // 对于未知的英雄类型，尝试根据枚举名称加载
                resourcePath = $"Sprites/Heroes/{heroType}";
                break;
        }
        
        // 尝试从Resources加载精灵图
        Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);
        if (loadedSprite != null)
        {
            return loadedSprite;
        }
        
        // 如果都失败了，返回一个默认图标或null
        Debug.LogWarning($"无法为英雄类型 {heroType} 找到图标，请确保已设置引用或在Resources中存在对应图标。");
        return null;
    }

    /// <summary>
    /// 更新单位行显示
    /// </summary>
    private void UpdateUnitRow(UnitInfoRow row, UnitData data, Sprite icon)
    {
        if (row == null || data == null) return;

        // 设置单位图标和名称
        if (row.unitIcon != null && icon != null)
        {
            row.unitIcon.sprite = icon;
        }
        if (row.unitName != null)
        {
            row.unitName.text = data.name;
        }

        // 设置各属性值文本
        if (row.healthText != null)
        {
            row.healthText.text = $"{Mathf.RoundToInt(data.maxHealth)}";
        }

        if (row.defenseText != null)
        {
            // 防御力显示一位小数
            row.defenseText.text = $"{data.defense:F1}";
        }

        if (row.speedText != null)
        {
            // 速度显示一位小数
            row.speedText.text = $"{data.moveSpeed:F1}";
        }

        if (row.damageMultText != null)
        {
            // 将伤害倍率转换为百分比格式，例如1.3倍显示为30%
            int damagePercent = Mathf.RoundToInt((data.damageMultiplier - 1) * 100);
            row.damageMultText.text = $"{damagePercent}%";
        }
    }
}
