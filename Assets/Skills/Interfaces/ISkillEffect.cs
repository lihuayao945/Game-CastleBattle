using UnityEngine;

/// <summary>
/// 技能效果接口 - 定义技能对目标产生的效果（伤害、治疗等）
/// </summary>
public interface ISkillEffect
{
    /// <summary>
    /// 初始化效果
    /// </summary>
    /// <param name="data">技能数据</param>
    /// <param name="caster">施法者</param>
    void Initialize(SkillData data, Unit caster);
    
    /// <summary>
    /// 对目标应用效果
    /// </summary>
    /// <param name="target">效果目标</param>
    void ApplyEffect(GameObject target);
    
    /// <summary>
    /// 判断是否可以应用效果
    /// </summary>
    /// <param name="target">目标单位</param>
    /// <param name="caster">施法者</param>
    /// <returns>是否可以应用效果</returns>
    bool CanApplyEffect(Unit target, Unit caster);
    
    /// <summary>
    /// 获取效果应该作用的目标层（敌人、友军等）
    /// </summary>
    LayerMask GetTargetLayer();
} 