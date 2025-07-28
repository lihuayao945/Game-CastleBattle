using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MagicBattle; // 添加MagicBattle命名空间，包含可能的UI类型
using UnityEngine.SceneManagement; // 添加场景管理命名空间

/// <summary>
/// AI管理器，负责初始化和管理所有AI组件
/// </summary>
public class AIManager : MonoBehaviour
{
    [Header("AI Settings")]
    [SerializeField] private bool isRightSideAI = true; // 是否启用右侧AI
    [SerializeField] private GameObject necromancerAIPrefab; // AI控制的邪术师预制体
    [SerializeField] private NecromancerAIController.AIDifficulty aiDifficulty = NecromancerAIController.AIDifficulty.Normal; // AI难度设置
    
    [Header("References")]
    [SerializeField] private Transform rightSpawnPoint; // 右侧出生点
    [SerializeField] private HeroHealthBar rightHeroHealthBar; // 修改为HeroHealthBar类型
    [SerializeField] private MinionSpawner minionSpawner; // 小兵生成器
    
    // AI组件引用
    private NecromancerAIController heroAI; // 英雄AI控制器
    private MinionSpawnAIController minionAI; // 小兵召唤AI控制器
    private UpgradeAIController upgradeAI; // 强化选择AI控制器
    
    // 单例实例
    public static AIManager Instance { get; private set; }
    
    // 重生控制变量
    private bool isRespawning = false;
    private Coroutine respawnCoroutine = null;
    
    private void Awake()
    {
        // 设置单例实例
        if (Instance == null)
        {
            Instance = this;
            
            // 确保在场景切换时不被销毁
            DontDestroyOnLoad(gameObject);
            
            // 注册场景加载事件
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            //Debug.Log("AIManager已初始化并设置为DontDestroyOnLoad");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void OnDestroy()
    {
        // 解除场景加载事件
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        // 取消正在进行的重生协程
        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
        }
    }
    
    /// <summary>
    /// 场景加载事件处理
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 如果离开了战斗场景，取消所有重生请求
        if (scene.name == "MainMenu" || scene.name == "Settings" || scene.name != "Game")
        {
            if (isRespawning)
            {
                //Debug.Log($"场景切换到{scene.name}，取消所有邪术师重生请求");
                CancelAllRespawnRequests();
            }
        }
    }
    
    /// <summary>
    /// 取消所有重生请求
    /// </summary>
    public void CancelAllRespawnRequests()
    {
        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
        }
        isRespawning = false;
        //Debug.Log("所有邪术师重生请求已取消");
    }
    
    private void Start()
    {
        // 游戏开始时不立即初始化AI，等待GameManager调用
    }
    
    /// <summary>
    /// 初始化AI系统
    /// </summary>
    public void InitializeAI()
    {
        if (IsRightSideAI())
        {
            StartCoroutine(InitializeAICoroutine());
        }
    }
    
    /// <summary>
    /// 初始化AI系统的协程
    /// </summary>
    /// <returns>协程</returns>
    private IEnumerator InitializeAICoroutine()
    {
        // 等待一帧，确保其他系统已初始化
        yield return null;
        
        // 创建AI控制的邪术师，保留原始缩放
        GameObject rightHero = Instantiate(necromancerAIPrefab, rightSpawnPoint.position, Quaternion.identity);
        
        // 确保保留预制体的原始缩放值
        rightHero.transform.localScale = necromancerAIPrefab.transform.localScale;
        
        // 设置英雄阵营为右方
        HeroUnit rightHeroUnit = rightHero.GetComponent<HeroUnit>();
        if (rightHeroUnit != null)
        {
            rightHeroUnit.faction = Unit.Faction.Right;
            
            // 绑定英雄血条UI
            if (rightHeroHealthBar != null)
            {
                rightHeroUnit.AssignHealthBar(rightHeroHealthBar);
            }
            
            // 设置出生点
            rightHeroUnit.SetSpawnPoint(rightSpawnPoint.position);
        }
        
        // 为AI英雄添加小地图图标
        MinimapIcon aiIcon = rightHero.GetComponent<MinimapIcon>();
        if (aiIcon == null)
        {
            aiIcon = rightHero.AddComponent<MinimapIcon>();
        }
        // 强制创建图标
        if (aiIcon != null)
        {
            aiIcon.ForceCreateIcon();
            //Debug.Log("强制为AI英雄创建了小地图图标");
        }
        
        // 添加AI控制器
        heroAI = rightHero.AddComponent<NecromancerAIController>();
        heroAI.Initialize(aiDifficulty);
        
        // 添加小兵召唤AI
        minionAI = gameObject.AddComponent<MinionSpawnAIController>();
        if (minionSpawner != null)
        {
            minionAI.Initialize(aiDifficulty);
        }
        else
        {
            Debug.LogError("AIManager: 无法找到MinionSpawner组件");
        }
        
        // 添加强化选择AI
        upgradeAI = gameObject.AddComponent<UpgradeAIController>();
        upgradeAI.Initialize(aiDifficulty);
        
        //Debug.Log("AI系统初始化完成，难度：" + aiDifficulty);
    }
    
    /// <summary>
    /// 判断右侧是否为AI
    /// </summary>
    /// <returns>右侧是否为AI</returns>
    public bool IsRightSideAI()
    {
        // 检查游戏模式是否为单人模式
        GameMode currentMode = GameManager.Instance.CurrentMode;
        return isRightSideAI && currentMode == GameMode.SinglePlayer;
    }
    
    /// <summary>
    /// 设置AI激活状态
    /// </summary>
    /// <param name="active">是否激活</param>
    public void SetAIActive(bool active)
    {
        // 设置英雄AI激活状态
        if (heroAI != null)
        {
            heroAI.SetActive(active);
        }
        
        // 设置小兵召唤AI激活状态
        if (minionAI != null)
        {
            minionAI.SetActive(active);
        }
        
        // 设置强化选择AI激活状态
        if (upgradeAI != null)
        {
            upgradeAI.SetActive(active);
        }
    }
    
    /// <summary>
    /// 设置AI难度
    /// </summary>
    /// <param name="difficulty">难度</param>
    public void SetAIDifficulty(NecromancerAIController.AIDifficulty difficulty)
    {
        aiDifficulty = difficulty;
        
        // 如果AI已经初始化，更新难度
        if (heroAI != null)
        {
            heroAI.Initialize(difficulty);
        }
        
        if (minionAI != null)
        {
            minionAI.SetDifficulty(difficulty);
        }
        
        if (upgradeAI != null)
        {
            upgradeAI.SetDifficulty(difficulty);
        }
    }
    
    /// <summary>
    /// 安排邪术师AI在指定时间后重生
    /// </summary>
    /// <param name="delay">延迟时间（秒）</param>
    /// <param name="difficulty">AI难度</param>
    public void ScheduleNecromancerRespawn(float delay, int difficulty)
    {
        // 如果已经有重生请求，先取消
        if (isRespawning)
        {
            CancelAllRespawnRequests();
        }
        
        // 转换整数难度为枚举类型
        NecromancerAIController.AIDifficulty aiDiff = 
            (NecromancerAIController.AIDifficulty)difficulty;
            
        // 标记为正在重生
        isRespawning = true;
            
        // 启动重生协程
        respawnCoroutine = StartCoroutine(RespawnNecromancerAfterDelay(delay, aiDiff));
        
        //Debug.Log($"AIManager: 已安排邪术师AI在{delay}秒后重生，难度：{aiDiff}");
    }
    
    /// <summary>
    /// 在指定延迟后重新生成邪术师AI
    /// </summary>
    /// <param name="delay">延迟时间</param>
    /// <param name="difficulty">AI难度</param>
    /// <returns>协程</returns>
    private IEnumerator RespawnNecromancerAfterDelay(float delay, NecromancerAIController.AIDifficulty difficulty)
    {
        //Debug.Log($"等待{delay}秒后重生邪术师AI");
        
        // 等待指定的延迟时间
        yield return new WaitForSeconds(delay);
        
        // 只在以下两种情况下取消邪术师重生：
        // 1. 一方通过击败对方城堡获得胜利时（GameState.GameOver）
        // 2. 玩家通过暂停界面中的返回主界面主动结束该局游戏时（GameState.NotStarted）
        if (GameManager.Instance.CurrentState == GameState.GameOver || 
            GameManager.Instance.CurrentState == GameState.NotStarted)
        {
            //Debug.Log("游戏已结束或返回主菜单，取消邪术师重生");
            isRespawning = false;
            respawnCoroutine = null;
            yield break;
        }
        
        // 检查是否已存在邪术师实例
        NecromancerAIController[] existingAIs = FindObjectsOfType<NecromancerAIController>();
        if (existingAIs.Length > 0)
        {
            //Debug.LogWarning($"场景中已存在{existingAIs.Length}个邪术师实例，取消重生");
            isRespawning = false;
            respawnCoroutine = null;
            yield break;
        }
        
        // 重新生成邪术师AI
        SpawnNecromancer(difficulty);
        
        // 重置状态
        isRespawning = false;
        respawnCoroutine = null;
        
        //Debug.Log("AIManager: 邪术师AI重生完成");
    }
    
    /// <summary>
    /// 立即重新生成邪术师AI
    /// </summary>
    public void RespawnNecromancer()
    {
        // 取消现有的重生请求
        CancelAllRespawnRequests();

        // 检查是否已存在邪术师实例 - 使用GameManager的注册系统
        bool necromancerExists = false;
        if (GameManager.Instance != null)
        {
            foreach (Unit unit in GameManager.Instance.GetRegisteredUnits())
            {
                if (unit != null && unit.CompareTag("Hero") && unit.faction == Unit.Faction.Right)
                {
                    necromancerExists = true;
                    break;
                }
            }
        }

        if (necromancerExists)
        {
            //Debug.LogWarning("场景中已存在邪术师实例，取消立即重生");
            return;
        }

        // 立即生成
        SpawnNecromancer(aiDifficulty);
        //Debug.Log("AIManager: 立即重生邪术师AI");
    }
    
    /// <summary>
    /// 生成邪术师AI
    /// </summary>
    /// <param name="difficulty">AI难度</param>
    private void SpawnNecromancer(NecromancerAIController.AIDifficulty difficulty)
    {
        // 检查是否有预制体
        if (necromancerAIPrefab == null)
        {
            Debug.LogError("AIManager: 无法重生邪术师AI，预制体未设置");
            return;
        }
        
        // 检查出生点
        Vector3 spawnPosition = rightSpawnPoint != null 
            ? rightSpawnPoint.position 
            : new Vector3(8f, 0f, 0f); // 默认出生位置
        
        // 创建AI控制的邪术师，保留原始缩放
        GameObject rightHero = Instantiate(necromancerAIPrefab, spawnPosition, Quaternion.identity);
        
        // 确保保留预制体的原始缩放值
        rightHero.transform.localScale = necromancerAIPrefab.transform.localScale;
        
        // 设置英雄阵营为右方
        HeroUnit rightHeroUnit = rightHero.GetComponent<HeroUnit>();
        if (rightHeroUnit != null)
        {
            rightHeroUnit.faction = Unit.Faction.Right;
            
            // 绑定英雄血条UI
            if (rightHeroHealthBar != null)
            {
                rightHeroUnit.AssignHealthBar(rightHeroHealthBar);
            }
            
            // 设置出生点
            if (rightSpawnPoint != null)
            {
                rightHeroUnit.SetSpawnPoint(rightSpawnPoint.position);
            }
        }
        
        // 为重生的AI英雄添加小地图图标
        MinimapIcon aiIcon = rightHero.GetComponent<MinimapIcon>();
        if (aiIcon == null)
        {
            aiIcon = rightHero.AddComponent<MinimapIcon>();
        }
        // 强制创建图标
        if (aiIcon != null)
        {
            aiIcon.ForceCreateIcon();
            //Debug.Log("强制为AI英雄创建了小地图图标");
        }
        
        // 添加AI控制器
        heroAI = rightHero.GetComponent<NecromancerAIController>();
        if (heroAI == null)
        {
            heroAI = rightHero.AddComponent<NecromancerAIController>();
        }
        // 初始化AI控制器
        heroAI.Initialize(difficulty);
        
        //Debug.Log($"AIManager: 生成了新的邪术师AI，难度：{difficulty}");
    }
} 