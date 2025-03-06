using UnityEngine;
using System.Collections.Generic;

public class SDFManager : MonoBehaviour
{
    private List<SDFPrimitive> sdfObjects = new List<SDFPrimitive>();

    private void Start()
    {
        // 查找所有SDF对象
        sdfObjects.AddRange(FindObjectsOfType<SDFPrimitive>());
        
        // 自动为带有MeshFilter的对象生成SDF
        MeshFilter[] meshFilters = FindObjectsOfType<MeshFilter>();
        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.GetComponent<SDFPrimitive>() == null)
            {
                MeshToSDF meshSDF = mf.gameObject.AddComponent<MeshToSDF>();
                sdfObjects.Add(meshSDF);
            }
        }
    }

    public float GetSceneDistance(Vector3 point)
    {
        if (sdfObjects.Count == 0)
            return float.MaxValue;

        float distance = sdfObjects[0].GetDistance(point);
        
        for (int i = 1; i < sdfObjects.Count; i++)
        {
            distance = SDFOperator.Union(distance, sdfObjects[i].GetDistance(point));
        }

        return distance;
    }

    public Vector3 GetSceneNormal(Vector3 point)
    {
        float epsilon = 0.001f;
        Vector3 normal = new Vector3(
            GetSceneDistance(point + new Vector3(epsilon, 0, 0)) - GetSceneDistance(point - new Vector3(epsilon, 0, 0)),
            GetSceneDistance(point + new Vector3(0, epsilon, 0)) - GetSceneDistance(point - new Vector3(0, epsilon, 0)),
            GetSceneDistance(point + new Vector3(0, 0, epsilon)) - GetSceneDistance(point - new Vector3(0, 0, epsilon))
        );
        return normal.normalized;
    }
}