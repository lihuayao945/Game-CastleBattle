using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 技能效果应用器 - 负责检测目标并应用技能效果
/// </summary>
public class SkillEffectApplier : MonoBehaviour
{
    // 效果组件
    private ISkillEffect effect;
    
    // 效果应用间隔
    private float effectInterval = 0.5f;
    
    // 已经应用效果的目标及其上次应用时间
    private Dictionary<GameObject, float> lastEffectTimes = new Dictionary<GameObject, float>();
    
    // 调试用
    [SerializeField] private bool debugMode = false;
    
    /// <summary>
    /// 设置效果组件
    /// </summary>
    public void SetEffect(ISkillEffect skillEffect)
    {
        effect = skillEffect;
        if (effect == null)
        {
            //Debug.LogError("SetEffect: 效果为空！", this);
        }
        else{

        }
        //Debug.Log($"SetEffect: 设置效果 {effect.GetType().Name}", this);
    }
    
    /// <summary>
    /// 在矩形区域内检测并应用效果
    /// </summary>
    public void DetectAndApplyEffects(Vector2 center, Vector2 size, float angle)
    {
        if (effect == null) 
        {
            if (debugMode) //Debug.LogWarning("DetectAndApplyEffects: 效果为空！", this);
            return;
        }
        
        // 检测区域内的所有单位
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, angle, LayerMask.GetMask("Unit"));
        //if (debugMode) //Debug.Log($"DetectAndApplyEffects: 检测到 {hits.Length} 个目标", this);
        
        float now = Time.time;
        foreach (Collider2D hit in hits)
        {
            GameObject target = hit.gameObject;
            Unit targetUnit = target.GetComponent<Unit>();
            
            if (targetUnit != null)
            {
                //if (debugMode) //Debug.Log($"检测到目标: {target.name}, Faction = {targetUnit.GetFaction()}", this);
                
                // 检查是否可以应用效果（间隔时间）
                if (!lastEffectTimes.TryGetValue(target, out float lastTime) || now - lastTime >= effectInterval)
                {
                    // 应用效果
                    effect.ApplyEffect(target);
                    
                    // 更新上次应用时间
                    lastEffectTimes[target] = now;
                }
            }
        }
    }
    
    /// <summary>
    /// 在圆形区域内检测并应用效果
    /// </summary>
    public void DetectAndApplyEffectsInCircle(Vector2 center, float radius)
    {
        if (effect == null) return;
        
        // 检测区域内的所有单位
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, LayerMask.GetMask("Unit"));
        
        float now = Time.time;
        foreach (Collider2D hit in hits)
        {
            GameObject target = hit.gameObject;
            Unit targetUnit = target.GetComponent<Unit>();
            
            if (targetUnit != null)
            {
                // 检查是否可以应用效果（间隔时间）
                if (!lastEffectTimes.TryGetValue(target, out float lastTime) || now - lastTime >= effectInterval)
                {
                    // 应用效果
                    effect.ApplyEffect(target);
                    
                    // 更新上次应用时间
                    lastEffectTimes[target] = now;
                }
            }
        }
    }
    
    /// <summary>
    /// 设置效果应用间隔
    /// </summary>
    public void SetEffectInterval(float interval)
    {
        effectInterval = interval;
    }
    
    /// <summary>
    /// 获取当前效果组件
    /// </summary>
    public ISkillEffect GetEffect()
    {
        return effect;
    }
    
    private void OnDrawGizmos()
    {
        if (!debugMode || effect == null) return;
        
        // 绘制检测范围
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, 1.0f);
    }
} 