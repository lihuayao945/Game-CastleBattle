using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

namespace MagicBattle
{
    /// <summary>
    /// 英雄选择界面 - 控制英雄选择的UI交互
    /// </summary>
    public class HeroSelectUI : MonoBehaviour
    {
        [Header("Title")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Hero Display")]
        [SerializeField] private Image heroImage;
        [SerializeField] private TextMeshProUGUI heroNameText;
        [SerializeField] private TextMeshProUGUI heroDescriptionText;
        private Vector3 heroImageInitialPosition; // 保存英雄图片的初始位置

        [Header("Skills Panel")]
        [SerializeField] private Transform skillsPanel;
        [SerializeField] private Image[] skillIcons;
        [SerializeField] private TextMeshProUGUI[] skillDescriptions;

        [Header("Navigation")]
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button selectButton;

        [Header("Hero Data")]
        [SerializeField] private HeroDataSO[] heroes = new HeroDataSO[2];
        private int currentHeroIndex = 0;

        //[Header("Animation Settings")]
        //[SerializeField] private float switchDuration = 0.5f;
        //[SerializeField] private float switchDistance = 100f;

        private void Start()
        {
            // 保存英雄图片的初始位置
            heroImageInitialPosition = heroImage.transform.localPosition;

            // 设置标题
            titleText.text = "选择英雄";

            // 设置按钮文本
            prevButton.GetComponentInChildren<TextMeshProUGUI>().text = "";
            nextButton.GetComponentInChildren<TextMeshProUGUI>().text = "";
            selectButton.GetComponentInChildren<TextMeshProUGUI>().text = "选择";

            // 添加按钮监听
            selectButton.onClick.AddListener(OnHeroSelect);
            prevButton.onClick.AddListener(OnPrevHero);
            nextButton.onClick.AddListener(OnNextHero);

            // 初始化显示第一个英雄
            UpdateHeroDisplay();

            // 在英雄选择阶段开始对象池预热
            StartObjectPoolPrewarm();
        }

        private void UpdateHeroDisplay()
        {
            HeroDataSO currentHero = heroes[currentHeroIndex];
            heroNameText.text = currentHero.heroName;
            heroDescriptionText.text = currentHero.description;
            heroImage.sprite = currentHero.heroSprite;
            
            // 重置英雄图片位置到初始位置
            heroImage.transform.localPosition = heroImageInitialPosition;

            // 更新技能信息
            for (int i = 0; i < 4; i++)
            {
                if (i < currentHero.skills.Length)
                {
                    skillIcons[i].sprite = currentHero.skills[i].icon;
                    skillDescriptions[i].text = currentHero.skills[i].description;
                    skillIcons[i].gameObject.SetActive(true);
                }
                else
                {
                    skillIcons[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnPrevHero()
        {
            currentHeroIndex = (currentHeroIndex - 1 + heroes.Length) % heroes.Length;
            UpdateHeroDisplay();
        }

        private void OnNextHero()
        {
            currentHeroIndex = (currentHeroIndex + 1) % heroes.Length;
            UpdateHeroDisplay();
        }

        private void OnHeroSelect()
        {
            // 保存选中的英雄索引
            PlayerPrefs.SetInt("SelectedHeroIndex", currentHeroIndex);
            PlayerPrefs.Save();

            // 切换到游戏面板
            UIManager.Instance.ShowGame();
        }

        /// <summary>
        /// 在英雄选择阶段开始对象池预热
        /// </summary>
        private void StartObjectPoolPrewarm()
        {
            // 小兵单位对象池预热
            if (UnitPoolManager.Instance != null)
            {
                UnitPoolManager.Instance.StartBatchPrewarm();
                //Debug.Log("英雄选择阶段：开始小兵单位对象池预热");
            }
            else
            {
                //Debug.LogWarning("小兵单位对象池管理器不存在，无法进行预热");
            }

            // 小兵图标对象池预热
            if (MagicBattle.MinionIconPool.Instance != null)
            {
                MagicBattle.MinionIconPool.Instance.StartPrewarm();
                //Debug.Log("英雄选择阶段：开始小兵图标对象池预热");
            }
            else
            {
                //Debug.LogWarning("小兵图标对象池管理器不存在，无法进行预热");
            }
        }
    }
}