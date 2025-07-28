using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class SkillDirectionController : BaseSkillController
{
    private Rigidbody2D rb;
    private BoxCollider2D collider;
    private SpriteRenderer spriteRenderer;
    private Vector2 originalSpriteSize;
    private float skillRangeY;
    private Dictionary<Enemy, float> enemyLastHitTime = new Dictionary<Enemy, float>();

    protected override void OnInitializeCustom()
    {
        // 初始化组件（原逻辑）
        rb = GetComponent<Rigidbody2D>();
        collider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalSpriteSize = spriteRenderer.sprite.bounds.size;
        skillRangeY = skillData.rangeY;

        // 速度设置（原逻辑）
        rb.velocity = (skillData.spawnOffsetType == SpawnOffsetType.Forward 
            ? transform.right : Vector2.right) * skillData.projectileSpeed;

        // 碰撞体与精灵缩放（原逻辑）
        SetSkillHeight(skillRangeY);
    }

    private void SetSkillHeight(float rangeY)
    {
        collider.size = new Vector2(collider.size.x, rangeY);
        float scaleY = rangeY / originalSpriteSize.y;
        spriteRenderer.transform.localScale = new Vector3(
            spriteRenderer.transform.localScale.x, scaleY, 1);
    }

    void FixedUpdate() => OnUpdateSkill();

    protected override void OnUpdateSkill()
    {
        // 伤害检测（原逻辑）
        Vector2 detectSize = new Vector2(
            collider.size.x * Mathf.Abs(transform.lossyScale.x), skillRangeY);
        Vector2 detectCenter = collider.bounds.center;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            detectCenter, detectSize, transform.eulerAngles.z, targetLayer);

        float now = Time.time;
        foreach (Collider2D hit in hits)
        {
            if (hit.GetComponent<Enemy>() is Enemy enemy && 
                (!enemyLastHitTime.TryGetValue(enemy, out float lastHit) || 
                 now - lastHit >= skillData.cooldownTime)) // 改为使用SkillData的冷却时间
            {
                enemy.TakeDamage(10);
                enemyLastHitTime[enemy] = now;
            }
        }
    }
}