using UnityEngine;

[CreateAssetMenu(fileName = "NewMinionBaseData", menuName = "Game/Unit/Minion Base Data")]
public class MinionBaseDataSO : BaseUnitDataSO
{
    // 小兵特有基础属性，例如：
    public float baseAttackRange = 1f; // 对于远程单位
    public float baseAttackCooldown = 1.5f; // 对于攻击单位
    public float baseDetectionRange = 5f; // 新增基础检测范围
    public float baseHealAreaRadius = 3f; // 新增基础治疗区域半径 (用于牧师)
    public float baseMageAOERadius = 4f; // 新增基础法师AOE半径 (用于法师)
    public float baseChargeSpeed = 10f; // 新增基础冲锋速度 (用于长枪兵)
    // ...
} 