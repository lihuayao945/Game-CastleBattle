using UnityEngine;

/// <summary>
/// 技能工厂 - 负责创建技能实例
/// </summary>
public static class SkillFactory
{
    /// <summary>
    /// 创建技能实例
    /// </summary>
    /// <param name="data">技能数据</param>
    /// <param name="owner">技能拥有者</param>
    /// <param name="position">技能生成位置</param>
    /// <returns>技能游戏对象</returns>
    public static GameObject CreateSkill(SkillData data, Transform owner, Vector3 position)
    {
        // 创建技能预制体实例
        GameObject skillObject = Object.Instantiate(data.effectPrefab, position, Quaternion.identity);
        
        // 获取或添加技能控制器
        SkillController controller = skillObject.GetComponent<SkillController>();
        if (controller == null)
        {
            controller = skillObject.AddComponent<SkillController>();
        }
        
        // 初始化技能（包括朝向调整）
        controller.Initialize(data, owner);

        // 自动添加渲染优化支持
        if (skillObject.GetComponent<MagicBattle.EffectWrapper>() == null)
        {
            skillObject.AddComponent<MagicBattle.EffectWrapper>();
        }

        return skillObject;
    }
} 