using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NeController : MonoBehaviour
{
    [Tooltip("技能4的快捷键")]
    public KeyCode skill4Key = KeyCode.I;
    
    [Header("攻击特效")]
    [Tooltip("魔法阵预制体")]
    public GameObject magicCirclePrefab;
    [Tooltip("魔法阵持续时间")]
    public float magicCircleDuration = 0.5f;
    
    private Animator animator;
    private bool isDead = false;
    private bool isCastingSkill = false;
    private bool isSkillLocked = false;  // 技能释放锁
    public CharacterSkillManager skillManager;
    private Vector3 originalScale; // 保存原始大小
    private Rigidbody2D rb;  // 刚体组件引用
    private HeroUnit unit;  // Unit组件引用
    
    [SerializeField] private HeroBaseDataSO heroDataSO; // 新增：引用英雄基础数据SO
    
    void Start()
    {
        animator = GetComponent<Animator>();
        skillManager = GetComponent<CharacterSkillManager>();
        originalScale = transform.localScale; // 保存初始大小
        rb = GetComponent<Rigidbody2D>();
        unit = GetComponent<HeroUnit>();
        
        // 确保有Unit组件
        if (unit == null)
        {
            unit = gameObject.AddComponent<HeroUnit>();
        }

        // 确保有Rigidbody2D组件
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0; // 2D游戏中不需要重力
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // 冻结旋转
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 使用连续碰撞检测
            
            // 设置碰撞层
            // 获取Skill层的索引
            int skillLayer = LayerMask.NameToLayer("Skill");
            // 设置与Skill层不碰撞
            Physics2D.IgnoreLayerCollision(gameObject.layer, skillLayer, true);
        }
        
        // 确保有BoxCollider2D组件
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.3593f, 0.55f); // 根据角色大小调整
            collider.offset = new Vector2(-0.02f, -0.05f); // 根据角色中心点调整
        }
    }

    void Update()
    {
        // 如果已经死亡，则不执行任何后续逻辑
        if (isDead) return;

        // 新增：自动检测生命值，如果为0则触发死亡
        if (unit != null && unit.currentHealth <= 0)
        {
            Die();
            return; // 死亡后停止执行本帧后续逻辑
        }

        HandleSkill();
        //HandleMovement();  // 将HandleMovement移到最后
        HandleAttack();
    }

    void FixedUpdate()
    {
        if (!isCastingSkill && !isDead && unit != null && !unit.isStunned)
        {
            HandleMovement();
        }
    }

    private void HandleSkill()
    {
        // 如果游戏暂停，不处理技能输入
        if (Time.timeScale == 0f) return;
        
        if (isSkillLocked) return;  // 如果技能被锁定，直接返回
        if (unit != null && unit.isStunned) return;

        if (Input.GetKeyDown(skill4Key))
        {
            if (skillManager != null && skillManager.IsSkillReady(3))
            {
                StartCoroutine(PerformSkillCast(3, 3f));
            }
            else
            {
                Debug.LogWarning("未设置技能管理器，无法激活技能");
            }
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            if (skillManager.IsSkillReady(0))
            {
                StartCoroutine(PerformSkillCast(0, 1f));
            }
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            if (skillManager.IsSkillReady(1))
            {
                StartCoroutine(PerformSkillCast(1, 1.5f));
            }
        }
        if (Input.GetKeyDown(KeyCode.U))
        {
            if (skillManager.IsSkillReady(2))
            {
                StartCoroutine(PerformSkillCast(2, 2.0f));
            }
        }
    }

    private IEnumerator PerformSkillCast(int skillIndex, float precastTime)
    {
        // 如果已经在释放技能，直接返回
        if (isSkillLocked || (unit != null && unit.isStunned)) yield break;

        // 锁定技能释放
        isSkillLocked = true;

        // 获取技能数据
        SkillData skill = skillManager.skills[skillIndex];

        // 设置施法状态
        isCastingSkill = true;

        // 保存原始刚体设置
        bool originalSimulated = rb.simulated;
        RigidbodyConstraints2D originalConstraints = rb.constraints;

        // 冻结刚体，防止被推动
        if (rb != null)
        {
            rb.simulated = true; // 保持物理模拟开启
            rb.constraints = RigidbodyConstraints2D.FreezeAll; // 完全冻结
            rb.velocity = Vector2.zero; // 清除速度
        }

        // 使用skillIndex动态调用对应的动画Trigger
        animator.SetTrigger($"SkillTrigger{skillIndex}");

        // 在角色前方生成魔法阵
        Vector3 magicCirclePos = transform.position + new Vector3(transform.localScale.x * 0.3f, 0, 0);
        GameObject magicCircle = Instantiate(magicCirclePrefab, magicCirclePos, Quaternion.identity);
        
        // 获取魔法阵的Animator组件并调整动画速度
        Animator magicCircleAnimator = magicCircle.GetComponent<Animator>();
        if (magicCircleAnimator != null)
        {
            // 设置动画速度为1/施法时间，这样动画会正好播放一次
            magicCircleAnimator.speed = 1f / skill.castingTime;
        }

        // 等待前摇时间
        yield return new WaitForSeconds(precastTime);

        // 正式释放技能
        skillManager.ActivateSkill(skillIndex);
        
        // 触发技能冷却
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerSkillCooldown(skillIndex);
        }

        // 等待动画播放完成
        yield return new WaitForSeconds(skill.castingTime - precastTime);

        // 销毁魔法阵
        if (magicCircle != null)
        {
            Destroy(magicCircle);
        }

        // 恢复原始刚体设置
        if (rb != null)
        {
            rb.simulated = originalSimulated;
            rb.constraints = originalConstraints;
        }

        // 结束施法状态
        isCastingSkill = false;
        
        // 解锁技能释放
        isSkillLocked = false;
    }

    void HandleMovement()
    {
        // 如果正在释放技能，停止移动
        if (isCastingSkill)
        {
            rb.MovePosition(transform.position);
            animator.SetFloat("speed", 0f);
            return;
        }

        // 直接检测WASD按键，不使用标准输入轴
        float horizontal = 0f;
        float vertical = 0f;
        
        if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.D)) horizontal += 1f;
        if (Input.GetKey(KeyCode.W)) vertical += 1f;
        if (Input.GetKey(KeyCode.S)) vertical -= 1f;

        Vector2 moveDirection = new Vector2(horizontal, vertical).normalized;
        float speed = moveDirection.magnitude;

        if (speed > 0.1f)
        {
            // 使用MovePosition代替velocity
            Vector2 newPos = (Vector2)transform.position + moveDirection * unit.GetCurrentMoveSpeed() * Time.fixedDeltaTime; // 使用 GetCurrentMoveSpeed()
            rb.MovePosition(newPos);

            // 只改变X轴的缩放来翻转角色
            if (horizontal < 0)
            {
                transform.localScale = new Vector3(-originalScale.x, originalScale.y, originalScale.z);
            }
            else if (horizontal > 0)
            {
                transform.localScale = new Vector3(originalScale.x, originalScale.y, originalScale.z);
            }

            animator.SetFloat("speed", speed);
        }
        else
        {
            // 停止时也使用MovePosition
            rb.MovePosition(transform.position);
            animator.SetFloat("speed", 0f);
        }
    }

    void HandleAttack()
    {
        // 如果游戏暂停，不处理攻击输入
        if (Time.timeScale == 0f) return;
        
        if (isSkillLocked) return;  // 如果技能被锁定，直接返回
        if (unit != null && unit.isStunned) return;

        if (Input.GetKeyDown(KeyCode.J))
        {
            if (skillManager != null && skillManager.IsSkillReady(4)) // 普通攻击是第5个技能（索引4）
            {
                StartCoroutine(PerformAttackWithEffect());
            }
        }
    }

    IEnumerator PerformAttackWithEffect()
    {
        // 如果已经在释放技能，直接返回
        if (isSkillLocked || (unit != null && unit.isStunned)) yield break;

        // 记录攻击开始时的位置
        Vector3 originalPosition = transform.position;
        
        // 锁定技能释放
        isSkillLocked = true;

        // 设置施法状态
        isCastingSkill = true;

        // 保存原始刚体设置
        bool originalSimulated = rb.simulated;
        RigidbodyConstraints2D originalConstraints = rb.constraints;

        // 冻结刚体，防止被推动
        if (rb != null)
        {
            rb.simulated = true; // 保持物理模拟开启
            rb.constraints = RigidbodyConstraints2D.FreezeAll; // 完全冻结
            rb.velocity = Vector2.zero; // 清除速度
        }

        // 播放攻击动画
        animator.SetTrigger("SkillTrigger4");

        // 在角色前方生成魔法阵，保持固定距离
        float direction = transform.localScale.x > 0 ? 1 : -1;
        Vector3 magicCirclePos = originalPosition + new Vector3(direction * 0.3f, 0, 0);
        GameObject magicCircle = Instantiate(magicCirclePrefab, magicCirclePos, Quaternion.identity);
        
        // 等待前摇时间
        yield return new WaitForSeconds(0.5f);

        // 释放技能
        skillManager.ActivateSkill(4);

        // 销毁魔法阵
        Destroy(magicCircle, magicCircleDuration);

        // 恢复原始刚体设置
        if (rb != null)
        {
            rb.simulated = originalSimulated;
            rb.constraints = originalConstraints;
        }

        // 确保角色位置不变
        transform.position = originalPosition;

        // 结束施法状态
        isCastingSkill = false;
        
        // 解锁技能释放
        isSkillLocked = false;
    }

    // 角色外部调用的死亡方法
    public void Die()
    {
        if (unit == null || unit.IsDead) return;
        unit.MarkAsDead();
        isDead = true;
        if (animator != null)
        {
            animator.SetTrigger("DeathTrigger");
        }
        // 不再立即禁用渲染/碰撞/物理，也不移到场外，等动画事件调用OnDeathAnimationEnd
    }

    // 死亡动画最后一帧动画事件调用
    public void OnDeathAnimationEnd()
    {
        // 禁用所有渲染器
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
        // 禁用所有碰撞体
        foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = false;
        // 禁用物理
        if (rb != null) rb.simulated = false;
        // 移动到场外
        transform.position = new Vector3(-100f, -100f, 0f);
        // 启动复活计时
        unit.StartResurrectionCoroutine();
    }

    /// <summary>
    /// 重置动画器到Idle状态，用于角色复活后。
    /// </summary>
    public void ResetToIdle()
    {
        if (animator != null)
        {
            animator.SetFloat("speed", 0f); // 确保速度为0
            animator.ResetTrigger("DeathTrigger"); // 确保死亡触发器被重置
            animator.Play("IDLE"); // 播放Idle动画（假设您的Idle动画状态名为"Idle"）
            // 如果有其他可能激活的动画状态，也需要在这里重置它们
        }
        isDead = false; // 重置死亡状态
        isCastingSkill = false; // 重置施法状态
        isSkillLocked = false; // 重置技能锁定
        // NeController没有attackCombo等属性，所以不需要重置
    }
}
