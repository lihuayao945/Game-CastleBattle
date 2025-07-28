using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MagicBattle
{
    /// <summary>
    /// 游戏开始界面 - 控制开始菜单的按钮事件
    /// </summary>
    public class GameStartUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button singlePlayerButton;
        [SerializeField] private Button multiPlayerButton;
        [SerializeField] private Button quitButton;
        
        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI singlePlayerText;
        [SerializeField] private TextMeshProUGUI multiPlayerText;
        [SerializeField] private TextMeshProUGUI quitText;
        
        private void Start()
        {
            // 添加按钮监听
            singlePlayerButton.onClick.AddListener(OnSinglePlayerClick);
            multiPlayerButton.onClick.AddListener(OnMultiPlayerClick);
            quitButton.onClick.AddListener(OnQuitClick);
            
            // 设置按钮文本
            singlePlayerText.text = "单人模式";
            multiPlayerText.text = "PVP模式";
            quitText.text = "退出游戏";
        }
        
        private void OnSinglePlayerClick()
        {
            UIManager.Instance.ShowHeroSelect();
        }
        
        private void OnMultiPlayerClick()
        {
            // TODO: 实现网络对战逻辑
            Debug.Log("网络对战功能暂未实现");
        }
        
        private void OnQuitClick()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
} 