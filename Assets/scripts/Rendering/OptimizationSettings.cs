using UnityEngine;

namespace MagicBattle
{
    /// <summary>
    /// 渲染优化配置设置
    /// </summary>
    [CreateAssetMenu(fileName = "RenderingOptimizationSettings", menuName = "Game/Rendering Optimization Settings")]
    public class OptimizationSettings : ScriptableObject
    {
        [Header("基础设置")]
        [Tooltip("是否启用渲染优化")]
        public bool enableOptimization = true;
        
        [Tooltip("视野检测更新间隔（秒）")]
        [Range(0.05f, 0.5f)]
        public float updateInterval = 0.1f;
        
        [Tooltip("视野边界扩展距离")]
        [Range(1f, 10f)]
        public float cullingMargin = 3f;
        
        [Tooltip("预测边界扩展距离")]
        [Range(1f, 8f)]
        public float predictionMargin = 2f;
        
        [Header("性能设置")]
        [Tooltip("每帧最大处理对象数")]
        [Range(1, 20)]
        public int maxObjectsPerFrame = 5;
        
        [Tooltip("启用渐进式过渡")]
        public bool enableGradualTransition = true;
        
        [Tooltip("启用性能监控")]
        public bool enablePerformanceMonitoring = true;
        
        [Tooltip("目标帧率")]
        [Range(30, 120)]
        public float targetFrameRate = 60f;
        
        [Header("设备适配")]
        [Tooltip("自动根据设备性能调整")]
        public bool autoAdjustForDevice = true;
        
        [Tooltip("低端设备性能倍数")]
        [Range(0.1f, 1f)]
        public float lowEndDeviceMultiplier = 0.5f;
        
        [Tooltip("高端设备性能倍数")]
        [Range(1f, 3f)]
        public float highEndDeviceMultiplier = 1.5f;
        
        [Header("调试设置")]
        [Tooltip("显示调试信息")]
        public bool showDebugInfo = false;

        [Tooltip("绘制视野边界")]
        public bool drawGizmos = false;

        [Tooltip("性能日志间隔（秒）")]
        [Range(1f, 30f)]
        public float performanceLogInterval = 10f;
        
        /// <summary>
        /// 根据设备性能调整设置
        /// </summary>
        public void AdjustForDevice()
        {
            if (!autoAdjustForDevice) return;
            
            // 简单的设备性能检测
            int deviceLevel = GetDevicePerformanceLevel();
            
            switch (deviceLevel)
            {
                case 0: // 低端设备
                    updateInterval *= (1f / lowEndDeviceMultiplier);
                    maxObjectsPerFrame = Mathf.RoundToInt(maxObjectsPerFrame * lowEndDeviceMultiplier);
                    cullingMargin *= lowEndDeviceMultiplier;
                    break;
                    
                case 2: // 高端设备
                    updateInterval *= (1f / highEndDeviceMultiplier);
                    maxObjectsPerFrame = Mathf.RoundToInt(maxObjectsPerFrame * highEndDeviceMultiplier);
                    cullingMargin *= highEndDeviceMultiplier;
                    break;
                    
                default: // 中端设备，保持默认设置
                    break;
            }
        }
        
        /// <summary>
        /// 简单的设备性能等级检测
        /// </summary>
        /// <returns>0=低端, 1=中端, 2=高端</returns>
        private int GetDevicePerformanceLevel()
        {
            // 基于内存和处理器核心数的简单判断
            int memoryMB = SystemInfo.systemMemorySize;
            int processorCount = SystemInfo.processorCount;
            
            if (memoryMB < 3000 || processorCount < 4)
            {
                return 0; // 低端
            }
            else if (memoryMB > 6000 && processorCount >= 8)
            {
                return 2; // 高端
            }
            else
            {
                return 1; // 中端
            }
        }
    }
}
