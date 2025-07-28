using UnityEngine;

namespace MagicBattle
{
    /// <summary>
    /// 池化的小兵图标组件 - 可复用的小地图图标
    /// </summary>
    public class PooledMinionIcon : MonoBehaviour
    {
        private SpriteRenderer iconRenderer;
        private bool isInUse = false;

        // 静态资源缓存
        private static Sprite minionSprite;
        private static Material defaultSpriteMaterial;

        private void Awake()
        {
            iconRenderer = GetComponent<SpriteRenderer>();
            LoadResources();
        }

        /// <summary>
        /// 设置图标状态
        /// </summary>
        public void Setup(Vector3 position, MinimapIcon.TeamColor teamColor, float iconSize)
        {
            // 设置位置
            transform.position = new Vector3(position.x, position.y, -1f);

            // 设置颜色
            iconRenderer.color = teamColor == MinimapIcon.TeamColor.Blue ? 
                new Color(0.2f, 0.4f, 1f) : new Color(1f, 0.2f, 0.2f);

            // 设置精灵和大小
            iconRenderer.sprite = minionSprite;
            iconRenderer.material = defaultSpriteMaterial;
            transform.localScale = new Vector3(iconSize * 4.0f, iconSize * 4.0f, 1f);

            // 设置排序层
            try
            {
                iconRenderer.sortingLayerName = "Minimap";
            }
            catch (System.Exception)
            {
                // 如果Minimap排序层不存在，保持默认
            }

            gameObject.SetActive(true);
            isInUse = true;
        }

        /// <summary>
        /// 重置图标状态
        /// </summary>
        public void Reset()
        {
            gameObject.SetActive(false);
            isInUse = false;
        }

        /// <summary>
        /// 更新位置
        /// </summary>
        public void UpdatePosition(Vector3 position)
        {
            if (isInUse)
            {
                transform.position = new Vector3(position.x, position.y, -1f);
            }
        }

        /// <summary>
        /// 检查是否正在使用
        /// </summary>
        public bool IsInUse => isInUse;

        /// <summary>
        /// 加载静态资源
        /// </summary>
        private void LoadResources()
        {
            if (minionSprite == null)
            {
                minionSprite = Resources.Load<Sprite>("MinimapIcons/MinionIcon");
                if (minionSprite == null)
                {
                    // 创建默认圆形精灵
                    minionSprite = CreateCircleSprite(16, 0.4f);
                }
            }

            if (defaultSpriteMaterial == null)
            {
                defaultSpriteMaterial = new Material(Shader.Find("Sprites/Default"));
            }
        }

        /// <summary>
        /// 创建圆形精灵
        /// </summary>
        private Sprite CreateCircleSprite(int size, float radius)
        {
            Texture2D texture = new Texture2D(size, size);
            Color[] colors = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radiusPixels = radius * size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    colors[y * size + x] = distance <= radiusPixels ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(colors);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 强制重新加载资源（用于调试）
        /// </summary>
        public static void ReloadResources()
        {
            minionSprite = null;
            defaultSpriteMaterial = null;
        }
    }
}
