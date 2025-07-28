using UnityEngine;

/// <summary>
/// 伤害效果 - 对敌人造成伤害的效果
/// </summary>
public class DamageEffect : MonoBehaviour, ISkillEffect
{
    // 伤害值
    private float damageAmount = 10f;
    
    // 伤害间隔（防止同一目标在短时间内被多次伤害）
    private float damageInterval = 0.5f;
    
    // 伤害特效预制体
    private GameObject damageEffectPrefab;
    
    // 特效持续时间
    private float effectDuration = 0.5f;
    
    // 施法者引用
    private Unit caster;

    // 技能数据引用
    private SkillData skillData; 

    /// <summary>
    /// 初始化伤害效果
    /// </summary>
    public void Initialize(SkillData data, Unit caster)
    {
        // 保存施法者引用
        this.caster = caster;
        // 保存技能数据引用
        this.skillData = data;
        
        // 从技能数据中获取伤害值
        damageAmount = data.effectValue;
        damageInterval = data.effectInterval;
        effectDuration = data.areaDuration; // 从技能数据中获取特效持续时间
        
        // 从技能数据中加载特效预制体
        damageEffectPrefab = data.damageEffectPrefab;
    }
    
    /// <summary>
    /// 判断是否可以应用效果
    /// </summary>
    public bool CanApplyEffect(Unit target, Unit caster)
    {
        return target != null && target.IsEnemy(caster);
    }
    
    /// <summary>
    /// 对目标应用伤害效果
    /// </summary>
    public void ApplyEffect(GameObject target)
    {
        // 获取目标单位组件
        Unit targetUnit = target.GetComponent<Unit>();
        if (targetUnit != null && CanApplyEffect(targetUnit, caster))
        {
            // 获取技能强化值和单位伤害强化值
            float finalDamage = damageAmount;
            if (GlobalGameUpgrades.Instance != null && caster != null)
            {
                FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(caster.faction);
                if (factionManager != null)
                {
                    // 1. 应用技能特定强化
                    if (skillData != null)
                    {
                        SkillModifiers skillMods = factionManager.GetSkillModifier(skillData);
                        finalDamage = (damageAmount + skillMods.damageAdditive) * skillMods.damageMultiplier;
                        //Debug.Log($"[伤害计算] 技能强化后: 基础={damageAmount}, 加成={skillMods.damageAdditive}, 倍率={skillMods.damageMultiplier}, 结果={finalDamage}");
                    }
                    
                    // 2. 应用单位类型的全局伤害强化 (只应用倍率)
                    UnitModifiers unitMods = factionManager.GetUnitModifier(caster.Type);
                    float beforeUnitMods = finalDamage;
                    finalDamage = (finalDamage ) * unitMods.damageMultiplier;
                    
                    //Debug.Log($"[伤害计算] {caster.name}({caster.Type}) -> {targetUnit.name}: 技能伤害={beforeUnitMods}, 单位倍率={unitMods.damageMultiplier}, 最终伤害={finalDamage}");
                }
            }

            // 造成伤害
            //Debug.Log($"[伤害应用] 对 {targetUnit.name} 造成伤害: {finalDamage} (防御前)");
            targetUnit.TakeDamage(finalDamage);
            
            // 创建特效，但如果是城堡则不显示
            if (damageEffectPrefab != null && targetUnit.ShowDamageEffect)
            {
                // 在目标位置创建特效
                GameObject effect = Instantiate(damageEffectPrefab, target.transform.position, Quaternion.identity);
                
                // 使用从技能数据中获取的持续时间
                Destroy(effect, effectDuration);
            }
        }
    }
    
    /// <summary>
    /// 获取伤害效果的目标层（敌人层）
    /// </summary>
    public LayerMask GetTargetLayer()
    {
        return LayerMask.GetMask("Enemy");
    }
} 