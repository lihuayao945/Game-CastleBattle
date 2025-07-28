using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

namespace MagicBattle
{
    /// <summary>
    /// 游戏状态枚举
    /// </summary>
    public enum GameState
    {
        NotStarted,  // 游戏未开始
        Playing,     // 游戏进行中
        Paused,      // 游戏暂停
        GameOver     // 游戏结束
    }

    [System.Serializable]
    public class UIHeroSkillData
    {
        public string name;
        public string description;
        public Sprite icon;
        [Tooltip("技能冷却时间（秒）")]
        public float cooldown = 1f;
    }
} 