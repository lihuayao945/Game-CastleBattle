using UnityEngine;
using MagicBattle;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 游戏管理器 - 控制游戏的主要流程和状态
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    public GameState CurrentState { get; private set; }
    public GameMode CurrentMode { get; private set; }
    
    [Header("Hero Prefabs")]
    [SerializeField] private GameObject knightPrefab;
    [SerializeField] private GameObject necromancerPrefab;

    [Header("Game Settings")]
    [SerializeField] private Transform heroSpawnPoint;
    [SerializeField] private Transform gamePanel;
    [SerializeField] private Transform bottomBar;
    [SerializeField] private MinionSpawner minionSpawner;

    [Header("AI Settings")]
    [SerializeField] private bool isRightSideAI = true; // 是否启用右侧AI
    [SerializeField] private NecromancerAIController.AIDifficulty aiDifficulty = NecromancerAIController.AIDifficulty.Normal; // AI难度设置
    [SerializeField] private Transform rightSpawnPoint; // 右侧出生点
    [SerializeField] private TextMeshProUGUI difficultyText; // 难度显示文本
    [SerializeField] private Color normalDifficultyColor = new Color(0.5f, 0.5f, 1f); // 普通难度颜色（蓝色）
    [SerializeField] private Color hardDifficultyColor = new Color(1f, 0.3f, 0.3f); // 困难难度颜色（红色）

    [Header("UI References")]
    [SerializeField] private UpgradeSelectionUI upgradeSelectionUI; // 肉鸽强化选择UI引用
    [SerializeField] private PlayerStatsPanelUI playerStatsPanelUI; // 玩家数据面板UI引用
    [SerializeField] private HeroHealthBar leftHeroHealthBar; // 新增：左方英雄血条UI引用
    [SerializeField] private HeroHealthBar rightHeroHealthBar; // 新增：右方英雄血条UI引用
    [SerializeField] private HeroResurrectionTimerUI resurrectionTimerUI; // 新增：英雄复活计时器UI引用

    [System.Serializable]
    public class SkillButtonUI
    {
        public Image icon;
        public Image cooldownMask;
        public TextMeshProUGUI keyText;
        public float currentCooldown;
        public float maxCooldown;
    }

    private GameObject currentHero;
    private CharacterSkillManager skillManager;
    private HeroDataSO currentHeroData;
    private SkillButtonUI[] skillButtons;

    private bool isPaused = false;
    private bool isUpgradeSelectionPaused = false; // 新增：标记是否因为强化选择而暂停
    private bool isStatsPanelPaused = false; // 新增：标记是否因为数据面板而暂停
    public bool IsPaused => isPaused;

    // 强化选择计时器
    private float upgradeTimer = 0f;
    [SerializeField] private float timeToNextUpgrade = 30f; // 每60秒触发一次强化选择
    
    // 死亡单位清理系统
    private float cleanupTimer = 0f;
    [SerializeField] private float cleanupInterval = 10f; // 每4秒检测一次卡死的单位

    // 单位注册系统（避免使用FindObjectsOfType）
    private HashSet<Unit> registeredUnits = new HashSet<Unit>();

    // AI系统引用
    private AIManager aiManager;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        
        // 初始化AI管理器
        aiManager = GetComponent<AIManager>();
        if (aiManager == null)
        {
            aiManager = gameObject.AddComponent<AIManager>();
        }
    }
    
    private void OnEnable()
    {
        // 订阅强化选择面板的可见性变化事件
        UpgradeSelectionUI.OnUpgradePanelVisibilityChanged += HandleUpgradePanelVisibility;
    }

    private void OnDisable()
    {
        // 取消订阅，防止内存泄漏
        UpgradeSelectionUI.OnUpgradePanelVisibilityChanged -= HandleUpgradePanelVisibility;
    }

    private void Start()
    {
        // 初始化游戏状态
        CurrentState = GameState.NotStarted;
    }

    private void Update()
    {
        // 在游戏进行中或普通暂停状态下检测ESC键，但不在强化选择或数据面板时响应ESC
        if ((CurrentState == GameState.Playing || (CurrentState == GameState.Paused && !isUpgradeSelectionPaused && !isStatsPanelPaused)) 
            && Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }

        // 新增：检测Tab键显示/隐藏玩家数据面板
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (UIManager.Instance != null)
            {
                if (UIManager.Instance.IsPlayerStatsPanelActive())
                {
                    UIManager.Instance.HidePlayerStatsPanel();
                    ResumeGameFromStatsPanel(); // 恢复游戏
                }
                else if (CurrentState == GameState.Playing) // 只有在游戏进行中才能打开数据面板
                {
                    UIManager.Instance.ShowPlayerStatsPanel();
                    PauseGameForStatsPanel(); // 暂停游戏
                }
            }
        }

        // 只有在非暂停状态下才更新技能冷却和进行单位清理
        if (!isPaused)
        {
            // 更新技能冷却
            if (skillButtons != null)
            {
                UpdateSkillCooldowns();
            }

            // 游戏进行中的计时逻辑
            if (CurrentState == GameState.Playing)
            { 
                // 强化选择计时
                upgradeTimer += Time.deltaTime;
                if (upgradeTimer >= timeToNextUpgrade)
                {
                    upgradeTimer = 0f; // 重置计时器
                    TriggerUpgradeSelection();
                }
                
                // 卡死单位清理计时
                cleanupTimer += Time.deltaTime;
                if (cleanupTimer >= cleanupInterval)
                {
                    cleanupTimer = 0f; // 重置计时器
                    StartCoroutine(CleanupDeadUnitsCoroutine());
                }
            }
        }
    }

    private void UpdateSkillCooldowns()
    {
        for (int i = 0; i < skillButtons.Length; i++)
        {
            if (skillButtons[i] != null && skillButtons[i].currentCooldown > 0)
            {
                // 减少当前冷却时间
                skillButtons[i].currentCooldown -= Time.deltaTime;
                
                // 计算冷却比例：剩余冷却时间 / 总冷却时间
                float cooldownRatio = skillButtons[i].currentCooldown / skillButtons[i].maxCooldown;
                
                // 确保比例在有效范围内
                cooldownRatio = Mathf.Clamp01(cooldownRatio);
                
                // 更新冷却遮罩显示
                skillButtons[i].cooldownMask.fillAmount = cooldownRatio;
                
                // 当冷却结束时，重置遮罩
                if (skillButtons[i].currentCooldown <= 0)
                {
                    skillButtons[i].currentCooldown = 0;
                    skillButtons[i].cooldownMask.fillAmount = 0;
                }
            }
        }
    }

    /// <summary>
    /// 触发强化选择UI的显示。
    /// </summary>
    public void TriggerUpgradeSelection()
    {
        if (upgradeSelectionUI != null)
        {
            // 假设玩家是左边阵营。如果未来有选择阵营的逻辑，这里需要动态获取。
            upgradeSelectionUI.ShowUpgradeSelection(Unit.Faction.Left);
        }
        else
        {
            Debug.LogWarning("UpgradeSelectionUI 引用未设置在 GameManager 中。");
        }
    }

    /// <summary>
    /// 处理强化选择面板的可见性变化，用于暂停/恢复游戏。
    /// </summary>
    /// <param name="isVisible">面板是否可见。</param>
    private void HandleUpgradePanelVisibility(bool isVisible)
    {
        if (isVisible)
        {
            // 当强化选择UI显示时，使用专门的强化选择暂停
            PauseGameForUpgrade();
        }
        else
        {
            // 当强化选择UI隐藏时，解除强化选择暂停
            ResumeGameFromUpgrade();
        }
    }

    /// <summary>
    /// 专门用于强化选择的暂停
    /// </summary>
    private void PauseGameForUpgrade()
    {
        if (!isPaused)
        {
            isPaused = true;
            isUpgradeSelectionPaused = true;
            Time.timeScale = 0f;
            CurrentState = GameState.Paused;
        }
    }

    /// <summary>
    /// 专门用于强化选择的恢复
    /// </summary>
    private void ResumeGameFromUpgrade()
    {
        if (isPaused && isUpgradeSelectionPaused)
        {
            isPaused = false;
            isUpgradeSelectionPaused = false;
            Time.timeScale = 1f;
            CurrentState = GameState.Playing;
        }
    }

    /// <summary>
    /// 专门用于数据面板的暂停
    /// </summary>
    private void PauseGameForStatsPanel()
    {
        if (!isPaused)
        {
            isPaused = true;
            isStatsPanelPaused = true;
            Time.timeScale = 0f;
            CurrentState = GameState.Paused;
        }
    }

    /// <summary>
    /// 专门用于数据面板的恢复
    /// </summary>
    private void ResumeGameFromStatsPanel()
    {
        if (isPaused && isStatsPanelPaused)
        {
            isPaused = false;
            isStatsPanelPaused = false;
            Time.timeScale = 1f;
            CurrentState = GameState.Playing;
        }
    }

    /// <summary>
    /// 开始游戏
    /// </summary>
    public void StartGame()
    {
        // 重置所有阵营的强化（重要，确保新游戏从干净状态开始）
        GlobalGameUpgrades.Instance.ResetAllFactionsUpgrades();
        upgradeTimer = 0f; // 重置强化选择计时器

        // 设置游戏模式为单人模式
        CurrentMode = GameMode.SinglePlayer;

        // 随机选择AI难度（50%概率为Normal，50%概率为Hard）
        if (UnityEngine.Random.value < 0.5f)
        {
            aiDifficulty = NecromancerAIController.AIDifficulty.Normal;
        }
        else
        {
            aiDifficulty = NecromancerAIController.AIDifficulty.Hard;
        }
        
        // 更新难度显示文本
        UpdateDifficultyText();

        // 重置游戏状态
        isPaused = false;
        isUpgradeSelectionPaused = false;
        Time.timeScale = 1f;
        CurrentState = GameState.Playing;
        
        // 重置摄像机控制器状态
        CameraController cameraController = FindObjectOfType<CameraController>();
        if (cameraController != null)
        {
            cameraController.ResetToDefaultState();
        }
        
        // 重置复活UI状态
        if (resurrectionTimerUI != null)
        {
            resurrectionTimerUI.ResetUI();
        }
        
        // 获取选中的英雄索引
        int selectedHeroIndex = PlayerPrefs.GetInt("SelectedHeroIndex", 0);
        
        // 加载英雄数据
        string heroDataPath = selectedHeroIndex == 0 ? "Heroes/Knight" : "Heroes/Ncromancer";
        currentHeroData = Resources.Load<HeroDataSO>(heroDataPath);
        
        // 根据索引创建对应的英雄
        GameObject heroPrefab = selectedHeroIndex == 0 ? knightPrefab : necromancerPrefab;
        currentHero = Instantiate(heroPrefab, heroSpawnPoint.position, Quaternion.identity);
        
        // 设置英雄阵营为左方
        HeroUnit heroUnit = currentHero.GetComponent<HeroUnit>();
        if (heroUnit == null)
        {
            heroUnit = currentHero.AddComponent<HeroUnit>();
        }
        heroUnit.faction = Unit.Faction.Left;

        // 为英雄添加小地图图标
        if (currentHero.GetComponent<MinimapIcon>() == null)
        {
            currentHero.AddComponent<MinimapIcon>();
        }

        // 绑定英雄血条UI
        if (leftHeroHealthBar != null)
        {
            heroUnit.AssignHealthBar(leftHeroHealthBar);
        }
        else
        {
            Debug.LogWarning("GameManager: leftHeroHealthBar 引用未设置，无法绑定英雄血条。");
        }

        // 设置英雄的出生点，用于复活
        heroUnit.SetSpawnPoint(heroSpawnPoint.position);
        
        // 新增：设置复活计时器UI的跟踪英雄
        if (resurrectionTimerUI != null)
        {
            resurrectionTimerUI.SetHero(heroUnit);
        }
        
        // 获取技能管理器
        skillManager = currentHero.GetComponent<CharacterSkillManager>();
        
        // 设置技能图标
        SetupSkillIcons();

        // 重置城堡血量
        var leftCastle = GameObject.FindObjectOfType<left_castle>();
        var rightCastle = GameObject.FindObjectOfType<right_castle>();
        
        if (leftCastle != null)
        {
            leftCastle.ResetHealth();
            // 为左方城堡添加小地图图标
            MinimapIcon leftIcon = leftCastle.GetComponent<MinimapIcon>();
            if (leftIcon == null)
            {
                leftIcon = leftCastle.gameObject.AddComponent<MinimapIcon>();
            }
            // 强制创建图标
            if (leftIcon != null)
            {
                leftIcon.ForceCreateIcon();
                //Debug.Log("强制为左方城堡创建了小地图图标");
            }
        }
        
        if (rightCastle != null)
        {
            rightCastle.ResetHealth();
            // 为右方城堡添加小地图图标
            MinimapIcon rightIcon = rightCastle.GetComponent<MinimapIcon>();
            if (rightIcon == null)
            {
                rightIcon = rightCastle.gameObject.AddComponent<MinimapIcon>();
            }
            // 强制创建图标
            if (rightIcon != null)
            {
                rightIcon.ForceCreateIcon();
                //Debug.Log("强制为右方城堡创建了小地图图标");
            }
        }

        // 新增：重置小兵生成器的金币资源和冷却时间
        if (minionSpawner != null)
        {
            minionSpawner.ResetResources();
        }
        else
        {
            Debug.LogWarning("GameManager: MinionSpawner 引用未设置，无法重置金币资源。");
        }

        // 初始化渲染优化系统
        InitializeRenderingOptimization();

        // 初始化AI系统
        if (aiManager != null && IsRightSideAI())
        {
            aiManager.InitializeAI();
        }
        
        // 设置游戏状态为进行中
        SetGameState(GameState.Playing);
        
        // 在所有游戏对象都准备好后，通知摄像机控制器游戏已开始
        if (cameraController != null)
        {
            cameraController.NotifyGameStarted();
        }
        
        // 刷新所有小地图图标
        MinimapSetup minimapSetup = FindObjectOfType<MinimapSetup>();
        if (minimapSetup != null)
        {
            minimapSetup.RefreshAllMinimapIcons();
            //Debug.Log("已刷新所有小地图图标");
        }
    }
    
    /// <summary>
    /// 暂停游戏
    /// </summary>
    public void PauseGame()
    {
        // 如果当前是因为强化选择而暂停，则不执行普通暂停
        if (!isUpgradeSelectionPaused && !isPaused)
        {
        isPaused = true;
        Time.timeScale = 0f;
            CurrentState = GameState.Paused;
        UIManager.Instance.ShowPause();
        }
        
        // 暂停AI
        if (aiManager != null)
        {
            aiManager.SetAIActive(false);
        }
    }
    
    /// <summary>
    /// 恢复游戏
    /// </summary>
    public void ResumeGame()
    {
        // 如果当前是因为强化选择而暂停，则不执行普通恢复
        if (!isUpgradeSelectionPaused && isPaused)
        {
        isPaused = false;
        Time.timeScale = 1f;
            CurrentState = GameState.Playing;
        UIManager.Instance.HidePause();
        }
        
        // 恢复AI
        if (aiManager != null)
        {
            aiManager.SetAIActive(true);
        }
    }
    
    /// <summary>
    /// 结束游戏
    /// </summary>
    public void EndGame()
    {
        // 重置所有游戏状态
        isPaused = false;
        Time.timeScale = 1f;
        CurrentState = GameState.NotStarted;
        
        // 清理当前英雄
        if (currentHero != null)
        {
            Destroy(currentHero);
            currentHero = null;
        }
        
        // 清理技能管理器
        skillManager = null;
        
        // 清理技能按钮
        skillButtons = null;

        // 在游戏结束时，也重置所有阵营的强化，确保下一次开始是干净的
        GlobalGameUpgrades.Instance.ResetAllFactionsUpgrades();
        
        // 停用AI
        if (aiManager != null)
        {
            aiManager.SetAIActive(false);
        }
        
        // 重置复活UI
        if (resurrectionTimerUI != null)
        {
            resurrectionTimerUI.ResetUI();
        }
        
        // 重置摄像机控制器
        CameraController cameraController = FindObjectOfType<CameraController>();
        if (cameraController != null)
        {
            cameraController.ResetToDefaultState();
        }
    }

    private void SetupSkillIcons()
    {
        if (currentHeroData == null || bottomBar == null)
        {
            Debug.LogError($"SetupSkillIcons失败: currentHeroData={currentHeroData != null}, bottomBar={bottomBar != null}");
            return;
        }

        // 初始化技能按钮数组
        skillButtons = new SkillButtonUI[4];

        // 获取所有技能按钮
        for (int i = 0; i < 4; i++)
        {
            if (i < bottomBar.childCount)
            {
                Transform buttonTransform = bottomBar.GetChild(i);
                Transform iconTransform = buttonTransform.Find("Icon");
                Transform cooldownTransform = buttonTransform.Find("CoolDown");
                Transform keyTextTransform = buttonTransform.Find("KeyText");

                if (iconTransform != null && cooldownTransform != null && keyTextTransform != null)
                {
                    skillButtons[i] = new SkillButtonUI
                    {
                        icon = iconTransform.GetComponent<Image>(),
                        cooldownMask = cooldownTransform.GetComponent<Image>(),
                        keyText = keyTextTransform.GetComponent<TextMeshProUGUI>(),
                        currentCooldown = 0f,
                        // 注意：这里需要获取强化后的冷却时间，但SkillData.cooldown是原始值。
                        // 在ApplyUpgrade中，技能冷却的强化会影响到SkillData.GetFinalCooldown。
                        // 这里MaxCooldown可以先用原始值，后续在技能实际使用时，从SkillData获取FinalCooldown。
                        maxCooldown = currentHeroData.skills[i].cooldown
                    };

                    // 设置技能图标
                    if (currentHeroData.skills[i].icon != null)
                    {
                        skillButtons[i].icon.sprite = currentHeroData.skills[i].icon;
                    }

                    // 设置按键文本
                    string[] keyLabels = { "K", "L", "U", "I" };
                    skillButtons[i].keyText.text = keyLabels[i];

                    // 设置冷却遮罩
                    skillButtons[i].cooldownMask.fillAmount = 0;
                    skillButtons[i].cooldownMask.type = Image.Type.Filled;
                    skillButtons[i].cooldownMask.fillMethod = Image.FillMethod.Vertical;
                    skillButtons[i].cooldownMask.fillOrigin = (int)Image.OriginVertical.Bottom;
                }
                else
                {
                    Debug.LogError($"技能按钮 {i + 1} 缺少必要的子对象");
                }
            }
        }
    }

    /// <summary>
    /// 触发技能冷却
    /// </summary>
    public void TriggerSkillCooldown(int skillIndex)
    {
        // 在暂停状态下不触发技能冷却
        if (isPaused) return;

        if (skillIndex >= 0 && skillIndex < skillButtons.Length && skillButtons[skillIndex] != null && currentHero != null)
        {
            // 获取技能的最终冷却时间（考虑强化）
            HeroUnit heroUnit = currentHero.GetComponent<HeroUnit>();
            if (heroUnit != null && skillManager != null && skillIndex < skillManager.skills.Length)
            {
                float finalCooldown = skillManager.skills[skillIndex].GetFinalCooldown(heroUnit.faction);
                skillButtons[skillIndex].currentCooldown = finalCooldown;
                skillButtons[skillIndex].maxCooldown = finalCooldown; // 同时更新maxCooldown以正确显示UI
                
                // 立即更新冷却遮罩显示
                float cooldownRatio = 1.0f; // 刚开始冷却，填充满
                skillButtons[skillIndex].cooldownMask.fillAmount = cooldownRatio;
                
                //Debug.Log($"技能{skillIndex}冷却时间: 原始={currentHeroData.skills[skillIndex].cooldown}秒, 最终={finalCooldown}秒, 遮罩比例={cooldownRatio}");
            }
            else
            {
                // 回退到使用原始冷却时间
                skillButtons[skillIndex].currentCooldown = currentHeroData.skills[skillIndex].cooldown;
                skillButtons[skillIndex].maxCooldown = currentHeroData.skills[skillIndex].cooldown;
                
                // 立即更新冷却遮罩显示
                skillButtons[skillIndex].cooldownMask.fillAmount = 1.0f;
            }
        }
    }

    /// <summary>
    /// 检查是否可以释放技能
    /// </summary>
    public bool CanUseSkill()
    {
        return CurrentState == GameState.Playing && !isUpgradeSelectionPaused && !isStatsPanelPaused; // 新增：数据面板激活时不能使用技能
    }

    /// <summary>
    /// 检查是否可以召唤小兵
    /// </summary>
    public bool CanSpawnMinion()
    {
        return CurrentState == GameState.Playing && !isUpgradeSelectionPaused && !isStatsPanelPaused; // 新增：数据面板激活时不能召唤小兵
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    /// <summary>
    /// 设置游戏状态
    /// </summary>
    public void SetGameState(GameState newState)
    {
        CurrentState = newState;
    }

    public void TogglePause()
    {
        // 如果游戏已经结束，不允许暂停/继续
        if (CurrentState == GameState.GameOver)
            return;
            
        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    /// <summary>
    /// 判断右侧是否为AI
    /// </summary>
    /// <returns>右侧是否为AI</returns>
    public bool IsRightSideAI()
    {
        return isRightSideAI && CurrentMode == GameMode.SinglePlayer;
    }
    
    /// <summary>
    /// 设置AI难度
    /// </summary>
    /// <param name="difficulty">难度</param>
    public void SetAIDifficulty(NecromancerAIController.AIDifficulty difficulty)
    {
        aiDifficulty = difficulty;
        
        // 更新难度显示文本
        UpdateDifficultyText();
        
        // 如果AI管理器已初始化，更新难度
        if (aiManager != null)
        {
            aiManager.SetAIDifficulty(difficulty);
        }
    }

    /// <summary>
    /// 清理卡死的单位（标记为死亡但未销毁的单位）- 分帧处理版本
    /// </summary>
    private IEnumerator CleanupDeadUnitsCoroutine()
    {
        var unitsToRemove = new List<Unit>();
        int cleanedCount = 0;
        int processedCount = 0;

        // 创建副本来避免集合修改异常
        var unitsToCheck = new List<Unit>(registeredUnits);

        foreach (Unit unit in unitsToCheck)
        {
            // 每处理3个单位等待一帧，避免卡顿
            if (processedCount >= 3)
            {
                processedCount = 0;
                yield return null;
            }

            if (unit == null)
            {
                // 单位已被销毁，标记为需要移除
                unitsToRemove.Add(unit);
            }
            else if (unit.IsDead && !(unit is HeroUnit))
            {
                // 检查是否是已标记死亡但仍然存在的单位
                // 排除英雄单位，因为英雄有自己的复活机制

                // 销毁卡死单位
                Destroy(unit.gameObject);
                unitsToRemove.Add(unit);
                cleanedCount++;
            }

            processedCount++;
        }

        // 清理无效引用
        foreach (Unit unit in unitsToRemove)
        {
            registeredUnits.Remove(unit);
        }

        // 如果有单位被清理，输出总数
        if (cleanedCount > 0)
        {
            Debug.Log($"[清理系统] 本次共清理了 {cleanedCount} 个卡死单位");
        }
    }

    private void UpdateDifficultyText()
    {
        if (difficultyText == null) return;
        
        // 设置难度文本内容（中文显示）
        if (aiDifficulty == NecromancerAIController.AIDifficulty.Normal)
        {
            difficultyText.text = "普通人机";
            difficultyText.color = normalDifficultyColor;
        }
        else
        {
            difficultyText.text = "困难人机";
            difficultyText.color = hardDifficultyColor;
        }
    }

    /// <summary>
    /// 获取当前AI难度
    /// </summary>
    /// <returns>当前AI难度</returns>
    public NecromancerAIController.AIDifficulty GetAIDifficulty()
    {
        return aiDifficulty;
    }

    /// <summary>
    /// 初始化渲染优化系统
    /// </summary>
    private void InitializeRenderingOptimization()
    {
        // 确保渲染优化器存在
        MagicBattle.ViewportRenderingOptimizer optimizer = MagicBattle.ViewportRenderingOptimizer.Instance;
    }

    /// <summary>
    /// 注册单位到清理系统
    /// </summary>
    /// <param name="unit">要注册的单位</param>
    public void RegisterUnit(Unit unit)
    {
        if (unit != null)
        {
            registeredUnits.Add(unit);
        }
    }

    /// <summary>
    /// 从清理系统中注销单位
    /// </summary>
    /// <param name="unit">要注销的单位</param>
    public void UnregisterUnit(Unit unit)
    {
        if (unit != null)
        {
            registeredUnits.Remove(unit);
        }
    }

    /// <summary>
    /// 获取所有注册的单位（供AI系统使用，避免FindObjectsOfType）
    /// </summary>
    /// <returns>注册的单位集合</returns>
    public HashSet<Unit> GetRegisteredUnits()
    {
        return registeredUnits;
    }
}