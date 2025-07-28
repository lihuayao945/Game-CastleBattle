using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 英雄复活计时器UI - 显示英雄死亡后的复活倒计时
/// </summary>
public class HeroResurrectionTimerUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject timerPanel; // 计时器面板
    [SerializeField] private TextMeshProUGUI timerText; // 倒计时文本
    [SerializeField] private Image timerFillImage; // 倒计时填充图像
    [SerializeField] private TextMeshProUGUI heroNameText; // 英雄名称文本
    
    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.5f; // 淡入动画持续时间
    [SerializeField] private float fadeOutDuration = 0.5f; // 淡出动画持续时间
    
    private HeroUnit trackedHero; // 当前跟踪的英雄
    private CanvasGroup canvasGroup; // 用于淡入淡出效果
    
    private void Awake()
    {
        // 获取或添加CanvasGroup组件
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // 确保面板初始状态是隐藏的
        timerPanel.SetActive(false);
        canvasGroup.alpha = 0f;
        
        // 新增：隐藏整个ResurrectionTimerUI对象
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 设置要跟踪的英雄单位
    /// </summary>
    /// <param name="hero">要跟踪的英雄单位</param>
    public void SetHero(HeroUnit hero)
    {
        // 如果已经在跟踪一个英雄，先取消订阅其事件
        if (trackedHero != null)
        {
            trackedHero.OnResurrectionTimeUpdated -= UpdateTimer;
            trackedHero.OnResurrectionStarted.RemoveListener(ShowTimer);
            trackedHero.OnResurrectionCompleted.RemoveListener(HideTimer);
        }
        
        trackedHero = hero;
        
        // 订阅新英雄的事件
        if (trackedHero != null)
        {
            trackedHero.OnResurrectionTimeUpdated += UpdateTimer;
            trackedHero.OnResurrectionStarted.AddListener(ShowTimer);
            trackedHero.OnResurrectionCompleted.AddListener(HideTimer);
            
            // 设置英雄名称 - 根据英雄类型显示本地化名称
            if (heroNameText != null)
            {
                // 根据英雄类型设置本地化名称
                string heroDisplayName = "英雄";
                
                if (trackedHero.Type == Unit.UnitType.Knight)
                {
                    heroDisplayName = "骑士";
                }
                else if (trackedHero.Type == Unit.UnitType.Necromancer)
                {
                    heroDisplayName = "邪术师";
                }
                
                heroNameText.text = heroDisplayName;
            }
        }
    }
    
    /// <summary>
    /// 更新计时器显示
    /// </summary>
    /// <param name="currentTime">当前剩余时间</param>
    /// <param name="totalTime">总复活时间</param>
    private void UpdateTimer(float currentTime, float totalTime)
    {
        if (timerText != null)
        {
            // 显示剩余时间（四舍五入到整数秒）
            int secondsRemaining = Mathf.CeilToInt(currentTime);
            timerText.text = $"{secondsRemaining}";
        }
        
        if (timerFillImage != null)
        {
            // 更新填充图像
            float fillAmount = currentTime / totalTime;
            timerFillImage.fillAmount = fillAmount;
        }
    }
    
    /// <summary>
    /// 显示计时器（在英雄开始复活时调用）
    /// </summary>
    /// <param name="spawnPoint">复活点位置（未使用）</param>
    private void ShowTimer(Vector3 spawnPoint)
    {
        // 确保整个对象和面板都是激活的
        gameObject.SetActive(true);
        timerPanel.SetActive(true);
        
        // 停止所有正在运行的动画协程
        StopAllCoroutines();
        
        // 启动淡入动画
        StartCoroutine(FadeIn());
    }
    
    /// <summary>
    /// 隐藏计时器（在英雄复活完成时调用）
    /// </summary>
    private void HideTimer()
    {
        // 停止所有正在运行的动画协程
        StopAllCoroutines();
        
        // 启动淡出动画
        StartCoroutine(FadeOut());
    }
    
    /// <summary>
    /// 淡入动画协程
    /// </summary>
    private IEnumerator FadeIn()
    {
        float elapsedTime = 0f;
        canvasGroup.alpha = 0f;
        
        while (elapsedTime < fadeInDuration)
        {
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeInDuration);
            elapsedTime += Time.unscaledDeltaTime; // 使用unscaledDeltaTime以便在游戏暂停时也能正常工作
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
    }
    
    /// <summary>
    /// 淡出动画协程
    /// </summary>
    private IEnumerator FadeOut()
    {
        float elapsedTime = 0f;
        canvasGroup.alpha = 1f;
        
        while (elapsedTime < fadeOutDuration)
        {
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeOutDuration);
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }
        
        canvasGroup.alpha = 0f;
        timerPanel.SetActive(false);
        
        // 新增：完全隐藏整个ResurrectionTimerUI对象
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 重置UI状态 - 在切换场景或开始新游戏时调用
    /// </summary>
    public void ResetUI()
    {
        // 停止所有协程
        StopAllCoroutines();
        
        // 取消订阅当前英雄的事件
        if (trackedHero != null)
        {
            trackedHero.OnResurrectionTimeUpdated -= UpdateTimer;
            trackedHero.OnResurrectionStarted.RemoveListener(ShowTimer);
            trackedHero.OnResurrectionCompleted.RemoveListener(HideTimer);
            trackedHero = null;
        }
        
        // 立即隐藏UI，不使用淡出动画
        canvasGroup.alpha = 0f;
        timerPanel.SetActive(false);
        gameObject.SetActive(false);
        
        //Debug.Log("英雄复活计时器UI已重置");
    }
    
    private void OnDestroy()
    {
        // 确保在销毁时取消订阅事件
        if (trackedHero != null)
        {
            trackedHero.OnResurrectionTimeUpdated -= UpdateTimer;
            trackedHero.OnResurrectionStarted.RemoveListener(ShowTimer);
            trackedHero.OnResurrectionCompleted.RemoveListener(HideTimer);
        }
    }
} 