using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationCtroller : MonoBehaviour
{
    [Tooltip("技能4的快捷键")]
    public KeyCode skill4Key = KeyCode.I;
    private Animator animator;
    private int attackCombo = 0;  // 当前攻击段数（0是Attack1, 1是Attack2, 2是Attack3）
    private float lastAttackTime = 0f;  // 记录上次攻击的时间
    private float comboResetTime = 0.5f;  // 连击重置的时间间隔（0.5s）
    private bool isDead = false;  // 角色是否死亡
    private bool isCastingSkill = false;  //是否正在释放技能
    private bool isSkillLocked = false;  // 技能释放锁
    public CharacterSkillManager skillManager;
    private Queue<int> attackQueue = new Queue<int>();  // 攻击队列
    //private bool isProcessingAttack = false;  // 是否正在处理攻击队列
    private const float ATTACK_DURATION = 0.33f;  // 每段攻击动画持续时间
    private const float DAMAGE_TIMING = 0.165f;  // 伤害触发时间点
    private bool canAttack = true;  // 是否可以开始新的攻击
    //private bool isAttacking = false;  // 是否正在攻击中
    private float nextAttackTime = 0f;  // 下一次可以攻击的时间
    private Rigidbody2D rb;  // 刚体组件引用
    private HeroUnit unit;  // Unit组件引用
    private Vector3 originalScale;  // 存储原始缩放

    [SerializeField] private HeroBaseDataSO heroDataSO; // 新增：引用英雄基础数据SO

    void Start()
    {
        animator = GetComponent<Animator>();
        skillManager = GetComponent<CharacterSkillManager>();
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
            collider.size = new Vector2(1f, 1.7f); // 根据角色大小调整
            collider.offset = new Vector2(0f, 0f); // 根据角色中心点调整
        }

        originalScale = transform.localScale;  // 存储原始缩放
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
        // HandleMovement();  // 将HandleMovement移到FixedUpdate中
        HandleAttack();
    }

    void FixedUpdate()
    {
        // 只有在非施法、非死亡和未眩晕状态下才处理移动
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
        if (unit != null && unit.isStunned) return; // 新增：眩晕时不能处理技能

        if (Input.GetKeyDown(skill4Key))
        {
            if (skillManager != null && skillManager.IsSkillReady(3))
            {
                StartCoroutine(PerformSkillCast(3)); // 使用协程处理技能4
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
                StartCoroutine(PerformSkillCast(0)); // 修改为协程处理
            }
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            if (skillManager.IsSkillReady(1))
            {
                StartCoroutine(PerformSkillCast(1)); // 修改为协程处理
            }
        }
        if (Input.GetKeyDown(KeyCode.U))
        {
            if (skillManager.IsSkillReady(2))
            {
                StartCoroutine(PerformSkillCast(2)); // 修改为协程处理
            }
        }
    }

    private IEnumerator PerformSkillCast(int skillIndex)
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

        // 等待前摇时间
        float precastTime = 0.33f;
        if (skillIndex == 3) // 技能4使用更长的前摇
        {
            precastTime = 0.5f;
        }
        yield return new WaitForSeconds(precastTime);

        // 正式释放技能
        skillManager.ActivateSkill(skillIndex);

        // 触发技能冷却
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerSkillCooldown(skillIndex);
        }

        // 保持禁止移动状态直到施法结束
        yield return new WaitForSeconds(skill.castingTime - precastTime);

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
            // 使用MovePosition代替velocity，确保物理移动平滑
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
        if (unit != null && unit.isStunned) return; // 新增：眩晕时不能处理攻击

        // 检测是否按下攻击键 J
        if (Input.GetKeyDown(KeyCode.J))
        {
            float timeSinceLastAttack = Time.time - lastAttackTime;

            // 如果距离上次攻击的时间超过 comboResetTime，则重置连击
            if (timeSinceLastAttack > comboResetTime)
            {
                attackCombo = 0;  // 设置攻击段数为第一段
            }

            // 如果当前时间已经超过下一次攻击时间，开始新的攻击
            if (Time.time >= nextAttackTime && skillManager != null && skillManager.IsSkillReady(4))
            {
                StartAttack();
                lastAttackTime = Time.time;
            }
        }
    }

    void StartAttack()
    {
        if (unit != null && unit.isStunned) return; // 新增：眩晕时不能开始攻击
        // 播放当前段数的攻击动画
        switch (attackCombo)
        {
            case 0:
                animator.SetTrigger("AttackTrigger1");
                break;
            case 1:
                animator.SetTrigger("AttackTrigger2");
                break;
            case 2:
                animator.SetTrigger("AttackTrigger3");
                break;
        }

        // 更新连击计数
        attackCombo = (attackCombo + 1) % 3;

        // 设置下一次可以攻击的时间（当前时间 + 0.1秒，允许提前输入）
        nextAttackTime = Time.time + 0.01f;
    }

    // 动画事件：在动画播放到0.165s时调用（第一段和第二段攻击）
    public void OnAttackDamage()
    {
        if (unit != null && unit.isStunned) return; // 新增：眩晕时不能造成伤害
        if (skillManager != null)
        {
            skillManager.ActivateSkill(4); // 使用技能4（普通攻击，伤害5）
        }
    }

    // 动画事件：在动画播放到0.165s时调用（第三段攻击）
    public void OnFinalAttackDamage()
    {
        if (unit != null && unit.isStunned) return; // 新增：眩晕时不能造成伤害
        if (skillManager != null)
        {
            skillManager.ActivateSkill(5); // 使用技能5（终结攻击，伤害10）
        }
    }

    // 动画事件：在动画结束时调用
    public void OnAttackEnd()
    {
        canAttack = true;  // 允许开始新的攻击
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
        attackCombo = 0; // 重置攻击连击
        canAttack = true; // 允许攻击
        nextAttackTime = 0f; // 重置攻击时间
        lastAttackTime = 0f; // 重置上次攻击时间
    }

}
