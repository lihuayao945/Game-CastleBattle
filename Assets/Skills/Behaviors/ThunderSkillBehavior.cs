using UnityEngine;

/// <summary>
/// 雷电技能行为 - 实现雷电技能的特殊效果
/// </summary>
public class ThunderSkillBehavior : MonoBehaviour, ISkillBehavior
{
    // 技能属性
    private float duration;
    private GameObject actionPrefab;
    private Vector3 actionOffset;
    private float areaRadius; // 区域半径
    
    // 引用
    private Transform ownerTransform;
    private GameObject actionInstance;
    private SkillEffectApplier effectApplier;
    private AreaEffectBehavior areaEffect; // 引用区域效果行为
    
    // 施法者引用
    private Unit caster;
    
    /// <summary>
    /// 初始化雷电技能行为
    /// </summary>
    public void Initialize(SkillData data, Transform owner)
    {
        // 获取施法者组件
        caster = owner.GetComponent<Unit>();
        if (caster == null)
        {
            //Debug.LogError("ThunderSkillBehavior: 施法者没有Unit组件！", this);
            return;
        }
        
        // 保存引用
        ownerTransform = owner;
        effectApplier = GetComponent<SkillEffectApplier>();
        areaEffect = GetComponent<AreaEffectBehavior>(); // 获取区域效果行为引用
        
        // 从技能数据中获取属性
        duration = data.areaDuration;
        actionPrefab = data.actionPrefab;
        actionOffset = data.actionOffset;
        areaRadius = data.areaSize.x; // 保存区域半径
        
        // 创建动作特效
        if (actionPrefab != null)
        {
            // 创建动作特效
            actionInstance = Instantiate(actionPrefab, owner.position, Quaternion.identity);
            
            // 设置为角色的子对象，跟随角色移动
            actionInstance.transform.SetParent(owner);
            
            // 设置相对位置
            actionInstance.transform.localPosition = actionOffset;
            
            //Debug.Log($"创建动作特效: {actionPrefab.name}");
        }
        
        // 确保区域效果行为已经初始化
        if (areaEffect == null)
        {
            //Debug.LogError("ThunderSkillBehavior: 找不到AreaEffectBehavior组件");
        }
        else
        {
            //Debug.Log($"ThunderSkillBehavior: 区域大小 = {areaEffect.areaSize}, 实际缩放 = {transform.localScale}");
        }
        
        // 设置生命周期
        Destroy(gameObject, duration);
        
        // 如果有动作实例，也设置其生命周期
        if (actionInstance != null)
        {
            Destroy(actionInstance, duration);
        }
    }
    
    /// <summary>
    /// 更新雷电技能行为
    /// </summary>
    public void UpdateBehavior()
    {
        // 应用区域效果
        ApplyAreaEffect();
        
        // 如果有动作实例，更新其位置
        if (actionInstance != null && ownerTransform != null)
        {
            // 保持动作特效在角色相对位置
            actionInstance.transform.localPosition = actionOffset;
            
            // 根据角色朝向调整特效
            AdjustActionEffectDirection();
        }
    }
    
    /// <summary>
    /// 根据角色朝向调整特效方向
    /// </summary>
    private void AdjustActionEffectDirection()
    {
        if (actionInstance == null || ownerTransform == null) return;
        
        // 获取角色朝向
        Vector2 facingDirection = DirectionHelper.GetFacingDirection(ownerTransform);
        bool isFacingLeft = facingDirection.x < 0;
        
        // 获取特效的SpriteRenderer
        SpriteRenderer spriteRenderer = actionInstance.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            // 根据角色朝向翻转特效
            spriteRenderer.flipX = isFacingLeft;
        }
    }
    
    /// <summary>
    /// 应用区域效果
    /// </summary>
    private void ApplyAreaEffect()
    {
        if (effectApplier == null) return;
        
        // 使用区域效果行为中的区域大小（如果可用）
        float radius = areaEffect != null ? areaEffect.areaSize.x : areaRadius;
        
        // 应用圆形区域效果，使用保存的区域半径
        effectApplier.DetectAndApplyEffectsInCircle(transform.position, radius);
    }
    
    /// <summary>
    /// 当对象被销毁时
    /// </summary>
    private void OnDestroy()
    {
        // 如果动作实例还存在，销毁它
        if (actionInstance != null)
        {
            Destroy(actionInstance);
        }
    }
    
    /// <summary>
    /// 在编辑器中绘制技能范围
    /// </summary>
    private void OnDrawGizmos()
    {
        // 使用区域效果行为中的区域大小（如果可用）
        float radius = areaEffect != null ? areaEffect.areaSize.x : areaRadius;
        
        // 绘制技能范围（绿色半透明）
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawSphere(transform.position, radius);
    }
} 