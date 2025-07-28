using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class MinionSpawnUI : MonoBehaviour
{
    [System.Serializable]
    public class SquadUIElement
    {
        public GameObject root;
        public TextMeshProUGUI costText;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI keyText;
        public Image cooldownOverlay;
        public Image costOverlay;
        public Image unitImage;
    }

    public List<SquadUIElement> squadUIElements = new List<SquadUIElement>();
    [SerializeField] private TextMeshProUGUI goldText;

    // 新增：公开 LeftSquadUI 属性供 MinionSpawnUIPrefab 访问
    public List<SquadUIElement> LeftSquadUI => squadUIElements;

    [Header("配置")]
    [SerializeField] private MinionSpawner spawner;
    [SerializeField] private float updateInterval = 0.1f;

    private float updateTimer = 0f;

    private void Start()
    {
        // UI元素的初始化现在由 MinionSpawnUIPrefab 负责
        
        // 订阅剑术大师解锁状态改变事件
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager leftManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Left);
            FactionUpgradeManager rightManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Right);
            
            if (leftManager != null)
            {
                leftManager.OnSwordMasterUnlockChanged += OnSwordMasterUnlockChanged;
            }
            
            if (rightManager != null)
            {
                rightManager.OnSwordMasterUnlockChanged += OnSwordMasterUnlockChanged;
            }
        }
    }
    
    private void OnDestroy()
    {
        // 取消订阅事件
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager leftManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Left);
            FactionUpgradeManager rightManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Right);
            
            if (leftManager != null)
            {
                leftManager.OnSwordMasterUnlockChanged -= OnSwordMasterUnlockChanged;
            }
            
            if (rightManager != null)
            {
                rightManager.OnSwordMasterUnlockChanged -= OnSwordMasterUnlockChanged;
            }
        }
    }
    
    // 剑术大师解锁状态改变时刷新UI
    private void OnSwordMasterUnlockChanged(bool isUnlocked)
    {
        RefreshSwordMasterUI();
        
        // 强制重新创建UI元素
        MinionSpawnUIPrefab prefabComponent = GetComponent<MinionSpawnUIPrefab>();
        if (prefabComponent != null)
        {
            // 清空现有UI元素
            foreach (var element in squadUIElements)
            {
                if (element.root != null)
                {
                    Destroy(element.root);
                }
            }
            squadUIElements.Clear();
            
            // 重新创建UI元素
            prefabComponent.RecreateSquadUI();
        }
    }
    
    // 刷新剑术大师UI显示
    public void RefreshSwordMasterUI()
    {
        // 获取剑术大师解锁状态
        bool leftSwordMasterUnlocked = false;
        bool rightSwordMasterUnlocked = false;
        
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager leftManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Left);
            FactionUpgradeManager rightManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Right);
            
            if (leftManager != null)
            {
                leftSwordMasterUnlocked = leftManager.swordMasterUnlocked;
            }
            
            if (rightManager != null)
            {
                rightSwordMasterUnlocked = rightManager.swordMasterUnlocked;
            }
        }
        
        // 获取问号图标
        Sprite questionMarkSprite = null;
        MinionSpawnUIPrefab prefabComponent = GetComponent<MinionSpawnUIPrefab>();
        if (prefabComponent != null)
        {
            questionMarkSprite = prefabComponent.questionMarkSprite;
        }
        
        // 更新UI显示
        for (int i = 0; i < squadUIElements.Count && i < spawner.LeftSquads.Count; i++)
        {
            var squad = spawner.LeftSquads[i];
            var ui = squadUIElements[i];
            
            // 检查是否是剑术大师小队
            bool isSwordMasterSquad = squad.minionPrefab != null && squad.minionPrefab.GetComponent<SwordMasterController>() != null;
            
            if (isSwordMasterSquad)
            {
                // 根据解锁状态更新UI
                if (leftSwordMasterUnlocked)
                {
                    ui.costText.text = squad.cost.ToString();
                    ui.unitImage.sprite = squad.squadIcon;
                }
                else
                {
                    ui.costText.text = "?";
                    ui.unitImage.sprite = questionMarkSprite != null ? questionMarkSprite : squad.squadIcon;
                }
            }
        }
    }

    private void Update()
    {
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            UpdateUI();
            updateTimer = 0f;
        }
    }

    private void UpdateUI()
    {
        // 更新金币显示
        goldText.text = $"{spawner.GetLeftGold():F0}";

        // 获取剑术大师解锁状态
        bool swordMasterUnlocked = false;
        if (GlobalGameUpgrades.Instance != null)
        {
            FactionUpgradeManager factionManager = GlobalGameUpgrades.Instance.GetFactionUpgrades(Unit.Faction.Left);
            if (factionManager != null)
            {
                swordMasterUnlocked = factionManager.swordMasterUnlocked;
            }
        }

        // 更新每个小队的UI状态
        for (int i = 0; i < squadUIElements.Count && i < spawner.LeftSquads.Count; i++)
        {
            var squad = spawner.LeftSquads[i];
            var ui = squadUIElements[i];
            
            // 检查是否是剑术大师小队
            bool isSwordMasterSquad = squad.minionPrefab != null && squad.minionPrefab.GetComponent<SwordMasterController>() != null;
            
            // 如果是剑术大师小队且未解锁，则不显示冷却
            if (isSwordMasterSquad && !swordMasterUnlocked)
            {
                ui.cooldownOverlay.gameObject.SetActive(false);
                continue;
            }

            // 更新冷却显示
            float cooldown = spawner.GetSquadCooldown(squad);
            if (cooldown > 0)
            {
                ui.cooldownOverlay.gameObject.SetActive(true);
                ui.cooldownOverlay.fillAmount = cooldown / squad.cooldown;
            }
            else
            {
                ui.cooldownOverlay.gameObject.SetActive(false);
            }
        }
    }

    // 新增：SetResourceTexts 方法，用于接收金币文本引用
    public void SetResourceTexts(TextMeshProUGUI leftText, TextMeshProUGUI rightText = null)
    {
        // 假设 MinionSpawnUI 负责左方金币文本，因此将传入的 leftText 赋值给 goldText
        // rightText 参数在此处不使用，但为了匹配 MinionSpawnUIPrefab 的调用签名而保留
        if (leftText != null)
        {
            this.goldText = leftText;
        }
    }

    // 新增：提供金币不足时的闪烁提示
    public void FlashCostOverlay(KeyCode key)
    {
        foreach (var uiElement in squadUIElements)
        {
            // 注意：这里需要从 MinionSpawner 找到对应的 MinionSquad 来获取 KeyCode
            // 暂时假设 keyText 的文本就是 KeyCode 的最后一个字符
            // 更严谨的做法是在 SquadUIElement 中存储对 MinionSquad 的引用
            // 或者 MinionSpawnUI 知道 squad 的 KeyCode 列表
            
            // 遍历 MinionSpawner 的 LeftSquads 找到对应的 MinionSquad
            MinionSpawner.MinionSquad targetSquad = null;
            foreach (var squad in spawner.LeftSquads)
            {
                if (squad.spawnKey == key)
                {
                    targetSquad = squad;
                    break;
                }
            }

            if (targetSquad != null && uiElement.keyText.text == targetSquad.spawnKey.ToString().Last().ToString())
            {
                StartCoroutine(DoFlash(uiElement.costOverlay));
                break;
            }
        }
    }

    private IEnumerator DoFlash(Image overlayImage)
    {
        if (overlayImage != null)
        {
            overlayImage.gameObject.SetActive(true);
            yield return new WaitForSeconds(1f); // 闪烁1秒
            overlayImage.gameObject.SetActive(false);
        }
    }
} 