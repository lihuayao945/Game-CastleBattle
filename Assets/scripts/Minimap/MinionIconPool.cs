using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagicBattle
{
    /// <summary>
    /// 小兵图标对象池管理器 - 专门管理小兵的小地图图标
    /// </summary>
    public class MinionIconPool : MonoBehaviour
    {
        private static MinionIconPool _instance;
        public static MinionIconPool Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject obj = new GameObject("MinionIconPool");
                    _instance = obj.AddComponent<MinionIconPool>();
                    DontDestroyOnLoad(obj);
                }
                return _instance;
            }
        }

        [Header("对象池配置")]
        [SerializeField] private int prewarmCount = 60;      // 预热数量
        [SerializeField] private int maxPoolSize = 100;      // 最大池大小
        [SerializeField] private int iconsPerFrame = 5;      // 每帧创建的图标数量

        [Header("调试信息")]
        [SerializeField] private bool showDebugInfo = false;

        // 对象池
        private Queue<PooledMinionIcon> availableIcons = new Queue<PooledMinionIcon>();
        private List<PooledMinionIcon> allPooledIcons = new List<PooledMinionIcon>();

        // 预热状态
        public bool IsPrewarmComplete { get; private set; } = false;

        // 性能统计
        private int totalIconsCreated = 0;
        private int totalIconsReused = 0;
        private int totalIconsReturned = 0;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 开始分批预热
        /// </summary>
        public void StartPrewarm()
        {
            if (!IsPrewarmComplete)
            {
                StartCoroutine(PrewarmCoroutine());
            }
        }

        /// <summary>
        /// 获取图标
        /// </summary>
        public PooledMinionIcon GetIcon()
        {
            if (availableIcons.Count > 0)
            {
                PooledMinionIcon icon = availableIcons.Dequeue();
                totalIconsReused++;
                return icon;
            }

            // 池为空时创建新的（如果未达到最大值）
            if (allPooledIcons.Count < maxPoolSize)
            {
                PooledMinionIcon newIcon = CreateNewIcon();
                totalIconsReused++;
                return newIcon;
            }

            if (showDebugInfo)
            {
                Debug.LogWarning("小兵图标对象池已满，无法获取新图标");
            }
            return null; // 池已满，返回null
        }

        /// <summary>
        /// 归还图标
        /// </summary>
        public void ReturnIcon(PooledMinionIcon icon)
        {
            if (icon != null && !availableIcons.Contains(icon))
            {
                icon.Reset();
                availableIcons.Enqueue(icon);
                totalIconsReturned++;
            }
        }

        /// <summary>
        /// 分批预热协程
        /// </summary>
        private IEnumerator PrewarmCoroutine()
        {
            int createdCount = 0;

            for (int i = 0; i < prewarmCount; i++)
            {
                PooledMinionIcon icon = CreateNewIcon();
                icon.Reset(); // 设置为非激活状态
                availableIcons.Enqueue(icon);

                createdCount++;

                // 每创建指定数量后等待一帧
                if (createdCount >= iconsPerFrame)
                {
                    createdCount = 0;
                    yield return null;
                }
            }

            IsPrewarmComplete = true;
            if (showDebugInfo)
            {
                Debug.Log($"小兵图标对象池预热完成: {prewarmCount}个图标");
            }
        }

        /// <summary>
        /// 创建新图标
        /// </summary>
        private PooledMinionIcon CreateNewIcon()
        {
            GameObject iconObj = new GameObject("PooledMinionIcon");
            iconObj.transform.SetParent(transform);

            // 添加SpriteRenderer
            SpriteRenderer renderer = iconObj.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 100;

            // 设置层级
            int minimapLayer = LayerMask.NameToLayer("Minimap");
            if (minimapLayer != -1)
            {
                iconObj.layer = minimapLayer;
            }

            // 添加池化组件
            PooledMinionIcon pooledIcon = iconObj.AddComponent<PooledMinionIcon>();
            allPooledIcons.Add(pooledIcon);
            totalIconsCreated++;

            return pooledIcon;
        }

        /// <summary>
        /// 获取池状态信息
        /// </summary>
        public string GetPoolStatus()
        {
            int availableCount = availableIcons.Count;
            int totalCount = allPooledIcons.Count;
            int inUseCount = totalCount - availableCount;

            return $"小兵图标池状态: 使用中={inUseCount}, 可用={availableCount}, 总计={totalCount}, 最大={maxPoolSize}";
        }

        /// <summary>
        /// 获取性能统计
        /// </summary>
        public string GetPerformanceStats()
        {
            return $"小兵图标池统计: 创建={totalIconsCreated}, 复用={totalIconsReused}, 归还={totalIconsReturned}";
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
