using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 全局游戏强化管理 - 单例模式
/// 存储所有肉鸽元素带来的全局强化效果
/// </summary>
public class GlobalGameUpgrades : MonoBehaviour
{
    public static GlobalGameUpgrades Instance { get; private set; }

    [SerializeField] private FactionUpgradeManager leftFactionUpgrades; // 左方阵营（玩家）的强化管理器
    [SerializeField] private FactionUpgradeManager rightFactionUpgrades; // 右方阵营（AI）的强化管理器

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Found duplicate GlobalGameUpgrades instance, destroying {gameObject.name}");
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            if (leftFactionUpgrades == null || rightFactionUpgrades == null)
            {
                Debug.LogError("FactionUpgradeManagers are not assigned in the inspector!", this);
            }
            // DontDestroyOnLoad(gameObject); // 如果希望跨场景保留，则启用
        }
    }

    /// <summary>
    /// 根据阵营获取对应的强化管理器。
    /// </summary>
    public FactionUpgradeManager GetFactionUpgrades(Unit.Faction faction)
    {
        FactionUpgradeManager result = null;
        switch (faction)
        {
            case Unit.Faction.Left:
                result = leftFactionUpgrades;
                break;
            case Unit.Faction.Right:
                result = rightFactionUpgrades;
                break;
            default:
                Debug.LogWarning($"尝试获取未知阵营的强化管理器: {faction}");
                break;
        }

        if (result == null)
        {
            Debug.LogError($"FactionUpgradeManager for {faction} is null!");
        }
        return result;
    }

    /// <summary>
    /// 重置所有阵营的强化数据 (例如新游戏开始时调用)
    /// </summary>
    public void ResetAllFactionsUpgrades()
    {
        if (leftFactionUpgrades != null)
        {
            leftFactionUpgrades.ResetUpgrades();
        }
        if (rightFactionUpgrades != null)
        {
            rightFactionUpgrades.ResetUpgrades();
        }
        Debug.Log("所有阵营的游戏强化已重置。");
    }

    // 移除所有旧的公共属性和方法，因为它们现在都在 FactionUpgradeManager 中管理
} 