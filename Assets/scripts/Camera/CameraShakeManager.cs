using System.Collections;
using UnityEngine;

/// <summary>
/// 摄像头抖动管理器 - 处理英雄受伤时的屏幕抖动效果
/// </summary>
public class CameraShakeManager : MonoBehaviour
{
    public static CameraShakeManager Instance;
    
    [Header("抖动设置")]
    [SerializeField] private float shakeIntensity = 0.3f;
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    
    [Header("调试")]
    [SerializeField] private bool enableShake = true;
    
    private Camera targetCamera;
    private Vector3 originalPosition;
    private Coroutine currentShake;
    
    private void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            InitializeCamera();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeCamera()
    {
        // 获取主摄像头
        targetCamera = Camera.main;
        if (targetCamera == null)
        {
            targetCamera = FindObjectOfType<Camera>();
        }
        
        if (targetCamera == null)
        {
            Debug.LogError("CameraShakeManager: 找不到摄像头！");
        }
    }
    
    /// <summary>
    /// 触发屏幕抖动
    /// </summary>
    public void TriggerShake()
    {
        if (!enableShake || targetCamera == null) return;
        
        // 如果已经在抖动，停止当前抖动
        if (currentShake != null) 
        {
            StopCoroutine(currentShake);
        }
        
        currentShake = StartCoroutine(ShakeCoroutine());
    }
    
    /// <summary>
    /// 抖动协程
    /// </summary>
    private IEnumerator ShakeCoroutine()
    {
        if (targetCamera == null) yield break;
        
        // 记录原始位置
        originalPosition = targetCamera.transform.position;
        float elapsed = 0f;
        
        while (elapsed < shakeDuration)
        {
            // 计算当前抖动强度（使用曲线控制衰减）
            float progress = elapsed / shakeDuration;
            float currentIntensity = shakeIntensity * shakeCurve.Evaluate(progress);
            
            // 生成随机偏移
            Vector3 randomOffset = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                0f
            ) * currentIntensity;
            
            // 应用抖动偏移
            targetCamera.transform.position = originalPosition + randomOffset;
            
            elapsed += Time.unscaledDeltaTime; // 使用unscaled避免时间缩放影响
            yield return null;
        }
        
        // 恢复原始位置
        targetCamera.transform.position = originalPosition;
        currentShake = null;
    }
    
    /// <summary>
    /// 设置抖动参数
    /// </summary>
    public void SetShakeParams(float intensity, float duration)
    {
        shakeIntensity = intensity;
        shakeDuration = duration;
    }
    
    /// <summary>
    /// 启用/禁用抖动
    /// </summary>
    public void SetShakeEnabled(bool enabled)
    {
        enableShake = enabled;
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
