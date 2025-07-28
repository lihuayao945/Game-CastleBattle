using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class MinionSpawnUIPrefab : MonoBehaviour
{
    [Header("左方UI")]
    // [SerializeField] private GameObject leftPanel; // 删除：此字段未使用
    [SerializeField] private TextMeshProUGUI leftGoldText;
    [SerializeField] private Transform leftSquadContainer;
    
    [Header("小队UI预制体")]
    [SerializeField] private GameObject squadCardPrefab;
    
    [SerializeField] private MinionSpawner minionSpawner; // 新增：MinionSpawner 引用

    private MinionSpawnUI spawnUI;
    
    private void Awake()
    {
        spawnUI = GetComponent<MinionSpawnUI>();
        if (spawnUI == null)
        {
            Debug.LogError("MinionSpawnUIPrefab: MinionSpawnUI component not found!");
            return;
        }
        
        // 设置资源文本
        spawnUI.SetResourceTexts(leftGoldText, null);
        
        // 创建左方小队UI
        CreateSquadUIElements(leftSquadContainer, spawnUI.squadUIElements, minionSpawner); // 修改：传入 minionSpawner 和 spawnUI.squadUIElements
        
        // 设置UI位置
        SetupUIPosition();
    }
    
    private void SetupUIPosition()
    {
        // 获取GamePanel
        GameObject gamePanel = GameObject.Find("GamePanel");
        if (gamePanel == null)
        {
            Debug.LogError("MinionSpawnUIPrefab: GamePanel not found!");
            return;
        }
        
        // 设置父物体
        transform.SetParent(gamePanel.transform, false);
        
        // 获取RectTransform
        RectTransform rectTransform = GetComponent<RectTransform>();
        
        // 设置锚点
        rectTransform.anchorMin = new Vector2(0, 0.5f);
        rectTransform.anchorMax = new Vector2(0, 0.5f);
        rectTransform.pivot = new Vector2(0, 0.5f);
        
        // 设置位置和大小
        rectTransform.anchoredPosition = new Vector2(0, 0); // 距离左侧20像素，垂直居中
        rectTransform.sizeDelta = new Vector2(150, 600); // 宽度150，高度600（6张卡牌）
        
        // 设置金币文本位置
        RectTransform goldTextRect = leftGoldText.GetComponent<RectTransform>();
        goldTextRect.anchorMin = new Vector2(0, 1);
        goldTextRect.anchorMax = new Vector2(0, 1);
        goldTextRect.pivot = new Vector2(0, 1);
        goldTextRect.anchoredPosition = new Vector2(100, -720);
        
        // 设置小队容器
        RectTransform containerRect = leftSquadContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 0);
        containerRect.anchorMax = new Vector2(1, 1);
        containerRect.offsetMin = new Vector2(0, 0);
        containerRect.offsetMax = new Vector2(0, 0); // 修改：不为金币文本预留空间，让容器充满整个高度
        
        // 设置垂直布局组
        VerticalLayoutGroup layoutGroup = leftSquadContainer.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.spacing = 0;
            layoutGroup.padding = new RectOffset(0, 0, 0, 0);
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = true;
        }
    }
    
    [SerializeField] public Sprite questionMarkSprite; // 添加可在Inspector中拖入的问号图标引用
    
    private void CreateSquadUIElements(Transform container, List<MinionSpawnUI.SquadUIElement> uiElements, MinionSpawner minionSpawner)
    {
        // 使用Inspector中设置的问号图标
        
        // 遍历 MinionSpawner 中的每个小队配置，动态创建UI元素
        foreach (var squad in minionSpawner.LeftSquads)
        {
            GameObject squadCard = Instantiate(squadCardPrefab, container);

            MinionSpawnUI.SquadUIElement ui = new MinionSpawnUI.SquadUIElement();
            
            // 获取UI组件
            ui.costText = squadCard.transform.Find("CostPanel/CostText").GetComponent<TextMeshProUGUI>();
            ui.keyText = squadCard.transform.Find("UnitImage/KeyText").GetComponent<TextMeshProUGUI>();
            ui.cooldownOverlay = squadCard.transform.Find("UnitImage/CooldownFill").GetComponent<Image>();
            ui.unitImage = squadCard.transform.Find("UnitImage").GetComponent<Image>();
            ui.costOverlay = squadCard.transform.Find("UnitImage/CostOverlay").GetComponent<Image>();
            ui.root = squadCard;
            if (ui.costOverlay == null)
            {
                Debug.LogError($"无法在 {squadCard.name} 中找到 CostOverlay。请检查预制体层级和名称。");
            }
            // 确保CostOverlay在初始时是禁用的
            if (ui.costOverlay != null)
            {
                ui.costOverlay.gameObject.SetActive(false);
                // 确保CostOverlay在运行时始终渲染在最上层
                ui.costOverlay.transform.SetAsLastSibling();
            }
            
            // 检查是否是剑术大师小队
            bool isSwordMasterSquad = squad.minionPrefab != null && squad.minionPrefab.GetComponent<SwordMasterController>() != null;
            bool swordMasterUnlocked = false;
            
            // 检查剑术大师是否已解锁
            if (isSwordMasterSquad && GlobalGameUpgrades.Instance != null)
            {
                FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Left);
                if (factionManager != null)
                {
                    swordMasterUnlocked = factionManager.swordMasterUnlocked;
                }
            }
            
            // 设置UI元素内容
            if (isSwordMasterSquad && !swordMasterUnlocked)
            {
                // 未解锁剑术大师，显示问号
                ui.costText.text = "?";
                ui.unitImage.sprite = questionMarkSprite != null ? questionMarkSprite : squad.squadIcon;
            }
            else
            {
                // 正常显示
                ui.costText.text = squad.cost.ToString();
                ui.unitImage.sprite = squad.squadIcon;
            }
            
            ui.keyText.text = squad.spawnKey.ToString().Last().ToString(); // 修改：只显示最后一个字符

            // 将新创建的UI元素添加到列表中
            uiElements.Add(ui);
            
            // 设置卡片布局
            RectTransform cardRect = squadCard.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(150, 100); // 设置卡片大小
        }
    }

    // 公共方法，用于重新创建UI元素
    public void RecreateSquadUI()
    {
        // 清空容器中的所有子物体
        foreach (Transform child in leftSquadContainer)
        {
            Destroy(child.gameObject);
        }
        
        // 重新创建UI元素
        if (spawnUI != null)
        {
            spawnUI.squadUIElements.Clear();
            CreateSquadUIElements(leftSquadContainer, spawnUI.squadUIElements, minionSpawner);
        }
    }
} 