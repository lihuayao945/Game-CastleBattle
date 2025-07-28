using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 投射物行为 - 实现向特定方向移动的技能（如剑气、箭矢等）
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ProjectileBehavior : MonoBehaviour, ISkillBehavior
{
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private Vector2 originalSpriteSize;
    
    // 技能属性
    private float speed;
    private float lifetime;
    private float rangeY;
    private Vector2 direction;
    
    // 效果应用器引用
    private SkillEffectApplier effectApplier;
    
    // 施法者引用
    private Unit caster;
    
    /// <summary>
    /// 初始化投射物行为
    /// </summary>
    public void Initialize(SkillData data, Transform owner)
    {
        // 获取施法者组件
        caster = owner.GetComponent<Unit>();
        if (caster == null)
        {
            //Debug.LogError("ProjectileBehavior: 施法者没有Unit组件！", this);
            return;
        }
        
        // 获取组件引用
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        effectApplier = GetComponent<SkillEffectApplier>();
        
        originalSpriteSize = spriteRenderer.sprite.bounds.size;
        
        // 从技能数据中获取属性
        speed = data.projectileSpeed;
        lifetime = data.projectileLifetime;
        
        // 应用范围强化：获取最终rangeY值
        rangeY = data.GetFinalRangeY(caster.faction);
        
        // 获取方向（基于角色朝向）
        direction = DirectionHelper.GetFacingDirection(owner);
        
        // 设置投射物朝向
        spriteRenderer.flipX = (direction.x < 0);
        
        // 设置移动速度
        rb.velocity = direction.normalized * speed;
        
        // 强制保持本地Y缩放为1，避免影响碰撞器
        transform.localScale = new Vector3(transform.localScale.x, 1f, transform.localScale.z);
        
        // 设置技能高度
        SetSkillHeight(rangeY);
        
        // 设置生命周期
        Destroy(gameObject, lifetime);
    }
    
    /// <summary>
    /// 更新投射物行为
    /// </summary>
    public void UpdateBehavior()
    {
        // 投射物只需要初始化时设置速度，不需要每帧更新行为
    }
    
    /// <summary>
    /// 设置技能高度（视觉和碰撞）
    /// </summary>
    private void SetSkillHeight(float height)
    {
        // 设置碰撞器高度
        boxCollider.size = new Vector2(boxCollider.size.x, height);
        
        // 设置精灵高度（基于原始尺寸缩放）
        float scaleY = height / originalSpriteSize.y;
        spriteRenderer.transform.localScale = new Vector3(
            spriteRenderer.transform.localScale.x,
            scaleY,
            1
        );
    }
    
    private void FixedUpdate()
    {
        // 检测碰撞区域内的目标
        if (effectApplier != null)
        {
            Vector2 detectSize = new Vector2(
                boxCollider.size.x * Mathf.Abs(transform.lossyScale.x),
                rangeY
            );
            Vector2 detectCenter = boxCollider.bounds.center;
            
            effectApplier.DetectAndApplyEffects(detectCenter, detectSize, 0);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying || boxCollider == null) return;
        
        // 绘制碰撞器边界（蓝色）
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(boxCollider.bounds.center, boxCollider.bounds.size);
        
        // 绘制伤害区域（红色半透明）
        Vector2 detectSize = new Vector2(
            boxCollider.size.x * Mathf.Abs(transform.lossyScale.x),
            rangeY
        );
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawCube(boxCollider.bounds.center, detectSize);
        #endif
    }
} 