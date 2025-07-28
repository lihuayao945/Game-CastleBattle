using UnityEngine;

/// <summary>
/// 区域效果行为 - 实现在固定区域内产生效果的技能（如火球、陨石等）
/// </summary>
public class AreaEffectBehavior : MonoBehaviour, ISkillBehavior
{
    private EffectShape areaShape;
    
    // 区域大小，公开属性以便其他类访问
    public Vector2 areaSize { get; private set; }
    
    private float duration;
    
    // 效果应用器引用
    private SkillEffectApplier effectApplier;
    
    // 效果应用间隔
    private float effectInterval = 0.5f;
    private float nextEffectTime = 0f;
    
    // 视觉组件引用
    private SpriteRenderer spriteRenderer;
    
    // 施法者引用
    private Unit caster;
    
    /// <summary>
    /// 初始化区域效果行为
    /// </summary>
    public void Initialize(SkillData data, Transform owner)
    {
        // 获取施法者组件
        caster = owner.GetComponent<Unit>();
        if (caster == null)
        {
            //Debug.LogError("AreaEffectBehavior: 施法者没有Unit组件！", this);
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
                    //Debug.Log($"矩形区域宽度强化: 原始={data.areaSize.x}, 加成={skillMods.areaSizeAdditive}, 最终={finalWidth}");
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
        effectInterval = data.effectInterval;
        
        // 调试日志
        //Debug.Log($"初始化区域效果行为: {gameObject.name}, 拥有者: {(owner != null ? owner.name : "无")}, 朝向: {(owner != null ? DirectionHelper.GetFacingDirection(owner) : Vector2.zero)}");
        
        // 根据拥有者的朝向调整特效方向
        AdjustDirectionBasedOnOwner(owner);
        
        // 调整特效大小以匹配区域范围
        AdjustEffectSize();
        
        // 设置生命周期
        Destroy(gameObject, duration);
    }
    
    /// <summary>
    /// 根据拥有者的朝向调整特效方向
    /// </summary>
    private void AdjustDirectionBasedOnOwner(Transform owner)
    {
        if (owner == null) return;
        
        // 获取拥有者的朝向
        Vector2 facingDirection = DirectionHelper.GetFacingDirection(owner);
        bool isFacingLeft = facingDirection.x < 0;
        
        // 使用flipX来翻转精灵，参考ProjectileBehavior的处理方式
        if (spriteRenderer != null)
        {
            // 直接设置flipX属性，而不是翻转整个对象
            spriteRenderer.flipX = isFacingLeft;
            //Debug.Log($"特效已根据角色朝向调整方向: {(isFacingLeft ? "面向左侧" : "面向右侧")}");
        }
        
        // 对于矩形区域，调整碰撞盒的方向
        if (areaShape == EffectShape.Rectangle)
        {
            // 获取碰撞器
            BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider != null)
            {
                // 如果面向左侧，需要调整碰撞盒的偏移
                if (isFacingLeft)
                {
                    // 如果碰撞盒有偏移，需要翻转偏移方向
                    if (boxCollider.offset.x != 0)
                    {
                        Vector2 offset = boxCollider.offset;
                        offset.x *= -1;
                        boxCollider.offset = offset;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 调整特效大小以匹配区域范围
    /// </summary>
    private void AdjustEffectSize()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;
        
        // 获取特效精灵的原始大小
        Vector2 originalSize = spriteRenderer.sprite.bounds.size;
        
        switch (areaShape)
        {
            case EffectShape.Circle:
                // 计算缩放比例（使直径等于2*半径）
                float targetDiameter = areaSize.x * 2;
                float scaleX = targetDiameter / originalSize.x;
                float scaleY = targetDiameter / originalSize.y;
                
                // 使用相同的缩放比例保持圆形
                float uniformScale = Mathf.Max(scaleX, scaleY);
                
                // 应用缩放
                transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
                
                //Debug.Log($"调整圆形特效大小: 原始大小={originalSize}, 目标直径={targetDiameter}, 缩放比例={uniformScale}");
                break;
                
            case EffectShape.Rectangle:
                // 计算缩放比例
                float scaleRectX = areaSize.x / originalSize.x;
                float scaleRectY = areaSize.y / originalSize.y;
                
                // 应用缩放
                transform.localScale = new Vector3(scaleRectX, scaleRectY, 1f);
                
                //Debug.Log($"调整矩形特效大小: 原始大小={originalSize}, 目标大小={areaSize}, 缩放比例=({scaleRectX}, {scaleRectY})");
                break;
        }
    }
    
    /// <summary>
    /// 更新区域效果行为
    /// </summary>
    public void UpdateBehavior()
    {
        // 按间隔应用效果
        if (Time.time >= nextEffectTime)
        {
            ApplyAreaEffect();
            nextEffectTime = Time.time + effectInterval;
        }
    }
    
    /// <summary>
    /// 应用区域效果
    /// </summary>
    private void ApplyAreaEffect()
    {
        if (effectApplier == null) return;
        
        switch (areaShape)
        {
            case EffectShape.Circle:
                effectApplier.DetectAndApplyEffectsInCircle(transform.position, areaSize.x);
                break;
                
            case EffectShape.Rectangle:
                // 获取当前旋转角度
                float angle = transform.eulerAngles.z;
                effectApplier.DetectAndApplyEffects(transform.position, areaSize, angle);
                break;
                
            case EffectShape.Sector:
                // 扇形区域检测逻辑（可以根据需要实现）
                break;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        
        // 根据区域形状绘制不同的Gizmo
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        
        switch (areaShape)
        {
            case EffectShape.Circle:
                Gizmos.DrawSphere(transform.position, areaSize.x);
                break;
                
            case EffectShape.Rectangle:
                // 应用当前旋转
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(
                    transform.position,
                    transform.rotation,
                    Vector3.one
                );
                Gizmos.matrix = rotationMatrix;
                
                // 绘制矩形
                Gizmos.DrawCube(Vector3.zero, new Vector3(areaSize.x, areaSize.y, 0.1f));
                
                // 重置矩阵
                Gizmos.matrix = Matrix4x4.identity;
                break;
        }
        #endif
    }
} 