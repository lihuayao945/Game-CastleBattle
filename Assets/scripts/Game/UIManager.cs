using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI管理器 - 控制所有UI界面的显示和隐藏
/// </summary>
namespace MagicBattle
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }
        
        [Header("UI Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject heroSelectPanel;
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private PlayerStatsPanelUI playerStatsPanelUI;
        
        [Header("Buttons")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button gameOverMainMenuButton;
        
        [Header("Game Over UI")]
        [SerializeField] private TextMeshProUGUI resultText;
        
        private TooltipTrigger pauseButtonTooltip;
        
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
        }
        
        private void Start()
        {
            // 默认显示主菜单
            ShowMainMenu();
            
            // 设置暂停按钮提示和点击事件
            if (pauseButton != null)
            {
                pauseButtonTooltip = pauseButton.gameObject.AddComponent<TooltipTrigger>();
                pauseButtonTooltip.tooltipText = "ESC暂停/继续游戏";
                
                // 添加点击事件监听
                pauseButton.onClick.AddListener(OnPauseButtonClick);
            }

            // 设置继续按钮点击事件
            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(OnResumeButtonClick);
            }

            // 设置主菜单按钮点击事件
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(OnMainMenuButtonClick);
            }

            // 设置游戏结束界面的主菜单按钮点击事件
            if (gameOverMainMenuButton != null)
            {
                gameOverMainMenuButton.onClick.AddListener(OnMainMenuButtonClick);
            }
        }

        /// <summary>
        /// 处理暂停按钮点击事件
        /// </summary>
        private void OnPauseButtonClick()
        {
            // 隐藏提示信息
            HidePauseButtonTooltip();
            
            GameManager.Instance.TogglePause();
        }

        /// <summary>
        /// 处理继续按钮点击事件
        /// </summary>
        private void OnResumeButtonClick()
        {
            GameManager.Instance.TogglePause();
        }

        /// <summary>
        /// 处理主菜单按钮点击事件
        /// </summary>
        private void OnMainMenuButtonClick()
        {
            // 清理游戏中的所有预制体
            CleanupGameObjects();
            
            // 结束当前游戏并重置游戏状态
            GameManager.Instance.EndGame();
            Time.timeScale = 1f; // 确保时间缩放被重置
            
            // 重置摄像机控制器状态
            CameraController cameraController = FindObjectOfType<CameraController>();
            if (cameraController != null)
            {
                cameraController.ResetToDefaultState();
            }
            
            // 确保复活UI被重置
            HeroResurrectionTimerUI resurrectUI = FindObjectOfType<HeroResurrectionTimerUI>();
            if (resurrectUI != null)
            {
                resurrectUI.ResetUI();
            }
            
            // 显示主菜单
            ShowMainMenu();
        }

        /// <summary>
        /// 清理游戏中的所有预制体
        /// </summary>
        private void CleanupGameObjects()
        {
            // 清理英雄预制体
            var heroes = GameObject.FindGameObjectsWithTag("Hero");
            foreach (var hero in heroes)
            {
                Destroy(hero);
            }

            // 清理技能预制体
            var skills = GameObject.FindGameObjectsWithTag("Skill");
            foreach (var skill in skills)
            {
                Destroy(skill);
            }

            // 清理其他游戏对象
            var gameObjects = GameObject.FindGameObjectsWithTag("GameObject");
            foreach (var go in gameObjects)
            {
                Destroy(go);
            }
        }

        /// <summary>
        /// 隐藏暂停按钮的提示框
        /// </summary>
        public void HidePauseButtonTooltip()
        {
            if (pauseButtonTooltip != null)
            {
                pauseButtonTooltip.HideTooltip();
            }
        }
        
        /// <summary>
        /// 显示主菜单
        /// </summary>
        public void ShowMainMenu()
        {
            mainMenuPanel.SetActive(true);
            heroSelectPanel.SetActive(false);
            gamePanel.SetActive(false);
            pausePanel.SetActive(false);
            gameOverPanel.SetActive(false);
            HidePlayerStatsPanel();
        }
        
        /// <summary>
        /// 显示英雄选择界面
        /// </summary>
        public void ShowHeroSelect()
        {
            mainMenuPanel.SetActive(false);
            heroSelectPanel.SetActive(true);
            gamePanel.SetActive(false);
            pausePanel.SetActive(false);
            gameOverPanel.SetActive(false);
            HidePlayerStatsPanel();
        }
        
        /// <summary>
        /// 显示游戏界面
        /// </summary>
        public void ShowGame()
        {
            HideAllPanels();
            gamePanel.SetActive(true);
            GameManager.Instance.StartGame();
            HidePlayerStatsPanel();
        }
        
        /// <summary>
        /// 显示暂停界面
        /// </summary>
        public void ShowPause()
        {
            HideAllPanels();
            pausePanel.SetActive(true);
            // 隐藏提示信息
            HidePauseButtonTooltip();
            HidePlayerStatsPanel();
        }
        
        /// <summary>
        /// 隐藏暂停界面
        /// </summary>
        public void HidePause()
        {
            pausePanel.SetActive(false);
            gamePanel.SetActive(true);
            // 隐藏提示信息
            HidePauseButtonTooltip();
            HidePlayerStatsPanel();
        }
        
        /// <summary>
        /// 显示游戏结束界面
        /// </summary>
        public void ShowGameOverUI(bool isWin)
        {
            HideAllPanels();
            gameOverPanel.SetActive(true);
            
            // 设置结果文本
            if (resultText != null)
            {
                resultText.text = isWin ? "胜利!" : "失败!";
            }
            
            // 暂停游戏并设置游戏状态为结束
            Time.timeScale = 0f;
            GameManager.Instance.SetGameState(GameState.GameOver);
            HidePlayerStatsPanel();
        }

        private void HideAllPanels()
        {
            mainMenuPanel.SetActive(false);
            heroSelectPanel.SetActive(false);
            gamePanel.SetActive(false);
            pausePanel.SetActive(false);
            gameOverPanel.SetActive(false);
            HidePlayerStatsPanel();
        }

        /// <summary>
        /// 显示玩家数据面板
        /// </summary>
        public void ShowPlayerStatsPanel()
        {
            if (playerStatsPanelUI != null)
            {
                playerStatsPanelUI.TogglePanel();
            }
        }

        /// <summary>
        /// 隐藏玩家数据面板
        /// </summary>
        public void HidePlayerStatsPanel()
        {
            if (playerStatsPanelUI != null && playerStatsPanelUI.IsPanelActive)
            {
                playerStatsPanelUI.TogglePanel();
            }
        }

        /// <summary>
        /// 检查玩家数据面板是否激活
        /// </summary>
        public bool IsPlayerStatsPanelActive()
        {
            return playerStatsPanelUI != null && playerStatsPanelUI.IsPanelActive;
        }
    }
} 