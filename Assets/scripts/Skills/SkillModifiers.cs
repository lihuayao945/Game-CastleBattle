using UnityEngine;
using System;

/// <summary>
/// 存储一个特定技能的累积强化值
/// </summary>
[Serializable]
public class SkillModifiers
{
    public float damageAdditive;        // 伤害加法值
    public float damageMultiplier;      // 伤害乘法值 (例如 1.1 代表 +10%)
    public float cooldownMultiplier;    // 冷却时间乘法值 (例如 0.9 代表 -10%)
    public float rangeAdditive;         // 射程/范围加法值
    public float rangeMultiplier;       // 射程/范围乘法值
    public float areaSizeAdditive;      // 区域大小加法值
    public float healAdditive;          // 治疗量加法值
    public float healMultiplier;        // 治疗量乘法值
    public float stunDurationAdditive;  // 眩晕时间加法值
    public float stunDurationMultiplier; // 眩晕时间乘法值

    // 构造函数，初始化为默认值 (无强化)
    public SkillModifiers(float defaultDamageMult = 1f, float defaultCooldownMult = 1f, float defaultHealMult = 1f, float defaultStunDurMult = 1f, float defaultRangeMult = 1f)
    {
        damageAdditive = 0f;
        damageMultiplier = defaultDamageMult;
        cooldownMultiplier = defaultCooldownMult;
        rangeAdditive = 0f;
        rangeMultiplier = defaultRangeMult;
        areaSizeAdditive = 0f;
        healAdditive = 0f;
        healMultiplier = defaultHealMult;
        stunDurationAdditive = 0f;
        stunDurationMultiplier = defaultStunDurMult;
    }
} 