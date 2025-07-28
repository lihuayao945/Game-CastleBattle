using UnityEngine;

public class MapBoundary : MonoBehaviour
{
    [Header("地图边界设置")]
    [Tooltip("地图宽度")]
    public float mapWidth = 20f;
    [Tooltip("地图高度")]
    public float mapHeight = 10f;
    [Tooltip("边界厚度")]
    public float boundaryThickness = 1f;

    void Start()
    {
        CreateBoundaries();
    }

    void CreateBoundaries()
    {
        // 创建四个边界
        CreateBoundary("LeftBoundary", new Vector3(-mapWidth/2 - boundaryThickness/2, 0, 0), new Vector3(boundaryThickness, mapHeight + boundaryThickness*2, 1));
        CreateBoundary("RightBoundary", new Vector3(mapWidth/2 + boundaryThickness/2, 0, 0), new Vector3(boundaryThickness, mapHeight + boundaryThickness*2, 1));
        CreateBoundary("TopBoundary", new Vector3(0, mapHeight/2 + boundaryThickness/2, 0), new Vector3(mapWidth + boundaryThickness*2, boundaryThickness, 1));
        CreateBoundary("BottomBoundary", new Vector3(0, -mapHeight/2 - boundaryThickness/2, 0), new Vector3(mapWidth + boundaryThickness*2, boundaryThickness, 1));
    }

    void CreateBoundary(string name, Vector3 position, Vector3 scale)
    {
        // 创建边界物体
        GameObject boundary = new GameObject(name);
        boundary.transform.parent = transform;
        boundary.transform.localPosition = position;
        boundary.transform.localScale = scale;

        // 添加碰撞器
        BoxCollider2D collider = boundary.AddComponent<BoxCollider2D>();
        collider.isTrigger = false; // 设置为非触发器，这样会阻挡角色移动
    }
} 