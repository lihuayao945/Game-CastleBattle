using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 提示系统 - 管理游戏中的提示显示
/// </summary>
public class TooltipSystem : MonoBehaviour
{
    private static TooltipSystem current;
    
    [SerializeField] private GameObject tooltipCanvas;
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipText;
    [SerializeField] private float offsetX = 20f;  // 默认水平偏移
    [SerializeField] private float offsetY = 20f;  // 稍微增加垂直偏移，避免鼠标重叠
    [SerializeField] private float forcedRightOffset = 120f;  // 强制右侧显示时的水平偏移，应该更大
    [SerializeField] private int fontSize = 24;  // 字体大小设置
    [SerializeField] private float padding = 10f; // 与屏幕边缘的最小间距

    private RectTransform tooltipRect;
    private Vector2 tooltipSize;
    private Vector2 lastMousePosition; // 存储上一帧的鼠标位置
    private Vector2 currentTriggerPosition; // 存储触发提示的位置
    private bool positionLocked = false; // 位置锁定标志，防止闪烁
    private bool forceRightPosition = false; // 是否强制在右侧显示

    private void Awake()
    {
        current = this;
        tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        tooltipText.fontSize = fontSize;  // 设置字体大小
        Hide();
    }

    private void Update()
    {
        if (current.tooltipPanel.activeSelf)
        {
            // 只有当鼠标移动超过一定距离才更新位置，防止闪烁
            Vector2 mousePosition = Input.mousePosition;
            float mouseDelta = Vector2.Distance(mousePosition, lastMousePosition);
            
            // 如果鼠标没有锁定且移动超过阈值，或者第一次显示，则更新位置
            if (!positionLocked && (mouseDelta > 3.0f || lastMousePosition == Vector2.zero))
            {
                UpdatePosition(mousePosition);
                lastMousePosition = mousePosition;
            }
        }
        else
        {
            // 当提示隐藏时重置状态
            lastMousePosition = Vector2.zero;
            positionLocked = false;
        }
    }

    private void UpdatePosition(Vector2 referencePosition)
    {
        // 确保我们有最新的提示框尺寸
        tooltipSize = tooltipRect.sizeDelta;
        
        // 存储触发位置或使用当前鼠标位置
        Vector2 anchorPosition = (currentTriggerPosition != Vector2.zero) ? 
                                 currentTriggerPosition : referencePosition;
        
        // 初始化位置变量
        float posX = anchorPosition.x + offsetX;
        float posY = anchorPosition.y;
        
        // 检查是否需要强制在右下方显示
        if (forceRightPosition)
        {
            // ==== 水平位置：强制在右侧，使用更大的偏移量 ====
            // 总是将提示框放在锚点的右侧，使用专门的强制右侧偏移量
            posX = anchorPosition.x + forcedRightOffset;
            
            // 如果会超出右边界，则尽量靠右但保持一定的padding
            if (posX + tooltipSize.x + padding > Screen.width)
            {
                posX = Screen.width - tooltipSize.x - padding;
            }
            
            // 如果位置调整后反而跑到鼠标左边去了，则强制回到右侧
            // 这确保了提示框始终在鼠标右侧，即使贴着右边界
            if (posX < anchorPosition.x)
            {
                posX = anchorPosition.x + forcedRightOffset * 0.5f; // 至少使用一半的偏移量
            }
        }
        else
        {
            // 原有逻辑，自动判断左右位置
            // 检查右侧是否有足够空间
            bool fitOnRight = (posX + tooltipSize.x + padding <= Screen.width);
            
            // 如果右侧没有足够空间，尝试左侧
            if (!fitOnRight)
            {
                float leftPosX = anchorPosition.x - offsetX - tooltipSize.x;
                bool fitOnLeft = (leftPosX >= padding);
                
                // 如果左侧有足够空间，放在左侧
                if (fitOnLeft)
                {
                    posX = leftPosX;
                }
                else
                {
                    // 两侧都没有足够空间，选择空间较大的一侧
                    float rightSpace = Screen.width - anchorPosition.x;
                    float leftSpace = anchorPosition.x;
                    
                    if (leftSpace > rightSpace)
                    {
                        // 放在左侧，并确保不超出边界
                        posX = padding;
                    }
                    else
                    {
                        // 放在右侧，但会超出一部分
                        posX = anchorPosition.x + offsetX;
                    }
                }
            }
            
            // 默认在鼠标上方显示，避免遮挡鼠标或UI
            posY = anchorPosition.y + offsetY + tooltipSize.y/2;
            
            // 如果上方空间不足，显示在下方
            if (posY + tooltipSize.y/2 > Screen.height - padding)
            {
                posY = anchorPosition.y - offsetY - tooltipSize.y/2;
            }
        }
        
        // 垂直位置处理 - 当强制右下方显示时
        if (forceRightPosition) 
        {
            // ==== 垂直位置：强制在下方 ====
            // 将提示框放在锚点下方，距离为offsetY
            posY = anchorPosition.y - offsetY - tooltipSize.y/2;
            
            // 确保不会超出底部边界
            if (posY - tooltipSize.y/2 < padding)
            {
                posY = tooltipSize.y/2 + padding;
            }
            

        }
        
        // 最终应用位置，确保在屏幕内
        // 对于强制右下方显示，我们只确保不超出屏幕，但不修改水平方向与鼠标的关系
        if (forceRightPosition)
        {
            // 只防止超出右边和底部边界
            float finalX = Mathf.Min(posX, Screen.width - tooltipSize.x - padding);
            float finalY = Mathf.Max(posY, tooltipSize.y/2 + padding);
            
            // 如果finalX小于鼠标位置（可能会出现在屏幕边缘），强制保持在鼠标右侧
            if (finalX < anchorPosition.x + forcedRightOffset * 0.3f) // 检查是否至少有30%的强制偏移
            {
                finalX = anchorPosition.x + forcedRightOffset * 0.5f; // 至少使用一半的强制偏移量
            }
            
            tooltipRect.position = new Vector3(finalX, finalY, 0);
            

        }
        else
        {
            // 普通模式完全控制在屏幕内
            float finalX = Mathf.Clamp(posX, padding, Screen.width - tooltipSize.x - padding);
            float finalY = Mathf.Clamp(posY, tooltipSize.y/2 + padding, Screen.height - tooltipSize.y/2 - padding);
            tooltipRect.position = new Vector3(finalX, finalY, 0);
            

        }
    }

    public static void Show(string content, bool forceShowOnRight = false)
    {
        if (current == null) return;
        
        // 激活提示面板并设置文本
        current.tooltipPanel.SetActive(true);
        current.tooltipText.text = content;
        
        // 设置是否强制在右侧显示
        current.forceRightPosition = forceShowOnRight;
        
        // 重置位置状态
        current.lastMousePosition = Vector2.zero;
        current.currentTriggerPosition = Input.mousePosition;
        current.positionLocked = false;
        
        // 延迟更新位置以获得正确尺寸
        current.CancelInvoke("DelayedUpdatePosition");
        current.Invoke("DelayedUpdatePosition", 0.05f);
    }
    
    private void DelayedUpdatePosition()
    {
        // 强制布局重建以获取正确的尺寸
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
        tooltipSize = tooltipRect.sizeDelta;
        
        // 更新位置并锁定，防止闪烁
        UpdatePosition(currentTriggerPosition);
        positionLocked = true;
    }

    public static void Hide()
    {
        if (current == null) return;
        current.tooltipPanel.SetActive(false);
        current.CancelInvoke("DelayedUpdatePosition");
        current.currentTriggerPosition = Vector2.zero;
        current.positionLocked = false;
        current.forceRightPosition = false; // 重置强制位置标志
    }
} 