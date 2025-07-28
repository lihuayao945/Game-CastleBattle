using UnityEngine;
using UnityEngine.UI;

namespace MagicBattle
{
    /// <summary>
    /// 渲染优化测试器 - 用于测试和调试渲染优化功能
    /// </summary>
    public class RenderingOptimizationTester : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private Text statsText;
        [SerializeField] private Button toggleOptimizationButton;
        [SerializeField] private Button forceUpdateButton;
        [SerializeField] private Slider cullingMarginSlider;
        [SerializeField] private Slider updateIntervalSlider;
        
        [Header("测试设置")]
        [SerializeField] private bool showStatsInConsole = false;
        [SerializeField] private float statsUpdateInterval = 1f;
        
        private ViewportRenderingOptimizer optimizer;
        private float lastStatsUpdateTime;
        private bool isOptimizationEnabled = true;
        
        private void Start()
        {
            // 获取优化器实例
            optimizer = ViewportRenderingOptimizer.Instance;
            
            // 初始化UI
            InitializeUI();
            
            // 开始统计更新
            lastStatsUpdateTime = Time.time;
        }
        
        private void Update()
        {
            // 定期更新统计信息
            if (Time.time - lastStatsUpdateTime >= statsUpdateInterval)
            {
                UpdateStatsDisplay();
                lastStatsUpdateTime = Time.time;
            }
            
            // 键盘快捷键
            HandleKeyboardInput();
        }
        
        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            // 切换优化按钮
            if (toggleOptimizationButton != null)
            {
                toggleOptimizationButton.onClick.AddListener(ToggleOptimization);
                UpdateToggleButtonText();
            }
            
            // 强制更新按钮
            if (forceUpdateButton != null)
            {
                forceUpdateButton.onClick.AddListener(ForceUpdateAllUnits);
            }
            
            // 剔除边界滑块
            if (cullingMarginSlider != null)
            {
                cullingMarginSlider.minValue = 1f;
                cullingMarginSlider.maxValue = 10f;
                cullingMarginSlider.value = 3f;
                cullingMarginSlider.onValueChanged.AddListener(OnCullingMarginChanged);
            }
            
            // 更新间隔滑块
            if (updateIntervalSlider != null)
            {
                updateIntervalSlider.minValue = 0.05f;
                updateIntervalSlider.maxValue = 0.5f;
                updateIntervalSlider.value = 0.1f;
                updateIntervalSlider.onValueChanged.AddListener(OnUpdateIntervalChanged);
            }
        }
        
        /// <summary>
        /// 更新统计信息显示
        /// </summary>
        private void UpdateStatsDisplay()
        {
            if (optimizer == null) return;
            
            string stats = GetDetailedStats();
            
            // 更新UI文本
            if (statsText != null)
            {
                statsText.text = stats;
            }
            
            // 控制台输出
            if (showStatsInConsole)
            {
                Debug.Log($"[渲染优化统计] {stats}");
            }
        }
        
        /// <summary>
        /// 获取详细统计信息
        /// </summary>
        /// <returns>统计信息字符串</returns>
        private string GetDetailedStats()
        {
            if (optimizer == null) return "优化器未找到";
            
            string basicStats = optimizer.GetOptimizationStats();
            float currentFPS = 1.0f / Time.unscaledDeltaTime;
            
            return $"FPS: {currentFPS:F1}\n" +
                   $"{basicStats}\n" +
                   $"优化状态: {(isOptimizationEnabled ? "启用" : "禁用")}\n" +
                   $"摄像头位置: {Camera.main?.transform.position.ToString("F1") ?? "未知"}";
        }
        
        /// <summary>
        /// 切换优化开关
        /// </summary>
        private void ToggleOptimization()
        {
            if (optimizer == null) return;
            
            isOptimizationEnabled = !isOptimizationEnabled;
            optimizer.SetOptimizationEnabled(isOptimizationEnabled);
            
            UpdateToggleButtonText();
            
            //Debug.Log($"渲染优化已{(isOptimizationEnabled ? "启用" : "禁用")}");
        }
        
        /// <summary>
        /// 强制更新所有单位
        /// </summary>
        private void ForceUpdateAllUnits()
        {
            if (optimizer == null) return;
            
            optimizer.ForceUpdateAllUnits();
            Debug.Log("已强制更新所有单位的可见性");
        }
        
        /// <summary>
        /// 剔除边界变化回调
        /// </summary>
        /// <param name="value">新的边界值</param>
        private void OnCullingMarginChanged(float value)
        {
            // 这里需要访问优化器的设置，但当前设计中没有公开接口
            // 可以考虑在优化器中添加运行时设置修改接口
            Debug.Log($"剔除边界调整为: {value}");
        }
        
        /// <summary>
        /// 更新间隔变化回调
        /// </summary>
        /// <param name="value">新的间隔值</param>
        private void OnUpdateIntervalChanged(float value)
        {
            Debug.Log($"更新间隔调整为: {value}");
        }
        
        /// <summary>
        /// 更新切换按钮文本
        /// </summary>
        private void UpdateToggleButtonText()
        {
            if (toggleOptimizationButton != null)
            {
                Text buttonText = toggleOptimizationButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = isOptimizationEnabled ? "禁用优化" : "启用优化";
                }
            }
        }
        
        /// <summary>
        /// 处理键盘输入
        /// </summary>
        private void HandleKeyboardInput()
        {
            // 移除T/U/I快捷键，避免与其他功能冲突
            // 如需调试可通过Inspector手动操作
        }
        
        /// <summary>
        /// 创建简单的调试UI
        /// </summary>
        [ContextMenu("创建调试UI")]
        private void CreateDebugUI()
        {
            // 创建Canvas
            GameObject canvasObj = new GameObject("RenderingOptimizationDebugUI");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // 创建统计文本
            GameObject textObj = new GameObject("StatsText");
            textObj.transform.SetParent(canvasObj.transform);
            
            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 16;
            text.color = Color.white;
            text.text = "渲染优化统计";
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(0, 1);
            textRect.pivot = new Vector2(0, 1);
            textRect.anchoredPosition = new Vector2(10, -10);
            textRect.sizeDelta = new Vector2(400, 200);
            
            // 创建切换按钮
            GameObject buttonObj = new GameObject("ToggleButton");
            buttonObj.transform.SetParent(canvasObj.transform);
            
            Button button = buttonObj.AddComponent<Button>();
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform);
            
            Text buttonText = buttonTextObj.AddComponent<Text>();
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            buttonText.fontSize = 14;
            buttonText.color = Color.white;
            buttonText.text = "切换优化";
            buttonText.alignment = TextAnchor.MiddleCenter;
            
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0, 1);
            buttonRect.anchorMax = new Vector2(0, 1);
            buttonRect.pivot = new Vector2(0, 1);
            buttonRect.anchoredPosition = new Vector2(10, -220);
            buttonRect.sizeDelta = new Vector2(100, 30);
            
            RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = Vector2.zero;
            buttonTextRect.offsetMax = Vector2.zero;
            
            // 设置引用
            statsText = text;
            toggleOptimizationButton = button;
            
            Debug.Log("调试UI创建完成");
        }
    }
}
