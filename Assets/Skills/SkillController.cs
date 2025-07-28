using UnityEngine;

/// <summary>
/// 技能控制器基类 - 管理技能的行为和效果
/// </summary>
public class SkillController : MonoBehaviour
{
    // 技能行为组件
    protected ISkillBehavior behavior;
    
    // 技能效果组件
    protected ISkillEffect effect;
    
    // 施法者引用
    protected Unit caster;
    
    /// <summary>
    /// 初始化技能
    /// </summary>
    public virtual void Initialize(SkillData data, Transform owner)
    {
        // 获取施法者组件
        caster = owner.GetComponent<Unit>();
        if (caster == null)
        {
            //Debug.LogError("SkillController: 施法者没有Unit组件！", this);
            return;
        }
        
        // 创建并初始化效果
        CreateEffect(data);
        
        // 设置效果应用器（在创建行为之前）
        SetupEffectApplier(data);
        
        // 创建并初始化行为
        CreateBehavior(data, owner);
    }
    
    /// <summary>
    /// 创建技能效果
    /// </summary>
    protected virtual void CreateEffect(SkillData data)
    {
        // 根据技能效果类型创建对应的效果组件
        switch (data.effectType)
        {
            case SkillEffectType.Damage:
                effect = gameObject.AddComponent<DamageEffect>();
                break;
                
            case SkillEffectType.Heal:
                effect = gameObject.AddComponent<HealEffect>();
                break;
                
            case SkillEffectType.Stun:
                effect = gameObject.AddComponent<StunEffect>();
                break;
                
            // 可以添加更多效果类型
        }
        
        // 初始化效果
        if (effect != null)
        {
            effect.Initialize(data, caster);
        }
    }
    
    /// <summary>
    /// 创建技能行为
    /// </summary>
    protected virtual void CreateBehavior(SkillData data, Transform owner)
    {
        // 根据技能行为类型创建对应的行为组件
        switch (data.behaviorType)
        {
            case SkillBehaviorType.Projectile:
                behavior = gameObject.AddComponent<ProjectileBehavior>();
                break;
                
            case SkillBehaviorType.Arrow:
                behavior = gameObject.AddComponent<ArrowBehavior>();
                break;
                
            case SkillBehaviorType.AreaEffect:
                // 如果有动作预制体，则创建雷电技能行为
                if (data.actionPrefab != null && !string.IsNullOrEmpty(data.triggerAnimationName))
                {
                    // 先添加区域效果行为并初始化
                    AreaEffectBehavior areaEffect = gameObject.AddComponent<AreaEffectBehavior>();
                    areaEffect.Initialize(data, owner);
                    
                    // 再添加雷电技能行为作为主要行为
                    behavior = gameObject.AddComponent<ThunderSkillBehavior>();
                }
                else
                {
                    behavior = gameObject.AddComponent<AreaEffectBehavior>();
                }
                break;
                
            case SkillBehaviorType.Follow:
                behavior = gameObject.AddComponent<FollowBehavior>();
                break;
                
            case SkillBehaviorType.DelayedDamageArea:
                behavior = gameObject.AddComponent<DelayedDamageAreaBehavior>();
                break;
                
            case SkillBehaviorType.DebuffArea:
                behavior = gameObject.AddComponent<DebuffAreaBehavior>();
                break;

            case SkillBehaviorType.Charge:
                behavior = gameObject.AddComponent<ChargeBehavior>();
                break;
                
            // 可以添加更多行为类型
        }
        
        // 初始化行为
        if (behavior != null)
        {
            behavior.Initialize(data, owner);
            
            // 如果是冲锋行为，立即开始冲锋
            if (behavior is ChargeBehavior chargeBehavior)
            {
                //Debug.Log("检测到冲锋行为，准备开始冲锋");
                // 获取朝向方向
                Vector2 direction = DirectionHelper.GetFacingDirection(owner);
                chargeBehavior.StartCharge(direction);
            }
        }
    }
    
    /// <summary>
    /// 设置效果应用器
    /// </summary>
    protected virtual void SetupEffectApplier(SkillData data)
    {
        if (effect == null) return;
        
        // 添加效果应用器组件
        SkillEffectApplier applier = gameObject.AddComponent<SkillEffectApplier>();
        
        // 设置效果
        applier.SetEffect(effect);
        
        // 设置效果应用间隔
        applier.SetEffectInterval(data.effectInterval);
    }
    
    /// <summary>
    /// 更新技能
    /// </summary>
    protected virtual void Update()
    {
        // 更新行为
        if (behavior != null)
        {
            behavior.UpdateBehavior();
        }
    }
} 