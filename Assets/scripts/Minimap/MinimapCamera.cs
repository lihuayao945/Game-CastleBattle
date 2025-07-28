using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using MagicBattle; // 添加命名空间引用

namespace MagicBattle
{
    /// <summary>
    /// 简单的小地图摄像机控制器 - 在右下角显示缩小版地图
    /// </summary>
    public class MinimapCamera : MonoBehaviour
    {
        [Header("小地图设置")]
        [SerializeField] private Vector2 minimapSize = new Vector2(200, 200); // 小地图尺寸
        [SerializeField] private Vector2 minimapPosition = new Vector2(20, 20); // 小地图位置（右下角偏移）
        [SerializeField] private Color minimapBorderColor = new Color(1, 0.5f, 0, 1.0f); // 小地图边框颜色（改为橙色）
        [SerializeField] private float minimapBorderWidth = 3f; // 小地图边框宽度
        [SerializeField] private Color backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1.0f); // 更亮的灰色背景，完全不透明
        [SerializeField] private bool enableGlowEffect = true; // 是否启用发光效果
        [SerializeField] private Color glowColor = new Color(1, 0.5f, 0, 0.3f); // 发光颜色（改为橙色）
        [SerializeField] private float glowWidth = 2f; // 发光宽度
        
        [Header("小地图切换设置")]
        [SerializeField] private KeyCode toggleMinimapKey = KeyCode.M; // 切换小地图大小和位置的按键
        [SerializeField] private float expansionMultiplier = 3f; // 扩大倍数（相对于当前小地图尺寸）
        [SerializeField] private float toggleAnimationDuration = 0.3f; // 切换动画持续时间
        [SerializeField] private bool isMinimapExpanded = false; // 当前小地图是否处于扩大状态
        
        [Header("边界设置")]
        [SerializeField] private float minX = -49f;
        [SerializeField] private float maxX = 51.9f;
        [SerializeField] private float minY = -5f;
        [SerializeField] private float maxY = 28f;

        [Header("游戏状态控制")]
        [SerializeField] private bool onlyShowDuringGameplay = true; // 是否只在游戏进行时显示小地图
        [SerializeField] private Transform gamePanel; // 游戏面板引用
        [SerializeField] private string minimapPanelName = "MinimapPanel"; // 小地图面板名称

        // 适配设置
        private bool autoAdjustHeight = true;
        private bool maintainAspectRatio = true;
        private float heightScaleFactor = 1.0f; // 高度缩放系数

        // 主摄像机视野设置
        private bool showMainCameraView = true; // 是否显示主摄像机视野范围
        private Color mainCameraViewColor = new Color(1, 1, 1, 0.5f); // 主摄像机视野范围颜色
        private float mainCameraViewLineWidth = 2f; // 主摄像机视野范围线宽

        // 点击跳转设置
        [SerializeField] private bool enableClickToMove = true; // 是否启用点击小地图跳转功能
        [SerializeField] private bool lockCameraMovement = false; // 是否锁定摄像机移动
        [SerializeField] private float clickTransitionSpeed = 10f; // 点击跳转速度，0表示立即跳转

        // 主摄像机和小地图摄像机
        private Camera mainCamera;
        private Camera minimapCamera;
        
        // 渲染纹理
        private RenderTexture minimapRenderTexture;
        
        // UI元素
        private GameObject minimapUI;
        private RectTransform minimapRectTransform;
        private RawImage minimapImage;
        
        // 实际使用的小地图尺寸（可能会根据地图比例调整）
        private Vector2 actualMinimapSize;
        
        // 主摄像机视野范围UI
        private GameObject mainCameraViewUI;
        private UILineRenderer mainCameraViewLineRenderer;
        
        // 点击跳转相关
        private Vector3? targetPosition = null; // 目标位置，null表示没有目标
        private bool isTransitioning = false; // 是否正在平滑过渡
        
        // 检测小地图点击的矩形区域
        private Rect minimapScreenRect;
        private bool minimapRectCalculated = false;
        
        [Header("性能优化设置")]
        [SerializeField] private float viewUpdateInterval = 0.1f; // 视野框更新间隔（秒）
        [SerializeField] private bool enablePerformanceOptimization = true; // 是否启用性能优化
        
        // 性能优化相关
        private float lastViewUpdateTime = 0f;
        private Vector3 lastMainCameraPosition;
        private float lastMainCameraOrthographicSize;
        private float lastMainCameraAspect;

        // 游戏状态相关
        private GameObject minimapPanel; // 小地图面板
        private bool isInitialized = false; // 是否已初始化
        
        private void Awake()
        {
            // 获取主摄像机
            mainCamera = Camera.main;
            
            // 如果只在游戏进行时显示小地图，则先不初始化
            if (!onlyShowDuringGameplay)
            {
                InitializeMinimapSystem();
                isInitialized = true;
            }
        }
        
        private void Start()
        {
            // 确保找到游戏面板
            if (gamePanel == null)
            {
                GameObject gamePanelObj = GameObject.Find("GamePanel");
                if (gamePanelObj != null)
                {
                    gamePanel = gamePanelObj.transform;
                }
            }

            // 创建或查找小地图面板
            SetupMinimapPanel();

            // 订阅游戏状态变化事件
            if (GameManager.Instance != null)
            {
                // 初始状态检查
                CheckGameState(GameManager.Instance.CurrentState);
            }
        }

        private void OnEnable()
        {
            // 订阅游戏状态变化事件
            if (GameManager.Instance != null)
            {
                // 初始状态检查
                CheckGameState(GameManager.Instance.CurrentState);
            }
        }

        private void OnDisable()
        {
            // 如果游戏结束，清理资源
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.NotStarted)
            {
                HideMinimap();
            }
        }
        
        private void Update()
        {
            // 检查游戏状态
            if (GameManager.Instance != null)
            {
                CheckGameState(GameManager.Instance.CurrentState);
            }

            // 如果小地图不应该显示，则跳过更新
            if (minimapUI == null || !minimapUI.activeSelf)
            {
                return;
            }

            // 如果启用了显示主摄像机视野，则更新视野范围（性能优化版本）
            if (showMainCameraView && mainCamera != null)
            {
                UpdateMainCameraViewOnMinimapOptimized();
            }
            
            // 检查小地图切换按键
            if (Input.GetKeyDown(toggleMinimapKey))
            {
                ToggleMinimapSize();
            }
            
            // 检查小地图点击
            if (enableClickToMove && Input.GetMouseButtonDown(0))
            {
                CheckMinimapClick();
            }
            
            // 处理摄像机平滑移动
            if (isTransitioning && targetPosition.HasValue && mainCamera != null)
            {
                // 检查CameraController的状态，如果进入锁定模式则取消跳转
                CameraController cameraController = mainCamera.GetComponent<CameraController>();
                if (cameraController != null && cameraController.IsFollowingHero())
                {
                    // 如果摄像机进入锁定模式，取消跳转
                    //Debug.Log("取消跳转：摄像机进入锁定模式");
                    isTransitioning = false;
                    targetPosition = null;
                    return;
                }
                
                // 计算目标位置（保持Z坐标不变）
                Vector3 target = new Vector3(targetPosition.Value.x, targetPosition.Value.y, mainCamera.transform.position.z);
                Vector3 currentPos = mainCamera.transform.position;
                
                if (clickTransitionSpeed <= 0)
                {
                    // 立即移动
                    mainCamera.transform.position = target;
                    //Debug.Log($"摄像机立即移动: 从{currentPos}到{target}");
                    isTransitioning = false;
                    targetPosition = null;
                }
                else
                {
                    // 平滑移动
                    Vector3 newPos = Vector3.Lerp(
                        currentPos, 
                        target, 
                        Time.deltaTime * clickTransitionSpeed
                    );
                    
                    // 如果移动距离足够大，输出日志
                    //if (Vector3.Distance(currentPos, newPos) > 0.1f)
                    //{
                    //    Debug.Log($"摄像机正在移动: 从{currentPos}到{newPos}, 目标={target}, 距离={Vector3.Distance(newPos, target)}");
                    //}
                    
                    // 应用新位置
                    mainCamera.transform.position = newPos;
                    
                    // 如果已经非常接近目标，则认为到达
                    if (Vector3.Distance(mainCamera.transform.position, target) < 0.1f)
                    {
                        mainCamera.transform.position = target;
                        //Debug.Log($"摄像机到达目标位置: {target}");
                        isTransitioning = false;
                        targetPosition = null;
                    }
                }
            }
        }

        /// <summary>
        /// 检查游戏状态并相应地显示或隐藏小地图
        /// </summary>
        private void CheckGameState(GameState currentState)
        {
            if (onlyShowDuringGameplay)
            {
                if (currentState == GameState.Playing || currentState == GameState.Paused)
                {
                    // 如果游戏正在进行中或暂停，显示小地图
                    if (!isInitialized)
                    {
                        ShowMinimap();
                    }
                }
                else
                {
                    // 如果游戏未开始或已结束，隐藏小地图
                    HideMinimap();
                }
            }
        }

        /// <summary>
        /// 显示小地图
        /// </summary>
        public void ShowMinimap()
        {
            // 如果小地图面板不存在，创建它
            if (minimapPanel == null)
            {
                SetupMinimapPanel();
            }

            // 显示小地图面板
            if (minimapPanel != null)
            {
                minimapPanel.SetActive(true);
            }

            // 如果小地图UI不存在，初始化小地图系统
            if (!isInitialized)
            {
                InitializeMinimapSystem();
                isInitialized = true;
            }

            // 确保小地图UI显示
            if (minimapUI != null)
            {
                minimapUI.SetActive(true);
            }
        }

        /// <summary>
        /// 隐藏小地图
        /// </summary>
        public void HideMinimap()
        {
            // 隐藏小地图面板
            if (minimapPanel != null)
            {
                minimapPanel.SetActive(false);
            }

            // 隐藏小地图UI
            if (minimapUI != null)
            {
                minimapUI.SetActive(false);
            }

            // 如果游戏已结束，清理资源
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.NotStarted)
            {
                CleanupMinimapIcons();
            }
        }

        /// <summary>
        /// 设置小地图面板
        /// </summary>
        private void SetupMinimapPanel()
        {
            // 查找游戏面板
            if (gamePanel == null)
            {
                GameObject gamePanelObj = GameObject.Find("GamePanel");
                if (gamePanelObj != null)
                {
                    gamePanel = gamePanelObj.transform;
                }
                else
                {
                    Debug.LogWarning("找不到GamePanel，小地图将直接添加到Canvas下");
                }
            }

            // 查找或创建小地图面板
            Transform panelTransform = null;
            if (gamePanel != null)
            {
                panelTransform = gamePanel.Find(minimapPanelName);
            }

            // 如果面板不存在，创建它
            if (panelTransform == null)
            {
                GameObject newPanel = new GameObject(minimapPanelName);
                if (gamePanel != null)
                {
                    newPanel.transform.SetParent(gamePanel, false);
                }
                else
                {
                    // 如果找不到游戏面板，直接添加到Canvas
                    Canvas canvas = FindObjectOfType<Canvas>();
                    if (canvas != null)
                    {
                        newPanel.transform.SetParent(canvas.transform, false);
                    }
                }

                // 设置RectTransform
                RectTransform rectTransform = newPanel.AddComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;

                minimapPanel = newPanel;
            }
            else
            {
                minimapPanel = panelTransform.gameObject;
            }

            // 默认情况下隐藏小地图面板
            if (onlyShowDuringGameplay)
            {
                minimapPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 清理小地图图标
        /// </summary>
        private void CleanupMinimapIcons()
        {
            // 查找所有MinimapIcon组件并销毁
            MinimapIcon[] icons = FindObjectsOfType<MinimapIcon>();
            foreach (MinimapIcon icon in icons)
            {
                if (icon != null)
                {
                    Destroy(icon);
                }
            }
        }
        
        /// <summary>
        /// 检测小地图点击
        /// </summary>
        private void CheckMinimapClick()
        {
            // 确保已计算小地图屏幕区域
            if (!minimapRectCalculated && minimapRectTransform != null)
            {
                CalculateMinimapScreenRect();
            }
            
            // 如果尚未计算小地图区域或者主摄像机不存在，则返回
            if (!minimapRectCalculated || mainCamera == null) return;
            
            // 获取鼠标位置
            Vector2 mousePos = Input.mousePosition;
            
            // 检查点击是否在小地图区域内
            if (minimapScreenRect.Contains(mousePos))
            {
                //Debug.Log($"检测到小地图点击：鼠标位置={mousePos}, 小地图区域={minimapScreenRect}");
                HandleMinimapClick(mousePos);
            }
        }
        
        /// <summary>
        /// 计算小地图在屏幕上的矩形区域
        /// </summary>
        private void CalculateMinimapScreenRect()
        {
            if (minimapRectTransform == null) return;
            
            // 计算小地图在屏幕上的位置和大小
            Vector3[] corners = new Vector3[4];
            minimapRectTransform.GetWorldCorners(corners);
            
            // 记录小地图区域
            Vector2 min = corners[0];
            Vector2 max = corners[2];
            
            minimapScreenRect = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            minimapRectCalculated = true;
            
            //Debug.Log($"小地图屏幕区域: {minimapScreenRect}");
        }
        
        /// <summary>
        /// 初始化或重新初始化小地图系统
        /// </summary>
        public void InitializeMinimapSystem()
        {
            // 重置小地图区域计算标志
            minimapRectCalculated = false;
            
            // 清理旧的资源
            CleanupResources();
            
            // 计算适合地图比例的小地图尺寸
            CalculateMinimapSize();
            
            // 创建小地图摄像机
            CreateMinimapCamera();
            
            // 创建小地图UI
            CreateMinimapUI();
            
            // 创建主摄像机视野范围UI
            if (showMainCameraView)
            {
                CreateMainCameraViewUI();
            }
        }
        
        /// <summary>
        /// 计算适合地图比例的小地图尺寸
        /// </summary>
        private void CalculateMinimapSize()
        {
            // 计算地图比例
            float mapWidth = maxX - minX;
            float mapHeight = maxY - minY;
            float mapAspectRatio = mapWidth / mapHeight;
            
            // 记录原始小地图尺寸
            actualMinimapSize = minimapSize;
            
            // 如果需要自动调整高度以适配地图比例
            if (autoAdjustHeight)
            {
                // 根据地图比例计算小地图高度
                float adjustedHeight = minimapSize.x / mapAspectRatio;
                
                // 不再应用高度缩放系数，保持原始比例
                actualMinimapSize.y = adjustedHeight;
                
                //Debug.Log($"小地图尺寸自动调整: {minimapSize.x} x {adjustedHeight}");
            }
        }
        
        /// <summary>
        /// 清理旧的资源
        /// </summary>
        private void CleanupResources()
        {
            // 销毁旧的渲染纹理
            if (minimapRenderTexture != null)
            {
                minimapRenderTexture.Release();
                Destroy(minimapRenderTexture);
                minimapRenderTexture = null;
            }
            
            // 销毁旧的UI
            if (minimapUI != null)
            {
                Destroy(minimapUI);
                minimapUI = null;
                minimapRectTransform = null;
                minimapImage = null;
            }
            
            // 销毁旧的摄像机
            if (minimapCamera != null)
            {
                Destroy(minimapCamera.gameObject);
                minimapCamera = null;
            }
            
            // 销毁主摄像机视野范围UI
            if (mainCameraViewUI != null)
            {
                Destroy(mainCameraViewUI);
                mainCameraViewUI = null;
            }
        }
        
        /// <summary>
        /// 创建小地图摄像机
        /// </summary>
        private void CreateMinimapCamera()
        {
            // 创建小地图摄像机对象
            GameObject minimapCameraObj = new GameObject("MinimapCamera");
            minimapCamera = minimapCameraObj.AddComponent<Camera>();
            minimapCameraObj.transform.parent = transform;
            
            // 设置摄像机属性
            minimapCamera.orthographic = true;
            
            // 计算地图中心点
            Vector3 mapCenter = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0);
            
            // 计算地图尺寸
            float mapWidth = maxX - minX;
            float mapHeight = maxY - minY;
            
            // 如果需要保持地图比例
            if (maintainAspectRatio)
            {
                // 设置摄像机尺寸，确保能够显示整个地图
                // 不再应用高度缩放系数，保持原始比例
                minimapCamera.orthographicSize = mapHeight / 2f;
            }
            else
            {
                // 如果不需要保持比例，则根据小地图UI的宽高比调整摄像机视野
                float minimapAspectRatio = actualMinimapSize.x / actualMinimapSize.y;
                
                // 计算合适的正交尺寸
                if (mapWidth / mapHeight > minimapAspectRatio)
                {
                    // 如果地图比例比小地图UI宽，则以宽度为基准
                    minimapCamera.orthographicSize = mapWidth / (2f * minimapAspectRatio);
                }
                else
                {
                    // 否则以高度为基准
                    minimapCamera.orthographicSize = mapHeight / 2f;
                }
            }
            
            minimapCamera.depth = 0;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = backgroundColor;
            
            // 设置小地图摄像机同时渲染Default和Minimap层
            int defaultLayer = LayerMask.NameToLayer("Default");
            int minimapLayer = LayerMask.NameToLayer("Minimap");
            
            // 检查Minimap层是否存在
            if (minimapLayer == -1)
            {
                Debug.LogWarning("未找到'Minimap'层! 请在项目设置中添加此层。目前只会渲染Default层。");
                // 只渲染Default层
                minimapCamera.cullingMask = 1 << defaultLayer;
            }
            else
            {
                // 同时渲染Default和Minimap层
                minimapCamera.cullingMask = (1 << defaultLayer) | (1 << minimapLayer);
                //Debug.Log($"小地图摄像机设置为同时渲染Default层(索引:{defaultLayer})和Minimap层(索引:{minimapLayer})");
            }
            
            // 设置摄像机位置 - 2D视角，位于地图中心
            minimapCamera.transform.position = new Vector3(mapCenter.x, mapCenter.y, -10);
            minimapCamera.transform.rotation = Quaternion.Euler(0, 0, 0); // 2D视角
            
            // 创建渲染纹理 - 确保分辨率与小地图尺寸匹配
            minimapRenderTexture = new RenderTexture(
                (int)actualMinimapSize.x, 
                (int)actualMinimapSize.y, 
                16, 
                RenderTextureFormat.ARGB32
            );
            minimapRenderTexture.Create();
            
            // 设置摄像机渲染到纹理
            minimapCamera.targetTexture = minimapRenderTexture;
            
            // 确保摄像机不会被剔除
            minimapCamera.enabled = true;
            
            //Debug.Log($"小地图摄像机设置完成：位置={mapCenter}, 尺寸={minimapCamera.orthographicSize}");
        }
        
        /// <summary>
        /// 创建小地图UI
        /// </summary>
        private void CreateMinimapUI()
        {
            // 确保小地图面板存在
            if (minimapPanel == null)
            {
                SetupMinimapPanel();
            }
            
            // 创建小地图UI容器
            minimapUI = new GameObject("MinimapUI");
            minimapUI.transform.SetParent(minimapPanel.transform, false);
            
            // 添加RectTransform组件，使用计算后的实际尺寸
            minimapRectTransform = minimapUI.AddComponent<RectTransform>();
            minimapRectTransform.sizeDelta = actualMinimapSize;
            
            // 设置位置到右下角
            minimapRectTransform.anchorMin = new Vector2(1, 0);
            minimapRectTransform.anchorMax = new Vector2(1, 0);
            minimapRectTransform.pivot = new Vector2(1, 0);
            minimapRectTransform.anchoredPosition = new Vector2(-minimapPosition.x, minimapPosition.y);
            
            // 添加发光效果（可选）
            if (enableGlowEffect)
            {
                GameObject glow = new GameObject("Glow");
                glow.transform.SetParent(minimapUI.transform, false);
                RectTransform glowRect = glow.AddComponent<RectTransform>();
                glowRect.anchorMin = Vector2.zero;
                glowRect.anchorMax = Vector2.one;
                glowRect.sizeDelta = new Vector2(glowWidth * 2, glowWidth * 2);
                glowRect.anchoredPosition = Vector2.zero;
                UnityEngine.UI.Image glowImage = glow.AddComponent<UnityEngine.UI.Image>();
                glowImage.color = glowColor;
                // 设置发光为最底层
                glow.transform.SetAsFirstSibling();
            }
            
            // 添加边框图像
            GameObject border = new GameObject("Border");
            border.transform.SetParent(minimapUI.transform, false);
            RectTransform borderRect = border.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            UnityEngine.UI.Image borderImage = border.AddComponent<UnityEngine.UI.Image>();
            borderImage.color = minimapBorderColor;
            
            // 创建内部边框（用于创建边框宽度效果）
            GameObject innerBorder = new GameObject("InnerBorder");
            innerBorder.transform.SetParent(border.transform, false);
            RectTransform innerBorderRect = innerBorder.AddComponent<RectTransform>();
            innerBorderRect.anchorMin = Vector2.zero;
            innerBorderRect.anchorMax = Vector2.one;
            innerBorderRect.sizeDelta = new Vector2(-minimapBorderWidth * 2, -minimapBorderWidth * 2);
            innerBorderRect.anchoredPosition = Vector2.zero;
            UnityEngine.UI.Image innerBorderImage = innerBorder.AddComponent<UnityEngine.UI.Image>();
            innerBorderImage.color = backgroundColor; // 使用背景色填充内部
            
            // 添加小地图图像
            GameObject imageObj = new GameObject("MinimapImage");
            imageObj.transform.SetParent(innerBorder.transform, false);
            RectTransform imageRect = imageObj.AddComponent<RectTransform>();
            
            // 完全填充内部边框
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.sizeDelta = Vector2.zero;
            
            minimapImage = imageObj.AddComponent<UnityEngine.UI.RawImage>();
            minimapImage.texture = minimapRenderTexture;
            minimapImage.raycastTarget = true; // 确保可以接收点击事件
            
            // 在下一帧计算小地图屏幕区域
            StartCoroutine(DelayedScreenRectCalculation());
            
            //Debug.Log("小地图UI创建完成");
        }
        
        /// <summary>
        /// 延迟计算小地图屏幕区域，确保UI布局已经完成
        /// </summary>
        private System.Collections.IEnumerator DelayedScreenRectCalculation()
        {
            // 等待两帧，确保UI已经完全布局
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            
            // 计算小地图屏幕区域
            CalculateMinimapScreenRect();
        }
        
        /// <summary>
        /// 处理小地图点击
        /// </summary>
        private void HandleMinimapClick(Vector2 mousePosition)
        {
            if (mainCamera == null || minimapCamera == null)
            {
                //Debug.Log("点击跳转被阻止：" + 
                //    (mainCamera == null ? "主摄像机为空" : "") + 
                //    (minimapCamera == null ? "小地图摄像机为空" : ""));
                return;
            }
            
            // 检查CameraController的状态，只有在非锁定模式下才允许点击跳转
            CameraController cameraController = mainCamera.GetComponent<CameraController>();
            if (cameraController != null && cameraController.IsFollowingHero())
            {
                // 如果摄像机正在跟随英雄（锁定模式），则不执行跳转
                //Debug.Log("小地图点击: 摄像机处于锁定模式，点击跳转被禁用");
                return;
            }
            
            // 计算点击位置在小地图上的归一化坐标 (0-1)
            // 将屏幕坐标转换为小地图上的相对位置
            float relX = (mousePosition.x - minimapScreenRect.x) / minimapScreenRect.width;
            float relY = (mousePosition.y - minimapScreenRect.y) / minimapScreenRect.height;
            
            //Debug.Log($"点击相对位置: x={relX}, y={relY}");
            
            // 将归一化坐标转换为世界坐标
            Vector3 worldPos = MinimapToWorldPoint(new Vector2(relX, relY));
            
            // 应用边界限制，防止摄像机移动到超出合理范围的位置
            Vector3 clampedWorldPos = ApplyBoundaryLimits(worldPos);
            
            // 设置主摄像机目标位置
            targetPosition = new Vector3(clampedWorldPos.x, clampedWorldPos.y, mainCamera.transform.position.z);
            isTransitioning = true;
            
            // 获取摄像机当前位置
            Vector3 cameraCurrentPos = mainCamera.transform.position;
            
            //Debug.Log($"小地图点击跳转: 归一化位置=({relX}, {relY}), 世界位置={worldPos}, 限制后位置={clampedWorldPos}, 摄像机当前位置={cameraCurrentPos}, 目标位置={targetPosition}");
        }
        
        /// <summary>
        /// 应用边界限制，防止摄像机移动到超出合理范围的位置
        /// </summary>
        private Vector3 ApplyBoundaryLimits(Vector3 position)
        {
            // 获取主摄像机的边界设置
            CameraController cameraController = mainCamera.GetComponent<CameraController>();
            if (cameraController == null)
            {
                // 如果没有CameraController，使用小地图的边界作为默认值
                return new Vector3(
                    Mathf.Clamp(position.x, minX, maxX),
                    Mathf.Clamp(position.y, minY, maxY),
                    position.z
                );
            }
            
            // 通过反射获取CameraController的边界设置
            var boundaryField = typeof(CameraController).GetField("minX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (boundaryField == null)
            {
                // 如果无法获取边界设置，使用小地图的边界
                return new Vector3(
                    Mathf.Clamp(position.x, minX, maxX),
                    Mathf.Clamp(position.y, minY, maxY),
                    position.z
                );
            }
            
            // 获取CameraController的边界值
            float camMinX = (float)typeof(CameraController).GetField("minX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(cameraController);
            float camMaxX = (float)typeof(CameraController).GetField("maxX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(cameraController);
            float camMinY = (float)typeof(CameraController).GetField("minY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(cameraController);
            float camMaxY = (float)typeof(CameraController).GetField("maxY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(cameraController);
            
            // 计算摄像机视野边界（与CameraController中的逻辑一致）
            float cameraHalfWidth = mainCamera.orthographicSize * mainCamera.aspect;
            float cameraHalfHeight = mainCamera.orthographicSize;
            
            // 限制X轴
            float clampedX = Mathf.Clamp(position.x, 
                camMinX + cameraHalfWidth, 
                camMaxX - cameraHalfWidth);
            
            // 限制Y轴
            float clampedY = Mathf.Clamp(position.y, 
                camMinY + cameraHalfHeight, 
                camMaxY - cameraHalfHeight);
            
            // 构建限制后的位置（保持Z轴不变）
            return new Vector3(clampedX, clampedY, position.z);
        }
        
        /// <summary>
        /// 将小地图上的归一化坐标(0-1)转换为世界坐标
        /// </summary>
        private Vector3 MinimapToWorldPoint(Vector2 normalizedPos)
        {
            // 计算世界坐标
            float worldX = Mathf.Lerp(minX, maxX, normalizedPos.x);
            float worldY = Mathf.Lerp(minY, maxY, normalizedPos.y);
            
            return new Vector3(worldX, worldY, 0);
        }
        
        /// <summary>
        /// 创建主摄像机视野范围UI
        /// </summary>
        private void CreateMainCameraViewUI()
        {
            if (minimapUI == null || mainCamera == null) return;
            
            // 创建主摄像机视野范围UI对象
            mainCameraViewUI = new GameObject("MainCameraViewUI");
            mainCameraViewUI.transform.SetParent(minimapUI.transform, false);
            
            // 添加RectTransform组件
            RectTransform rectTransform = mainCameraViewUI.AddComponent<RectTransform>();
            // 修复：设置锚点为(0,0)，覆盖整个小地图区域
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = Vector2.zero;
            rectTransform.sizeDelta = actualMinimapSize;
            rectTransform.anchoredPosition = Vector2.zero;
            
            // 添加图像组件，用于绘制视野范围
            Image viewImage = mainCameraViewUI.AddComponent<Image>();
            viewImage.color = Color.clear; // 透明背景
            
            // 创建线条渲染器用于绘制视野边框
            GameObject lineObj = new GameObject("ViewLineRenderer");
            lineObj.transform.SetParent(mainCameraViewUI.transform, false);
            
            // 添加必要的CanvasRenderer组件
            lineObj.AddComponent<CanvasRenderer>();
            
            // 设置RectTransform
            RectTransform lineRectTransform = lineObj.AddComponent<RectTransform>();
            lineRectTransform.anchorMin = Vector2.zero;
            lineRectTransform.anchorMax = Vector2.zero;
            lineRectTransform.pivot = Vector2.zero;
            lineRectTransform.sizeDelta = actualMinimapSize;
            lineRectTransform.anchoredPosition = Vector2.zero;
            
            // 使用UI线条绘制视野范围
            UILineRenderer lineRenderer = lineObj.AddComponent<UILineRenderer>();
            lineRenderer.color = mainCameraViewColor;
            lineRenderer.lineWidth = mainCameraViewLineWidth;
            lineRenderer.LineList = false; // 使用连续线条模式
            lineRenderer.raycastTarget = false; // 禁用射线检测以避免UI交互问题
            
            // 初始化线条点
            lineRenderer.Points = new Vector2[5]; // 5个点形成一个闭合矩形（最后一个点回到起点）
            
            // 保存引用
            mainCameraViewLineRenderer = lineRenderer;
            
            // 立即更新一次视野范围
            UpdateMainCameraViewOnMinimap();
        }
        
        /// <summary>
        /// 更新主摄像机视野范围在小地图上的显示（性能优化版本）
        /// </summary>
        private void UpdateMainCameraViewOnMinimapOptimized()
        {
            if (mainCameraViewUI == null || mainCamera == null || minimapCamera == null || 
                mainCameraViewLineRenderer == null) return;
            
            // 性能优化：检查是否需要更新
            if (enablePerformanceOptimization)
            {
                // 检查时间间隔
                if (Time.time - lastViewUpdateTime < viewUpdateInterval)
                {
                    return;
                }
                
                // 检查摄像机状态是否发生变化
                Vector3 currentCameraPos = mainCamera.transform.position;
                float currentOrthographicSize = mainCamera.orthographicSize;
                float currentAspect = mainCamera.aspect;
                
                if (lastMainCameraPosition == currentCameraPos && 
                    lastMainCameraOrthographicSize == currentOrthographicSize && 
                    lastMainCameraAspect == currentAspect)
                {
                    return; // 摄像机状态没有变化，不需要更新
                }
                
                // 更新记录的状态
                lastMainCameraPosition = currentCameraPos;
                lastMainCameraOrthographicSize = currentOrthographicSize;
                lastMainCameraAspect = currentAspect;
                lastViewUpdateTime = Time.time;
            }
            
            // 调用原始的更新方法
            UpdateMainCameraViewOnMinimap();
        }
        
        /// <summary>
        /// 更新主摄像机视野范围在小地图上的显示
        /// </summary>
        private void UpdateMainCameraViewOnMinimap()
        {
            if (mainCameraViewUI == null || mainCamera == null || minimapCamera == null || 
                mainCameraViewLineRenderer == null) return;
            
            // 获取主摄像机在世界中的四个角点坐标
            Vector3 camPos = mainCamera.transform.position;
            float cameraHeight = mainCamera.orthographicSize * 2; // 完整高度
            float cameraWidth = cameraHeight * mainCamera.aspect; // 宽度 = 高度 * 宽高比
            
            Vector3 bottomLeft = new Vector3(camPos.x - cameraWidth/2, camPos.y - cameraHeight/2, camPos.z);
            Vector3 bottomRight = new Vector3(camPos.x + cameraWidth/2, camPos.y - cameraHeight/2, camPos.z);
            Vector3 topRight = new Vector3(camPos.x + cameraWidth/2, camPos.y + cameraHeight/2, camPos.z);
            Vector3 topLeft = new Vector3(camPos.x - cameraWidth/2, camPos.y + cameraHeight/2, camPos.z);
            
            // 计算归一化坐标（0-1范围）
            float blX = Mathf.InverseLerp(minX, maxX, bottomLeft.x);
            float blY = Mathf.InverseLerp(minY, maxY, bottomLeft.y);
            float brX = Mathf.InverseLerp(minX, maxX, bottomRight.x);
            float brY = Mathf.InverseLerp(minY, maxY, bottomRight.y);
            float trX = Mathf.InverseLerp(minX, maxX, topRight.x);
            float trY = Mathf.InverseLerp(minY, maxY, topRight.y);
            float tlX = Mathf.InverseLerp(minX, maxX, topLeft.x);
            float tlY = Mathf.InverseLerp(minY, maxY, topLeft.y);
            
            // 确保归一化坐标在0-1范围内
            blX = Mathf.Clamp01(blX); blY = Mathf.Clamp01(blY);
            brX = Mathf.Clamp01(brX); brY = Mathf.Clamp01(brY);
            trX = Mathf.Clamp01(trX); trY = Mathf.Clamp01(trY);
            tlX = Mathf.Clamp01(tlX); tlY = Mathf.Clamp01(tlY);
            
            // 计算视野范围的四个角点（UI坐标）
            Vector2[] uiPoints = new Vector2[5];
            
            // 修复：移除X轴翻转，使用正常坐标映射
            // 由于锚点已经统一为右下角，现在使用正常的坐标转换
            uiPoints[0] = new Vector2(blX * actualMinimapSize.x, blY * actualMinimapSize.y); // 左下
            uiPoints[1] = new Vector2(brX * actualMinimapSize.x, brY * actualMinimapSize.y); // 右下
            uiPoints[2] = new Vector2(trX * actualMinimapSize.x, trY * actualMinimapSize.y); // 右上
            uiPoints[3] = new Vector2(tlX * actualMinimapSize.x, tlY * actualMinimapSize.y); // 左上
            uiPoints[4] = new Vector2(blX * actualMinimapSize.x, blY * actualMinimapSize.y); // 闭合线条
            
            // 输出调试信息
            //Debug.Log($"主摄像机位置: {camPos}, 尺寸: {mainCamera.orthographicSize}, 宽高比: {mainCamera.aspect}");
            //Debug.Log($"视野范围: 宽={cameraWidth}(世界), 高={cameraHeight}(世界)");
            //Debug.Log($"视野角点(世界): BL={bottomLeft}, BR={bottomRight}, TR={topRight}, TL={topLeft}");
            //Debug.Log($"归一化坐标: BL=({blX}, {blY}), BR=({brX}, {brY}), TR=({trX}, {trY}), TL=({tlX}, {tlY})");
            //Debug.Log($"UI坐标: BL={uiPoints[0]}, BR={uiPoints[1]}, TR={uiPoints[2]}, TL={uiPoints[3]}");
            //Debug.Log($"小地图尺寸: {actualMinimapSize}");
            
            // 更新线条渲染器
            if (mainCameraViewLineRenderer != null)
            {
                mainCameraViewLineRenderer.Points = uiPoints;
                mainCameraViewLineRenderer.SetVerticesDirty(); // 标记需要重新绘制
            }
        }
        
        /// <summary>
        /// 将世界坐标转换为小地图UI上的坐标
        /// </summary>
        private Vector2 WorldToMinimapUIPoint(Vector3 worldPoint)
        {
            // 计算世界坐标相对于地图边界的归一化位置
            float normalizedX = Mathf.InverseLerp(minX, maxX, worldPoint.x);
            float normalizedY = Mathf.InverseLerp(minY, maxY, worldPoint.y);
            
            // 确保值在0-1范围内
            normalizedX = Mathf.Clamp01(normalizedX);
            normalizedY = Mathf.Clamp01(normalizedY);
            
            // 转换为小地图UI坐标
            float x = normalizedX * actualMinimapSize.x;
            float y = normalizedY * actualMinimapSize.y;
            
            return new Vector2(x, y);
        }
        
        #region 公共设置方法
        
        /// <summary>
        /// 设置小地图尺寸
        /// </summary>
        public void SetMinimapSize(Vector2 size)
        {
            this.minimapSize = size;
        }
        
        /// <summary>
        /// 设置小地图位置
        /// </summary>
        public void SetMinimapPosition(Vector2 position)
        {
            this.minimapPosition = position;
        }
        
        /// <summary>
        /// 设置边框颜色
        /// </summary>
        public void SetBorderColor(Color color)
        {
            this.minimapBorderColor = color;
        }
        
        /// <summary>
        /// 设置边框宽度
        /// </summary>
        public void SetBorderWidth(float width)
        {
            this.minimapBorderWidth = Mathf.Max(0, width);
        }
        
        /// <summary>
        /// 设置是否启用发光效果
        /// </summary>
        public void SetEnableGlowEffect(bool enable)
        {
            this.enableGlowEffect = enable;
        }
        
        /// <summary>
        /// 设置发光颜色
        /// </summary>
        public void SetGlowColor(Color color)
        {
            this.glowColor = color;
        }
        
        /// <summary>
        /// 设置发光宽度
        /// </summary>
        public void SetGlowWidth(float width)
        {
            this.glowWidth = Mathf.Max(0, width);
        }
        
        /// <summary>
        /// 设置背景颜色
        /// </summary>
        public void SetBackgroundColor(Color color)
        {
            this.backgroundColor = color;
        }
        
        /// <summary>
        /// 设置地图边界
        /// </summary>
        public void SetMapBoundaries(float minX, float maxX, float minY, float maxY)
        {
            this.minX = minX;
            this.maxX = maxX;
            this.minY = minY;
            this.maxY = maxY;
        }
        
        /// <summary>
        /// 设置是否自动调整高度
        /// </summary>
        public void SetAutoAdjustHeight(bool autoAdjust)
        {
            this.autoAdjustHeight = autoAdjust;
        }
        
        /// <summary>
        /// 设置是否保持地图比例
        /// </summary>
        public void SetMaintainAspectRatio(bool maintain)
        {
            this.maintainAspectRatio = maintain;
        }
        
        /// <summary>
        /// 设置高度缩放系数
        /// </summary>
        public void SetHeightScaleFactor(float factor)
        {
            this.heightScaleFactor = factor;
        }
        
        /// <summary>
        /// 设置是否显示主摄像机视野范围
        /// </summary>
        public void SetShowMainCameraView(bool show)
        {
            this.showMainCameraView = show;
            
            // 如果已经创建了小地图UI，则根据设置显示或隐藏视野范围
            if (minimapUI != null)
            {
                if (show && mainCameraViewUI == null)
                {
                    CreateMainCameraViewUI();
                }
                else if (!show && mainCameraViewUI != null)
                {
                    Destroy(mainCameraViewUI);
                    mainCameraViewUI = null;
                }
            }
        }
        
        /// <summary>
        /// 设置主摄像机视野范围颜色
        /// </summary>
        public void SetMainCameraViewColor(Color color)
        {
            this.mainCameraViewColor = color;
            
            // 更新线条渲染器颜色
            if (mainCameraViewLineRenderer != null)
            {
                mainCameraViewLineRenderer.color = color;
                mainCameraViewLineRenderer.SetVerticesDirty();
            }
        }
        
        /// <summary>
        /// 设置主摄像机视野范围线宽
        /// </summary>
        public void SetMainCameraViewLineWidth(float width)
        {
            this.mainCameraViewLineWidth = width;
            
            // 更新线条渲染器线宽
            if (mainCameraViewLineRenderer != null)
            {
                mainCameraViewLineRenderer.lineWidth = width;
                mainCameraViewLineRenderer.SetVerticesDirty();
            }
        }
        
        /// <summary>
        /// 设置是否启用点击小地图跳转功能
        /// </summary>
        public void SetEnableClickToMove(bool enable)
        {
            this.enableClickToMove = enable;
            
            // 如果已经创建了小地图UI，则需要重新创建以应用新设置
            if (minimapUI != null)
            {
                InitializeMinimapSystem();
            }
        }
        
        /// <summary>
        /// 设置是否锁定摄像机移动
        /// </summary>
        public void SetLockCameraMovement(bool locked)
        {
            this.lockCameraMovement = locked;
            
            // 如果锁定，则取消任何正在进行的过渡
            if (locked)
            {
                isTransitioning = false;
                targetPosition = null;
            }
        }
        
        /// <summary>
        /// 设置点击跳转速度
        /// </summary>
        /// <param name="speed">速度值，0表示立即跳转</param>
        public void SetClickTransitionSpeed(float speed)
        {
            this.clickTransitionSpeed = Mathf.Max(0, speed);
        }
        
        /// <summary>
        /// 设置切换小地图的按键
        /// </summary>
        public void SetToggleMinimapKey(KeyCode key)
        {
            this.toggleMinimapKey = key;
        }
        
        /// <summary>
        /// 设置扩大倍数
        /// </summary>
        public void SetExpansionMultiplier(float multiplier)
        {
            this.expansionMultiplier = multiplier;
        }
        
        /// <summary>
        /// 设置切换动画持续时间
        /// </summary>
        public void SetToggleAnimationDuration(float duration)
        {
            this.toggleAnimationDuration = Mathf.Max(0, duration);
        }
        
        /// <summary>
        /// 获取当前小地图是否处于扩大状态
        /// </summary>
        public bool IsMinimapExpanded()
        {
            return isMinimapExpanded;
        }
        
        /// <summary>
        /// 切换小地图状态（公共方法，供其他脚本调用）
        /// </summary>
        public void ToggleMinimap()
        {
            ToggleMinimapSize();
        }
        
        /// <summary>
        /// 切换小地图大小和位置
        /// </summary>
        private void ToggleMinimapSize()
        {
            if (minimapRectTransform == null) return;
            
            isMinimapExpanded = !isMinimapExpanded;
            
            if (isMinimapExpanded)
            {
                // 切换到扩大状态（屏幕中央）
                Vector2 expandedSize = actualMinimapSize * expansionMultiplier;
                StartCoroutine(AnimateMinimapTransition(expandedSize, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)));
                //Debug.Log($"小地图切换到扩大状态（屏幕中央）: {actualMinimapSize} -> {expandedSize}");
            }
            else
            {
                // 切换到正常状态（右下角）
                StartCoroutine(AnimateMinimapTransition(actualMinimapSize, new Vector2(-minimapPosition.x, minimapPosition.y), new Vector2(1, 0), new Vector2(1, 0)));
                //Debug.Log($"小地图切换到正常状态（右下角）: {actualMinimapSize}");
            }
            
            // 立即更新主摄像机视野范围
            if (showMainCameraView && mainCamera != null)
            {
                UpdateMainCameraViewOnMinimap();
            }
        }
        
        /// <summary>
        /// 动画过渡小地图大小和位置
        /// </summary>
        private IEnumerator AnimateMinimapTransition(Vector2 targetSize, Vector2 targetPosition, Vector2 targetAnchor, Vector2 targetPivot)
        {
            if (minimapRectTransform == null) yield break;
            
            // 记录初始状态
            Vector2 startSize = minimapRectTransform.sizeDelta;
            Vector2 startPosition = minimapRectTransform.anchoredPosition;
            Vector2 startAnchor = minimapRectTransform.anchorMin;
            Vector2 startPivot = minimapRectTransform.pivot;
            
            float elapsedTime = 0f;
            
            while (elapsedTime < toggleAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / toggleAnimationDuration;
                
                // 使用平滑插值
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                
                // 插值大小
                minimapRectTransform.sizeDelta = Vector2.Lerp(startSize, targetSize, smoothT);
                
                // 插值位置
                minimapRectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothT);
                
                // 插值锚点
                minimapRectTransform.anchorMin = Vector2.Lerp(startAnchor, targetAnchor, smoothT);
                minimapRectTransform.anchorMax = Vector2.Lerp(startAnchor, targetAnchor, smoothT);
                
                // 插值pivot
                minimapRectTransform.pivot = Vector2.Lerp(startPivot, targetPivot, smoothT);
                
                // 更新实际尺寸（用于视野框计算）
                actualMinimapSize = minimapRectTransform.sizeDelta;
                
                // 重新计算小地图屏幕区域
                minimapRectCalculated = false;
                CalculateMinimapScreenRect();
                
                // 每帧更新主摄像机视野范围
                if (showMainCameraView && mainCamera != null)
                {
                    UpdateMainCameraViewOnMinimap();
                }
                
                yield return null;
            }
            
            // 确保最终状态精确
            minimapRectTransform.sizeDelta = targetSize;
            minimapRectTransform.anchoredPosition = targetPosition;
            minimapRectTransform.anchorMin = targetAnchor;
            minimapRectTransform.anchorMax = targetAnchor;
            minimapRectTransform.pivot = targetPivot;
            
            // 更新实际尺寸
            actualMinimapSize = targetSize;
            
            // 如果缩回到正常状态，重新计算原始尺寸
            if (!isMinimapExpanded)
            {
                CalculateMinimapSize();
                minimapRectTransform.sizeDelta = actualMinimapSize;
            }
            
            // 重新计算小地图屏幕区域
            minimapRectCalculated = false;
            CalculateMinimapScreenRect();
            
            // 最后再次更新主摄像机视野范围
            if (showMainCameraView && mainCamera != null)
            {
                UpdateMainCameraViewOnMinimap();
            }
            
            //Debug.Log($"小地图动画完成: 尺寸={targetSize}, 位置={targetPosition}, 锚点={targetAnchor}, pivot={targetPivot}");
        }
        
        /// <summary>
        /// 立即设置小地图状态（无动画）
        /// </summary>
        public void SetMinimapExpanded(bool expanded)
        {
            if (minimapRectTransform == null) return;
            
            isMinimapExpanded = expanded;
            
            if (expanded)
            {
                // 立即设置为扩大状态
                Vector2 expandedSize = actualMinimapSize * expansionMultiplier;
                minimapRectTransform.sizeDelta = expandedSize;
                minimapRectTransform.anchoredPosition = Vector2.zero;
                minimapRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                minimapRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                minimapRectTransform.pivot = new Vector2(0.5f, 0.5f);
                actualMinimapSize = expandedSize;
            }
            else
            {
                // 立即设置为正常状态
                // 重新计算原始尺寸
                CalculateMinimapSize();
                minimapRectTransform.sizeDelta = actualMinimapSize;
                minimapRectTransform.anchoredPosition = new Vector2(-minimapPosition.x, minimapPosition.y);
                minimapRectTransform.anchorMin = new Vector2(1, 0);
                minimapRectTransform.anchorMax = new Vector2(1, 0);
                minimapRectTransform.pivot = new Vector2(1, 0);
            }
            
            // 重新计算小地图屏幕区域
            minimapRectCalculated = false;
            CalculateMinimapScreenRect();
            
            // 立即更新主摄像机视野范围
            if (showMainCameraView && mainCamera != null)
            {
                UpdateMainCameraViewOnMinimap();
            }
        }
        
        /// <summary>
        /// 设置是否启用性能优化
        /// </summary>
        public void SetEnablePerformanceOptimization(bool enable)
        {
            this.enablePerformanceOptimization = enable;
        }
        
        /// <summary>
        /// 设置视野框更新间隔
        /// </summary>
        public void SetViewUpdateInterval(float interval)
        {
            this.viewUpdateInterval = Mathf.Max(0.01f, interval);
        }
        
        /// <summary>
        /// 设置是否只在游戏进行时显示小地图
        /// </summary>
        public void SetOnlyShowDuringGameplay(bool onlyShowInGame)
        {
            this.onlyShowDuringGameplay = onlyShowInGame;
            
            // 立即应用设置
            if (GameManager.Instance != null)
            {
                CheckGameState(GameManager.Instance.CurrentState);
            }
        }
        
        /// <summary>
        /// 设置小地图面板名称
        /// </summary>
        public void SetMinimapPanelName(string panelName)
        {
            this.minimapPanelName = panelName;
            
            // 如果已经创建了小地图面板，且名称不同，则重新创建
            if (minimapPanel != null && minimapPanel.name != minimapPanelName)
            {
                // 保存旧面板中的内容
                Transform[] children = new Transform[minimapPanel.transform.childCount];
                for (int i = 0; i < minimapPanel.transform.childCount; i++)
                {
                    children[i] = minimapPanel.transform.GetChild(i);
                }
                
                // 创建新面板
                GameObject oldPanel = minimapPanel;
                SetupMinimapPanel();
                
                // 将内容移到新面板
                foreach (Transform child in children)
                {
                    child.SetParent(minimapPanel.transform, false);
                }
                
                // 销毁旧面板
                Destroy(oldPanel);
            }
        }
        
        /// <summary>
        /// 设置游戏面板引用
        /// </summary>
        public void SetGamePanel(Transform panel)
        {
            this.gamePanel = panel;
            
            // 如果已经创建了小地图面板，且父对象不同，则重新创建
            if (minimapPanel != null && minimapPanel.transform.parent != gamePanel)
            {
                // 保存旧面板中的内容
                Transform[] children = new Transform[minimapPanel.transform.childCount];
                for (int i = 0; i < minimapPanel.transform.childCount; i++)
                {
                    children[i] = minimapPanel.transform.GetChild(i);
                }
                
                // 创建新面板
                GameObject oldPanel = minimapPanel;
                SetupMinimapPanel();
                
                // 将内容移到新面板
                foreach (Transform child in children)
                {
                    child.SetParent(minimapPanel.transform, false);
                }
                
                // 销毁旧面板
                Destroy(oldPanel);
            }
        }
        
        #endregion
        
        private void OnDestroy()
        {
            // 清理资源
            CleanupResources();
        }
    }

    /// <summary>
    /// UI线条渲染器 - 用于在UI上绘制线条
    /// </summary>
    public class UILineRenderer : Graphic
    {
        public float lineWidth = 2;
        public bool LineList = false;
        
        [SerializeField]
        private Vector2[] points;
        
        public Vector2[] Points
        {
            get { return points; }
            set
            {
                points = value;
                SetVerticesDirty();
            }
        }
        
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            
            if (points == null || points.Length < 2)
                return;
            
            for (int i = 0; i < points.Length - 1; i++)
            {
                if (LineList && i % 2 == 1)
                    continue;
                    
                Vector2 p1 = points[i];
                Vector2 p2 = points[i + 1];
                
                Vector2 dir = (p2 - p1).normalized;
                Vector2 perpendicular = new Vector2(-dir.y, dir.x) * lineWidth / 2;
                
                UIVertex[] vertices = new UIVertex[4];
                vertices[0].position = p1 + perpendicular;
                vertices[1].position = p1 - perpendicular;
                vertices[2].position = p2 + perpendicular;
                vertices[3].position = p2 - perpendicular;
                
                vertices[0].color = color;
                vertices[1].color = color;
                vertices[2].color = color;
                vertices[3].color = color;
                
                vertices[0].uv0 = new Vector2(0, 0);
                vertices[1].uv0 = new Vector2(0, 1);
                vertices[2].uv0 = new Vector2(1, 0);
                vertices[3].uv0 = new Vector2(1, 1);
                
                vh.AddUIVertexQuad(vertices);
            }
        }
    }
} 