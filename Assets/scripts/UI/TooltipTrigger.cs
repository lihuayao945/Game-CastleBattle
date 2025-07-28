using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 提示触发器 - 用于在鼠标悬停时显示提示信息
/// </summary>
namespace MagicBattle
{
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea(3, 10)]
        public string tooltipText;
        
        [Tooltip("勾选此选项可强制提示框显示在鼠标右下方，适用于左上角图标等位置")]
        public bool forceShowOnRight = false;

        private void OnMouseEnter()
        {
            ShowTooltip();
        }

        private void OnMouseExit()
        {
            HideTooltip();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltip();
        }

        private void ShowTooltip()
        {
            if (!string.IsNullOrEmpty(tooltipText))
            {
                TooltipSystem.Show(tooltipText, forceShowOnRight);
            }
        }

        public void HideTooltip()
        {
            TooltipSystem.Hide();
        }
    }
} 