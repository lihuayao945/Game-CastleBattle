using UnityEngine;
using System.Collections;

/// <summary>
/// 冲锋行为 - 实现冲锋技能的特殊效果
/// </summary>
public class ChargeBehavior : MonoBehaviour, ISkillBehavior
{
    private LancerController lancer;
    private SkillData skillData;
    private bool isCharging = false;
    private Vector2 chargeDirection;
    private float chargeStartTime;
    private Animator animator;
    private SkillEffectApplier effectApplier;
    private Unit caster;

    // 检测相关
    private float nextDetectionTime = 0f;
    private const float DETECTION_INTERVAL = 0.1f; // 每0.1秒检测一次
    private Vector2 lastDetectionCenter;
    private bool hasHitEnemy = false;

    public void Initialize(SkillData data, Transform owner)
    {
        //Debug.Log("初始化冲锋行为");
        skillData = data;
        lancer = owner.GetComponent<LancerController>();
        animator = owner.GetComponent<Animator>();
        caster = owner.GetComponent<Unit>();

        if (lancer == null)
        {
            //Debug.LogError("ChargeBehavior: 需要LancerController组件！");
            return;
        }

        // 获取或添加效果应用器
        effectApplier = GetComponent<SkillEffectApplier>();
        if (effectApplier == null)
        {
            //Debug.LogWarning("ChargeBehavior: 找不到SkillEffectApplier组件，尝试添加");
            effectApplier = gameObject.AddComponent<SkillEffectApplier>();
        }

        // 确保效果应用器有正确的效果
        if (effectApplier != null)
        {
            // 添加伤害效果
            DamageEffect damageEffect = gameObject.AddComponent<DamageEffect>();
            damageEffect.Initialize(data, caster);
            effectApplier.SetEffect(damageEffect);

            // 添加眩晕效果
            StunEffect stunEffect = gameObject.AddComponent<StunEffect>();
            stunEffect.Initialize(data, caster);
            effectApplier.SetEffect(stunEffect);

            // 将效果预制体设置为骑士的子对象
            transform.SetParent(owner);
            transform.localPosition = Vector3.zero;
        }
    }

    public void UpdateBehavior()
    {
        if (!isCharging || hasHitEnemy) return;

        // 使用时间计算是否达到最大距离
        float elapsedTime = Time.time - chargeStartTime;
        if (elapsedTime >= skillData.projectileLifetime)
        {
            EndCharge();
            return;
        }

        // 按间隔检测敌人
        if (Time.time >= nextDetectionTime)
        {
            // 计算检测区域的位置（在角色前方）
            Vector2 detectionCenter = (Vector2)transform.position + chargeDirection * skillData.spawnDistance;
            lastDetectionCenter = detectionCenter;

            // 检测范围内的敌人
            Collider2D[] hits = Physics2D.OverlapBoxAll(
                detectionCenter, 
                new Vector2(skillData.areaSize.x, skillData.areaSize.y), 
                transform.eulerAngles.z, 
                LayerMask.GetMask("Unit")
            );

            foreach (var hit in hits)
            {
                Unit hitUnit = hit.GetComponent<Unit>();
                if (hitUnit != null && hitUnit != caster && caster.IsEnemy(hitUnit))
                {
                    hasHitEnemy = true;
                    if (effectApplier != null)
                    {
                        effectApplier.DetectAndApplyEffectsInCircle(detectionCenter, skillData.auraRadius);
                    }
                    EndCharge();
                    return;
                }
            }

            nextDetectionTime = Time.time + DETECTION_INTERVAL;
        }
    }

    public void StartCharge(Vector2 direction)
    {
        if (isCharging) return;

        //Debug.Log($"开始冲锋，方向：{direction}，起始位置：{transform.position}");
        isCharging = true;
        hasHitEnemy = false;
        chargeDirection = direction.normalized;
        chargeStartTime = Time.time;
        nextDetectionTime = Time.time;

        // 通知LancerController开始冲锋
        if (lancer != null)
        {
            lancer.SetCharging(true, chargeDirection);
        }

        // 播放冲锋动画
        if (animator != null)
        {
            animator.SetTrigger("ChargeTrigger");
            animator.SetBool("IsCharging", true);
        }
    }

    private void EndCharge()
    {
        if (!isCharging) return;

        isCharging = false;
        hasHitEnemy = false;
        if (lancer != null)
        {
            lancer.SetCharging(false, Vector2.zero);
        }

        // 停止冲锋动画
        if (animator != null)
        {
            animator.SetBool("IsCharging", false);
        }

        //Debug.Log("结束冲锋");
    }

    private void OnDestroy()
    {
        if (isCharging && lancer != null)
        {
            lancer.SetCharging(false, Vector2.zero);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || skillData == null) return;

        // 绘制效果应用范围（蓝色）
        Gizmos.color = new Color(0, 0, 1, 0.2f);
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(
            lastDetectionCenter,
            transform.rotation,
            Vector3.one
        );
        Gizmos.matrix = rotationMatrix;
        Gizmos.DrawCube(Vector3.zero, new Vector3(skillData.areaSize.x, skillData.areaSize.y, 0.1f));
        Gizmos.matrix = Matrix4x4.identity;
    }
} 