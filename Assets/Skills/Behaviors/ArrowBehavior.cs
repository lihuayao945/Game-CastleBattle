using UnityEngine;

/// <summary>
/// 箭矢行为 - 命中一次敌人后立即销毁
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ArrowBehavior : MonoBehaviour, ISkillBehavior
{
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    
    // 技能属性
    private float speed;
    private float lifetime;
    private float rangeY;
    private float effectDuration; // 添加特效持续时间
    private Vector2 direction;
    
    // 效果应用器引用
    private SkillEffectApplier effectApplier;
    
    // 施法者引用
    private Unit caster;
    private bool hasHit = false;

    // 伤害区域宽度系数
    private const float DAMAGE_AREA_WIDTH_MULTIPLIER = 2.5f;

    /// <summary>
    /// 初始化箭矢行为
    /// </summary>
    public void Initialize(SkillData data, Transform owner)
    {
        caster = owner.GetComponent<Unit>();
        if (caster == null)
        {
            Debug.LogError("ArrowBehavior: 施法者没有Unit组件！", this);
            return;
        }
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        effectApplier = GetComponent<SkillEffectApplier>();
        
        speed = data.projectileSpeed;
        lifetime = data.projectileLifetime;
        rangeY = data.rangeY;
        effectDuration = data.areaDuration; // 从技能数据中获取特效持续时间
        direction = DirectionHelper.GetFacingDirection(owner);
        spriteRenderer.flipX = (direction.x < 0);
        rb.velocity = direction.normalized * speed;
        
        // 设置碰撞体大小与伤害区域一致
        boxCollider.size = new Vector2(boxCollider.size.x, rangeY);
        
        Destroy(gameObject, lifetime);
        hasHit = false;
    }

    public void UpdateBehavior()
    {
        // 箭矢只需要初始化时设置速度，不需要每帧更新行为
    }

    private void FixedUpdate()
    {
        if (hasHit) return;
        if (effectApplier != null)
        {
            Vector2 detectSize = new Vector2(
                boxCollider.size.x * DAMAGE_AREA_WIDTH_MULTIPLIER,  // 增加伤害区域宽度
                rangeY
            );
            Vector2 detectCenter = boxCollider.bounds.center;
            
            // 检测是否有敌人在伤害区域内
            Collider2D[] hits = Physics2D.OverlapBoxAll(detectCenter, detectSize, 0);
            bool hitEnemy = false;
            
            foreach (Collider2D hit in hits)
            {
                Unit hitUnit = hit.GetComponent<Unit>();
                if (hitUnit != null && hitUnit != caster && IsEnemy(hitUnit))
                {
                    hitEnemy = true;
                    break;
                }
            }
            
            if (hitEnemy)
            {
                // 只有在检测到敌人时才应用伤害并销毁箭矢
                effectApplier.DetectAndApplyEffects(detectCenter, detectSize, 0);
                hasHit = true;
                Destroy(gameObject);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        Unit hitUnit = other.GetComponent<Unit>();
        if (hitUnit != null && hitUnit != caster && IsEnemy(hitUnit))
        {
            hasHit = true;
            // 立即销毁箭矢
            Destroy(gameObject);
        }
    }

    private bool IsEnemy(Unit unit)
    {
        if (caster == null) return false;
        return unit.faction != caster.faction;
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying || boxCollider == null) return;
        // 绘制碰撞体范围（蓝色）
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(boxCollider.bounds.center, boxCollider.bounds.size);
        
        // 绘制伤害检测区域（红色）
        Vector2 detectSize = new Vector2(
            boxCollider.size.x * DAMAGE_AREA_WIDTH_MULTIPLIER,  // 增加伤害区域宽度
            rangeY
        );
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawCube(boxCollider.bounds.center, detectSize);
#endif
    }
} 