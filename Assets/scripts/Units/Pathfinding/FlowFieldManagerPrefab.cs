using UnityEngine;

[CreateAssetMenu(fileName = "FlowFieldManagerPrefab", menuName = "Game/FlowFieldManagerPrefab")]
public class FlowFieldManagerPrefab : ScriptableObject
{
    public GameObject prefab;

    public void CreateInstance()
    {
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab);
            instance.name = "FlowFieldManager";
        }
    }
} 