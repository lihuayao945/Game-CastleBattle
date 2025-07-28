using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace MagicBattle
{
    /// <summary>
    /// 渲染状态管理器 - 负责管理单位的渲染状态
    /// </summary>
    public class RenderingStateManager
    {
        /// <summary>
        /// 可见性状态枚举
        /// </summary>
        public enum VisibilityState
        {
            Visible,        // 完全可见
            Predicted,      // 预测可见（即将进入视野）
            Hidden,         // 隐藏
            Transitioning   // 过渡中
        }
        
        // 单位状态管理
        private Dictionary<Unit, VisibilityState> unitStates = new Dictionary<Unit, VisibilityState>();
        private Dictionary<Unit, SpriteRenderer[]> unitRenderers = new Dictionary<Unit, SpriteRenderer[]>();
        private Dictionary<Unit, bool> unitOriginalStates = new Dictionary<Unit, bool>();

        // GameObject状态管理（用于技能特效等）
        private Dictionary<GameObject, VisibilityState> objectStates = new Dictionary<GameObject, VisibilityState>();
        private Dictionary<GameObject, SpriteRenderer[]> objectRenderers = new Dictionary<GameObject, SpriteRenderer[]>();
        
        // 渐进式处理队列
        private Queue<System.Action> transitionQueue = new Queue<System.Action>();
        private MonoBehaviour coroutineRunner;
        
        // 统计信息
        public int TotalUnits => unitStates.Count;
        public int TotalObjects => objectStates.Count;
        public int TotalAll => TotalUnits + TotalObjects;
        public int VisibleUnits { get; private set; }
        public int HiddenUnits { get; private set; }
        public int PredictedUnits { get; private set; }
        public int VisibleObjects { get; private set; }
        public int HiddenObjects { get; private set; }
        public int PredictedObjects { get; private set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="runner">用于运行协程的MonoBehaviour</param>
        public RenderingStateManager(MonoBehaviour runner)
        {
            coroutineRunner = runner;
        }
        
        /// <summary>
        /// 注册单位到渲染管理系统
        /// </summary>
        /// <param name="unit">要注册的单位</param>
        public void RegisterUnit(Unit unit)
        {
            if (unit == null || !unit.gameObject.activeInHierarchy || unitStates.ContainsKey(unit)) return;

            // 收集所有SpriteRenderer（包括子物体）
            SpriteRenderer[] allRenderers = unit.GetComponentsInChildren<SpriteRenderer>();

            // 过滤掉MinimapIcon的渲染器
            List<SpriteRenderer> gameRenderers = new List<SpriteRenderer>();
            foreach (var renderer in allRenderers)
            {
                if (renderer != null && !IsMinimapRenderer(renderer))
                {
                    gameRenderers.Add(renderer);
                }
            }

            if (gameRenderers.Count > 0)
            {
                unitRenderers[unit] = gameRenderers.ToArray();
                unitStates[unit] = VisibilityState.Visible; // 默认可见
                unitOriginalStates[unit] = true; // 记录原始状态

                // 更新统计
                VisibleUnits++;
            }
        }

        /// <summary>
        /// 注册GameObject到渲染管理系统（用于技能特效等）
        /// </summary>
        /// <param name="obj">要注册的游戏对象</param>
        public void RegisterGameObject(GameObject obj)
        {
            if (obj == null || !obj.activeInHierarchy || objectStates.ContainsKey(obj)) return;

            // 收集所有SpriteRenderer（包括子物体）
            SpriteRenderer[] allRenderers = obj.GetComponentsInChildren<SpriteRenderer>();

            // 过滤掉MinimapIcon的渲染器
            List<SpriteRenderer> gameRenderers = new List<SpriteRenderer>();
            foreach (var renderer in allRenderers)
            {
                if (renderer != null && !IsMinimapRenderer(renderer))
                {
                    gameRenderers.Add(renderer);
                }
            }

            if (gameRenderers.Count > 0)
            {
                objectRenderers[obj] = gameRenderers.ToArray();
                objectStates[obj] = VisibilityState.Visible; // 默认可见

                // 更新统计
                VisibleObjects++;
            }
        }
        
        /// <summary>
        /// 从渲染管理系统中注销单位
        /// </summary>
        /// <param name="unit">要注销的单位</param>
        public void UnregisterUnit(Unit unit)
        {
            if (unit == null || !unitStates.ContainsKey(unit)) return;
            
            // 更新统计
            VisibilityState currentState = unitStates[unit];
            UpdateStatistics(currentState, -1);
            
            // 移除记录
            unitStates.Remove(unit);
            unitRenderers.Remove(unit);
            unitOriginalStates.Remove(unit);
        }

        /// <summary>
        /// 从渲染管理系统中注销GameObject
        /// </summary>
        /// <param name="obj">要注销的游戏对象</param>
        public void UnregisterGameObject(GameObject obj)
        {
            if (obj == null || !objectStates.ContainsKey(obj)) return;

            // 更新统计
            VisibilityState currentState = objectStates[obj];
            UpdateObjectStatistics(currentState, -1);

            // 移除记录
            objectStates.Remove(obj);
            objectRenderers.Remove(obj);
        }
        
        /// <summary>
        /// 设置单位的可见性状态
        /// </summary>
        /// <param name="unit">目标单位</param>
        /// <param name="newState">新的可见性状态</param>
        /// <param name="immediate">是否立即应用</param>
        public void SetUnitVisibility(Unit unit, VisibilityState newState, bool immediate = false)
        {
            if (unit == null || !unitStates.ContainsKey(unit)) return;
            
            VisibilityState currentState = unitStates[unit];
            if (currentState == newState) return;
            
            // 更新统计
            UpdateStatistics(currentState, -1);
            UpdateStatistics(newState, 1);
            
            // 更新状态
            unitStates[unit] = newState;
            
            // 应用可见性变化
            if (immediate)
            {
                ApplyVisibilityImmediate(unit, newState);
            }
            else
            {
                ApplyVisibilityGradual(unit, newState);
            }
        }

        /// <summary>
        /// 设置GameObject的可见性状态
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="newState">新的可见性状态</param>
        /// <param name="immediate">是否立即应用</param>
        public void SetObjectVisibility(GameObject obj, VisibilityState newState, bool immediate = false)
        {
            if (obj == null || !objectStates.ContainsKey(obj)) return;

            VisibilityState currentState = objectStates[obj];
            if (currentState == newState) return;

            // 更新统计
            UpdateObjectStatistics(currentState, -1);
            UpdateObjectStatistics(newState, 1);

            // 更新状态
            objectStates[obj] = newState;

            // 应用可见性变化
            if (immediate)
            {
                ApplyObjectVisibilityImmediate(obj, newState);
            }
            else
            {
                ApplyObjectVisibilityGradual(obj, newState);
            }
        }

        /// <summary>
        /// 获取单位当前的可见性状态
        /// </summary>
        /// <param name="unit">目标单位</param>
        /// <returns>可见性状态</returns>
        public VisibilityState GetUnitVisibility(Unit unit)
        {
            if (unit == null || !unitStates.ContainsKey(unit))
                return VisibilityState.Hidden;

            return unitStates[unit];
        }
        
        /// <summary>
        /// 立即应用可见性变化
        /// </summary>
        /// <param name="unit">目标单位</param>
        /// <param name="state">可见性状态</param>
        private void ApplyVisibilityImmediate(Unit unit, VisibilityState state)
        {
            if (!unitRenderers.ContainsKey(unit)) return;

            bool shouldBeVisible = ShouldBeVisible(state);

            foreach (var renderer in unitRenderers[unit])
            {
                if (renderer != null)
                {
                    renderer.enabled = shouldBeVisible;
                }
            }
        }
        
        /// <summary>
        /// 渐进式应用可见性变化
        /// </summary>
        /// <param name="unit">目标单位</param>
        /// <param name="state">可见性状态</param>
        private void ApplyVisibilityGradual(Unit unit, VisibilityState state)
        {
            if (!unitRenderers.ContainsKey(unit)) return;

            // 将变化操作加入队列，避免一帧内处理太多
            transitionQueue.Enqueue(() => ApplyVisibilityImmediate(unit, state));
        }

        /// <summary>
        /// 立即应用GameObject可见性变化
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="state">可见性状态</param>
        private void ApplyObjectVisibilityImmediate(GameObject obj, VisibilityState state)
        {
            if (!objectRenderers.ContainsKey(obj)) return;

            bool shouldBeVisible = ShouldBeVisible(state);

            foreach (var renderer in objectRenderers[obj])
            {
                if (renderer != null)
                {
                    renderer.enabled = shouldBeVisible;
                }
            }
        }

        /// <summary>
        /// 渐进式应用GameObject可见性变化
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="state">可见性状态</param>
        private void ApplyObjectVisibilityGradual(GameObject obj, VisibilityState state)
        {
            if (!objectRenderers.ContainsKey(obj)) return;

            // 将变化操作加入队列，避免一帧内处理太多
            transitionQueue.Enqueue(() => ApplyObjectVisibilityImmediate(obj, state));
        }
        
        /// <summary>
        /// 处理渐进式过渡队列
        /// </summary>
        /// <param name="maxOperationsPerFrame">每帧最大操作数</param>
        public void ProcessTransitionQueue(int maxOperationsPerFrame)
        {
            int processedCount = 0;
            
            while (transitionQueue.Count > 0 && processedCount < maxOperationsPerFrame)
            {
                System.Action operation = transitionQueue.Dequeue();
                operation?.Invoke();
                processedCount++;
            }
        }
        
        /// <summary>
        /// 判断给定状态是否应该可见
        /// </summary>
        /// <param name="state">可见性状态</param>
        /// <returns>是否应该可见</returns>
        private bool ShouldBeVisible(VisibilityState state)
        {
            return state == VisibilityState.Visible || state == VisibilityState.Predicted;
        }
        
        /// <summary>
        /// 检查是否是小地图渲染器
        /// </summary>
        /// <param name="renderer">要检查的渲染器</param>
        /// <returns>是否是小地图渲染器</returns>
        private bool IsMinimapRenderer(SpriteRenderer renderer)
        {
            if (renderer == null) return false;
            
            // 检查对象名称
            if (renderer.gameObject.name.Contains("MinimapIcon") || 
                renderer.gameObject.name.Contains("Minimap"))
            {
                return true;
            }
            
            // 检查层级
            if (renderer.gameObject.layer == LayerMask.NameToLayer("Minimap"))
            {
                return true;
            }
            
            // 检查是否有MinimapIcon组件
            if (renderer.GetComponent<MinimapIcon>() != null)
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 更新统计信息
        /// </summary>
        /// <param name="state">状态</param>
        /// <param name="delta">变化量</param>
        private void UpdateStatistics(VisibilityState state, int delta)
        {
            switch (state)
            {
                case VisibilityState.Visible:
                    VisibleUnits += delta;
                    break;
                case VisibilityState.Predicted:
                    PredictedUnits += delta;
                    break;
                case VisibilityState.Hidden:
                    HiddenUnits += delta;
                    break;
            }
        }

        /// <summary>
        /// 更新GameObject统计信息
        /// </summary>
        /// <param name="state">状态</param>
        /// <param name="delta">变化量</param>
        private void UpdateObjectStatistics(VisibilityState state, int delta)
        {
            switch (state)
            {
                case VisibilityState.Visible:
                    VisibleObjects += delta;
                    break;
                case VisibilityState.Predicted:
                    PredictedObjects += delta;
                    break;
                case VisibilityState.Hidden:
                    HiddenObjects += delta;
                    break;
            }
        }
        
        /// <summary>
        /// 重置所有单位为可见状态
        /// </summary>
        public void ResetAllToVisible()
        {
            // 创建键的副本，避免在遍历时修改集合
            var units = new List<Unit>(unitStates.Keys);
            foreach (var unit in units)
            {
                if (unit != null && unitStates.ContainsKey(unit))
                {
                    SetUnitVisibility(unit, VisibilityState.Visible, true);
                }
            }
        }
        
        /// <summary>
        /// 清理所有数据
        /// </summary>
        public void Clear()
        {
            unitStates.Clear();
            unitRenderers.Clear();
            unitOriginalStates.Clear();
            objectStates.Clear();
            objectRenderers.Clear();
            transitionQueue.Clear();

            VisibleUnits = 0;
            HiddenUnits = 0;
            PredictedUnits = 0;
            VisibleObjects = 0;
            HiddenObjects = 0;
            PredictedObjects = 0;
        }
    }
}
