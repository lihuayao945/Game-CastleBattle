using UnityEngine;

/// <summary>
/// 强化测试管理器 - 用于测试特定强化效果
/// </summary>
public class UpgradeTestManager : MonoBehaviour
{
    [Header("测试配置")]
    [SerializeField] private UpgradeDataSO forcedFirstUpgrade; // 要强制作为第一个选项的强化
    [SerializeField] private bool enableTestMode = true; // 是否启用测试模式
    
    [Header("引用")]
    [SerializeField] private UpgradeSelectionUI upgradeSelectionUI; // 强化选择UI引用
    
    private void Start()
    {
        // 如果没有指定强化选择UI，尝试在场景中查找
        if (upgradeSelectionUI == null)
        {
            upgradeSelectionUI = FindObjectOfType<UpgradeSelectionUI>();
            if (upgradeSelectionUI == null)
            {
                Debug.LogError("无法找到 UpgradeSelectionUI 组件，测试管理器无法工作。");
                return;
            }
        }
        
        // 设置强制第一个强化选项
        if (forcedFirstUpgrade != null)
        {
            upgradeSelectionUI.SetForcedFirstUpgrade(forcedFirstUpgrade, enableTestMode);
            //Debug.Log($"[测试管理器] 已设置强制第一个强化为: {forcedFirstUpgrade.upgradeName}");
        }
        else if (enableTestMode)
        {
            Debug.LogWarning("[测试管理器] 测试模式已启用，但未指定强制第一个强化。");
        }
    }
    
    // 可以在游戏运行时通过编辑器调用此方法来更改测试设置
    public void UpdateTestSettings(UpgradeDataSO newForcedUpgrade, bool newEnableTestMode)
    {
        forcedFirstUpgrade = newForcedUpgrade;
        enableTestMode = newEnableTestMode;
        
        if (upgradeSelectionUI != null)
        {
            upgradeSelectionUI.SetForcedFirstUpgrade(forcedFirstUpgrade, enableTestMode);
            //Debug.Log($"[测试管理器] 已更新测试设置: 强制强化 = {(forcedFirstUpgrade != null ? forcedFirstUpgrade.upgradeName : "无")}, 测试模式 = {enableTestMode}");
        }
    }
} 