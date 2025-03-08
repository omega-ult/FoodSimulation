using UnityEngine;
using System.Collections.Generic;

public class ColliderSDFExample : MonoBehaviour
{
    [Header("碰撞体设置")]
    public Collider[] targetColliders;
    public CombinedSDF.CombineOperation[] operations;
    
    [Header("可视化设置")]
    public bool visualizeSlice = true;
    public bool visualizeVolume = true;
    public float sliceHeight = 0f;
    public Vector3 volumeSize = new Vector3(10f, 10f, 10f);
    public int resolution = 100;
    public float isoLevel = 0f;
    
    [Header("SDF设置")]
    public int rayDirectionCount = 6;
    
    private CombinedSDF combinedSDF;
    private GameObject sliceVisualizer;
    private GameObject volumeVisualizer;
    
    void Start()
    {
        // 创建组合SDF
        combinedSDF = gameObject.AddComponent<CombinedSDF>();
        
        // 为每个碰撞体添加ColliderToSDF组件
        for (int i = 0; i < targetColliders.Length; i++)
        {
            if (targetColliders[i] != null)
            {
                GameObject colliderObj = targetColliders[i].gameObject;
                
                // 检查是否已有ColliderToSDF组件
                ColliderToSDF colliderSDF = colliderObj.GetComponent<ColliderToSDF>();
                if (colliderSDF == null)
                {
                    // 添加ColliderToSDF组件
                    colliderSDF = colliderObj.AddComponent<ColliderToSDF>();
                    colliderSDF.rayDirectionCount = rayDirectionCount;
                }
                
                // 添加到组合SDF
                CombinedSDF.CombineOperation operation = i < operations.Length ? 
                    operations[i] : CombinedSDF.CombineOperation.Union;
                combinedSDF.AddComponent(colliderSDF, operation);
            }
        }
        
        // 可视化
        if (visualizeSlice)
            CreateSDFSliceVisualizer();
            
        if (visualizeVolume)
            CreateSDFVolumeVisualizer();
    }
    
    void Update()
    {
        // 实时更新可视化
        if (visualizeSlice && sliceVisualizer != null)
            UpdateSDFSliceVisualizer();
    }
    
    // 2D切片可视化
    void CreateSDFSliceVisualizer()
    {
        // 创建一个平面来显示纹理
        sliceVisualizer = GameObject.CreatePrimitive(PrimitiveType.Plane);
        sliceVisualizer.name = "Collider SDF Slice Visualizer";
        sliceVisualizer.transform.SetParent(transform);
        sliceVisualizer.transform.localPosition = new Vector3(0, sliceHeight, 0);
        sliceVisualizer.transform.localScale = new Vector3(volumeSize.x/10f, 1f, volumeSize.z/10f);
        
        // 创建材质
        Material sliceMaterial = new Material(Shader.Find("Unlit/Texture"));
        sliceVisualizer.GetComponent<Renderer>().material = sliceMaterial;
        
        UpdateSDFSliceVisualizer();
    }
    
    void UpdateSDFSliceVisualizer()
    {
        Texture2D texture = new Texture2D(resolution, resolution);
        
        for (int x = 0; x < resolution; x++)
        for (int z = 0; z < resolution; z++)
        {
            Vector3 worldPos = transform.position + new Vector3(
                (x / (float)resolution - 0.5f) * volumeSize.x,
                sliceHeight,
                (z / (float)resolution - 0.5f) * volumeSize.z
            );
            
            float distance = combinedSDF.GetDistance(worldPos);
            
            // 使用热图颜色表示距离
            Color color;
            if (distance < 0)
            {
                // 内部：红色到黄色
                float t = Mathf.Clamp01(-distance / 2f);
                color = Color.Lerp(Color.yellow, Color.red, t);
            }
            else
            {
                // 外部：青色到蓝色
                float t = Mathf.Clamp01(distance / 2f);
                color = Color.Lerp(Color.cyan, Color.blue, t);
            }
            
            // 零等值线：白色
            if (Mathf.Abs(distance) < 0.1f)
                color = Color.white;
                
            texture.SetPixel(x, z, color);
        }
        
        texture.Apply();
        sliceVisualizer.GetComponent<Renderer>().material.mainTexture = texture;
    }
    
    // 3D体积可视化
    void CreateSDFVolumeVisualizer()
    {
        volumeVisualizer = new GameObject("Collider SDF Volume Visualizer");
        volumeVisualizer.transform.SetParent(transform);
        volumeVisualizer.transform.localPosition = Vector3.zero;
        
        MeshFilter meshFilter = volumeVisualizer.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = volumeVisualizer.AddComponent<MeshRenderer>();
        
        // 创建材质
        Material volumeMaterial = new Material(Shader.Find("Standard"));
        volumeMaterial.color = Color.white;
        meshRenderer.material = volumeMaterial;
        
        // 生成等值面网格
        Mesh mesh = GenerateIsoSurfaceMesh();
        meshFilter.mesh = mesh;
    }
    
    Mesh GenerateIsoSurfaceMesh()
    {
        // 采样SDF值
        int resX = Mathf.CeilToInt(resolution * volumeSize.x / Mathf.Max(volumeSize.x, volumeSize.y, volumeSize.z));
        int resY = Mathf.CeilToInt(resolution * volumeSize.y / Mathf.Max(volumeSize.x, volumeSize.y, volumeSize.z));
        int resZ = Mathf.CeilToInt(resolution * volumeSize.z / Mathf.Max(volumeSize.x, volumeSize.y, volumeSize.z));
        
        float[,,] sdfValues = new float[resX+1, resY+1, resZ+1];
        
        for (int x = 0; x <= resX; x++)
        for (int y = 0; y <= resY; y++)
        for (int z = 0; z <= resZ; z++)
        {
            Vector3 worldPos = transform.position + new Vector3(
                (x / (float)resX - 0.5f) * volumeSize.x,
                (y / (float)resY - 0.5f) * volumeSize.y,
                (z / (float)resZ - 0.5f) * volumeSize.z
            );
            
            sdfValues[x, y, z] = combinedSDF.GetDistance(worldPos);
        }
        
        // 使用Marching Cubes生成等值面
        SimplifiedMarchingCubes marchingCubes = new SimplifiedMarchingCubes();
        Mesh mesh = marchingCubes.GenerateMesh(sdfValues, isoLevel, volumeSize);
        
        return mesh;
    }
    
    // 编辑器功能：重新生成可视化
    public void RegenerateVisualizations()
    {
        if (visualizeSlice && sliceVisualizer != null)
            UpdateSDFSliceVisualizer();
            
        if (visualizeVolume && volumeVisualizer != null)
        {
            Destroy(volumeVisualizer);
            CreateSDFVolumeVisualizer();
        }
    }
    
    private void OnDestroy()
    {
        // 清理可视化对象
        if (sliceVisualizer != null)
            Destroy(sliceVisualizer);
            
        if (volumeVisualizer != null)
            Destroy(volumeVisualizer);
    }
}