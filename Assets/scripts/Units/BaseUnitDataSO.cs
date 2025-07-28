using UnityEngine;

public abstract class BaseUnitDataSO : ScriptableObject
{
    public Unit.UnitType unitType;
    public float baseMaxHealth = 100f;
    public float baseMoveSpeed = 5f;
    public float baseDefense = 1f;
    // ... 其他通用基础属性
} 