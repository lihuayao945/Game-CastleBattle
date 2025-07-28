using UnityEngine;

public abstract class BaseSkillController : MonoBehaviour
{
    protected SkillData skillData;  // 技能配置数据
    protected LayerMask targetLayer; // 目标层级

    /// <summary>
    /// 初始化技能（公共逻辑）
    /// </summary>
    public virtual void Initialize(SkillData data, Vector2 dir, LayerMask target)
    {
        skillData = data;
        targetLayer = target;
        SetDirection(dir);       // 方向调整（公共）
        SetLifetime(data.projectileLifetime); // 生命周期（公共）
        OnInitializeCustom();    // 子类自定义初始化
    }

    protected virtual void SetDirection(Vector2 dir)
    {
        // 公共方向逻辑（如精灵翻转）
        var sprite = GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.flipX = dir.x < 0;
    }

    protected virtual void SetLifetime(float lifetime)
    {
        Destroy(gameObject, lifetime);
    }

    protected abstract void OnInitializeCustom(); // 子类实现自定义初始化
    protected abstract void OnUpdateSkill();      // 子类实现每帧逻辑
    protected virtual void OnDestroy() { }         // 可选：技能销毁时的逻辑
}