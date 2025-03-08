using UnityEngine;
using System.Collections.Generic;

public class CombinedSDFExample : MonoBehaviour
{
    public MeshFilter[] foodComponents;
    public CombinedSDF.CombineOperation[] operations;
    
    [Header("可视化设置")]
    public bool visualizeSlice = true;
    public bool visualizeVolume = true;
    public float sliceHeight = 0f;
    public Vector3 volumeSize = new Vector3(10f, 10f, 10f);
    public int resolution = 100;
    public float isoLevel = 0f;
    
    private CombinedSDF combinedSDF;
    private GameObject sliceVisualizer;
    private GameObject volumeVisualizer;
    
    void Start()
    {
        // 创建组合SDF
        // 修复：使用正确的方法创建CombinedSDF
        combinedSDF = gameObject.AddComponent<CombinedSDF>();
        
        // 添加每个食物组件到CombinedSDF
        for (int i = 0; i < foodComponents.Length; i++)
        {
            if (foodComponents[i] != null)
            {
                SDFPrimitive sdfPrimitive = foodComponents[i].gameObject.GetComponent<SDFPrimitive>();
                if (sdfPrimitive == null)
                {
                    // 如果没有SDFPrimitive组件，添加一个MeshSDF
                    sdfPrimitive = foodComponents[i].gameObject.AddComponent<MeshSDF>();
                }
                
                // 添加到组合SDF
                CombinedSDF.CombineOperation operation = i < operations.Length ? 
                    operations[i] : CombinedSDF.CombineOperation.Union;
                combinedSDF.AddComponent(sdfPrimitive, operation);
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
        // 可选：实时更新可视化
        if (visualizeSlice && sliceVisualizer != null)
            UpdateSDFSliceVisualizer();
    }
    
    // 2D切片可视化
    void CreateSDFSliceVisualizer()
    {
        // 创建一个平面来显示纹理
        sliceVisualizer = GameObject.CreatePrimitive(PrimitiveType.Plane);
        sliceVisualizer.name = "SDF Slice Visualizer";
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
        volumeVisualizer = new GameObject("SDF Volume Visualizer");
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
        // 简化版的Marching Cubes算法
        int resX = Mathf.CeilToInt(resolution * volumeSize.x / Mathf.Max(volumeSize.x, volumeSize.y, volumeSize.z));
        int resY = Mathf.CeilToInt(resolution * volumeSize.y / Mathf.Max(volumeSize.x, volumeSize.y, volumeSize.z));
        int resZ = Mathf.CeilToInt(resolution * volumeSize.z / Mathf.Max(volumeSize.x, volumeSize.y, volumeSize.z));
        
        float[,,] sdfValues = new float[resX+1, resY+1, resZ+1];
        
        // 采样SDF值
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
        
        // 创建简化的等值面提取器
        SimplifiedMarchingCubes marchingCubes = new SimplifiedMarchingCubes();
        Mesh mesh = marchingCubes.GenerateMesh(sdfValues, isoLevel, volumeSize);
        
        return mesh;
    }
}

// 简化版的Marching Cubes实现
public class SimplifiedMarchingCubes
{
    // 三角形表 - 存储每种立方体配置的三角形顶点索引
    private static readonly int[] triangleTable = {
        // 这里应该是完整的Marching Cubes三角形表
        // 为简化，这里只提供部分数据
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        // ... 更多三角形表数据 ...
    };
    
    // 边缘表 - 存储每种立方体配置的活动边
    private static readonly int[] edgeTable = {
        0x0, 0x109, 0x203, 0x30a, 0x406, 0x50f, 0x605, 0x70c,
        0x80c, 0x905, 0xa0f, 0xb06, 0xc0a, 0xd03, 0xe09, 0xf00,
        // ... 更多边缘表数据 ...
    };
    
    // 边缘顶点对应关系
    private static readonly int[,] edgeVertices = {
        {0, 1}, {1, 2}, {2, 3}, {3, 0},
        {4, 5}, {5, 6}, {6, 7}, {7, 4},
        {0, 4}, {1, 5}, {2, 6}, {3, 7}
    };
    
    public Mesh GenerateMesh(float[,,] sdfValues, float isoLevel, Vector3 size)
    {
        int resX = sdfValues.GetLength(0) - 1;
        int resY = sdfValues.GetLength(1) - 1;
        int resZ = sdfValues.GetLength(2) - 1;
        
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        
        // 遍历体素
        for (int x = 0; x < resX; x++)
        for (int y = 0; y < resY; y++)
        for (int z = 0; z < resZ; z++)
        {
            // 获取体素的8个顶点
            float[] cubeValues = new float[8];
            cubeValues[0] = sdfValues[x, y, z];
            cubeValues[1] = sdfValues[x+1, y, z];
            cubeValues[2] = sdfValues[x+1, y, z+1];
            cubeValues[3] = sdfValues[x, y, z+1];
            cubeValues[4] = sdfValues[x, y+1, z];
            cubeValues[5] = sdfValues[x+1, y+1, z];
            cubeValues[6] = sdfValues[x+1, y+1, z+1];
            cubeValues[7] = sdfValues[x, y+1, z+1];
            
            // 立方体顶点位置
            Vector3[] cubePos = new Vector3[8] {
                new Vector3(x, y, z),
                new Vector3(x+1, y, z),
                new Vector3(x+1, y, z+1),
                new Vector3(x, y, z+1),
                new Vector3(x, y+1, z),
                new Vector3(x+1, y+1, z),
                new Vector3(x+1, y+1, z+1),
                new Vector3(x, y+1, z+1)
            };
            
            // 确定立方体配置索引
            int cubeIndex = 0;
            for (int i = 0; i < 8; i++)
                if (cubeValues[i] < isoLevel)
                    cubeIndex |= 1 << i;
                    
            // 如果立方体完全在等值面内部或外部，则跳过
            if (cubeIndex == 0 || cubeIndex == 255)
                continue;
                
            // 立方体边缘
            Vector3[] edgeVertices = new Vector3[12];
            
            // 计算边缘交点
            if ((edgeTable[cubeIndex] & 1) != 0)
                edgeVertices[0] = InterpolateVertex(cubePos[0], cubePos[1], cubeValues[0], cubeValues[1], isoLevel, size, resX, resY, resZ);
            if ((edgeTable[cubeIndex] & 2) != 0)
                edgeVertices[1] = InterpolateVertex(cubePos[1], cubePos[2], cubeValues[1], cubeValues[2], isoLevel, size, resX, resY, resZ);
            if ((edgeTable[cubeIndex] & 4) != 0)
                edgeVertices[2] = InterpolateVertex(cubePos[2], cubePos[3], cubeValues[2], cubeValues[3], isoLevel, size, resX, resY, resZ);
            if ((edgeTable[cubeIndex] & 8) != 0)
                edgeVertices[3] = InterpolateVertex(cubePos[3], cubePos[0], cubeValues[3], cubeValues[0], isoLevel, size, resX, resY, resZ);
            if ((edgeTable[cubeIndex] & 16) != 0)
                edgeVertices[4] = InterpolateVertex(cubePos[4], cubePos[5], cubeValues[4], cubeValues[5], isoLevel, size, resX, resY, resZ);
            if ((edgeTable[cubeIndex] & 32) != 0)
                edgeVertices[5] = InterpolateVertex(cubePos[5], cubePos[6], cubeValues[5], cubeValues[6], isoLevel, size, resX, resY, resZ);
            if ((edgeTable[cubeIndex] & 64) != 0)
                edgeVertices[6] = InterpolateVertex(cubePos[6], cubePos[7], cubeValues[6], cubeValues[7], isoLevel, size, resX, resY, resZ);
            if ((edgeTable[cubeIndex] & 128) != 0)
                edgeVertices[7] = InterpolateVertex(cubePos[7], cubePos[4], cubeValues[7], cubeValues[4], isoLevel, size, resX, resY, resZ);
            if ((edgeTable[cubeIndex] & 256) != 0)
                edgeVertices[8] = InterpolateVertex(cubePos[0], cubePos[4], cubeValues[0], cubeValues[4], isoLevel, size, resX, resY, resZ);
            if ((edgeTable[cubeIndex] & 512) != 0)
                edgeVertices[9] = InterpolateVertex(cubePos[1], cubePos[5], cubeValues[1], cubeValues[5], isoLevel, size, resX, resY, resZ);
            if ((edgeTable[cubeIndex] & 1024) != 0)
                edgeVertices[10] = InterpolateVertex(cubePos[2], cubePos[6], cubeValues[2], cubeValues[6], isoLevel, size, resX, resY, resZ);
            if ((edgeTable[cubeIndex] & 2048) != 0)
                edgeVertices[11] = InterpolateVertex(cubePos[3], cubePos[7], cubeValues[3], cubeValues[7], isoLevel, size, resX, resY, resZ);
            
            // 创建三角形
            for (int i = 0; triangleTable[cubeIndex * 16 + i] != -1; i += 3)
            {
                int index1 = triangleTable[cubeIndex * 16 + i];
                int index2 = triangleTable[cubeIndex * 16 + i + 1];
                int index3 = triangleTable[cubeIndex * 16 + i + 2];
                
                int vertIndex = vertices.Count;
                vertices.Add(edgeVertices[index1]);
                vertices.Add(edgeVertices[index2]);
                vertices.Add(edgeVertices[index3]);
                
                triangles.Add(vertIndex);
                triangles.Add(vertIndex + 1);
                triangles.Add(vertIndex + 2);
            }
        }
        
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        
        return mesh;
    }
    
    private Vector3 InterpolateVertex(Vector3 v1, Vector3 v2, float val1, float val2, float isoLevel, Vector3 size, int resX, int resY, int resZ)
    {
        if (Mathf.Abs(isoLevel - val1) < 0.00001f)
            return ConvertToWorldSpace(v1, size, resX, resY, resZ);
        if (Mathf.Abs(isoLevel - val2) < 0.00001f)
            return ConvertToWorldSpace(v2, size, resX, resY, resZ);
        if (Mathf.Abs(val1 - val2) < 0.00001f)
            return ConvertToWorldSpace(v1, size, resX, resY, resZ);
            
        float t = (isoLevel - val1) / (val2 - val1);
        Vector3 vertexPosition = Vector3.Lerp(v1, v2, t);
        
        return ConvertToWorldSpace(vertexPosition, size, resX, resY, resZ);
    }
    
    private Vector3 ConvertToWorldSpace(Vector3 gridPos, Vector3 size, int resX, int resY, int resZ)
    {
        // 修复坐标转换
        return new Vector3(
            (gridPos.x / resX - 0.5f) * size.x,
            (gridPos.y / resY - 0.5f) * size.y,
            (gridPos.z / resZ - 0.5f) * size.z
        );
    }
}