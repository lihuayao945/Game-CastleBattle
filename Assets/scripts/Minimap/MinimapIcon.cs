using UnityEngine;
using MagicBattle; // 添加命名空间引用

namespace MagicBattle
{
    /// <summary>
    /// 小地图图标 - 在小地图上显示单位的图标
    /// </summary>
    public class MinimapIcon : MonoBehaviour
    {
        public enum IconType
        {
            Hero,
            Minion,
            Castle
        }
        
        public enum TeamColor
        {
            Blue,   // 左边阵营
            Red     // 右边阵营
        }

        // 新增：池化类型枚举
        private enum IconPoolType
        {
            Static,    // 静态图标（英雄、城堡）
            Pooled     // 池化图标（小兵）
        }

        [Header("图标设置")]
        [SerializeField] private IconType iconType = IconType.Minion;
        [SerializeField] private TeamColor teamColor = TeamColor.Blue;
        [SerializeField] private float iconSize = 3.0f; // 默认图标尺寸设为3.0
        
        [Header("自动检测")]
        [SerializeField] private bool autoDetectTeam = true;
        [SerializeField] private bool autoDetectType = true;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        [Header("性能优化")]
        [SerializeField] private float updateInterval = 0.1f; // 位置更新间隔（秒）

        // 图标游戏对象
        private GameObject iconObject;
        private SpriteRenderer iconRenderer;

        // 性能优化相关
        private float lastUpdateTime = 0f;

        // 新增：池化相关字段
        private IconPoolType poolType;
        private PooledMinionIcon pooledIcon; // 仅小兵使用

        // 图标预设
        private static Sprite heroSprite;
        private static Sprite minionSprite;
        private static Sprite castleSprite;

        // 默认材质
        private static Material defaultSpriteMaterial;

        // 游戏状态相关
        private bool isIconCreated = false;
        
        private void Start()
        {
            // 自动检测单位类型和阵营
            if (autoDetectType || autoDetectTeam)
            {
                AutoDetectSettings();
            }

            // 确定池化类型
            poolType = GetIconPoolType();

            // 只在游戏进行中才创建图标
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                CreateIcon();
            }
            else
            {
                // 延迟检查游戏状态并创建图标
                Invoke("CheckGameStateAndCreateIcon", 0.5f);
            }

            // 输出调试信息
            if (showDebugInfo)
            {
                //Debug.Log($"MinimapIcon: 为 {gameObject.name} 准备创建小地图图标，类型={iconType}，阵营={teamColor}，池化类型={poolType}，位置={transform.position}");
            }
        }

        /// <summary>
        /// 检查游戏状态并创建图标
        /// </summary>
        private void CheckGameStateAndCreateIcon()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                if (!isIconCreated)
                {
                    CreateIcon();
                }
            }
            else
            {
                // 如果游戏还没开始，继续延迟检查
                Invoke("CheckGameStateAndCreateIcon", 1.0f);
            }
        }
        
        private void Update()
        {
            // 检查游戏状态，如果游戏结束则销毁图标
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.NotStarted)
            {
                if ((poolType == IconPoolType.Static && iconObject != null) ||
                    (poolType == IconPoolType.Pooled && pooledIcon != null))
                {
                    DestroyIcon();
                    return;
                }
            }

            // 性能优化：检查更新间隔
            if (Time.time - lastUpdateTime < updateInterval)
            {
                return;
            }
            lastUpdateTime = Time.time;

            // 位置同步
            if (poolType == IconPoolType.Static && iconObject != null)
            {
                // 静态图标位置同步（原有逻辑）
                Vector3 position = transform.position;
                iconObject.transform.position = new Vector3(position.x, position.y, -1f);
            }
            else if (poolType == IconPoolType.Pooled && pooledIcon != null)
            {
                // 池化图标位置同步
                pooledIcon.UpdatePosition(transform.position);
            }
        }
        
        /// <summary>
        /// 获取池化类型
        /// </summary>
        private IconPoolType GetIconPoolType()
        {
            string tag = gameObject.tag;

            // 静态单位（不使用对象池）
            if (tag == "Hero" || tag == "LeftCastle" || tag == "RightCastle")
            {
                return IconPoolType.Static;
            }

            // 小兵单位（使用对象池）
            return IconPoolType.Pooled;
        }

        /// <summary>
        /// 自动检测单位类型和阵营
        /// </summary>
        private void AutoDetectSettings()
        {
            // 检测单位类型
            if (autoDetectType)
            {
                if (GetComponent<HeroUnit>() != null)
                {
                    iconType = IconType.Hero;
                }
                else if (GetComponent<left_castle>() != null || GetComponent<right_castle>() != null || gameObject.name.Contains("Castle"))
                {
                    iconType = IconType.Castle;
                }
                else
                {
                    iconType = IconType.Minion;
                }
            }
            
            // 检测阵营
            if (autoDetectTeam)
            {
                Unit unit = GetComponent<Unit>();
                if (unit != null)
                {
                    teamColor = unit.faction == Unit.Faction.Left ? TeamColor.Blue : TeamColor.Red;
                }
            }
        }
        
        /// <summary>
        /// 创建小地图图标
        /// </summary>
        private void CreateIcon()
        {
            // 如果图标已经创建，则不重复创建
            if (isIconCreated || (iconObject != null && pooledIcon != null))
            {
                return;
            }

            if (poolType == IconPoolType.Static)
            {
                CreateStaticIcon();
            }
            else
            {
                CreatePooledIcon();
            }
        }

        /// <summary>
        /// 创建静态图标（原有逻辑）
        /// </summary>
        private void CreateStaticIcon()
        {
            // 加载图标精灵
            LoadIconSprites();
            
            // 加载默认Sprite材质
            if (defaultSpriteMaterial == null)
            {
                defaultSpriteMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            
            // 创建图标游戏对象
            iconObject = new GameObject(gameObject.name + "_MinimapIcon");
            iconObject.transform.position = new Vector3(transform.position.x, transform.position.y, -1f); // 使用更小的Z值确保在摄像机前方
            
            // 添加精灵渲染器
            iconRenderer = iconObject.AddComponent<SpriteRenderer>();
            
            // 设置材质
            iconRenderer.material = defaultSpriteMaterial;
            
            // 根据单位类型设置精灵和大小
            switch (iconType)
            {
                case IconType.Hero:
                    iconRenderer.sprite = heroSprite;
                    iconRenderer.transform.localScale = new Vector3(iconSize * 3.0f, iconSize * 3.0f, 1f); // 英雄图标为默认的2倍大小
                    break;
                case IconType.Castle:
                    iconRenderer.sprite = castleSprite;
                    iconRenderer.transform.localScale = new Vector3(iconSize * 5.0f, iconSize * 5.0f, 1f); // 城堡图标为默认的4倍大小
                    break;
                case IconType.Minion:
                    iconRenderer.sprite = minionSprite;
                    iconRenderer.transform.localScale = new Vector3(iconSize * 4.0f, iconSize * 4.0f, 1f); // 增大小兵图标尺寸为默认的1.5倍
                    break;
            }
            
            // 根据阵营设置颜色
            iconRenderer.color = teamColor == TeamColor.Blue ? new Color(0.2f, 0.4f, 1f) : new Color(1f, 0.2f, 0.2f);
            
            // 设置排序层，确保图标不会被其他物体遮挡
            iconRenderer.sortingOrder = 100;
            
            // 尝试设置SortingLayer为"Minimap"
            try
            {
                iconRenderer.sortingLayerName = "Minimap";
                if (showDebugInfo)
                {
                    //Debug.Log($"MinimapIcon: 图标 {iconObject.name} 已设置排序层为 Minimap");
                }
            }
            catch (System.Exception)
            {
                //Debug.LogWarning($"无法设置排序层为Minimap: {e.Message}，请在项目设置中添加此排序层");
            }
            
            // 设置图标到Minimap层
            int minimapLayer = LayerMask.NameToLayer("Minimap");
            if (minimapLayer == -1)
            {
                //Debug.LogWarning("未找到'Minimap'层! 请在项目设置中添加此层。图标将保留在默认层。");
                // 如果没有Minimap层，使用UI层作为备选
                minimapLayer = LayerMask.NameToLayer("UI");
                if (minimapLayer != -1)
                {
                    iconObject.layer = minimapLayer;
                    //Debug.Log("使用UI层作为备选层");
                }
            }
            else
            {
                iconObject.layer = minimapLayer;
                if (showDebugInfo)
                {
                    //Debug.Log($"MinimapIcon: 图标 {iconObject.name} 已设置到Minimap层(层索引={minimapLayer})");
                }
            }

            // 标记图标已创建
            isIconCreated = true;
        }

        /// <summary>
        /// 创建池化图标
        /// </summary>
        private void CreatePooledIcon()
        {
            if (MinionIconPool.Instance != null)
            {
                pooledIcon = MinionIconPool.Instance.GetIcon();
                if (pooledIcon != null)
                {
                    pooledIcon.Setup(transform.position, teamColor, iconSize);
                    isIconCreated = true;
                }
                else if (showDebugInfo)
                {
                    Debug.LogWarning($"无法从对象池获取小兵图标: {gameObject.name}");
                }
            }
            else if (showDebugInfo)
            {
                Debug.LogWarning("小兵图标对象池未初始化");
            }
        }

        /// <summary>
        /// 销毁图标
        /// </summary>
        private void DestroyIcon()
        {
            if (poolType == IconPoolType.Static)
            {
                if (iconObject != null)
                {
                    Destroy(iconObject);
                    iconObject = null;
                    isIconCreated = false;

                    if (showDebugInfo)
                    {
                        //Debug.Log($"MinimapIcon: 销毁了 {gameObject.name} 的静态小地图图标");
                    }
                }
            }
            else if (poolType == IconPoolType.Pooled)
            {
                if (pooledIcon != null)
                {
                    MinionIconPool.Instance?.ReturnIcon(pooledIcon);
                    pooledIcon = null;
                    isIconCreated = false;

                    if (showDebugInfo)
                    {
                        //Debug.Log($"MinimapIcon: 归还了 {gameObject.name} 的池化小地图图标");
                    }
                }
            }
        }
        
        /// <summary>
        /// 加载图标精灵
        /// </summary>
        private void LoadIconSprites()
        {
            // 如果精灵已经加载，则跳过
            if (heroSprite != null && minionSprite != null && castleSprite != null)
                return;
            
            // 尝试从Resources加载精灵
            // 如果没有自定义精灵，则创建简单的形状
            if (heroSprite == null)
            {
                // 尝试从Resources加载
                heroSprite = Resources.Load<Sprite>("MinimapIcons/HeroIcon");
                
                // 如果加载失败，创建一个圆形精灵
                if (heroSprite == null)
                {
                    heroSprite = CreateCircleSprite(32, 0.5f);
                    if (showDebugInfo)
                    {
                        //Debug.Log("MinimapIcon: 创建了默认英雄图标");
                    }
                }
            }
            
            if (minionSprite == null)
            {
                minionSprite = Resources.Load<Sprite>("MinimapIcons/MinionIcon");
                if (minionSprite == null)
                {
                    minionSprite = CreateCircleSprite(16, 0.4f);
                    if (showDebugInfo)
                    {
                        //Debug.Log("MinimapIcon: 创建了默认小兵图标");
                    }
                }
            }
            
            if (castleSprite == null)
            {
                castleSprite = Resources.Load<Sprite>("MinimapIcons/CastleIcon");
                if (castleSprite == null)
                {
                    castleSprite = CreateSquareSprite(24, 0.5f);
                    if (showDebugInfo)
                    {
                        //Debug.Log("MinimapIcon: 创建了默认城堡图标");
                    }
                }
            }
        }
        
        /// <summary>
        /// 创建圆形精灵
        /// </summary>
        private Sprite CreateCircleSprite(int resolution, float radius)
        {
            // 创建纹理
            Texture2D texture = new Texture2D(resolution * 2, resolution * 2, TextureFormat.RGBA32, false);
            
            // 设置中心点
            Vector2 center = new Vector2(resolution, resolution);
            
            // 填充纹理
            for (int x = 0; x < texture.width; x++)
            {
                for (int y = 0; y < texture.height; y++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= resolution * radius)
                    {
                        texture.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }
            
            // 应用更改
            texture.Apply();
            
            // 创建精灵
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        
        /// <summary>
        /// 创建方形精灵
        /// </summary>
        private Sprite CreateSquareSprite(int resolution, float size)
        {
            // 创建纹理
            Texture2D texture = new Texture2D(resolution * 2, resolution * 2, TextureFormat.RGBA32, false);
            
            // 计算边界
            int min = Mathf.RoundToInt(resolution * (1 - size));
            int max = Mathf.RoundToInt(resolution * (1 + size));
            
            // 填充纹理
            for (int x = 0; x < texture.width; x++)
            {
                for (int y = 0; y < texture.height; y++)
                {
                    if (x >= min && x <= max && y >= min && y <= max)
                    {
                        texture.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }
            
            // 应用更改
            texture.Apply();
            
            // 创建精灵
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        
        private void OnDestroy()
        {
            // 销毁图标
            DestroyIcon();
        }

        private void OnDisable()
        {
            // 对象池回收时也需要销毁图标
            if (showDebugInfo && iconObject != null)
            {
                //Debug.Log($"[MinimapIcon] OnDisable - 销毁图标: {gameObject.name}");
            }
            DestroyIcon();
        }

        private void OnEnable()
        {
            // 对象池重新激活时需要重新创建图标
            if (Application.isPlaying && !isIconCreated)
            {
                // 重新确定池化类型（对象池重用时可能会变化）
                poolType = GetIconPoolType();

                // 延迟几帧创建，确保对象完全初始化（包括ResetUnit调用）
                StartCoroutine(DelayedCreateIcon());
            }
        }

        private System.Collections.IEnumerator DelayedCreateIcon()
        {
            // 等待几帧，确保ResetUnit已经被调用
            yield return null;
            yield return null;

            // 重新检测设置（因为对象池可能改变了阵营等属性）
            if (autoDetectType || autoDetectTeam)
            {
                AutoDetectSettings();
            }

            // 只有在游戏进行中才创建图标
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[MinimapIcon] DelayedCreateIcon - 创建图标: {gameObject.name}, 阵营: {teamColor}");
                }
                CreateIcon();
            }
            else if (showDebugInfo)
            {
                Debug.Log($"[MinimapIcon] DelayedCreateIcon - 跳过创建（游戏状态不对）: {gameObject.name}");
            }
        }

        /// <summary>
        /// 强制创建图标，无论游戏状态如何
        /// </summary>
        public void ForceCreateIcon()
        {
            // 先销毁现有图标（如果有的话）
            if (isIconCreated)
            {
                DestroyIcon();
            }

            // 自动检测单位类型和阵营（如果需要）
            if (autoDetectType || autoDetectTeam)
            {
                AutoDetectSettings();
            }

            // 重新确定池化类型
            poolType = GetIconPoolType();

            // 直接创建图标，不检查游戏状态
            CreateIcon();

            if (showDebugInfo)
            {
                Debug.Log($"[MinimapIcon] ForceCreateIcon - 强制创建图标: {gameObject.name}, 阵营: {teamColor}, 池化类型: {poolType}");
            }
        }
    }
} 