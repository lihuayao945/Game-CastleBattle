# 🎮 城堡战争
> Unity 2D英雄召唤实时战略游戏 | 完整AI系统 | 高性能优化

[![Unity](https://img.shields.io/badge/Unity-2022.3_LTS-black?logo=unity)]()
[![C#](https://img.shields.io/badge/C%23-9.0-blue?logo=csharp)]()
[![License](https://img.shields.io/badge/License-MIT-green)]()

[🎥 游戏演示视频](https://www.bilibili.com/video/BV1Xk8XzbEJc/?spm_id_from=333.1007.top_right_bar_window_dynamic.content.click&vd_source=ad47b3cdc94968f93e5e6c07e83cd9ee) 


![image](https://github.com/lihuayao945/Game-CastleBattle/blob/main/Images/gamePresentation.png)


**项目特色**: 英雄操控 + 小兵召唤 + Roguelike强化 + 智能AI对战

## 🎮 游戏试玩

### 立即体验
📦 **Windows版本**: [CastleWar.zip](https://github.com/lihuayao945/Game-CastleBattle/releases/download/Game/CastleWar.zip) (约50MB)

**系统要求**: Windows 10/11, DirectX 11, 2GB RAM

**快速开始**:
1. 下载并解压游戏文件
2. 运行 `城堡战争.exe`
3. 选择单人模式 → 选择英雄 → 开始游戏
4. 使用WSAD移动，数字键键召唤小兵，J/K/U/I键释放技能

**操作说明**:
- `空格键`: 切换摄像机模式（锁定/自由）
- `F键`: 快速聚焦英雄
- `M键`: 放大小地图
- `Tab键`: 查看数据面板
- `ESC键`: 暂停游戏

## 🏗️ 游戏结构

### 核心游戏架构
```
游戏管理层 (GameManager)
├── 技能系统 (Skill System)
│   ├── 技能工厂 (SkillFactory)
│   ├── 技能管理器 (CharacterSkillManager)
│   └── 技能数据 (SkillData SO)
├── 单位系统 (Unit System)
│   ├── 英雄单位 (HeroUnit)
│   ├── 小兵控制器 (Minion Controllers)
│   ├── 英雄控制器 (Hero Controllers)
│   ├── 对象池管理 (UnitPoolManager)
│   └── 网格移动系统 (FlowFieldManager)
├── AI系统 (AI System)
│   ├── AI管理器 (AIManager)
│   ├── 英雄AI (NecromancerAIController)
│   ├── 召唤AI (MinionSpawnAIController)
│   └── 强化AI (UpgradeAIController)
├── 强化系统 (Upgrade System)
│   ├── 全局强化管理 (GlobalGameUpgrades)
│   ├── 强化选择UI (UpgradeSelectionUI)
│   └── 强化数据 (UpgradeDataSO)
├── 摄像机系统 (Camera System)
│   ├── 摄像机控制器 (CameraController)
│   ├── 双模式切换 (锁定/自由)
│   └── 摄像机震动 (CameraShakeManager)
├── 小地图系统 (Minimap System)
│   ├── 小地图设置 (MinimapSetup)
│   ├── 小地图摄像机 (MinimapCamera)
│   ├── 图标管理 (MinimapIcon)
│   └── 图标对象池 (MinionIconPool)
├── 地图系统 (Map System)
│   ├── Tilemap地图制作 (Unity Tilemap)
│   ├── 地图边界 (MapBoundary)
│   └── 碰撞检测 (Physics2D)
├── UI系统 (UI System)
│   ├── UI管理器 (UIManager)
│   ├── 游戏界面 (GamePanel)
│   ├── 英雄血条 (HeroHealthBar)
│   ├── 复活计时器 (HeroResurrectionTimerUI)
│   └── 数据面板 (PlayerStatsPanelUI)
└── 渲染优化系统 (Rendering Optimization)
    ├── 视野剔除 (ViewportRenderingOptimizer)
    ├── 状态管理器 (RenderingStateManager)
    └── 优化配置 (OptimizationSettings)
```

### 游戏架构概要图
![image](https://github.com/lihuayao945/Game-CastleBattle/blob/main/Images/%E6%B8%B8%E6%88%8F%E8%AE%BE%E8%AE%A1%E7%BB%93%E6%9E%84%E6%A6%82%E8%A6%81%E5%9B%BE.png)

### 游戏结构详细图图
![image](https://github.com/lihuayao945/Game-CastleBattle/blob/main/Images/%E8%AF%A6%E7%BB%86%E7%B3%BB%E7%BB%9F%E7%BB%93%E6%9E%84%E5%9B%BE.png)

### 技术栈详情
| 分类 | 技术 | 版本 | 应用场景 |
|------|------|------|----------|
| 引擎 | Unity | 2022.3 LTS | 游戏开发框架 |
| 语言 | C# | 9.0 | 核心逻辑开发 |
| 地图制作 | Unity Tilemap | - | 2D地图构建 |
| 架构 | 事件驱动 + 模块化 | - | 系统解耦设计 |
| 数据 | ScriptableObject | - | 配置数据管理 |
| 移动系统 | 网格缓存 + 避障力 | 自实现 | 单位移动优化 |
| UI | Unity UI + TextMeshPro | - | 用户界面系统 |
| 物理 | Unity Physics2D | - | 碰撞检测系统 |

## 🎨 设计模式应用

### 核心模式

| 模式 | 应用场景 | 实现类 | 技术价值 |
|------|----------|--------|----------|
| **单例模式** | 全局管理器 | `GameManager`, `UIManager`, `AIManager` | 全局状态一致性 |
| **工厂模式** | 对象创建 | `SkillFactory` | 统一创建接口 |
| **观察者模式** | 事件通信 | `UnityEvent`系统 | 松耦合架构 |
| **对象池模式** | 性能优化 | `UnitPoolManager`, `MinionIconPool` | 内存管理优化 |
| **状态机模式** | 行为控制 | AI控制器, 游戏状态 | 清晰的逻辑流程 |
| **策略模式** | 配置系统 | `UpgradeDataSO` | 灵活的效果配置 |

## ⚙️ 核心系统实现

### ⚔️ 技能系统 - 工厂模式 + 数据驱动

#### 技能工厂统一创建
```csharp
public static class SkillFactory
{
    public static GameObject CreateSkill(SkillData data, Transform owner, Vector3 position)
    {
        GameObject skillObject = Object.Instantiate(data.effectPrefab, position, Quaternion.identity);
        SkillController controller = skillObject.GetComponent<SkillController>();
        
        if (controller == null)
        {
            controller = skillObject.AddComponent<SkillController>();
        }
        
        // 初始化技能参数
        controller.Initialize(data, owner);
        return skillObject;
    }
}
```

#### 数据驱动的技能配置
```csharp
[CreateAssetMenu(fileName = "NewSkill", menuName = "Game/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("基础属性")]
    public string skillName;
    public float damage;
    public float cooldown;
    public float range;
    
    [Header("效果设置")]
    public GameObject effectPrefab;
    public SkillType skillType;
    public TargetType targetType;
    
    [Header("强化支持")]
    public bool canBeUpgraded = true;
    public float damageMultiplier = 1f;
    public float cooldownMultiplier = 1f;
}
```

**技能系统亮点**:
- 🏭 **工厂模式**: 统一的技能创建接口，支持动态扩展
- 📊 **数据驱动**: ScriptableObject配置，策划友好
- 🎯 **类型系统**: 支持投射物、区域效果、治疗、削弱等多种类型
- ⚡ **强化集成**: 与强化系统无缝集成，支持动态属性调整
- 🔄 **对象池兼容**: 技能特效支持对象池复用

### 🎯 网格移动系统 - 缓存优化的方向查询

#### 网格方向缓存
```csharp
public class FlowFieldManager : MonoBehaviour
{
    private Dictionary<Vector2Int, FlowField> activeFields = new Dictionary<Vector2Int, FlowField>();
    
    public FlowField GenerateFlowField(Vector2 targetPosition, float radius)
    {
        Vector2Int gridPos = WorldToGrid(targetPosition);
        
        // 缓存机制：相同目标位置的方向计算可以复用
        if (activeFields.TryGetValue(gridPos, out FlowField existingField))
        {
            return existingField;
        }
        
        // 为新目标位置创建方向网格
        FlowField newField = new FlowField(gridPos, radius, cellSize, obstacleMask);
        activeFields[gridPos] = newField;
        return newField;
    }
}
```

#### 单位移动的实际逻辑
```csharp
private void MoveToAttackPosition(Vector2 targetPos)
{
    Vector2 currentPos = transform.position;
    Vector2 desiredDir;

    // 查询网格缓存的方向（主要用于性能优化）
    if (currentFlowField != null)
    {
        Vector2 flowDir = currentFlowField.GetFlowDirection(currentPos);
        desiredDir = flowDir != Vector2.zero ? flowDir : (targetPos - currentPos).normalized;
    }
    else
    {
        desiredDir = (targetPos - currentPos).normalized;
    }

    // 真正的移动逻辑：基础方向 + 避障力
    if (cachedAvoidanceDirection != Vector2.zero)
    {
        finalDirection = (desiredDir + cachedAvoidanceDirection).normalized;
    }
    
    // 避障力计算：远离周围单位
    for (int i = 0; i < cachedColliderCount; i++)
    {
        Vector2 toUnit = (Vector2)unit.transform.position - currentPos;
        avoidanceDir += -toUnit.normalized; // 远离其他单位
    }
}
```

**移动系统特点**:
- 🎯 **直线移动**: 单位主要通过直线朝目标移动
- 💾 **方向缓存**: 相同目标的方向计算会被多个单位复用
- 🚧 **避障力系统**: 通过排斥力避开其他单位，实现流畅移动
- ⚡ **性能优化**: 避免重复计算相同目标的移动方向
- 🎮 **适合场景**: 开放地形的实时战略游戏

### 🗺️ Tilemap地图系统 + 智能边界

#### Unity Tilemap地图制作
项目使用Unity原生Tilemap系统构建2D地图：
- **地图资源**: 使用Tile Palette进行地图绘制
- **分层渲染**: 背景层、装饰层、碰撞层分离
- **性能优化**: Tilemap自动批处理，减少Draw Call

#### 动态地图边界系统
```csharp
public class MapBoundary : MonoBehaviour
{
    [Header("地图边界设置")]
    public float mapWidth = 20f;
    public float mapHeight = 10f;
    public float boundaryThickness = 1f;

    void CreateBoundaries()
    {
        // 动态创建四个边界碰撞器
        CreateBoundary("LeftBoundary", new Vector3(-mapWidth/2 - boundaryThickness/2, 0, 0),
                      new Vector3(boundaryThickness, mapHeight + boundaryThickness*2, 1));
        CreateBoundary("RightBoundary", new Vector3(mapWidth/2 + boundaryThickness/2, 0, 0),
                      new Vector3(boundaryThickness, mapHeight + boundaryThickness*2, 1));
        // ... 其他边界
    }
}
```

**地图系统特点**:
- 🎨 **Tilemap制作**: Unity原生工具链，高效的2D地图构建
- 🚧 **动态边界**: 根据地图尺寸自动生成物理边界
- 📐 **精确控制**: 可配置边界厚度和位置
- 🔧 **易于调试**: 可视化边界设置和碰撞检测

### 🗺️ 小地图系统 - 双摄像机架构

#### 基于Tilemap的小地图渲染
```csharp
public class MinimapCamera : MonoBehaviour
{
    private void CreateMinimapCamera()
    {
        // 创建独立的小地图摄像机
        minimapCamera.orthographic = true;

        // 设置渲染层：同时渲染Default和Minimap层
        int defaultLayer = LayerMask.NameToLayer("Default");
        int minimapLayer = LayerMask.NameToLayer("Minimap");
        minimapCamera.cullingMask = (1 << defaultLayer) | (1 << minimapLayer);

        // 覆盖整个Tilemap地图区域
        minimapCamera.orthographicSize = mapHeight / 2f;
    }

    // 点击小地图跳转功能
    private void HandleMinimapClick()
    {
        Vector3 worldPosition = minimapCamera.ScreenToWorldPoint(Input.mousePosition);
        mainCamera.transform.position = new Vector3(worldPosition.x, worldPosition.y, mainCamera.transform.position.z);
    }
}
```

**小地图技术亮点**:
- 🎨 **Tilemap集成**: 直接渲染Tilemap地图到小地图
- 📷 **双摄像机架构**: 主视图 + 小地图独立渲染管线
- 🖱️ **精确坐标转换**: 小地图点击到世界坐标的数学转换
- 🏷️ **动态图标系统**: 单位图标实时同步，支持对象池

### 🎮 摄像机系统 - 双模式智能控制

#### 智能摄像机控制器
```csharp
public class CameraController : MonoBehaviour
{
    public enum CameraMode
    {
        FollowHero,    // 跟随英雄模式
        FreeMove       // 自由移动模式
    }

    private void Update()
    {
        // 空格键切换模式
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleCameraMode();
        }

        // F键快速聚焦英雄
        if (Input.GetKeyDown(focusKey) && currentHero != null)
        {
            FocusOnHero();
        }
    }
}
```

**摄像机功能特性**:
- 🎯 **双模式切换**: 锁定跟随 ↔ 自由移动
- 🔍 **快速聚焦**: F键一键回到英雄位置
- 📐 **地图边界限制**: 与MapBoundary系统集成
- 🎬 **平滑过渡**: 使用Vector3.Lerp实现流畅移动

### 🤖 三层AI架构系统

#### AI统一管理
```csharp
public class AIManager : MonoBehaviour
{
    // 三大AI组件
    private NecromancerAIController heroAI;        // 英雄AI控制器
    private MinionSpawnAIController minionAI;      // 小兵召唤AI
    private UpgradeAIController upgradeAI;         // 强化选择AI

    public void InitializeAI()
    {
        if (IsRightSideAI() && CurrentMode == GameMode.SinglePlayer)
        {
            StartCoroutine(InitializeAICoroutine());
        }
    }
}
```

#### AI逻辑图

![image](https://github.com/lihuayao945/Game-CastleBattle/blob/main/Images/%E9%82%AA%E6%9C%AF%E5%B8%88AI.png)

![image](https://github.com/lihuayao945/Game-CastleBattle/blob/main/Images/%E5%BC%BA%E5%8C%96%E9%80%89%E6%8B%A9AI.png)

![image](https://github.com/lihuayao945/Game-CastleBattle/blob/main/Images/%E5%B0%8F%E5%85%B5%E5%8F%AC%E5%94%A4AI.png)


**AI层次结构**:
- 🧠 **决策层**: 战场分析和策略制定
- ⚡ **执行层**: 具体行为的精确控制
- 🎯 **适应层**: 根据玩家行为动态调整

### ⚡ 渐进式渲染优化

#### 智能视野剔除
```csharp
public class ViewportRenderingOptimizer : MonoBehaviour
{
    private void ProcessUnitsVisibility()
    {
        // 分帧处理，避免性能峰值
        int processedCount = 0;
        while (unitsToProcess.Count > 0 && processedCount < maxObjectsPerFrame)
        {
            Unit unit = unitsToProcess.Dequeue();
            RenderingStateManager.VisibilityState newState = CalculateVisibilityState(unit.gameObject);
            stateManager.SetUnitVisibility(unit, newState, false);
            processedCount++;
        }
    }
}
```

**优化策略**:
- 👁️ **视野剔除**: 摄像机视野外对象停止渲染
- 🔮 **预测性加载**: 摄像机移动方向的提前准备
- 📈 **渐进式处理**: 分帧更新避免卡顿
- 📊 **性能监控**: 实时优化率统计

## 🎨 设计模式应用

### 核心模式实现

### 事件驱动架构实现
```csharp
// 单位系统中的事件声明
public class Unit : MonoBehaviour
{
    public UnityEvent OnDeath;
    public UnityEvent<float> OnDamaged;
    public UnityEvent<float, float> OnHealthUpdated;

    public virtual void TakeDamage(float damage)
    {
        currentHealth -= damage;
        OnDamaged?.Invoke(damage);              // 触发受伤事件
        OnHealthUpdated?.Invoke(currentHealth, maxHealth);  // 血量更新
        if (currentHealth <= 0) OnDeath?.Invoke();          // 死亡事件
    }
}

// UI系统响应单位事件
public class HeroHealthBar : MonoBehaviour
{
    private void Start()
    {
        if (trackedHero != null)
        {
            trackedHero.OnHealthUpdated.AddListener(UpdateHealthBar);
            trackedHero.OnDeath.AddListener(OnHeroDeath);
        }
    }
}
```

**事件系统应用**:
- 🎯 **UI响应**: 血条、计时器自动更新
- 🤖 **AI决策**: AI监听战场事件调整策略
- 🎵 **效果触发**: 音效、特效的事件驱动
- 📊 **数据统计**: 战斗数据的实时收集

## 🚀 性能优化技术

### 优化策略全览
| 优化类型 | 技术方案 | 性能提升 | 实现细节 |
|----------|----------|----------|----------|
| **内存管理** | 对象池 + 智能清理 | 减少70%GC | 小兵单位复用，英雄单位特殊处理 |
| **渲染优化** | 视野剔除 + 分帧处理 | 提升40%帧率 | 摄像机视野外单位停止渲染 |
| **移动优化** | 网格缓存 + 避障力 | 减少重复计算 | 方向缓存，避障力系统 |
| **地图渲染** | Tilemap批处理 | 减少Draw Call | Unity原生Tilemap优化 |
| **UI优化** | 事件驱动更新 | 消除冗余刷新 | 按需更新，避免每帧检查 |

### 对象池系统 - 智能内存管理
```csharp
public class UnitPoolManager : MonoBehaviour
{
    public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation, Unit.Faction faction)
    {
        // 英雄单位特殊处理：直接实例化，避免状态混乱
        if (prefab.CompareTag("Hero"))
        {
            return Instantiate(prefab, position, rotation);
        }

        // 小兵单位使用对象池复用
        if (!poolDictionary.ContainsKey(prefab))
            CreatePool(prefab);

        return poolDictionary[prefab].Count > 0
            ? ActivatePooledObject(poolDictionary[prefab].Dequeue(), position, rotation, faction)
            : CreateNewPooledObject(prefab, position, rotation, faction);
    }
}
```

### 实际性能数据
- **帧率稳定性**: 100+单位同屏保持60+ FPS
- **内存占用**: 对象池优化后稳定在500MB以下
- **渲染优化率**: 视野外单位剔除率60-80%
- **AI响应延迟**: 决策响应时间<50ms
- **地图渲染**: Tilemap批处理减少60%Draw Call

## 📋 开发信息

### 项目规模统计
- **开发周期**: 40天
- **代码规模**: ~15,000行 C#代码
- **系统数量**: 10个核心系统，30+个子模块
- **配置文件**: 100+ ScriptableObject数据文件
- **地图资源**: 基于Tilemap的完整地图系统

### 功能完成状态
- ✅ **完整单人模式**: AI对战系统全面实现
- ✅ **核心游戏循环**: 完整的游戏流程
- ✅ **性能优化系统**: 大规模战斗优化完成
- ✅ **UI交互系统**: 完善的用户界面
- ✅ **摄像机系统**: 双模式智能控制
- ✅ **小地图系统**: 基于Tilemap的双摄像机架构
- ✅ **地图系统**: Tilemap地图制作 + 智能边界管理

### 技术挑战与解决方案
| 技术挑战 | 解决方案 | 关键技术点 |
|----------|----------|------------|
| **大量单位性能问题** | 对象池+视野剔除 | 内存复用+渲染优化 |
| **AI决策复杂度** | 分层状态机架构 | 决策层次化+权重系统 |
| **地图边界管理** | 动态边界生成 | Physics2D+自动化配置 |
| **小地图坐标转换** | 数学计算优化 | 屏幕坐标↔世界坐标 |
| **单位移动卡顿** | 避障力+网格缓存 | 排斥力系统+方向缓存 |

### 后续发展方向
- 🌐 **网络对战模式**: 基于左右方设计实现
- 🎨 **关卡编辑器**: 基于Tilemap的可视化地图编辑
- 📱 **移动端适配**: UI和操作的移动端优化
- 🔧 **模组系统**: 开放API支持社区创作
- 🎵 **音效系统**: 完整的音频管理系统


---

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📧 联系方式

- 邮箱: 2926814355@qq.com
- GitHub: [lihuayao945](https://github.com/lihuayao945)
