using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 眩晕效果 - 使敌人眩晕一段时间
/// </summary>
public class StunEffect : MonoBehaviour, ISkillEffect
{
    // 眩晕持续时间
    private float stunDuration = 2f;
    
    // 伤害值
    private float damageAmount = 10f;
    
    // 眩晕特效预制体
    private GameObject stunEffectPrefab;
    
    // 已受伤害的目标字典，用于防止重复伤害
    private Dictionary<GameObject, float> damagedTargets = new Dictionary<GameObject, float>();
    
    // 伤害间隔时间
    private float damageInterval = 0.5f;
    
    // 施法者引用
    private Unit caster;
    
    // 技能数据引用
    private SkillData skillData;
    
    /// <summary>
    /// 初始化眩晕效果
    /// </summary>
    public void Initialize(SkillData data, Unit caster)
    {
        // 保存施法者引用
        this.caster = caster;
        // 保存技能数据引用
        this.skillData = data;
        
        // 从技能数据中获取眩晕持续时间和伤害值
        stunDuration = data.stunDuration;
        damageAmount = data.effectValue;
        damageInterval = data.effectInterval;
        
        // 从技能数据中加载特效预制体
        stunEffectPrefab = data.stunEffectPrefab;
    }
    
    /// <summary>
    /// 判断是否可以应用效果
    /// </summary>
    public bool CanApplyEffect(Unit target, Unit caster)
    {
        return target != null && target.IsEnemy(caster);
    }
    
    /// <summary>
    /// 对目标应用眩晕效果
    /// </summary>
    public void ApplyEffect(GameObject target)
    {
        // 获取目标单位组件
        Unit targetUnit = target.GetComponent<Unit>();
        if (targetUnit != null && CanApplyEffect(targetUnit, caster))
        {
            //Debug.Log($"[StunEffect] Attempting to apply effect on {target.name}. Caster: {caster.name}");
            
            // 检查是否可以造成伤害
            if (CanDamageTarget(target))
            {
                // 计算最终伤害值，应用单位伤害倍率
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
                        }
                        
                        // 2. 应用单位类型的全局伤害强化 (只应用倍率)
                        UnitModifiers unitMods = factionManager.GetUnitModifier(caster.Type);
                        finalDamage = finalDamage * unitMods.damageMultiplier;
                    }
                }
                
                // 造成伤害
                targetUnit.TakeDamage(finalDamage);
                
                // 记录伤害时间
                damagedTargets[target] = Time.time;
            }
            
            // 获取技能强化值
            float finalStunDuration = stunDuration;
            if (GlobalGameUpgrades.Instance != null && skillData != null && caster != null)
            {
                FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(caster.faction);
                if (factionManager != null)
                {
                    SkillModifiers skillMods = factionManager.GetSkillModifier(skillData);
                    finalStunDuration = (stunDuration + skillMods.stunDurationAdditive) * skillMods.stunDurationMultiplier;
                }
            }
            
            //Debug.Log($"[StunEffect] Final Stun Duration: {finalStunDuration} on {target.name}");

            // 应用眩晕
            targetUnit.Stun(finalStunDuration);
            
            //Debug.Log($"[StunEffect] stunEffectPrefab is {(stunEffectPrefab == null ? "NULL" : "NOT NULL")}. " +
            //    $"targetUnit.ShowDamageEffect is {targetUnit.ShowDamageEffect} for {target.name}.");
                      
            // 创建特效
            if (stunEffectPrefab != null && targetUnit.ShowDamageEffect)
            {
                // 计算特效位置（在目标头上方）
                Vector3 effectPosition = target.transform.position + new Vector3(0, 0.5f, 0);
                
                // 创建特效，并设置为目标的子对象
                GameObject effect = Instantiate(stunEffectPrefab, effectPosition, Quaternion.identity);
                effect.transform.SetParent(target.transform);
                
                // 特效持续时间
                Destroy(effect, finalStunDuration);
                
                //Debug.Log($"[StunEffect] Stun visual effect instantiated on {target.name}.");
            }
            else
            {
                //Debug.LogWarning($"[StunEffect] Did not instantiate stun visual effect on {target.name}. " +
                //    "stunEffectPrefab is null.");
            }
        }
    }
    
    /// <summary>
    /// 检查是否可以对目标造成伤害
    /// </summary>
    private bool CanDamageTarget(GameObject target)
    {
        // 如果目标不在字典中，可以造成伤害
        if (!damagedTargets.ContainsKey(target))
        {
            return true;
        }
        
        // 检查是否已经过了伤害间隔时间
        float lastDamageTime = damagedTargets[target];
        if (Time.time - lastDamageTime >= damageInterval)
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 获取眩晕效果的目标层（敌人层）
    /// </summary>
    public LayerMask GetTargetLayer()
    {
        return LayerMask.GetMask("Enemy");
    }
} 