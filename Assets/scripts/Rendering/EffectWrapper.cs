using UnityEngine;

namespace MagicBattle
{
    /// <summary>
    /// 特效包装器 - 为技能特效等非Unit对象提供渲染优化支持
    /// </summary>
    public class EffectWrapper : MonoBehaviour
    {
        private bool isRegisteredForOptimization = false;
        
        private void Start()
        {
            // 自动注册到渲染优化系统
            RegisterForRenderingOptimization();
        }
        
        private void OnEnable()
        {
            // 对象激活时注册
            if (Application.isPlaying)
            {
                RegisterForRenderingOptimization();
            }
        }
        
        private void OnDisable()
        {
            // 对象禁用时注销
            if (Application.isPlaying)
            {
                UnregisterFromRenderingOptimization();
            }
        }
        
        private void OnDestroy()
        {
            // 对象销毁时注销
            UnregisterFromRenderingOptimization();
        }
        
        /// <summary>
        /// 注册到渲染优化系统
        /// </summary>
        private void RegisterForRenderingOptimization()
        {
            if (isRegisteredForOptimization || !gameObject.activeInHierarchy) return;
            
            ViewportRenderingOptimizer optimizer = ViewportRenderingOptimizer.Instance;
            if (optimizer != null)
            {
                optimizer.RegisterGameObject(gameObject);
                isRegisteredForOptimization = true;
            }
        }
        
        /// <summary>
        /// 从渲染优化系统中注销
        /// </summary>
        private void UnregisterFromRenderingOptimization()
        {
            if (!isRegisteredForOptimization) return;
            
            ViewportRenderingOptimizer optimizer = ViewportRenderingOptimizer.Instance;
            if (optimizer != null)
            {
                optimizer.UnregisterGameObject(gameObject);
                isRegisteredForOptimization = false;
            }
        }
    }
}
