using UnityEngine;

namespace MagicBattle
{
    [CreateAssetMenu(fileName = "HeroData", menuName = "MagicBattle/Hero Data")]
    public class HeroDataSO : ScriptableObject
    {
        [Header("基本信息")]
        public HeroType heroType;
        public string heroName;
        public string description;
        public Sprite heroSprite;

        [Header("战斗属性")]
        public float maxHealth = 100f;
        public float moveSpeed = 5f;
        public float attackDamage = 10f;
        public float attackSpeed = 1f;

        [Header("技能配置")]
        public UIHeroSkillData[] skills;
    }

    public enum HeroType
    {
        Knight,     // 骑士王
        Necromancer // 死灵法师
    }
} 