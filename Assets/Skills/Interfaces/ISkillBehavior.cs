using UnityEngine;

/// <summary>
/// 技能行为接口 - 定义技能如何在场景中表现和移动
/// </summary>
public interface ISkillBehavior
{
    /// <summary>
    /// 初始化技能行为
    /// </summary>
    /// <param name="data">技能数据</param>
    /// <param name="owner">技能拥有者</param>
    void Initialize(SkillData data, Transform owner);
    
    /// <summary>
    /// 更新技能行为（由MonoBehaviour的Update调用）
    /// </summary>
    void UpdateBehavior();
} 