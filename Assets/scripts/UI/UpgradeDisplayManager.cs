using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using MagicBattle; // 假设 FactionUpgradeManager 在 MagicBattle 命名空间下
using UnityEngine.EventSystems;
using System.Collections; // 引入 System.Collections 命名空间以使用 Coroutine

/// <summary>
/// 强化数据显示管理器，负责展示玩家已获得的各项强化
/// </summary>
public class UpgradeDisplayManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform leftFactionUpgradeContainer; // 左侧阵营强化显示区域
    [SerializeField] private Transform rightFactionUpgradeContainer; // 右侧阵营强化显示区域
    [SerializeField] private GameObject upgradeItemPrefab; // 单个强化条目的预制体 (包含图标和名称)
    [SerializeField] private GameObject upgradeDescriptionTooltipPrefab; // 新增：强化描述提示框预制体

    // 新增：GridLayoutGroup 的引用
    [Header("Layout Group References")]
    [SerializeField] private GridLayoutGroup leftFactionGridLayoutGroup;
    [SerializeField] private GridLayoutGroup rightFactionGridLayoutGroup;

    private GameObject currentUpgradeTooltipInstance; // 当前活跃的强化描述提示框实例

    private void OnEnable()
    {
        // 当面板启用时，刷新强化显示
        DisplayUpgrades();
    }

    private void OnDisable()
    {
        // 当面板禁用时，隐藏所有提示框，防止残留
        HideUpgradeDescriptionTooltip();
    }

    /// <summary>
    /// 刷新并显示所有已获得的强化数据
    /// </summary>
    public void DisplayUpgrades()
    {
        // 1. 在清除和实例化之前，暂时禁用布局组
        if (leftFactionGridLayoutGroup != null && leftFactionGridLayoutGroup.isActiveAndEnabled)
        {
            leftFactionGridLayoutGroup.enabled = false;
        }
        if (rightFactionGridLayoutGroup != null && rightFactionGridLayoutGroup.isActiveAndEnabled)
        {
            rightFactionGridLayoutGroup.enabled = false;
        }

        // 2. 清除现有显示
        ClearUpgradeDisplay(leftFactionUpgradeContainer);
        ClearUpgradeDisplay(rightFactionUpgradeContainer);

        // 3. 获取左右阵营的强化数据
        FactionUpgradeManager leftFactionUpgrades = GlobalGameUpgrades.Instance?.GetFactionUpgrades(Unit.Faction.Left);
        FactionUpgradeManager rightFactionUpgrades = GlobalGameUpgrades.Instance?.GetFactionUpgrades(Unit.Faction.Right);

        if (leftFactionUpgrades != null)
        {
            InstantiateUpgradeItems(leftFactionUpgrades.GetAllActiveUpgrades(), leftFactionUpgradeContainer);
        }
        if (rightFactionUpgrades != null)
        {
            InstantiateUpgradeItems(rightFactionUpgrades.GetAllActiveUpgrades(), rightFactionUpgradeContainer);
        }

        // 4. 启动协程来延迟布局更新
        StartCoroutine(EnableLayoutGroupsAndRebuildAfterFrame());
    }

    private IEnumerator EnableLayoutGroupsAndRebuildAfterFrame()
    {
        // 1. 等待所有子物体实例化并激活
        yield return null;

        // 2. 重新启用 GridLayoutGroups (确保其属性可读，例如 cellSize, spacing)
        if (leftFactionGridLayoutGroup != null && leftFactionGridLayoutGroup.isActiveAndEnabled)
        {
            leftFactionGridLayoutGroup.enabled = true;
        }
        if (rightFactionGridLayoutGroup != null && rightFactionGridLayoutGroup.isActiveAndEnabled)
        {
            rightFactionGridLayoutGroup.enabled = true;
        }

        // 3. 手动计算并设置Content高度
        ManualSetContentHeight(leftFactionUpgradeContainer, leftFactionUpgradeContainer != null ? leftFactionUpgradeContainer.childCount : 0);
        ManualSetContentHeight(rightFactionUpgradeContainer, rightFactionUpgradeContainer != null ? rightFactionUpgradeContainer.childCount : 0);
        
        // 4. 重置滚动条位置
        ResetScrollViews();
    }

    private void ResetScrollViews()
    {
        ResetScrollView(leftFactionUpgradeContainer);
        ResetScrollView(rightFactionUpgradeContainer);
    }

    private void ResetScrollView(Transform contentContainer)
    {
        if (contentContainer == null || contentContainer.parent == null) return;
        var scrollRect = contentContainer.parent.parent?.GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f; // 重置到顶部
            if (scrollRect.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
                scrollRect.Rebuild(CanvasUpdate.PostLayout);
            }
        }
    }

    /// <summary>
    /// 根据子物体数量手动计算并设置Content的高度。
    /// </summary>
    /// <param name="container">包含子物体的Transform（通常是GridLayoutGroup所在的Transform）</param>
    /// <param name="childCount">当前容器中的子物体数量</param>
    private void ManualSetContentHeight(Transform container, int childCount)
    {
        if (container == null || childCount == 0)
        {
            return;
        }

        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        if (grid == null || !grid.isActiveAndEnabled)
        {
            return;
        }

        // 从GridLayoutGroup获取参数
        float itemHeight = grid.cellSize.y;
        float spacingY = grid.spacing.y;
        int constraintCount = grid.constraintCount;

        if (constraintCount == 0)
        {
            constraintCount = 1; // Prevent division by zero
        }

        int rows = Mathf.CeilToInt(childCount / (float)constraintCount);
        // 计算总高度：行数 * 单个item高度 + (行数 - 1) * 垂直间距
        float totalHeight = rows * itemHeight + (rows - 1) * spacingY;
        
        // 确保至少有一行时，间距不为负
        if (rows <= 1) {
            totalHeight = rows * itemHeight; // 如果只有一行，没有间距
        }


        // 设置容器自身的RectTransform高度
        var rect = container.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
        }

        // 同时设置父级Content的RectTransform高度（如果ContentSizeFitter失效）
        if (container.parent != null)
        {
            var contentRect = container.parent.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
            }
        }
    }

    /// <summary>
    /// 为给定的强化列表实例化UI条目
    /// </summary>
    /// <param name="upgrades">要显示的强化列表</param>
    /// <param name="container">强化UI条目的父Transform</param>
    private void InstantiateUpgradeItems(List<UpgradeDataSO> upgrades, Transform container)
    {
        foreach (var upgrade in upgrades)
        {
            if (upgradeItemPrefab == null)
            {
                Debug.LogError("UpgradeDisplayManager: upgradeItemPrefab is not assigned!");
                return;
            }

            GameObject item = Instantiate(upgradeItemPrefab, container);
            UpgradeItemUI upgradeItemUI = item.GetComponent<UpgradeItemUI>();

            if (upgradeItemUI == null)
            {
                Debug.LogError($"UpgradeDisplayManager: UpgradeItemPrefab '{upgradeItemPrefab.name}' is missing UpgradeItemUI component!");
                Destroy(item); // 销毁错误的实例
                continue;
            }

            // 精确查找图标和名称组件
            Image iconImage = item.transform.Find("UpgradeIcon")?.GetComponent<Image>();
            TextMeshProUGUI nameText = item.transform.Find("UpgradeName")?.GetComponent<TextMeshProUGUI>();

            if (iconImage == null)
            {
                Debug.LogWarning($"UpgradeDisplayManager: UpgradeItemPrefab for '{upgrade.name}' is missing an 'UpgradeIcon' Image child.");
            }
            if (nameText == null)
            {
                Debug.LogWarning($"UpgradeDisplayManager: UpgradeItemPrefab for '{upgrade.name}' is missing an 'UpgradeName' TextMeshProUGUI child.");
            }

            // 初始化UpgradeItemUI组件
            upgradeItemUI.Initialize(upgrade.icon, upgrade.upgradeName, upgrade.description);

            // 订阅UpgradeItemUI的事件
            upgradeItemUI.OnShowUpgradeTooltip += ShowUpgradeDescriptionTooltip;
            upgradeItemUI.OnHideUpgradeTooltip += HideUpgradeDescriptionTooltip;
        }
        // 新增这一行日志
                    //Debug.Log($"Instantiated {container.childCount} items into {container.name}.");
        //foreach (Transform child in container)
            //Debug.Log($"[DEBUG] 子物体: {child.name}, activeSelf={child.gameObject.activeSelf}, Height={child.GetComponent<RectTransform>().rect.height}");
    }

    /// <summary>
    /// 清除指定容器中的所有子UI元素
    /// </summary>
    /// <param name="container">要清除的Transform容器</param>
    private void ClearUpgradeDisplay(Transform container)
    {
        // 取消订阅所有现有条目的事件，防止内存泄漏和重复事件
        // 使用一个列表来收集子对象，避免在遍历时修改集合
        List<GameObject> childrenToDestroy = new List<GameObject>();
        foreach (Transform child in container)
        {
            childrenToDestroy.Add(child.gameObject);
        }

        foreach (GameObject childGO in childrenToDestroy)
        {
            if (childGO != null) // 再次检查是否为空，以防万一
            {
                UpgradeItemUI itemUI = childGO.GetComponent<UpgradeItemUI>();
                if (itemUI != null)
                {
                    itemUI.OnShowUpgradeTooltip -= ShowUpgradeDescriptionTooltip;
                    itemUI.OnHideUpgradeTooltip -= HideUpgradeDescriptionTooltip;
                }
                Destroy(childGO);
            }
        }
    }

    /// <summary>
    /// 显示强化描述提示框
    /// </summary>
    /// <param name="description">强化描述文本</param>
    /// <param name="mousePosition">鼠标当前位置</param>
    private void ShowUpgradeDescriptionTooltip(string description, Vector2 mousePosition)
    {
        // 如果当前有提示框实例，先销毁它，确保只有一个
        if (currentUpgradeTooltipInstance != null)
        {
            Destroy(currentUpgradeTooltipInstance);
        }

        if (upgradeDescriptionTooltipPrefab == null)
        {
            Debug.LogError("UpgradeDisplayManager: upgradeDescriptionTooltipPrefab is not assigned!");
            return;
        }

        // 实例化提示框，并将其设置为Canvas的子对象 (通常是UIManager所在的Canvas)
        Canvas parentCanvas = FindObjectOfType<Canvas>(); // 寻找场景中的Canvas
        if (parentCanvas == null)
        {
            Debug.LogError("UpgradeDisplayManager: Cannot find Canvas to instantiate tooltip.");
            return;
        }

        currentUpgradeTooltipInstance = Instantiate(upgradeDescriptionTooltipPrefab, parentCanvas.transform);

        // 设置提示文本 (通过名称精确查找 'DescriptionText' 组件)
        TextMeshProUGUI descriptionText = currentUpgradeTooltipInstance.transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
        if (descriptionText != null)
        {
            descriptionText.text = description;
        }
        else
        {
            Debug.LogWarning("UpgradeDisplayManager: Tooltip prefab is missing a TextMeshProUGUI component (named 'DescriptionText' recommended)!");
        }

        // 定位提示框 (可以根据鼠标位置进行偏移和边界检查)
        RectTransform tooltipRect = currentUpgradeTooltipInstance.GetComponent<RectTransform>();
        if (tooltipRect != null)
        {
            // 设置初始偏移量和屏幕边界检查
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float tooltipWidth = tooltipRect.rect.width * tooltipRect.localScale.x;
            float tooltipHeight = tooltipRect.rect.height * tooltipRect.localScale.y;
            
            // 默认设置 - 在鼠标右下方显示
            Vector2 pivot = new Vector2(0f, 1f); // 默认左上角锚点
            Vector2 offset = new Vector2(10f, -10f);
            
            // 水平方向调整
            if (mousePosition.x + tooltipWidth + offset.x > screenWidth)
            {
                // 如果右侧空间不足，切换到左侧显示
                pivot.x = 1f;
                offset.x = -10f;
            }
            
            // 垂直方向调整
            if (mousePosition.y + offset.y - tooltipHeight < 0)
            {
                // 如果底部空间不足，切换到上方显示
                pivot.y = 0f;
                offset.y = 10f;
            }
            
            // 应用锚点和位置
            tooltipRect.pivot = pivot;
            tooltipRect.position = mousePosition + offset;
            
            // 进行最终的边界检查和调整
            Vector3 finalPosition = tooltipRect.position;
            Vector3[] corners = new Vector3[4];
            tooltipRect.GetWorldCorners(corners);
            
            // 检查并调整水平位置
            if (corners[2].x > screenWidth) // 右边界检查
            {
                finalPosition.x -= (corners[2].x - screenWidth);
            }
            if (corners[0].x < 0) // 左边界检查
            {
                finalPosition.x -= corners[0].x;
            }
            
            // 检查并调整垂直位置
            if (corners[1].y > screenHeight) // 上边界检查
            {
                finalPosition.y -= (corners[1].y - screenHeight);
            }
            if (corners[0].y < 0) // 下边界检查
            {
                finalPosition.y -= corners[0].y;
            }
            
            tooltipRect.position = finalPosition;
        }
    }

    /// <summary>
    /// 隐藏强化描述提示框
    /// </summary>
    private void HideUpgradeDescriptionTooltip()
    {
        if (currentUpgradeTooltipInstance != null)
        {
            Destroy(currentUpgradeTooltipInstance);
            currentUpgradeTooltipInstance = null;
        }
    }
} 