using UnityEngine;
using System.Collections.Generic;

public class FlowFieldManager : MonoBehaviour
{
    private static FlowFieldManager instance;
    public static FlowFieldManager Instance => instance;

    [Header("流场参数")]
    [SerializeField] private float cellSize = 0.5f;  // 网格大小
    //[SerializeField] private int fieldRadius = 7;    // 流场半径（格子数）
    [SerializeField] private LayerMask obstacleMask; // 障碍物层

    private Dictionary<Vector2Int, FlowField> activeFields = new Dictionary<Vector2Int, FlowField>();

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    // 生成流场
    public FlowField GenerateFlowField(Vector2 targetPosition, float radius)
    {
        Vector2Int gridPos = WorldToGrid(targetPosition);
        
        // 如果已存在该位置的流场，直接返回
        if (activeFields.TryGetValue(gridPos, out FlowField existingField))
        {
            return existingField;
        }

        // 创建新流场
        FlowField newField = new FlowField(gridPos, radius, cellSize, obstacleMask);
        activeFields[gridPos] = newField;
        return newField;
    }

    // 获取流场方向
    public Vector2 GetFlowDirection(Vector2 worldPosition)
    {
        Vector2Int gridPos = WorldToGrid(worldPosition);
        if (activeFields.TryGetValue(gridPos, out FlowField field))
        {
            return field.GetFlowDirection(worldPosition);
        }
        return Vector2.zero;
    }

    // 世界坐标转网格坐标
    private Vector2Int WorldToGrid(Vector2 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x / cellSize),
            Mathf.RoundToInt(worldPos.y / cellSize)
        );
    }

    // 清理过期流场
    public void CleanupFields()
    {
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var field in activeFields)
        {
            if (field.Value.IsExpired())
            {
                toRemove.Add(field.Key);
            }
        }
        foreach (var key in toRemove)
        {
            activeFields.Remove(key);
        }
    }

    private void Update()
    {
        CleanupFields();
    }
}

// 流场类
public class FlowField
{
    private Vector2Int targetGridPos;
    private float radius;
    private float cellSize;
    private LayerMask obstacleMask;
    private Dictionary<Vector2Int, Vector2> flowDirections;
    private float lastUpdateTime;
    private const float UPDATE_INTERVAL = 0.1f; // 更新间隔

    public FlowField(Vector2Int targetPos, float radius, float cellSize, LayerMask obstacleMask)
    {
        this.targetGridPos = targetPos;
        this.radius = radius;
        this.cellSize = cellSize;
        this.obstacleMask = obstacleMask;
        this.flowDirections = new Dictionary<Vector2Int, Vector2>();
        this.lastUpdateTime = Time.time;
        UpdateField();
    }

    private void UpdateField()
    {
        flowDirections.Clear();
        int radiusCells = Mathf.CeilToInt(radius / cellSize);

        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                Vector2Int currentGrid = targetGridPos + new Vector2Int(x, y);
                Vector2 worldPos = GridToWorld(currentGrid);
                
                // 检查是否有障碍物
                if (Physics2D.OverlapCircle(worldPos, cellSize * 0.5f, obstacleMask))
                {
                    continue;
                }

                // 计算流向 - 将Vector2Int转换为Vector2来计算方向
                Vector2 targetPos = new Vector2(targetGridPos.x, targetGridPos.y);
                Vector2 currentPos = new Vector2(currentGrid.x, currentGrid.y);
                Vector2 direction = (targetPos - currentPos).normalized;
                flowDirections[currentGrid] = direction;
            }
        }
    }

    public Vector2 GetFlowDirection(Vector2 worldPosition)
    {
        Vector2Int gridPos = WorldToGrid(worldPosition);
        
        // 检查是否需要更新
        if (Time.time - lastUpdateTime > UPDATE_INTERVAL)
        {
            UpdateField();
            lastUpdateTime = Time.time;
        }

        if (flowDirections.TryGetValue(gridPos, out Vector2 direction))
        {
            return direction;
        }
        return Vector2.zero;
    }

    private Vector2Int WorldToGrid(Vector2 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x / cellSize),
            Mathf.RoundToInt(worldPos.y / cellSize)
        );
    }

    private Vector2 GridToWorld(Vector2Int gridPos)
    {
        return new Vector2(
            gridPos.x * cellSize,
            gridPos.y * cellSize
        );
    }

    public bool IsExpired()
    {
        return Time.time - lastUpdateTime > 5f; // 5秒后过期
    }
} 