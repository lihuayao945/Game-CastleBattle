using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace MagicBattle
{
    /// <summary>
    /// 视野渲染优化器 - 主控制器
    /// </summary>
    public class ViewportRenderingOptimizer : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private OptimizationSettings settings;
        
        [Header("运行时设置")]
        [SerializeField] private bool enableOptimization = true;
        [SerializeField] private float updateInterval = 0.1f;
        [SerializeField] private float cullingMargin = 3f;
        [SerializeField] private float predictionMargin = 2f;
        [SerializeField] private int maxObjectsPerFrame = 5;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool drawGizmos = false;
        
        // 核心组件
        private Camera mainCamera;
        private RenderingStateManager stateManager;
        
        // 视野计算
        private Bounds currentViewportBounds;
        private Bounds predictionBounds;
        private Vector3 lastCameraPosition;
        private float lastUpdateTime;
        
        // 对象管理
        private List<Unit> registeredUnits = new List<Unit>();
        private Queue<Unit> unitsToProcess = new Queue<Unit>();
        private List<GameObject> registeredObjects = new List<GameObject>();
        private Queue<GameObject> objectsToProcess = new Queue<GameObject>();
        
        // 性能统计
        private float lastPerformanceLogTime;
        private int frameCount;
        private float totalFrameTime;
        
        // 单例模式
        private static ViewportRenderingOptimizer _instance;
        public static ViewportRenderingOptimizer Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ViewportRenderingOptimizer>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject("ViewportRenderingOptimizer");
                        _instance = obj.AddComponent<ViewportRenderingOptimizer>();
                        DontDestroyOnLoad(obj);
                    }
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
        
        private void Initialize()
        {
            // 获取主摄像头
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
            
            // 创建状态管理器
            stateManager = new RenderingStateManager(this);
            
            // 加载或应用设置
            ApplySettings();
            
            // 初始化性能监控
            lastPerformanceLogTime = Time.time;
            

        }
        
        private void Update()
        {
            if (!enableOptimization || mainCamera == null) return;
            
            // 检查是否需要更新
            if (Time.time - lastUpdateTime < updateInterval) return;
            
            // 更新视野边界
            UpdateViewportBounds();
            
            // 处理单位可见性
            ProcessUnitsVisibility();
            
            // 处理渐进式过渡队列
            stateManager.ProcessTransitionQueue(maxObjectsPerFrame);
            
            // 更新性能统计
            UpdatePerformanceStats();
            
            lastUpdateTime = Time.time;
        }
        
        /// <summary>
        /// 应用配置设置
        /// </summary>
        private void ApplySettings()
        {
            if (settings != null)
            {
                // 应用设备适配
                settings.AdjustForDevice();
                
                // 应用设置到运行时变量
                enableOptimization = settings.enableOptimization;
                updateInterval = settings.updateInterval;
                cullingMargin = settings.cullingMargin;
                predictionMargin = settings.predictionMargin;
                maxObjectsPerFrame = settings.maxObjectsPerFrame;
                showDebugInfo = settings.showDebugInfo;
                drawGizmos = settings.drawGizmos;
            }
        }
        
        /// <summary>
        /// 更新视野边界
        /// </summary>
        private void UpdateViewportBounds()
        {
            if (mainCamera == null) return;
            
            Vector3 camPos = mainCamera.transform.position;
            float height = mainCamera.orthographicSize * 2;
            float width = height * mainCamera.aspect;
            
            // 当前视野边界（用于剔除）
            currentViewportBounds = new Bounds(
                camPos,
                new Vector3(width + cullingMargin * 2, height + cullingMargin * 2, 100f)
            );
            
            // 预测边界（用于提前启用）
            predictionBounds = new Bounds(
                camPos,
                new Vector3(width + predictionMargin * 2, height + predictionMargin * 2, 100f)
            );
            
            lastCameraPosition = camPos;
        }
        
        /// <summary>
        /// 处理单位可见性
        /// </summary>
        private void ProcessUnitsVisibility()
        {
            // 清理无效的单位引用
            registeredUnits.RemoveAll(unit => unit == null || !unit.gameObject.activeInHierarchy);
            registeredObjects.RemoveAll(obj => obj == null || !obj.activeInHierarchy);

            // 重新填充处理队列
            if (unitsToProcess.Count == 0)
            {
                foreach (var unit in registeredUnits)
                {
                    if (unit != null && unit.gameObject.activeInHierarchy)
                    {
                        unitsToProcess.Enqueue(unit);
                    }
                }
            }

            if (objectsToProcess.Count == 0)
            {
                foreach (var obj in registeredObjects)
                {
                    if (obj != null && obj.activeInHierarchy)
                    {
                        objectsToProcess.Enqueue(obj);
                    }
                }
            }

            // 处理队列中的单位和对象
            int processedCount = 0;
            int halfFrame = maxObjectsPerFrame / 2;

            // 处理单位
            while (unitsToProcess.Count > 0 && processedCount < halfFrame)
            {
                Unit unit = unitsToProcess.Dequeue();
                if (unit != null && unit.gameObject.activeInHierarchy)
                {
                    RenderingStateManager.VisibilityState newState = CalculateVisibilityState(unit.gameObject);
                    stateManager.SetUnitVisibility(unit, newState, false);
                    processedCount++;
                }
            }

            // 处理GameObject
            while (objectsToProcess.Count > 0 && processedCount < maxObjectsPerFrame)
            {
                GameObject obj = objectsToProcess.Dequeue();
                if (obj != null && obj.activeInHierarchy)
                {
                    RenderingStateManager.VisibilityState newState = CalculateVisibilityState(obj);
                    stateManager.SetObjectVisibility(obj, newState, false);
                    processedCount++;
                }
            }
        }
        
        /// <summary>
        /// 计算GameObject的可见性状态
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>可见性状态</returns>
        private RenderingStateManager.VisibilityState CalculateVisibilityState(GameObject obj)
        {
            if (obj == null) return RenderingStateManager.VisibilityState.Hidden;

            // 英雄单位始终可见
            if (obj.CompareTag("Hero")) return RenderingStateManager.VisibilityState.Visible;

            Vector3 objPos = obj.transform.position;

            if (currentViewportBounds.Contains(objPos))
            {
                return RenderingStateManager.VisibilityState.Visible;
            }
            else if (predictionBounds.Contains(objPos))
            {
                return RenderingStateManager.VisibilityState.Predicted;
            }
            else
            {
                return RenderingStateManager.VisibilityState.Hidden;
            }
        }
        
        /// <summary>
        /// 注册单位到优化系统
        /// </summary>
        /// <param name="unit">要注册的单位</param>
        public void RegisterUnit(Unit unit)
        {
            if (unit == null || !unit.gameObject.activeInHierarchy || registeredUnits.Contains(unit)) return;

            // 检查stateManager是否已初始化
            if (stateManager == null)
            {
                Debug.LogWarning($"ViewportRenderingOptimizer: stateManager未初始化，跳过单位注册: {unit.name}");
                return;
            }

            registeredUnits.Add(unit);
            stateManager.RegisterUnit(unit);
        }

        /// <summary>
        /// 注册任意GameObject到优化系统（用于技能特效等）
        /// </summary>
        /// <param name="obj">要注册的游戏对象</param>
        public void RegisterGameObject(GameObject obj)
        {
            if (obj == null || !obj.activeInHierarchy || registeredObjects.Contains(obj)) return;

            registeredObjects.Add(obj);
            stateManager.RegisterGameObject(obj);
        }
        
        /// <summary>
        /// 从优化系统中注销单位
        /// </summary>
        /// <param name="unit">要注销的单位</param>
        public void UnregisterUnit(Unit unit)
        {
            if (unit == null) return;

            bool wasRegistered = registeredUnits.Remove(unit);
            if (wasRegistered && stateManager != null)
            {
                stateManager.UnregisterUnit(unit);
            }
        }

        /// <summary>
        /// 从优化系统中注销GameObject
        /// </summary>
        /// <param name="obj">要注销的游戏对象</param>
        public void UnregisterGameObject(GameObject obj)
        {
            if (obj == null) return;

            registeredObjects.Remove(obj);
            stateManager.UnregisterGameObject(obj);
        }
        
        /// <summary>
        /// 更新性能统计
        /// </summary>
        private void UpdatePerformanceStats()
        {
            frameCount++;
            totalFrameTime += Time.unscaledDeltaTime;
            
            // 定期输出性能日志
            if (showDebugInfo && settings != null && 
                Time.time - lastPerformanceLogTime >= settings.performanceLogInterval)
            {
                LogPerformanceStats();
                lastPerformanceLogTime = Time.time;
            }
        }
        
        /// <summary>
        /// 输出性能统计日志
        /// </summary>
        private void LogPerformanceStats()
        {
            float avgFrameTime = totalFrameTime / frameCount;
            float avgFPS = 1.0f / avgFrameTime;
            
            //Debug.Log($"=== 渲染优化性能统计 ===");
            //Debug.Log($"平均帧率: {avgFPS:F1} FPS");
            //Debug.Log($"总注册单位: {stateManager.TotalUnits}");
            //Debug.Log($"可见单位: {stateManager.VisibleUnits}");
            //Debug.Log($"预测单位: {stateManager.PredictedUnits}");
            //Debug.Log($"隐藏单位: {stateManager.HiddenUnits}");
            //Debug.Log($"优化率: {(float)stateManager.HiddenUnits / stateManager.TotalUnits * 100:F1}%");
            
            // 重置统计
            frameCount = 0;
            totalFrameTime = 0f;
        }

        /// <summary>
        /// 启用/禁用优化
        /// </summary>
        /// <param name="enabled">是否启用</param>
        public void SetOptimizationEnabled(bool enabled)
        {
            enableOptimization = enabled;

            if (!enabled)
            {
                // 禁用优化时，恢复所有单位为可见
                stateManager.ResetAllToVisible();
            }


        }

        /// <summary>
        /// 获取优化统计信息
        /// </summary>
        /// <returns>统计信息字符串</returns>
        public string GetOptimizationStats()
        {
            if (stateManager == null) return "优化系统未初始化";

            int totalAll = stateManager.TotalAll;
            int hiddenAll = stateManager.HiddenUnits + stateManager.HiddenObjects;
            float optimizationRate = totalAll > 0 ? (float)hiddenAll / totalAll * 100 : 0;

            return $"总对象: {totalAll} (单位:{stateManager.TotalUnits}, 特效:{stateManager.TotalObjects}), " +
                   $"可见: {stateManager.VisibleUnits + stateManager.VisibleObjects}, " +
                   $"隐藏: {hiddenAll}, " +
                   $"优化率: {optimizationRate:F1}%";
        }

        /// <summary>
        /// 验证渲染优化是否正常工作
        /// </summary>
        [ContextMenu("验证渲染优化")]
        public void VerifyOptimization()
        {
            if (!enableOptimization)
            {
                Debug.LogWarning("[验证] 渲染优化已禁用");
                return;
            }

            if (stateManager == null)
            {
                Debug.LogError("[验证] 状态管理器为空");
                return;
            }

            //Debug.Log($"[验证] 优化状态: {GetOptimizationStats()}");
            //Debug.Log($"[验证] 摄像头位置: {mainCamera?.transform.position}");
            //Debug.Log($"[验证] 视野边界: {currentViewportBounds}");
            //Debug.Log($"[验证] 更新间隔: {updateInterval}s");

            // 检查一些单位的实际状态
            int checkedCount = 0;
            foreach (var unit in registeredUnits)
            {
                if (unit != null && checkedCount < 5) // 只检查前5个
                {
                    var state = stateManager.GetUnitVisibility(unit);
                    var renderers = unit.GetComponentsInChildren<SpriteRenderer>();
                    bool actuallyVisible = renderers.Length > 0 && renderers[0].enabled;

                    //Debug.Log($"[验证] {unit.name}: 状态={state}, SpriteRenderer.enabled={actuallyVisible}");
                    checkedCount++;
                }
            }
        }

        /// <summary>
        /// 强制更新所有单位的可见性
        /// </summary>
        public void ForceUpdateAllUnits()
        {
            if (!enableOptimization) return;

            foreach (var unit in registeredUnits)
            {
                if (unit != null)
                {
                    RenderingStateManager.VisibilityState newState = CalculateVisibilityState(unit.gameObject);
                    stateManager.SetUnitVisibility(unit, newState, true);
                }
            }


        }

        /// <summary>
        /// 清理优化系统
        /// </summary>
        public void Cleanup()
        {
            if (stateManager != null)
            {
                stateManager.Clear();
            }

            registeredUnits.Clear();
            unitsToProcess.Clear();
            registeredObjects.Clear();
            objectsToProcess.Clear();


        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || mainCamera == null) return;

            // 绘制当前视野边界
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(currentViewportBounds.center, currentViewportBounds.size);

            // 绘制预测边界
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(predictionBounds.center, predictionBounds.size);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                Cleanup();
                _instance = null;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // 应用暂停时禁用优化，避免状态混乱
                SetOptimizationEnabled(false);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && settings != null && settings.enableOptimization)
            {
                // 应用重新获得焦点时恢复优化
                SetOptimizationEnabled(true);
            }
        }
    }
}
