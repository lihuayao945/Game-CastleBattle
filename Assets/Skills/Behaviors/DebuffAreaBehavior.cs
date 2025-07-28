using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 减速减防区域行为 - 在区域内对敌人施加减速和减防效果
/// </summary>
public class DebuffAreaBehavior : MonoBehaviour, ISkillBehavior
{
    private EffectShape areaShape;
    private Vector2 areaSize;
    private float duration;
    private float debuffDuration = 7f; // 减益效果持续时间
    private GameObject debuffEffectPrefab; // 减益效果预制体
    
    // 效果应用器引用
    private SkillEffectApplier effectApplier;
    
    // 视觉组件引用
    private SpriteRenderer spriteRenderer;
    
    // 存储区域内已挂载减益效果的单位和对应的特效实例
    private Dictionary<GameObject, GameObject> affectedUnits = new Dictionary<GameObject, GameObject>();
    
    // 施法者引用
    private Unit caster;
    
    // 用于存储受影响单位和相应的减益移除协程管理器
    private static Dictionary<Unit, List<DebuffRemover>> activeDebuffs = new Dictionary<Unit, List<DebuffRemover>>();
    
    /// <summary>
    /// 减益效果移除器 - 用于确保即使DebuffAreaBehavior被销毁也能正确移除减益效果
    /// </summary>
    private class DebuffRemover : MonoBehaviour
    {
        public Unit targetUnit;
        public float modifier;
        public float duration;
        private float remainingTime;
        
        public void Initialize(Unit unit, float mod, float dur)
        {
            targetUnit = unit;
            modifier = mod;
            duration = dur;
            remainingTime = duration;
        }
        
        private void Update()
        {
            if (targetUnit == null || targetUnit.IsDead)
            {
                Destroy(gameObject);
                return;
            }
            
            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0)
            {
                // 移除减益效果
                if (targetUnit != null)
                {
                    // 恢复速度 (乘以倒数以正确恢复)
                    targetUnit.RemoveSpeedModifier(modifier);
                    
                    // 恢复防御力 (乘以倒数以正确恢复)
                    targetUnit.RemoveDefenseModifier(modifier);
                    
                    //Debug.Log($"[DebuffRemover] 减益效果结束: {targetUnit.name} 的速度和防御力已恢复");
                    
                    // 从活动减益列表中移除
                    if (activeDebuffs.ContainsKey(targetUnit))
                    {
                        activeDebuffs[targetUnit].Remove(this);
                        if (activeDebuffs[targetUnit].Count == 0)
                        {
                            activeDebuffs.Remove(targetUnit);
                        }
                    }
                }
                
                // 销毁自身
                Destroy(gameObject);
            }
        }
    }
    
    /// <summary>
    /// 初始化减速减防区域行为
    /// </summary>
    public void Initialize(SkillData data, Transform owner)
    {
        // 获取施法者组件
        caster = owner.GetComponent<Unit>();
        if (caster == null)
        {
            //Debug.LogError("DebuffAreaBehavior: 施法者没有Unit组件！", this);
            return;
        }
        
        // 获取组件引用
        effectApplier = GetComponent<SkillEffectApplier>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // 从技能数据中获取属性
        areaShape = data.areaShape;
        areaSize = data.areaSize;
        duration = data.areaDuration;
        debuffEffectPrefab = data.damageEffectPrefab;
        
        // 根据拥有者的朝向调整特效方向
        AdjustDirectionBasedOnOwner(owner);
        
        // 调整特效大小以匹配区域范围
        AdjustEffectSize();
        
        // 立即检测并应用减益效果
        ApplyDebuffs();
        
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
    /// 应用减益效果
    /// </summary>
    private void ApplyDebuffs()
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
            GameObject unitObj = collider.gameObject;
            Unit targetUnit = unitObj.GetComponent<Unit>();
            
            // 检查目标是否为敌人
            if (targetUnit != null && targetUnit.IsEnemy(caster) && targetUnit.ShowDamageEffect)
            {
                float debuffModifier = 0.5f; // 减速和减防50%
                
                // 应用减速效果
                targetUnit.ApplySpeedModifier(debuffModifier);
                
                // 应用减防效果
                targetUnit.ApplyDefenseModifier(debuffModifier);
                
                //Debug.Log($"[DebuffAreaBehavior] 应用减益效果到: {targetUnit.name} - 减速和减防至 {debuffModifier * 100}%");
                
                // 创建减益效果预制体
                if (debuffEffectPrefab != null)
                {
                    Vector3 effectPosition = unitObj.transform.position;
                    GameObject effectInstance = Instantiate(
                        debuffEffectPrefab,
                        effectPosition,
                        Quaternion.identity
                    );
                    
                    // 将特效设置为单位的子对象
                    effectInstance.transform.SetParent(unitObj.transform);
                    
                    // 设置特效的生命周期为debuffDuration
                    Destroy(effectInstance, debuffDuration);
                    
                    // 添加到字典中以便跟踪
                    affectedUnits[unitObj] = effectInstance;
                }
                
                // 创建独立的减益移除器而不是使用协程
                CreateDebuffRemover(targetUnit, debuffModifier, debuffDuration);
            }
        }
    }
    
    /// <summary>
    /// 创建独立的减益移除器，确保即使该对象被销毁也能正确移除减益效果
    /// </summary>
    private void CreateDebuffRemover(Unit targetUnit, float modifier, float duration)
    {
        // 创建一个空游戏对象用于托管减益移除器
        GameObject removerObj = new GameObject($"DebuffRemover_{targetUnit.name}");
        DebuffRemover remover = removerObj.AddComponent<DebuffRemover>();
        remover.Initialize(targetUnit, modifier, duration);
        
        // 将减益移除器添加到全局跟踪字典中
        if (!activeDebuffs.ContainsKey(targetUnit))
        {
            activeDebuffs[targetUnit] = new List<DebuffRemover>();
        }
        activeDebuffs[targetUnit].Add(remover);
        
        // 设置为DontDestroyOnLoad，确保场景切换时不会丢失（如果需要的话）
        // DontDestroyOnLoad(removerObj);
    }
    
    /// <summary>
    /// 当对象被销毁时
    /// </summary>
    private void OnDestroy()
    {
        // 不再在这里处理减益移除，由DebuffRemover负责
        affectedUnits.Clear();
    }
    
    /// <summary>
    /// 更新行为
    /// </summary>
    public void UpdateBehavior()
    {
        // 不需要每帧更新
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