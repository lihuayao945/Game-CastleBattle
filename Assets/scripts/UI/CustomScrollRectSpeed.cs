using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 引入EventSystems命名空间

public class CustomScrollRectSpeed : MonoBehaviour, IScrollHandler
{
    [Tooltip("鼠标滚轮滚动速度，您可以根据需要调整此值。")]
    [SerializeField] private float scrollSpeed = 0.1f; // 默认值，可以根据需要调整

    private ScrollRect scrollRect;

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            Debug.LogError("CustomScrollRectSpeed 脚本需要挂载在包含 ScrollRect 组件的 GameObject 上。", this);
            enabled = false; // 禁用此脚本
        }
    }

    // 当鼠标滚轮事件发生时调用
    public void OnScroll(PointerEventData eventData)
    {
        if (scrollRect == null) return;

        // eventData.scrollDelta.y: 鼠标滚轮的垂直滚动量。向上滚动为正，向下滚动为负。
        // scrollRect.verticalNormalizedPosition: ScrollRect的垂直滚动位置，范围0-1。0为底部，1为顶部。

        // 计算新的滚动位置
        float newNormalizedPosition = scrollRect.verticalNormalizedPosition + eventData.scrollDelta.y * scrollSpeed;

        // 确保滚动位置保持在0到1之间
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(newNormalizedPosition);
    }
} 