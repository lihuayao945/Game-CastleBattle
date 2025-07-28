using UnityEngine;
using System;

/// <summary>
/// 存储一个特定单位类型通用属性的累积强化值
/// </summary>
public class UnitModifiers
{
    public float healthAdditive;
    public float healthMultiplier;
    public float damageAdditive;
    public float damageMultiplier;
    public float moveSpeedAdditive;
    public float moveSpeedMultiplier;
    public float defenseAdditive;
    public float defenseMultiplier;
    public float detectionRangeAdditive; // 例如，弓箭手、牧师等有检测范围的单位
    public float detectionRangeMultiplier;

    // 构造函数，初始化为默认值 (无强化)
    public UnitModifiers(
        float defaultHealthMult = 1f,
        float defaultDamageMult = 1f,
        float defaultMoveSpeedMult = 1f,
        float defaultDefenseMult = 1f,
        float defaultDetectionRangeMult = 1f)
    {
        // 加法修改器默认为0
        healthAdditive = 0f;
        damageAdditive = 0f;
        moveSpeedAdditive = 0f;
        defenseAdditive = 0f;
        detectionRangeAdditive = 0f;

        // 乘法修改器默认为传入的值（默认为1）
        healthMultiplier = defaultHealthMult;
        damageMultiplier = defaultDamageMult;
        moveSpeedMultiplier = defaultMoveSpeedMult;
        defenseMultiplier = defaultDefenseMult;
        detectionRangeMultiplier = defaultDetectionRangeMult;
    }
} 