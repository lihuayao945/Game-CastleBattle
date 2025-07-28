using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 技能数据 - 用于配置技能的ScriptableObject
/// 可以在Unity编辑器中配置不同技能参数
/// </summary>
[CreateAssetMenu(fileName = "NewSkill", menuName = "Skill/SkillData")]
public class SkillData : ScriptableObject
{
    // ----------------- 基础属性 -----------------
    [Header("基础属性")]
    [Tooltip("技能行为类型")]
    public SkillBehaviorType behaviorType;
    [Tooltip("技能效果类型")]
    public SkillEffectType effectType;
    [Tooltip("技能效果预制体")]
    public GameObject effectPrefab;

    // 新增枚举：指定要修改的技能属性类型
    public enum SkillPropertyType
    {
        None,
        Damage,
        Heal,
        Cooldown,
        Range,      // 投射物射程
        AreaSize,   // 区域技能大小
        StunDuration, // 眩晕持续时间
        // ... 其他可被强化的技能属性
    }

    // ----------------- 投射物属性 -----------------
    [Header("投射物属性")]
    [Tooltip("投射物移动速度")]
    public float projectileSpeed = 10f;
    [Tooltip("投射物生存时间（秒）")]
    public float projectileLifetime = 2f;
    [Tooltip("X方向射程（自动计算：速度 × 时间）")]
    public float rangeX => projectileSpeed * projectileLifetime; // 自动计算
    [Header("范围属性")]
    [Tooltip("Y方向射程宽度")]
    public float rangeY = 1.5f; // 碰撞宽度

    // ----------------- 区域效果属性 -----------------
    [Header("区域效果属性")]
    [Tooltip("区域形状")]
    public EffectShape areaShape;
    
    [Tooltip("区域大小")]
    public Vector2 areaSize;
    
    [Tooltip("区域持续时间")]
    public float areaDuration;
    
    [Tooltip("延迟伤害时间")]
    public float delayTime = 0.5f;
    
    [Tooltip("特效位置偏移")]
    public Vector3 effectOffset = new Vector3(0, -0.05f, 0);

    // ----------------- 光环属性 -----------------
    [Header("光环属性")]
    [Tooltip("光环半径")]
    public float auraRadius = 3f;
    [Tooltip("效果触发间隔（秒）")]
    public float effectInterval = 1f;
    [Tooltip("效果值（伤害/治疗量）")]
    public float effectValue = 10f;

    // ----------------- 特效属性 -----------------
    [Header("特效属性")]
    [Tooltip("治疗特效预制体")]
    public GameObject healEffectPrefab;
    [Tooltip("伤害特效预制体")]
    public GameObject damageEffectPrefab;
    [Tooltip("眩晕特效预制体")]
    public GameObject stunEffectPrefab;

    // ----------------- 眩晕属性 -----------------
    [Header("眩晕属性")]
    [Tooltip("眩晕持续时间（秒）")]
    public float stunDuration = 2f;

    // ----------------- 生成位置属性 -----------------
    [Header("生成位置属性")]
    [Tooltip("生成位置偏移类型")]
    public SpawnOffsetType spawnOffsetType = SpawnOffsetType.Forward;
    [Tooltip("生成距离角色的偏移距离")]
    public float spawnDistance = 0.1f;
    [Tooltip("跟随技能的偏移位置")]
    public Vector3 followOffset = Vector3.zero;
    
    // ----------------- 冷却属性 -----------------
    [Header("冷却属性")]
    [Tooltip("技能冷却时间（秒）")]
    public float cooldownTime = 1f; // 技能冷却时间参数
    
    // ----------------- 施法属性 -----------------
    [Header("施法属性")]
    [Tooltip("施法期间禁止移动的时间（秒）")]
    public float castingTime = 0.5f; // 技能施法时间参数
    
    // ----------------- 动画属性 -----------------
    [Header("动画属性")]
    [Tooltip("技能动作预制体")]
    public GameObject actionPrefab;
    [Tooltip("技能触发动画名称")]
    public string triggerAnimationName;
    [Tooltip("动作位置偏移")]
    public Vector3 actionOffset = new Vector3(0, 0, 0);

    // 新增：获取最终冷却时间
    public float GetFinalCooldown(Unit.Faction casterFaction, float globalHeroCooldownMultiplier = 1f)
    {
        float finalCooldown = cooldownTime;
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(casterFaction);
            if (factionManager != null)
            {
                SkillModifiers skillMods = factionManager.GetSkillModifier(this);
                finalCooldown *= skillMods.cooldownMultiplier;
                // 英雄特有冷却强化也应用在这里
                finalCooldown *= globalHeroCooldownMultiplier;
            }
        }
        return finalCooldown;
    }

    // 新增：获取最终眩晕时间
    public float GetFinalStunDuration(Unit.Faction casterFaction)
    {
        float finalStunDuration = stunDuration;
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(casterFaction);
            if (factionManager != null)
            {
                SkillModifiers skillMods = factionManager.GetSkillModifier(this);
                finalStunDuration = (finalStunDuration + skillMods.stunDurationAdditive) * skillMods.stunDurationMultiplier;
            }
        }
        return finalStunDuration;
    }
    
    // 新增：获取最终投射物Y方向宽度（碰撞体宽度）
    public float GetFinalRangeY(Unit.Faction casterFaction)
    {
        float finalRangeY = rangeY;
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(casterFaction);
            if (factionManager != null)
            {
                SkillModifiers skillMods = factionManager.GetSkillModifier(this);
                finalRangeY += skillMods.rangeAdditive; // 只应用加法修改
            }
        }
        return finalRangeY;
    }
    
    // 新增：获取最终圆形区域半径
    public float GetFinalAreaRadius(Unit.Faction casterFaction)
    {
        // 对于圆形区域，areaSize.x表示半径
        float finalRadius = areaSize.x;
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(casterFaction);
            if (factionManager != null)
            {
                SkillModifiers skillMods = factionManager.GetSkillModifier(this);
                finalRadius += skillMods.areaSizeAdditive; // 使用areaSizeAdditive而不是rangeAdditive
            }
        }
        return finalRadius;
    }
}

/// <summary>
/// 技能行为类型枚举
/// </summary>
public enum SkillBehaviorType
{
    Projectile,  // 投射物（剑气、箭矢）
    AreaEffect,  // 区域效果（陨石、火球）
    Follow,      // 跟随效果（光环、护盾）
    DelayedDamageArea, // 延迟伤害区域
    DebuffArea, // 减益区域
    Heal, // 治疗
    Arrow, // 箭矢（一次伤害后消失）
    Charge, // 冲锋（快速接近目标）
}

/// <summary>
/// 技能效果类型枚举
/// </summary>
public enum SkillEffectType
{
    Damage,      // 伤害效果
    Heal,        // 治疗效果
    Stun         // 眩晕效果
}

/// <summary>
/// 区域形状枚举
/// </summary>
public enum EffectShape
{
    Circle,      // 圆形区域
    Rectangle,   // 矩形区域
    Sector       // 扇形区域
}

/// <summary>
/// 生成位置偏移类型
/// </summary>
public enum SpawnOffsetType
{
    None,        // 无偏移（以角色为中心点）
    Forward,     // 向角色朝向方向偏移
    Custom       // 自定义方向偏移（需要外部设置）
}