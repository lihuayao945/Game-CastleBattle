using UnityEngine;
using MagicBattle; // 添加命名空间引用

namespace MagicBattle
{
    /// <summary>
    /// 小地图设置 - 在场景中添加小地图功能
    /// </summary>
    public class MinimapSetup : MonoBehaviour
    {
        [Header("小地图UI设置")]
        [SerializeField] private Vector2 minimapSize = new Vector2(280, 100); // 小地图尺寸
        [SerializeField] private Vector2 minimapPosition = new Vector2(20, 20); // 小地图位置（右下角偏移）
        [SerializeField] private Color minimapBorderColor = new Color(1, 1, 1, 0.8f); // 小地图边框颜色
        [SerializeField] private Color backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1.0f); // 更亮的灰色背景，完全不透明
        
        [Header("地图边界设置")]
        [SerializeField] private float minX = -49f;
        [SerializeField] private float maxX = 51.9f;
        [SerializeField] private float minY = -2f;
        [SerializeField] private float maxY = 18f;
        
        [Header("适配设置")]
        [SerializeField] private bool autoAdjustHeight = true; // 是否自动调整高度以适配地图比例
        [SerializeField] private bool maintainAspectRatio = true; // 是否保持地图比例
        [Range(1.0f, 3.0f)]
        [SerializeField] private float heightScaleFactor = 1.0f; // 高度缩放系数，设为1.0不再拉伸
        [SerializeField] private bool showDebugInfo = true; // 是否显示调试信息
        
        [Header("主摄像机视野设置")]
        [SerializeField] private bool showMainCameraView = true; // 是否显示主摄像机视野范围
        [SerializeField] private Color mainCameraViewColor = new Color(1, 1, 1, 0.5f); // 主摄像机视野范围颜色
        [Range(1.0f, 5.0f)]
        [SerializeField] private float mainCameraViewLineWidth = 2f; // 主摄像机视野范围线宽
        
        [Header("游戏状态控制")]
        [SerializeField] private bool onlyShowDuringGameplay = true; // 是否只在游戏进行时显示小地图
        [SerializeField] private string minimapPanelName = "MinimapPanel"; // 小地图面板名称
        
        // 小地图系统引用
        private static GameObject minimapSystemInstance;
        private MinimapCamera minimapCameraComponent;
        
        // 在场景加载时自动创建小地图
        private void Awake()
        {
            // 检查是否已经存在小地图系统
            if (minimapSystemInstance == null)
            {
                // 创建小地图摄像机对象
                minimapSystemInstance = new GameObject("MinimapSystem");
                minimapCameraComponent = minimapSystemInstance.AddComponent<MinimapCamera>();
                
                // 确保不会被销毁
                DontDestroyOnLoad(minimapSystemInstance);
                
                // 应用设置
                ApplySettings();
                
                //Debug.Log("小地图系统已创建");
            }
            else
            {
                // 如果已存在，获取引用并应用新设置
                minimapCameraComponent = minimapSystemInstance.GetComponent<MinimapCamera>();
                if (minimapCameraComponent != null)
                {
                    ApplySettings();
                    //Debug.Log("小地图系统已更新设置");
                }
                else
                {
                    //Debug.LogWarning("找到小地图系统但未找到MinimapCamera组件");
                }
            }
            
            // 为场景中的城堡添加小地图图标
            AddMinimapIconsToCastles();
        }

        private void Start()
        {
            // 检查游戏状态，如果不是游戏进行中，且设置为只在游戏中显示，则隐藏小地图
            if (onlyShowDuringGameplay && GameManager.Instance != null)
            {
                CheckGameState(GameManager.Instance.CurrentState);
            }
        }

        private void Update()
        {
            // 持续检查游戏状态
            if (onlyShowDuringGameplay && GameManager.Instance != null)
            {
                CheckGameState(GameManager.Instance.CurrentState);
            }
        }

        /// <summary>
        /// 检查游戏状态并相应地显示或隐藏小地图
        /// </summary>
        private void CheckGameState(GameState currentState)
        {
            if (minimapCameraComponent != null)
            {
                if (currentState == GameState.Playing || currentState == GameState.Paused)
                {
                    // 如果游戏正在进行中或暂停，显示小地图
                    minimapCameraComponent.ShowMinimap();
                }
                else
                {
                    // 如果游戏未开始或已结束，隐藏小地图
                    minimapCameraComponent.HideMinimap();
                }
            }
        }
        
        /// <summary>
        /// 为场景中的城堡添加小地图图标
        /// </summary>
        public void AddMinimapIconsToCastles()
        {
            // 查找所有城堡
            left_castle[] leftCastles = FindObjectsOfType<left_castle>();
            right_castle[] rightCastles = FindObjectsOfType<right_castle>();
            
            // 为左方城堡添加小地图图标
            foreach (left_castle castle in leftCastles)
            {
                MinimapIcon castleIcon = castle.GetComponent<MinimapIcon>();
                if (castleIcon == null)
                {
                    castleIcon = castle.gameObject.AddComponent<MinimapIcon>();
                }
                // 强制创建图标
                if (castleIcon != null)
                {
                    castleIcon.ForceCreateIcon();
                    if (showDebugInfo)
                    {
                        //Debug.Log($"MinimapSetup: 强制为左方城堡 {castle.name} 创建了小地图图标");
                    }
                }
            }
            
            // 为右方城堡添加小地图图标
            foreach (right_castle castle in rightCastles)
            {
                MinimapIcon castleIcon = castle.GetComponent<MinimapIcon>();
                if (castleIcon == null)
                {
                    castleIcon = castle.gameObject.AddComponent<MinimapIcon>();
                }
                // 强制创建图标
                if (castleIcon != null)
                {
                    castleIcon.ForceCreateIcon();
                    if (showDebugInfo)
                    {
                        //Debug.Log($"MinimapSetup: 强制为右方城堡 {castle.name} 创建了小地图图标");
                    }
                }
            }
        }
        
        /// <summary>
        /// 刷新所有小地图图标 - 可以在游戏开始时调用
        /// </summary>
        public void RefreshAllMinimapIcons()
        {
            // 为城堡添加小地图图标
            AddMinimapIconsToCastles();
            
            // 查找所有英雄单位
            HeroUnit[] heroes = FindObjectsOfType<HeroUnit>();
            
            // 为英雄添加小地图图标
            foreach (HeroUnit hero in heroes)
            {
                MinimapIcon heroIcon = hero.GetComponent<MinimapIcon>();
                if (heroIcon == null)
                {
                    heroIcon = hero.gameObject.AddComponent<MinimapIcon>();
                }
                // 强制创建图标
                if (heroIcon != null)
                {
                    heroIcon.ForceCreateIcon();
                    if (showDebugInfo)
                    {
                        //Debug.Log($"MinimapSetup: 强制为英雄 {hero.name} 创建了小地图图标");
                    }
                }
            }
        }
        
        // 应用设置到MinimapCamera组件
        private void ApplySettings()
        {
            if (minimapCameraComponent != null)
            {
                minimapCameraComponent.SetMinimapSize(minimapSize);
                minimapCameraComponent.SetMinimapPosition(minimapPosition);
                minimapCameraComponent.SetBorderColor(minimapBorderColor);
                minimapCameraComponent.SetBackgroundColor(backgroundColor);
                minimapCameraComponent.SetMapBoundaries(minX, maxX, minY, maxY);
                minimapCameraComponent.SetAutoAdjustHeight(autoAdjustHeight);
                minimapCameraComponent.SetMaintainAspectRatio(maintainAspectRatio);
                minimapCameraComponent.SetHeightScaleFactor(heightScaleFactor);
                minimapCameraComponent.SetShowMainCameraView(showMainCameraView);
                minimapCameraComponent.SetMainCameraViewColor(mainCameraViewColor);
                minimapCameraComponent.SetMainCameraViewLineWidth(mainCameraViewLineWidth);
                
                // 设置游戏状态控制选项
                minimapCameraComponent.SetOnlyShowDuringGameplay(onlyShowDuringGameplay);
                minimapCameraComponent.SetMinimapPanelName(minimapPanelName);
                
                // 查找GamePanel并设置
                GameObject gamePanelObj = GameObject.Find("GamePanel");
                if (gamePanelObj != null)
                {
                    minimapCameraComponent.SetGamePanel(gamePanelObj.transform);
                }
                
                // 重新初始化小地图
                minimapCameraComponent.InitializeMinimapSystem();
            }
        }
        
        // 在运行时修改设置后调用此方法
        public void UpdateMinimapSettings()
        {
            ApplySettings();
        }
        
        // 在编辑器中可视化
        //private void OnDrawGizmos()
        //{
        //    if (!showDebugInfo) return;
            
        //    // 绘制边界
        //    Gizmos.color = Color.yellow;
        //    Gizmos.DrawLine(new Vector3(minX, minY, 0), new Vector3(maxX, minY, 0));
        //    Gizmos.DrawLine(new Vector3(maxX, minY, 0), new Vector3(maxX, maxY, 0));
        //    Gizmos.DrawLine(new Vector3(maxX, maxY, 0), new Vector3(minX, maxY, 0));
        //    Gizmos.DrawLine(new Vector3(minX, maxY, 0), new Vector3(minX, minY, 0));
            
        //    // 绘制中心点
        //    Gizmos.color = Color.red;
        //    Vector3 center = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0);
        //    Gizmos.DrawSphere(center, 1f);
            
        //    // 绘制文本标签
        //    UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, "小地图系统");
            
        //    // 计算并显示地图比例
        //    float mapWidth = maxX - minX;
        //    float mapHeight = maxY - minY;
        //    float mapAspectRatio = mapWidth / mapHeight;
            
        //    // 显示地图比例信息
        //    string infoText = $"地图比例: {mapAspectRatio:F2} ({mapWidth:F1} x {mapHeight:F1})";
        //    if (autoAdjustHeight)
        //    {
        //        float adjustedHeight = minimapSize.x / mapAspectRatio;
        //        infoText += $"\n调整后小地图尺寸: {minimapSize.x} x {adjustedHeight:F1}";
        //        // 不再显示高度缩放系数
        //    }
        //    else
        //    {
        //        infoText += $"\n当前小地图尺寸: {minimapSize.x} x {minimapSize.y}";
        //    }
            
        //    // 显示主摄像机视野信息
        //    if (showMainCameraView)
        //    {
        //        infoText += "\n已启用主摄像机视野显示";
        //    }

        //    // 显示游戏状态控制信息
        //    if (onlyShowDuringGameplay)
        //    {
        //        infoText += "\n小地图仅在游戏进行时显示";
        //    }
            
        //    UnityEditor.Handles.Label(center + Vector3.up * 3, infoText);
        //}
    }
} 