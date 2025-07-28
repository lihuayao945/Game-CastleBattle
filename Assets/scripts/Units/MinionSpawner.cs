 using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using MagicBattle; // 添加对MagicBattle命名空间的引用

// 小兵类型枚举
public enum MinionType
{
    Soldier,    // 士兵
    Archer,     // 弓箭手
    Lancer,     // 长枪兵
    Priest,     // 牧师
    Mage,       // 法师
    SwordMaster // 剑术大师
}

/// <summary>
/// 小兵生成器 - 负责生成和管理小兵单位
/// </summary>
public class MinionSpawner : MonoBehaviour
{
    [System.Serializable]
    public class MinionSquad
    {
        public GameObject minionPrefab;      // 小兵预制体
        public int count = 3;                // 生成数量
        public float spawnRadius = 1f;       // 生成半径
        public KeyCode spawnKey;             // 生成按键
        public int cost = 100;               // 生成消耗
        public float cooldown = 10f;         // 冷却时间
        public string squadName;             // 小队名称
        public Sprite squadIcon;             // 新增：小队图标，用于UI显示
        
        // 判断小队是否已满（用于AI决策）
        public bool IsFull()
        {
            // 默认情况下小队不会满，这里可以根据实际情况实现
            // 例如：可以检查场上该类型小兵的数量是否达到上限
            return false;
        }
    }
    
    [Header("小兵小队配置")]
    [SerializeField] private List<MinionSquad> leftSquads = new List<MinionSquad>();    // 左方小兵小队
    [SerializeField] public List<MinionSquad> rightSquads = new List<MinionSquad>();   // 右方小兵小队
    
    public List<MinionSquad> LeftSquads => leftSquads; // 确保这里只有一次定义
    public List<MinionSquad> RightSquads => rightSquads; // 添加右侧小队的访问器

    [Header("生成点配置")]
    [SerializeField] private Transform leftSpawnPoint;    // 左方生成点
    [SerializeField] private Transform rightSpawnPoint;   // 右方生成点
    
    [Header("资源系统")]
    [SerializeField] private float leftResources = 1000f;    // 左方初始资源
    [SerializeField] private float rightResources = 1000f;   // 右方初始资源
    private float initialLeftResources; // 新增：保存左方初始资源
    private float initialRightResources; // 新增：保存右方初始资源
    
    [Header("UI配置")]
    [SerializeField] private TextMeshProUGUI leftResourceText;    // 左方资源显示
    [SerializeField] private TextMeshProUGUI rightResourceText;   // 右方资源显示
    [SerializeField] private GameObject leftSquadUI;             // 左方小队UI
    [SerializeField] private GameObject rightSquadUI;            // 右方小队UI
    
    [SerializeField] private MinionSpawnUI minionSpawnUI; // 新增：MinionSpawnUI引用

    private Dictionary<MinionSquad, float> squadCooldowns = new Dictionary<MinionSquad, float>();
    
    private void Start()
    {
        // 保存初始资源值
        initialLeftResources = leftResources;
        initialRightResources = rightResources;
        // 初始化冷却时间字典
        foreach (var squad in leftSquads)
        {
            squadCooldowns[squad] = 0f;
        }
        foreach (var squad in rightSquads)
        {
            squadCooldowns[squad] = 0f;
        }
        
        // 初始化金币系统
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager leftManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Left);
            FactionUpgradeManager rightManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Right);
            
            if (leftManager != null)
            {
                leftManager.currentGold = initialLeftResources;
                leftManager.OnGoldChanged += (gold) => UpdateGoldUI(Unit.Faction.Left, gold);
            }
            
            if (rightManager != null)
            {
                rightManager.currentGold = initialRightResources;
                rightManager.OnGoldChanged += (gold) => UpdateGoldUI(Unit.Faction.Right, gold);
            }
        }
        
        // 更新UI
        UpdateResourceUI();
        UpdateSquadUI();
    }
    
    private void Update()
    {
        // 如果游戏管理器不允许生成小兵，直接返回
        if (GameManager.Instance != null && !GameManager.Instance.CanSpawnMinion())
        {
            return;
        }

        // 更新冷却时间
        UpdateCooldowns();
        
        // 检测左方小兵生成
        foreach (var squad in leftSquads)
        {
            if (Input.GetKeyDown(squad.spawnKey))
            {
                // 检查是否是剑术大师
                bool isSwordMaster = squad.minionPrefab != null && squad.minionPrefab.GetComponent<SwordMasterController>() != null;
                // if (isSwordMaster)
                // {
                //     Debug.Log($"按下按键 {squad.spawnKey}，尝试生成剑术大师");
                // }
                
                float actualCost = CalculateActualCost(squad.cost, Unit.Faction.Left);
                bool canAfford = CanAffordSquad(squad, Unit.Faction.Left);
                bool isCooldownReady = squadCooldowns[squad] <= 0f;
                bool canSpawn = CanSpawnSquad(squad, Unit.Faction.Left);
                
                // if (isSwordMaster)
                // {
                //     Debug.Log($"剑术大师生成条件: 金币充足={canAfford}, 冷却就绪={isCooldownReady}, 总体可生成={canSpawn}");
                // }

                if (canSpawn)
                {
                    // if (isSwordMaster)
                    // {
                    //     Debug.Log("所有条件满足，开始生成剑术大师");
                    // }
                    
                    SpawnSquad(squad, leftSpawnPoint, Unit.Faction.Left);
                    // 使用FactionUpgradeManager的金币系统支付费用
                    if (GlobalGameUpgrades.Instance != null)
                    {
                        FactionUpgradeManager leftManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Left);
                        if (leftManager != null)
                        {
                            leftManager.SpendGold(actualCost);
                        }
                    }
                    squadCooldowns[squad] = squad.cooldown;
                    UpdateSquadUI();
                }
                else if (!canAfford) // 金币不足时闪烁
                {
                    if (minionSpawnUI != null)
                    {
                        minionSpawnUI.FlashCostOverlay(squad.spawnKey);
                    }
                }
                else if (!canSpawn && isSwordMaster)
                {
                    // Debug.Log("剑术大师无法生成，可能是未解锁");
                    
                    // 显示提示信息
                    if (minionSpawnUI != null)
                    {
                        minionSpawnUI.FlashCostOverlay(squad.spawnKey);
                    }
                }
            }
        }
        
        // 检测右方小兵生成
        foreach (var squad in rightSquads)
        {
            if (Input.GetKeyDown(squad.spawnKey) && CanSpawnSquad(squad, Unit.Faction.Right))
            {
                SpawnSquad(squad, rightSpawnPoint, Unit.Faction.Right);
                float actualCost = CalculateActualCost(squad.cost, Unit.Faction.Right);
                // 使用FactionUpgradeManager的金币系统支付费用
                if (GlobalGameUpgrades.Instance != null)
                {
                    FactionUpgradeManager rightManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Right);
                    if (rightManager != null)
                    {
                        rightManager.SpendGold(actualCost);
                    }
                }
                squadCooldowns[squad] = squad.cooldown;
                UpdateSquadUI();
            }
        }
    }
    
    // 新增：计算考虑强化后的实际小兵召唤费用
    private float CalculateActualCost(float baseCost, Unit.Faction faction)
    {
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(faction);
            if (factionManager != null)
            {
                // 应用加法和乘法强化
                float actualCost = (baseCost + factionManager.minionCostAdditive) * factionManager.minionCostMultiplier;
                // 确保费用不会变成负数
                return Mathf.Max(0, actualCost);
            }
        }
        return baseCost;
    }
    
    // 新增：检查是否能负担小兵召唤费用
    private bool CanAffordSquad(MinionSquad squad, Unit.Faction faction)
    {
        float actualCost = CalculateActualCost(squad.cost, faction);
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(faction);
            if (factionManager != null)
            {
                return factionManager.GetCurrentGold() >= actualCost;
            }
        }
        // 如果无法获取FactionUpgradeManager，则使用旧系统
        float resources = faction == Unit.Faction.Left ? leftResources : rightResources;
        return resources >= actualCost;
    }
    
    private void UpdateCooldowns()
    {
        foreach (var squad in squadCooldowns.Keys.ToList())
        {
            if (squadCooldowns[squad] > 0)
            {
                squadCooldowns[squad] -= Time.deltaTime;
                if (squadCooldowns[squad] < 0)
                {
                    squadCooldowns[squad] = 0f;
                }
            }
        }
    }
    
    private bool CanSpawnSquad(MinionSquad squad, Unit.Faction faction)
    {
        // 首先检查金币和冷却
        bool canAffordAndCooldown = CanAffordSquad(squad, faction) && squadCooldowns[squad] <= 0f;
        
        if (!canAffordAndCooldown)
        {
            return false;
        }
        
        // 检查是否是剑术大师小队，如果是则需要检查是否已解锁
        if (squad.minionPrefab != null)
        {
            SwordMasterController swordMaster = squad.minionPrefab.GetComponent<SwordMasterController>();
            if (swordMaster != null)
            {
                // 检查是否已解锁剑术大师
                if (GlobalGameUpgrades.Instance != null)
                {
                    FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(faction);
                    if (factionManager != null)
                    {
                        bool isUnlocked = factionManager.swordMasterUnlocked;
                        
                        if (!isUnlocked)
                        {
                            // 未解锁剑术大师，不允许生成
                            return false;
                        }
                    }
                }
            }
        }
        
        return true;
    }
    
    // 新增：更新金币UI
    private void UpdateGoldUI(Unit.Faction faction, float gold)
    {
        if (faction == Unit.Faction.Left && leftResourceText != null)
        {
            leftResourceText.text = $"金币: {Mathf.Floor(gold)}";
        }
        else if (faction == Unit.Faction.Right && rightResourceText != null)
        {
            rightResourceText.text = $"金币: {Mathf.Floor(gold)}";
        }
    }
    
    private void UpdateResourceUI()
    {
        // 使用FactionUpgradeManager的金币系统更新UI
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager leftManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Left);
            FactionUpgradeManager rightManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Right);
            
            if (leftManager != null && leftResourceText != null)
            {
                leftResourceText.text = $"金币: {Mathf.Floor(leftManager.GetCurrentGold())}";
            }
            
            if (rightManager != null && rightResourceText != null)
            {
                rightResourceText.text = $"金币: {Mathf.Floor(rightManager.GetCurrentGold())}";
            }
        }
        else
        {
            // 如果无法获取FactionUpgradeManager，则使用旧系统
            if (leftResourceText != null)
            {
                leftResourceText.text = $"资源: {leftResources}";
            }
            if (rightResourceText != null)
            {
                rightResourceText.text = $"资源: {rightResources}";
            }
        }
    }
    
    private void SpawnSquad(MinionSquad squad, Transform spawnPoint, Unit.Faction faction)
    {
        for (int i = 0; i < squad.count; i++)
        {
            // 在生成点周围随机位置生成小兵
            Vector2 randomOffset = Random.insideUnitCircle * squad.spawnRadius;
            Vector3 spawnPosition = spawnPoint.position + new Vector3(randomOffset.x, randomOffset.y, 0);
            
            GameObject minion;

            // 使用对象池生成小兵
            if (MagicBattle.UnitPoolManager.Instance != null)
            {
                minion = MagicBattle.UnitPoolManager.Instance.GetFromPool(squad.minionPrefab, spawnPosition, Quaternion.identity, faction);
            }
            else
            {
                // 如果对象池管理器不存在，则使用原始方法
                minion = Instantiate(squad.minionPrefab, spawnPosition, Quaternion.identity);
            }
            
                            // 设置小兵阵营
                Unit unit = minion.GetComponent<Unit>();
                if (unit != null)
                {
                    unit.faction = faction;
                    // 立即设置小兵的初始朝向
                    if (faction == Unit.Faction.Left)
                    {
                        // 左方单位，通常朝向右边（默认localScale.x为正）
                        unit.transform.localScale = new Vector3(Mathf.Abs(unit.transform.localScale.x), unit.transform.localScale.y, unit.transform.localScale.z);
                    }
                    else // Faction.Right
                    {
                        // 右方单位，通常朝向左边（localScale.x为负）
                        unit.transform.localScale = new Vector3(-Mathf.Abs(unit.transform.localScale.x), unit.transform.localScale.y, unit.transform.localScale.z);
                    }
                }
            
            // 添加小地图图标组件
            if (minion.GetComponent<MinimapIcon>() == null)
            {
                minion.AddComponent<MinimapIcon>();
            }

            // 注册到渲染优化系统
            Unit unitComponent = minion.GetComponent<Unit>();
            if (unitComponent != null)
            {
                MagicBattle.ViewportRenderingOptimizer optimizer = MagicBattle.ViewportRenderingOptimizer.Instance;
                if (optimizer != null)
                {
                    optimizer.RegisterUnit(unitComponent);
                }
            }
        }
        
        // 播放生成特效
        PlaySpawnEffect(spawnPoint.position);
    }
    
    private void PlaySpawnEffect(Vector3 position)
    {
        // TODO: 添加生成特效
    }
    
    private void UpdateSquadUI()
    {
        // TODO: 更新小队UI，显示冷却时间和资源消耗
    }

    // 新增：提供金币获取方法
    public float GetLeftGold()
    {
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager leftManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Left);
            if (leftManager != null)
            {
                return leftManager.GetCurrentGold();
            }
        }
        return leftResources;
    }

    // 新增：提供小队冷却时间获取方法
    public float GetSquadCooldown(MinionSquad squad)
    {
        if (squadCooldowns.ContainsKey(squad))
        {
            return squadCooldowns[squad];
        }
        return 0f;
    }

    // 新增：提供尝试召唤小队的方法
    public bool TrySpawnSquad(KeyCode key)
    {
        MinionSquad squadToSpawn = null;
        foreach (var squad in leftSquads)
        {
            if (squad.spawnKey == key)
            {
                squadToSpawn = squad;
                break;
            }
        }

        if (squadToSpawn != null && CanSpawnSquad(squadToSpawn, Unit.Faction.Left))
        {
            SpawnSquad(squadToSpawn, leftSpawnPoint, Unit.Faction.Left);
            
            // 使用FactionUpgradeManager的金币系统支付费用
            float actualCost = CalculateActualCost(squadToSpawn.cost, Unit.Faction.Left);
            if (GlobalGameUpgrades.Instance != null)
            {
                FactionUpgradeManager leftManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Left);
                if (leftManager != null)
                {
                    leftManager.SpendGold(actualCost);
                }
                else
                {
                    leftResources -= squadToSpawn.cost; // 如果无法获取FactionUpgradeManager，则使用旧系统
                }
            }
            else
            {
                leftResources -= squadToSpawn.cost; // 如果无法获取GlobalGameUpgrades，则使用旧系统
            }
            
            squadCooldowns[squadToSpawn] = squadToSpawn.cooldown;
            UpdateSquadUI();
            return true;
        }
        return false;
    }
    
    // 新增：为AI控制器提供的小兵生成方法
    public bool TrySpawnSquad(MinionType minionType, Unit.Faction faction)
    {
        // 根据阵营和小兵类型查找对应的小队
        List<MinionSquad> squads = (faction == Unit.Faction.Left) ? leftSquads : rightSquads;
        Transform spawnPoint = (faction == Unit.Faction.Left) ? leftSpawnPoint : rightSpawnPoint;
        
        MinionSquad squadToSpawn = null;
        foreach (var squad in squads)
        {
            // 通过预制体上的组件类型判断小兵类型
            if (squad.minionPrefab != null)
            {
                if (minionType == MinionType.Soldier && squad.minionPrefab.GetComponent<SoldierController>() != null)
                {
                    squadToSpawn = squad;
                    break;
                }
                else if (minionType == MinionType.Archer && squad.minionPrefab.GetComponent<ArcherController>() != null)
                {
                    squadToSpawn = squad;
                    break;
                }
                else if (minionType == MinionType.Lancer && squad.minionPrefab.GetComponent<LancerController>() != null)
                {
                    squadToSpawn = squad;
                    break;
                }
                else if (minionType == MinionType.Priest && squad.minionPrefab.GetComponent<PriestController>() != null)
                {
                    squadToSpawn = squad;
                    break;
                }
                else if (minionType == MinionType.Mage && squad.minionPrefab.GetComponent<MageController>() != null)
                {
                    squadToSpawn = squad;
                    break;
                }
            }
        }
        
        if (squadToSpawn != null && CanSpawnSquad(squadToSpawn, faction))
        {
            SpawnSquad(squadToSpawn, spawnPoint, faction);
            
            // 使用FactionUpgradeManager的金币系统支付费用
            float actualCost = CalculateActualCost(squadToSpawn.cost, faction);
            if (GlobalGameUpgrades.Instance != null)
            {
                FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(faction);
                if (factionManager != null)
                {
                    factionManager.SpendGold(actualCost);
                }
            }
            
            squadCooldowns[squadToSpawn] = squadToSpawn.cooldown;
            UpdateSquadUI();
            return true;
        }
        return false;
    }
    
    // 新增：获取指定类型小兵的成本
    public float GetMinionCost(MinionType minionType)
    {
        // 查找对应类型的小队
        foreach (var squad in rightSquads) // 使用右侧小队作为参考
        {
            if (squad.minionPrefab != null)
            {
                if (minionType == MinionType.Soldier && squad.minionPrefab.GetComponent<SoldierController>() != null)
                {
                    return squad.cost;
                }
                else if (minionType == MinionType.Archer && squad.minionPrefab.GetComponent<ArcherController>() != null)
                {
                    return squad.cost;
                }
                else if (minionType == MinionType.Lancer && squad.minionPrefab.GetComponent<LancerController>() != null)
                {
                    return squad.cost;
                }
                else if (minionType == MinionType.Priest && squad.minionPrefab.GetComponent<PriestController>() != null)
                {
                    return squad.cost;
                }
                else if (minionType == MinionType.Mage && squad.minionPrefab.GetComponent<MageController>() != null)
                {
                    return squad.cost;
                }
            }
        }
        
        // 默认返回士兵的成本
        return 10;
    }
    
    // 新增：获取当前资源
    public float CurrentResource
    {
        get
        {
            if (GlobalGameUpgrades.Instance != null)
            {
                FactionUpgradeManager rightManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Right);
                if (rightManager != null)
                {
                    return rightManager.GetCurrentGold();
                }
            }
            return rightResources;
        }
    }
    
    // 新增：获取最大资源
    public float MaxResource
    {
        get
        {
            return 200f; // 最大资源200
        }
    }
    
    public void ResetResources()
    {
        // 重置FactionUpgradeManager的金币系统
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager leftManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Left);
            FactionUpgradeManager rightManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Right);
            
            if (leftManager != null)
            {
                leftManager.currentGold = initialLeftResources;
            }
            
            if (rightManager != null)
            {
                rightManager.currentGold = initialRightResources;
            }
        }
        else
        {
            // 如果无法获取FactionUpgradeManager，则使用旧系统
            leftResources = initialLeftResources;
            rightResources = initialRightResources;
        }

        // 重置所有小队的冷却时间
        var keys = squadCooldowns.Keys.ToList(); // ToList() 创建一个副本以避免在 foreach 中修改集合
        foreach (var squad in keys)
        {
            squadCooldowns[squad] = 0f;
        }

        UpdateResourceUI(); // 更新UI显示
    }
} 