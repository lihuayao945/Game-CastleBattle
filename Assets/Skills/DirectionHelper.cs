using UnityEngine;

/// <summary>
/// 方向辅助工具 - 用于获取角色朝向等方向信息
/// </summary>
public static class DirectionHelper
{
    /// <summary>
    /// 获取角色朝向的方向向量
    /// </summary>
    /// <param name="transform">角色Transform</param>
    /// <returns>朝向的单位向量</returns>
    public static Vector2 GetFacingDirection(Transform transform)
    {
        // 默认使用X轴缩放判断朝向，负值表示朝左
        return transform.localScale.x < 0 ? Vector2.left : Vector2.right;
    }
} 