using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 延迟伤害区域行为 - 在区域内挂载伤害预制体，延迟后造成伤害
/// </summary>
public class DelayedDamageAreaBehavior : MonoBehaviour, ISkillBehavior
{
    private EffectShape areaShape;
    private Vector2 areaSize;
    private float duration;
    private float damageDelay;
    private float effectDuration;
    private GameObject damageEffectPrefab;
    private float damageAmount;
    private Vector3 effectOffset;
    
    // 效果应用器引用
    private SkillEffectApplier effectApplier;
    
    // 视觉组件引用
    private SpriteRenderer spriteRenderer;
    
    // 存储区域内已挂载伤害效果的单位和对应的特效实例
    private Dictionary<GameObject, GameObject> affectedUnits = new Dictionary<GameObject, GameObject>();
    
    // 施法者引用
    private Unit caster;
    
    /// <summary>
    /// 初始化延迟伤害区域行为
    /// </summary>
    public void Initialize(SkillData data, Transform owner)
    {
        // 获取施法者组件
        caster = owner.GetComponent<Unit>();
        if (caster == null)
        {
            //Debug.LogError("DelayedDamageAreaBehavior: 施法者没有Unit组件！", this);
            return;
        }
        
        // 获取组件引用
        effectApplier = GetComponent<SkillEffectApplier>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // 从技能数据中获取属性
        areaShape = data.areaShape;
        
        // 根据区域形状应用不同的强化
        if (areaShape == EffectShape.Circle)
        {
            // 对于圆形区域，应用范围强化
            float finalRadius = data.GetFinalAreaRadius(caster.faction);
            areaSize = new Vector2(finalRadius, data.areaSize.y);
        }
        else if (areaShape == EffectShape.Rectangle)
        {
            // 对于矩形区域，应用范围强化到X方向
            float finalWidth = data.areaSize.x;
            if (GlobalGameUpgrades.Instance != null)
            {
                FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(caster.faction);
                if (factionManager != null)
                {
                    SkillModifiers skillMods = factionManager.GetSkillModifier(data);
                    finalWidth += skillMods.areaSizeAdditive; // 使用areaSizeAdditive
                    //Debug.Log($"矩形区域宽度强化: 原始={data.areaSize.x}, 增加={skillMods.areaSizeAdditive}, 最终={finalWidth}");
                }
            }
            areaSize = new Vector2(finalWidth, data.areaSize.y);
        }
        else
        {
            // 对于其他形状，使用原始大小
            areaSize = data.areaSize;
        }
        
        duration = data.areaDuration;
        damageEffectPrefab = data.damageEffectPrefab;
        damageAmount = data.effectValue; // 从技能数据中获取伤害值
        damageDelay = data.delayTime; // 使用技能数据中的延迟时间
        effectDuration = data.areaDuration; // 使用区域持续时间作为特效持续时间
        effectOffset = data.effectOffset;
        
        // 根据拥有者的朝向调整特效方向
        AdjustDirectionBasedOnOwner(owner);
        
        // 调整特效大小以匹配区域范围
        AdjustEffectSize();
        
        // 立即检测并挂载伤害效果
        ApplyInitialEffects();
        
        // 设置延迟伤害
        Invoke("ApplyDelayedDamage", damageDelay);
        
        // 设置生命周期
        Destroy(gameObject, duration);
    }
    
    /// <summary>
    /// 根据拥有者的朝向调整特效方向
    /// </summary>
    private void AdjustDirectionBasedOnOwner(Transform owner)
    {
        if (owner == null) return;
        
        Vector2 facingDirection = DirectionHelper.GetFacingDirection(owner);
        bool isFacingLeft = facingDirection.x < 0;
        
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = isFacingLeft;
        }
    }
    
    /// <summary>
    /// 调整特效大小以匹配区域范围
    /// </summary>
    private void AdjustEffectSize()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;
        
        Vector2 originalSize = spriteRenderer.sprite.bounds.size;
        
        switch (areaShape)
        {
            case EffectShape.Circle:
                float targetDiameter = areaSize.x * 2;
                float scaleX = targetDiameter / originalSize.x;
                float scaleY = targetDiameter / originalSize.y;
                float uniformScale = Mathf.Max(scaleX, scaleY);
                transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
                break;
                
            case EffectShape.Rectangle:
                float scaleRectX = areaSize.x / originalSize.x;
                float scaleRectY = areaSize.y / originalSize.y;
                transform.localScale = new Vector3(scaleRectX, scaleRectY, 1f);
                break;
        }
    }
    
    /// <summary>
    /// 初始检测并挂载伤害效果
    /// </summary>
    private void ApplyInitialEffects()
    {
        if (effectApplier == null) return;
        
        // 检测区域内的所有单位
        Collider2D[] colliders = Physics2D.OverlapBoxAll(
            transform.position,
            areaSize,
            0f,
            LayerMask.GetMask("Unit")
        );
        
        foreach (Collider2D collider in colliders)
        {
            // 获取单位组件
            Unit targetUnit = collider.GetComponent<Unit>();
            if (targetUnit != null && targetUnit.IsEnemy(caster))
            {
                // 在单位身上挂载伤害效果预制体
                if (damageEffectPrefab != null)
                {
                    // 创建特效实例，并设置位置偏移
                    Vector3 effectPosition = collider.transform.position + effectOffset;
                    GameObject effectInstance = Instantiate(
                        damageEffectPrefab,
                        effectPosition,
                        Quaternion.identity
                    );
                    
                    // 将特效实例设置为敌方单位的子对象，使其跟随移动
                    effectInstance.transform.SetParent(collider.transform);
                    
                    // 如果是城堡，隐藏特效
                    if (!targetUnit.ShowDamageEffect)
                    {
                        effectInstance.SetActive(false);
                    }
                    
                    // 设置特效的生命周期为1秒
                    Destroy(effectInstance, effectDuration);
                    
                    // 存储单位和对应的特效实例
                    affectedUnits[collider.gameObject] = effectInstance;
                }
            }
        }
    }
    
    /// <summary>
    /// 应用延迟伤害
    /// </summary>
    private void ApplyDelayedDamage()
    {
        foreach (var pair in affectedUnits)
        {
            GameObject unitObj = pair.Key;
            
            if (unitObj != null)
            {
                // 对单位造成伤害
                Unit targetUnit = unitObj.GetComponent<Unit>();
                if (targetUnit != null && targetUnit.IsEnemy(caster))
                {
                    // 计算最终伤害值，应用技能特定强化和单位伤害倍率
                    float finalDamage = damageAmount;
                    if (GlobalGameUpgrades.Instance != null && caster != null)
                    {
                        FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(caster.faction);
                        if (factionManager != null)
                        {
                            // 1. 应用技能特定强化
                            SkillData skillData = null;
                            // 尝试从施法者的技能管理器中找到匹配的技能数据
                            CharacterSkillManager skillManager = caster.GetComponent<CharacterSkillManager>();
                            if (skillManager != null && skillManager.skills != null)
                            {
                                foreach (var skill in skillManager.skills)
                                {
                                    if (skill != null && skill.behaviorType == SkillBehaviorType.DelayedDamageArea && 
                                        Mathf.Approximately(skill.effectValue, damageAmount))
                                    {
                                        skillData = skill;
                                        break;
                                    }
                                }
                            }
                            
                            if (skillData != null)
                            {
                                SkillModifiers skillMods = factionManager.GetSkillModifier(skillData);
                                finalDamage = (damageAmount + skillMods.damageAdditive) * skillMods.damageMultiplier;
                                //Debug.Log($"[延迟伤害计算] 技能强化后: 基础={damageAmount}, 加成={skillMods.damageAdditive}, 倍率={skillMods.damageMultiplier}, 结果={finalDamage}");
                            }
                            
                            // 2. 应用单位类型的全局伤害强化 (只应用倍率)
                            UnitModifiers unitMods = factionManager.GetUnitModifier(caster.Type);
                            float beforeUnitMods = finalDamage;
                            finalDamage = finalDamage * unitMods.damageMultiplier;
                            
                            //Debug.Log($"[延迟伤害计算] {caster.name}({caster.Type}) -> {targetUnit.name}: 技能伤害={beforeUnitMods}, 单位倍率={unitMods.damageMultiplier}, 最终伤害={finalDamage}");
                        }
                    }
                    
                    // 造成伤害
                    targetUnit.TakeDamage(finalDamage);
                }
            }
        }
        
        // 清空字典
        affectedUnits.Clear();
    }
    
    /// <summary>
    /// 更新行为
    /// </summary>
    public void UpdateBehavior()
    {
        // 不需要每帧更新
    }
    
    /// <summary>
    /// 当对象被销毁时
    /// </summary>
    private void OnDestroy()
    {
        // 确保所有特效实例都被销毁
        foreach (var effectInstance in affectedUnits.Values)
        {
            if (effectInstance != null)
            {
                Destroy(effectInstance);
            }
        }
        affectedUnits.Clear();
    }
    
    private void OnDrawGizmosSelected()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        
        switch (areaShape)
        {
            case EffectShape.Circle:
                Gizmos.DrawSphere(transform.position, areaSize.x);
                break;
                
            case EffectShape.Rectangle:
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(
                    transform.position,
                    transform.rotation,
                    Vector3.one
                );
                Gizmos.matrix = rotationMatrix;
                Gizmos.DrawCube(Vector3.zero, new Vector3(areaSize.x, areaSize.y, 0.1f));
                Gizmos.matrix = Matrix4x4.identity;
                break;
        }
        #endif
    }
} 