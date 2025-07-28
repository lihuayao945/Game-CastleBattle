using UnityEngine;

/// <summary>
/// 治疗效果 - 对友军进行治疗的效果
/// </summary>
public class HealEffect : MonoBehaviour, ISkillEffect
{
    // 治疗值
    private float healAmount = 5f;
    
    // 治疗间隔（防止同一目标在短时间内被多次治疗）
    private float healInterval = 1.0f;
    
    // 治疗特效预制体
    private GameObject healEffectPrefab;
    
    // 施法者引用
    private Unit caster;

    // 技能数据引用
    private SkillData skillData;
    
    /// <summary>
    /// 初始化治疗效果
    /// </summary>
    public void Initialize(SkillData data, Unit caster)
    {
        // 保存施法者引用
        this.caster = caster;
        // 保存技能数据引用
        this.skillData = data;
        
        // 从技能数据中获取治疗值
        healAmount = data.effectValue;
        healInterval = data.effectInterval;
        
        // 从技能数据中加载特效预制体
        healEffectPrefab = data.healEffectPrefab;
    }
    
    /// <summary>
    /// 判断是否可以应用效果
    /// </summary>
    public bool CanApplyEffect(Unit target, Unit caster)
    {
        return target != null && target.IsAlly(caster);
    }
    
    /// <summary>
    /// 对目标应用治疗效果
    /// </summary>
    public void ApplyEffect(GameObject target)
    {
        // 获取目标单位组件
        Unit targetUnit = target.GetComponent<Unit>();
        if (targetUnit != null && CanApplyEffect(targetUnit, caster))
        {
            // 获取技能强化值
            float finalHealAmount = healAmount;
            if (GlobalGameUpgrades.Instance != null && skillData != null && caster != null)
            {
                FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(caster.faction);
                if (factionManager != null)
                {
                    SkillModifiers skillMods = factionManager.GetSkillModifier(skillData);
                    finalHealAmount = (healAmount + skillMods.healAdditive) * skillMods.healMultiplier;
                }
            }

            // 进行治疗
            targetUnit.Heal(finalHealAmount);
            
            // 创建治疗特效
            if (healEffectPrefab != null)
            {
                // 在目标位置创建特效，并设置为目标的子对象
                GameObject effect = Instantiate(healEffectPrefab, target.transform.position, Quaternion.identity);
                effect.transform.SetParent(target.transform);
                
                // 特效持续时间
                Destroy(effect, 1.0f);
            }
        }
    }
    
    /// <summary>
    /// 获取治疗效果的目标层（友军层）
    /// </summary>
    public LayerMask GetTargetLayer()
    {
        return LayerMask.GetMask("Ally");
    }
} 