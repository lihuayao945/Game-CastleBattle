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

## 🔧 系统实现详解
详细的系统架构、代码实现和设计模式应用请查看：

**[📖 系统实现文档 →](./SYSTEM_IMPLEMENTATION.md)**

包含内容：
- ⚔️ **技能系统** - 工厂模式 + 数据驱动
- 🎯 **网格移动系统** - 缓存优化的方向查询
- 🗺️ **Tilemap地图系统** + 智能边界
- 🗺️ **小地图系统** - 双摄像机架构
- 🎮 **摄像机系统** - 双模式智能控制
- 🤖 **三层AI架构系统**
- ⚡ **渐进式渲染优化**
- 🎨 **设计模式应用**
- 🔧 **性能优化技术**



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
