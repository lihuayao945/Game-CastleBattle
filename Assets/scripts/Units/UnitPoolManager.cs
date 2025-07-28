using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace MagicBattle
{
    /// <summary>
    /// 对象池管理器 - 负责管理不同类型小兵的对象池
    /// </summary>
    public class UnitPoolManager : MonoBehaviour
    {
        private static UnitPoolManager _instance;
        public static UnitPoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<UnitPoolManager>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject("UnitPoolManager");
                        _instance = obj.AddComponent<UnitPoolManager>();
                        DontDestroyOnLoad(obj);
                    }
                }
                return _instance;
            }
        }

        [Header("对象池配置")]
        [SerializeField] private int maxPoolSizePerType = 100; // 每种类型的最大池大小
        [SerializeField] private bool enablePoolExpansion = true; // 是否允许池扩展
        [SerializeField] private int expansionStep = 10; // 每次扩展的数量
        
        [Header("性能监控")]
        [SerializeField] private bool enablePerformanceLogging = false;
        [SerializeField] private float logInterval = 15f; // 每15秒记录一次性能数据
        
        // 对象池字典，键为"预制体ID_阵营"的组合字符串
        private Dictionary<string, Queue<GameObject>> poolDictionary = new Dictionary<string, Queue<GameObject>>();

        // 对象池父物体字典，用于组织层级结构
        private Dictionary<string, Transform> poolParents = new Dictionary<string, Transform>();

        // 记录每个池的当前大小
        private Dictionary<string, int> poolSizes = new Dictionary<string, int>();
        
        // 性能统计
        private float lastLogTime = 0f;
        private int totalObjectsCreated = 0;
        private int totalObjectsReused = 0;
        private int totalObjectsReturned = 0;
        
        // 预热状态
        public bool IsPrewarmComplete { get; private set; } = false;

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

        private void Update()
        {
            if (enablePerformanceLogging && Time.time - lastLogTime >= logInterval)
            {
                LogPerformanceStats();
                lastLogTime = Time.time;
            }
        }
        
        /// <summary>
        /// 生成池键（预制体ID + 阵营）
        /// </summary>
        private string GeneratePoolKey(GameObject prefab, Unit.Faction faction)
        {
            return $"{prefab.GetInstanceID()}_{faction}";
        }

        /// <summary>
        /// 预热对象池
        /// </summary>
        public void PrewarmPool(GameObject prefab, int count, Unit.Faction faction)
        {
            if (prefab == null) return;

            string poolKey = GeneratePoolKey(prefab, faction);

            // 确保对象池存在
            EnsurePoolExists(prefab, faction);

            // 预热指定数量的对象
            for (int i = 0; i < count; i++)
            {
                GameObject obj = CreateNewInstance(prefab, faction);
                // 直接放入池中，不调用ReturnToPool避免循环
                obj.SetActive(false);
                obj.transform.SetParent(poolParents[poolKey]);
                poolDictionary[poolKey].Enqueue(obj);
                poolSizes[poolKey]++;
            }

            //Debug.Log($"预热完成: {prefab.name} ({faction}) x {count}个对象");
        }
        
        /// <summary>
        /// 从对象池获取对象
        /// </summary>
        public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation, Unit.Faction faction)
        {
            if (prefab == null) return null;

            // 如果是英雄单位，直接实例化
            if (prefab.CompareTag("Hero"))
            {
                return Instantiate(prefab, position, rotation);
            }

            string poolKey = GeneratePoolKey(prefab, faction);

            // 确保对象池存在
            EnsurePoolExists(prefab, faction);

            GameObject obj;

            if (poolDictionary[poolKey].Count > 0)
            {
                // 从池中获取对象
                obj = poolDictionary[poolKey].Dequeue();
                obj.transform.SetPositionAndRotation(position, rotation);
                obj.SetActive(true);
                totalObjectsReused++;
            }
            else
            {
                // 池为空时的处理策略
                if (enablePoolExpansion && poolSizes[poolKey] < maxPoolSizePerType)
                {
                    // 批量扩展池大小
                    ExpandPool(prefab, expansionStep, faction);

                    // 从扩展后的池中获取对象
                    obj = poolDictionary[poolKey].Dequeue();
                    obj.transform.SetPositionAndRotation(position, rotation);
                    obj.SetActive(true);
                    totalObjectsReused++;

                    //Debug.Log($"池扩展: {prefab.name} ({faction}) 当前大小: {poolSizes[poolKey]}");
                }
                else
                {
                    // 达到最大池大小或禁用扩展时，创建临时对象
                    obj = CreateTemporaryInstance(prefab, faction);
                    obj.transform.SetPositionAndRotation(position, rotation);
                    totalObjectsCreated++;

                    //Debug.LogWarning($"池已满，创建临时对象: {prefab.name} ({faction})");
                }
            }
            
            // 重置对象状态
            ResetObjectForReuse(obj);
            
            return obj;
        }
        
        /// <summary>
        /// 将对象返回对象池
        /// </summary>
        public void ReturnToPool(GameObject obj)
        {
            if (obj == null) return;
            
            // 英雄单位不进入对象池，直接返回
            if (obj.CompareTag("Hero")) return;
            
            // 检查是否是临时对象
            TemporaryObject tempObj = obj.GetComponent<TemporaryObject>();
            if (tempObj != null)
            {
                Destroy(obj);
                return;
            }
            
            // 获取对象池标记组件
            PooledObject pooledObj = obj.GetComponent<PooledObject>();
            if (pooledObj == null)
            {
                //Debug.LogWarning("尝试返回非对象池对象: " + obj.name);
                Destroy(obj);
                return;
            }

            string poolKey = pooledObj.poolKey;

            // 确保对象池存在
            if (!poolDictionary.ContainsKey(poolKey))
            {
                //Debug.LogWarning("尝试返回到不存在的对象池: " + obj.name);
                Destroy(obj);
                return;
            }

            // 检查池是否已满
            if (poolDictionary[poolKey].Count >= maxPoolSizePerType)
            {
                //Debug.Log($"池已满，销毁多余对象: {obj.name}");
                Destroy(obj);
                return;
            }

            // 重置对象状态
            ResetObjectForPool(obj);

            // 设置为非激活状态
            obj.SetActive(false);

            // 将对象放回对象池父物体下
            obj.transform.SetParent(poolParents[poolKey]);

            // 返回到对象池
            poolDictionary[poolKey].Enqueue(obj);
            totalObjectsReturned++;
        }
        
        /// <summary>
        /// 分批预热对象池（在英雄选择阶段调用）
        /// </summary>
        public void StartBatchPrewarm()
        {
            StartCoroutine(BatchPrewarmCoroutine());
        }

        /// <summary>
        /// 分批预热协程
        /// </summary>
        private IEnumerator BatchPrewarmCoroutine()
        {
            // 获取小兵生成器
            MinionSpawner spawner = FindObjectOfType<MinionSpawner>();
            if (spawner == null)
            {
                //Debug.LogWarning("未找到MinionSpawner，无法进行预热");
                yield break;
            }

            // 预热配置（方案A：减少预热数量）
            Dictionary<string, int> prewarmConfig = new Dictionary<string, int>
            {
                {"Soldier", 20},      // 左右各20个，总共40个
                {"Archer", 15},       // 左右各15个，总共30个
                {"Lancer", 12},       // 左右各12个，总共24个
                {"Priest", 10},       // 左右各10个，总共20个
                {"Mage", 10},         // 左右各10个，总共20个
                {"SwordMaster", 6}    // 左右各6个，总共12个
            };

            int objectsPerFrame = 3; // 每帧预热3个对象
            int currentCount = 0;

            // 预热左方小兵
            foreach (var squad in spawner.LeftSquads)
            {
                if (squad.minionPrefab != null && !squad.minionPrefab.CompareTag("Hero"))
                {
                    string minionType = GetMinionType(squad.minionPrefab);
                    int prewarmCount = prewarmConfig.ContainsKey(minionType) ? prewarmConfig[minionType] : 15;

                    // 确保对象池存在
                    EnsurePoolExists(squad.minionPrefab, Unit.Faction.Left);
                    string poolKey = GeneratePoolKey(squad.minionPrefab, Unit.Faction.Left);

                    for (int i = 0; i < prewarmCount; i++)
                    {
                        GameObject obj = CreateNewInstance(squad.minionPrefab, Unit.Faction.Left);
                        // 直接放入池中
                        obj.SetActive(false);
                        obj.transform.SetParent(poolParents[poolKey]);
                        poolDictionary[poolKey].Enqueue(obj);
                        poolSizes[poolKey]++;
                        currentCount++;

                        // 每3个对象后等待一帧
                        if (currentCount >= objectsPerFrame)
                        {
                            currentCount = 0;
                            yield return null;
                        }
                    }

                    //Debug.Log($"预热左方 {minionType}: {prewarmCount}个");
                }
            }

            // 预热右方小兵
            foreach (var squad in spawner.RightSquads)
            {
                if (squad.minionPrefab != null && !squad.minionPrefab.CompareTag("Hero"))
                {
                    string minionType = GetMinionType(squad.minionPrefab);
                    int prewarmCount = prewarmConfig.ContainsKey(minionType) ? prewarmConfig[minionType] : 15;

                    // 确保对象池存在
                    EnsurePoolExists(squad.minionPrefab, Unit.Faction.Right);
                    string poolKey = GeneratePoolKey(squad.minionPrefab, Unit.Faction.Right);

                    for (int i = 0; i < prewarmCount; i++)
                    {
                        GameObject obj = CreateNewInstance(squad.minionPrefab, Unit.Faction.Right);
                        // 直接放入池中
                        obj.SetActive(false);
                        obj.transform.SetParent(poolParents[poolKey]);
                        poolDictionary[poolKey].Enqueue(obj);
                        poolSizes[poolKey]++;
                        currentCount++;

                        // 每3个对象后等待一帧
                        if (currentCount >= objectsPerFrame)
                        {
                            currentCount = 0;
                            yield return null;
                        }
                    }

                    //Debug.Log($"预热右方 {minionType}: {prewarmCount}个");
                }
            }

            IsPrewarmComplete = true;
            //Debug.Log("对象池预热完成，游戏性能已优化");
        }

        /// <summary>
        /// 根据预制体上的组件判断小兵类型
        /// </summary>
        private string GetMinionType(GameObject prefab)
        {
            if (prefab.GetComponent<SoldierController>() != null) return "Soldier";
            if (prefab.GetComponent<ArcherController>() != null) return "Archer";
            if (prefab.GetComponent<LancerController>() != null) return "Lancer";
            if (prefab.GetComponent<PriestController>() != null) return "Priest";
            if (prefab.GetComponent<MageController>() != null) return "Mage";
            if (prefab.GetComponent<SwordMasterController>() != null) return "SwordMaster";
            return "Unknown";
        }

        /// <summary>
        /// 扩展对象池
        /// </summary>
        private void ExpandPool(GameObject prefab, int expandCount, Unit.Faction faction)
        {
            string poolKey = GeneratePoolKey(prefab, faction);

            for (int i = 0; i < expandCount; i++)
            {
                if (poolSizes[poolKey] >= maxPoolSizePerType) break;

                GameObject obj = CreateNewInstance(prefab, faction);
                // 直接放入池中，不调用ReturnToPool
                obj.SetActive(false);
                obj.transform.SetParent(poolParents[poolKey]);
                poolDictionary[poolKey].Enqueue(obj);
                poolSizes[poolKey]++;
            }
        }

        /// <summary>
        /// 创建临时实例（不加入对象池）
        /// </summary>
        private GameObject CreateTemporaryInstance(GameObject prefab, Unit.Faction faction)
        {
            GameObject obj = Instantiate(prefab);

            // 设置阵营
            Unit unit = obj.GetComponent<Unit>();
            if (unit != null)
            {
                unit.faction = faction;
            }

            // 添加临时对象标记，销毁时直接Destroy而不返回池
            obj.AddComponent<TemporaryObject>();

            return obj;
        }

        /// <summary>
        /// 确保对象池存在
        /// </summary>
        private void EnsurePoolExists(GameObject prefab, Unit.Faction faction)
        {
            string poolKey = GeneratePoolKey(prefab, faction);

            if (!poolDictionary.ContainsKey(poolKey))
            {
                // 创建新的对象池
                poolDictionary[poolKey] = new Queue<GameObject>();
                poolSizes[poolKey] = 0;

                // 创建对象池父物体
                GameObject poolParent = new GameObject($"{prefab.name}_{faction}_Pool");
                poolParent.transform.SetParent(transform);
                poolParents[poolKey] = poolParent.transform;
            }
        }

        /// <summary>
        /// 创建新实例
        /// </summary>
        private GameObject CreateNewInstance(GameObject prefab, Unit.Faction faction)
        {
            string poolKey = GeneratePoolKey(prefab, faction);

            // 实例化对象
            GameObject obj = Instantiate(prefab);

            // 设置阵营
            Unit unit = obj.GetComponent<Unit>();
            if (unit != null)
            {
                unit.faction = faction;
            }

            // 添加对象池标记组件
            PooledObject pooledObj = obj.GetComponent<PooledObject>();
            if (pooledObj == null)
            {
                pooledObj = obj.AddComponent<PooledObject>();
            }
            pooledObj.poolKey = poolKey;
            pooledObj.originalScale = obj.transform.localScale; // 保存原始缩放

            return obj;
        }

        /// <summary>
        /// 为对象池重置对象状态
        /// </summary>
        private void ResetObjectForPool(GameObject obj)
        {
            // 重置Unit组件状态
            Unit unit = obj.GetComponent<Unit>();
            if (unit != null)
            {
                unit.ResetUnit();
            }

            // 重置渲染器透明度
            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color color = sr.color;
                sr.color = new Color(color.r, color.g, color.b, 1f);
            }

            // 停止所有协程
            MonoBehaviour[] monoBehaviours = obj.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour mb in monoBehaviours)
            {
                if (mb != null) mb.StopAllCoroutines();
            }

            // 重置Transform，恢复原始缩放
            PooledObject pooledObj = obj.GetComponent<PooledObject>();
            if (pooledObj != null)
            {
                obj.transform.localScale = pooledObj.originalScale; // 恢复原始缩放
            }
            obj.transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// 为重用重置对象状态
        /// </summary>
        private void ResetObjectForReuse(GameObject obj)
        {
            // 重置Unit组件状态
            Unit unit = obj.GetComponent<Unit>();
            if (unit != null)
            {
                unit.ResetUnit();
            }
        }

        /// <summary>
        /// 记录性能统计
        /// </summary>
        private void LogPerformanceStats()
        {
            float reuseRate = totalObjectsCreated > 0 ? (float)totalObjectsReused / (totalObjectsCreated + totalObjectsReused) * 100f : 0f;

            //Debug.Log($"=== 对象池性能统计 ===");
            //Debug.Log($"总创建对象数: {totalObjectsCreated}");
            //Debug.Log($"重用对象数: {totalObjectsReused}");
            //Debug.Log($"返回池对象数: {totalObjectsReturned}");
            //Debug.Log($"重用率: {reuseRate:F1}%");
            //Debug.Log($"当前活跃池数量: {poolDictionary.Count}");

            // 显示各池的状态
            foreach (var kvp in poolDictionary)
            {
                string poolKey = kvp.Key;
                int availableCount = kvp.Value.Count;
                int totalSize = poolSizes.ContainsKey(poolKey) ? poolSizes[poolKey] : 0;
                int inUseCount = totalSize - availableCount;

                //Debug.Log($"  池 {poolKey}: 使用中={inUseCount}, 可用={availableCount}, 总计={totalSize}");
            }

            // 重置统计
            totalObjectsCreated = 0;
            totalObjectsReused = 0;
            totalObjectsReturned = 0;
        }

        /// <summary>
        /// 清理所有对象池
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in poolDictionary.Values)
            {
                while (pool.Count > 0)
                {
                    GameObject obj = pool.Dequeue();
                    if (obj != null) Destroy(obj);
                }
            }

            poolDictionary.Clear();
            poolSizes.Clear();

            // 销毁池父物体
            foreach (var parent in poolParents.Values)
            {
                if (parent != null) Destroy(parent.gameObject);
            }
            poolParents.Clear();
        }
    }

    /// <summary>
    /// 对象池标记组件 - 用于标记对象池中的对象
    /// </summary>
    public class PooledObject : MonoBehaviour
    {
        public string poolKey; // 池键（预制体ID + 阵营）
        public Vector3 originalScale; // 保存原始缩放
    }

    /// <summary>
    /// 临时对象标记组件 - 用于标记临时创建的对象
    /// </summary>
    public class TemporaryObject : MonoBehaviour
    {
        // 这个组件用于标记临时创建的对象，销毁时不返回对象池
    }
}
