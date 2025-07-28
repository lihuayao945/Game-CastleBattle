using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 跟随行为 - 实现跟随目标移动的技能（如光环、护盾等）
/// </summary>
public class FollowBehavior : MonoBehaviour, ISkillBehavior
{
    private Transform target;
    private Vector3 offset;
    private float duration;
    
    // 效果应用器引用
    private SkillEffectApplier effectApplier;
    
    // 光环效果参数
    private float auraRadius;
    private float effectInterval = 1.0f;
    private float nextEffectTime = 0f;
    
    // 光环视觉组件
    private SpriteRenderer spriteRenderer;
    private Transform visualTransform;
    
    // 施法者引用
    private Unit caster;
    
    // 调试用
    [SerializeField] private bool debugMode = false;
    
    /// <summary>
    /// 初始化跟随行为
    /// </summary>
    public void Initialize(SkillData data, Transform owner)
    {
        // 获取施法者组件
        caster = owner.GetComponent<Unit>();
        if (caster == null)
        {
            //Debug.LogError("FollowBehavior: 施法者没有Unit组件！", this);
            return;
        }
        
        // 设置目标和偏移
        target = owner;
        offset = data.followOffset;
        
        // 获取组件引用
        effectApplier = GetComponent<SkillEffectApplier>();
        if (effectApplier == null)
        {
            //Debug.LogWarning("[FollowBehavior] 找不到SkillEffectApplier组件，尝试添加");
            effectApplier = gameObject.AddComponent<SkillEffectApplier>();
            
            // 创建并设置效果组件
            if (data.effectType == SkillEffectType.Heal)
            {
                HealEffect healEffect = gameObject.AddComponent<HealEffect>();
                healEffect.Initialize(data, caster);
                effectApplier.SetEffect(healEffect);
            }
            else if (data.effectType == SkillEffectType.Damage)
            {
                DamageEffect damageEffect = gameObject.AddComponent<DamageEffect>();
                damageEffect.Initialize(data, caster);
                effectApplier.SetEffect(damageEffect);
            }
        }
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        visualTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
        
        // 从技能数据中获取属性
        duration = data.areaDuration; // 使用区域持续时间作为光环持续时间
        auraRadius = data.auraRadius;
        effectInterval = data.effectInterval;
        
        // 调整光环视觉大小与实际治愈范围一致
        AdjustVisualSize();
        
        // 立即更新位置
        UpdatePosition();
        
        // 设置生命周期
        if (duration > 0)
        {
            Destroy(gameObject, duration);
        }
    }
    
    /// <summary>
    /// 调整光环视觉大小，使其与实际治愈范围一致
    /// </summary>
    private void AdjustVisualSize()
    {
        if (spriteRenderer != null)
        {
            // 获取精灵原始尺寸
            Vector2 originalSize = spriteRenderer.sprite.bounds.size;
            
            // 计算缩放比例（使直径等于2*auraRadius）
            float scaleX = (2 * auraRadius) / originalSize.x;
            float scaleY = (2 * auraRadius) / originalSize.y;
            
            // 应用缩放
            visualTransform.localScale = new Vector3(scaleX, scaleY, 1f);
            
            // 如果是圆形光环，可以确保XY缩放相同，保持圆形
            if (Mathf.Abs(originalSize.x - originalSize.y) < 0.01f)
            {
                float uniformScale = Mathf.Max(scaleX, scaleY);
                visualTransform.localScale = new Vector3(uniformScale, uniformScale, 1f);
            }
            
            //Debug.Log($"光环视觉大小已调整: 半径={auraRadius}, 缩放={visualTransform.localScale}, 原始尺寸={originalSize}");
            
            // 检查材质设置
            if (spriteRenderer.material == null || spriteRenderer.material.shader == null)
            {
                //Debug.LogWarning("光环材质或着色器为空，可能导致紫色问题");
            }
            else
            {
                //Debug.Log($"光环材质: {spriteRenderer.material.name}, 着色器: {spriteRenderer.material.shader.name}");
            }
        }
        else
        {
            //Debug.LogWarning("光环预制体缺少SpriteRenderer组件，无法调整视觉大小");
        }
    }
    
    /// <summary>
    /// 更新跟随行为
    /// </summary>
    public void UpdateBehavior()
    {
        // 更新位置
        UpdatePosition();
        
        // 按间隔应用光环效果
        if (Time.time >= nextEffectTime)
        {
            ApplyAuraEffect();
            nextEffectTime = Time.time + effectInterval;
        }
    }
    
    /// <summary>
    /// 更新位置到目标位置
    /// </summary>
    private void UpdatePosition()
    {
        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }
    
    /// <summary>
    /// 应用光环效果
    /// </summary>
    private void ApplyAuraEffect()
    {
        if (effectApplier == null)
        {
            //Debug.LogWarning("[FollowBehavior] effectApplier为空，尝试重新获取");
            effectApplier = GetComponent<SkillEffectApplier>();
            
            if (effectApplier == null) return;
        }
        
        // 在圆形区域内应用效果
        effectApplier.DetectAndApplyEffectsInCircle(transform.position, auraRadius);
    }
    
    // 已删除GetAlliesInRange方法 - 未被使用的死代码，包含性能问题的FindObjectsOfType调用
    
    private void OnDrawGizmosSelected()
    {
        #if UNITY_EDITOR
        // 绘制光环范围（绿色半透明）
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawSphere(transform.position, auraRadius);
        #endif
    }
    
    private void OnGUI()
    {
        if (debugMode && spriteRenderer != null)
        {
            // 将世界坐标转换为屏幕坐标
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            
            // 绘制调试信息
            GUI.Label(new Rect(screenPos.x - 100, Screen.height - screenPos.y + 20, 200, 100), 
                $"光环半径: {auraRadius}\n" +
                $"精灵尺寸: {spriteRenderer.sprite.bounds.size}\n" +
                $"缩放: {visualTransform.localScale}\n" +
                $"材质: {spriteRenderer.material.name}");
        }
    }
} 