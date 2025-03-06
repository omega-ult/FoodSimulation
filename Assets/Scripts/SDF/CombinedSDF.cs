using UnityEngine;
using System.Collections.Generic;

public class CombinedSDF : SDFPrimitive
{
    public enum CombineOperation
    {
        Union,
        Intersection,
        Subtraction,
        SmoothUnion
    }

    [System.Serializable]
    public class SDFComponent
    {
        public SDFPrimitive sdfObject;
        public CombineOperation operation = CombineOperation.Union;
        public float smoothFactor = 0.1f;
    }

    public List<SDFComponent> sdfComponents = new List<SDFComponent>();
    public float defaultDistance = 1000f;
    
    [Header("预计算设置")]
    public Vector3Int gridResolution = new Vector3Int(64, 64, 64);
    public bool usePrecomputed = false;
    public bool useComputeShader = true;
    
    private float[] precomputedGrid;
    private Vector3 boundsMin;
    private Vector3 boundsMax;
    private float cellSize;
    private bool isPrecomputed = false;
    
    private ComputeShader computeShader;
    private ComputeBuffer resultBuffer;
    private ComputeBuffer[] sdfBuffers;
    
    [System.Serializable]
    private struct SDFData
    {
        public float distance;
        public int operation;
        public float smoothFactor;
    }

    private void Start()
    {
        if (usePrecomputed)
        {
            PrecomputeSDF();
        }
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }

    private void ReleaseBuffers()
    {
        if (resultBuffer != null)
        {
            resultBuffer.Release();
            resultBuffer = null;
        }
        
        if (sdfBuffers != null)
        {
            foreach (var buffer in sdfBuffers)
            {
                if (buffer != null)
                    buffer.Release();
            }
            sdfBuffers = null;
        }
    }

    public void PrecomputeSDF()
    {
        if (sdfComponents.Count == 0) return;
        
        // 计算边界
        CalculateBounds();
        
        int totalSize = gridResolution.x * gridResolution.y * gridResolution.z;
        precomputedGrid = new float[totalSize];
        
        if (useComputeShader && SystemInfo.supportsComputeShaders)
        {
            PrecomputeWithComputeShader();
        }
        else
        {
            PrecomputeOnCPU();
        }
        
        isPrecomputed = true;
    }

    private void CalculateBounds()
    {
        // 初始化边界
        boundsMin = Vector3.one * float.MaxValue;
        boundsMax = Vector3.one * float.MinValue;
        
        // 合并所有组件的边界
        foreach (var component in sdfComponents)
        {
            if (component.sdfObject == null) continue;
            
            MeshFilter meshFilter = component.sdfObject.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                Bounds bounds = meshFilter.mesh.bounds;
                Vector3 min = component.sdfObject.transform.TransformPoint(bounds.min);
                Vector3 max = component.sdfObject.transform.TransformPoint(bounds.max);
                
                boundsMin = Vector3.Min(boundsMin, min);
                boundsMax = Vector3.Max(boundsMax, max);
            }
        }
        
        // 添加边界填充
        Vector3 padding = (boundsMax - boundsMin) * 0.1f;
        boundsMin -= padding;
        boundsMax += padding;
        
        // 计算单元格大小
        cellSize = Mathf.Max(
            (boundsMax.x - boundsMin.x) / gridResolution.x,
            (boundsMax.y - boundsMin.y) / gridResolution.y,
            (boundsMax.z - boundsMin.z) / gridResolution.z
        );
    }

    private void PrecomputeOnCPU()
    {
        int totalSize = gridResolution.x * gridResolution.y * gridResolution.z;
        
        // 预计算每个组件的SDF值
        float[][] componentGrids = new float[sdfComponents.Count][];
        for (int c = 0; c < sdfComponents.Count; c++)
        {
            componentGrids[c] = new float[totalSize];
            
            for (int x = 0; x < gridResolution.x; x++)
            for (int y = 0; y < gridResolution.y; y++)
            for (int z = 0; z < gridResolution.z; z++)
            {
                Vector3 worldPos = GetWorldPositionFromGrid(x, y, z);
                int index = GetGridIndex(x, y, z);
                componentGrids[c][index] = sdfComponents[c].sdfObject.GetDistance(worldPos);
            }
        }
        
        // 组合SDF值
        for (int i = 0; i < totalSize; i++)
        {
            float result = componentGrids[0][i];
            
            for (int c = 1; c < sdfComponents.Count; c++)
            {
                float distance = componentGrids[c][i];
                CombineOperation operation = sdfComponents[c].operation;
                float smoothFactor = sdfComponents[c].smoothFactor;
                
                switch (operation)
                {
                    case CombineOperation.Union:
                        result = Mathf.Min(result, distance);
                        break;
                        
                    case CombineOperation.Intersection:
                        result = Mathf.Max(result, distance);
                        break;
                        
                    case CombineOperation.Subtraction:
                        result = Mathf.Max(result, -distance);
                        break;
                        
                    case CombineOperation.SmoothUnion:
                        float h = Mathf.Clamp01(0.5f + 0.5f * (distance - result) / smoothFactor);
                        result = Mathf.Lerp(distance, result, h) - smoothFactor * h * (1.0f - h);
                        break;
                }
            }
            
            precomputedGrid[i] = result;
        }
    }

    private void PrecomputeWithComputeShader()
    {
        computeShader = Resources.Load<ComputeShader>("CombinedSDFCompute");
        if (computeShader == null)
        {
            Debug.LogError("无法加载Compute Shader，回退到CPU计算");
            PrecomputeOnCPU();
            return;
        }
        
        int totalSize = gridResolution.x * gridResolution.y * gridResolution.z;
        
        // 创建结果缓冲区
        resultBuffer = new ComputeBuffer(totalSize, sizeof(float));
        
        // 创建每个SDF组件的缓冲区
        sdfBuffers = new ComputeBuffer[sdfComponents.Count];
        
        // 预计算每个组件的SDF值并设置缓冲区
        for (int c = 0; c < sdfComponents.Count; c++)
        {
            SDFData[] sdfData = new SDFData[totalSize];
            
            for (int x = 0; x < gridResolution.x; x++)
            for (int y = 0; y < gridResolution.y; y++)
            for (int z = 0; z < gridResolution.z; z++)
            {
                Vector3 worldPos = GetWorldPositionFromGrid(x, y, z);
                int index = GetGridIndex(x, y, z);
                
                sdfData[index] = new SDFData
                {
                    distance = sdfComponents[c].sdfObject.GetDistance(worldPos),
                    operation = (int)sdfComponents[c].operation,
                    smoothFactor = sdfComponents[c].smoothFactor
                };
            }
            
            sdfBuffers[c] = new ComputeBuffer(totalSize, sizeof(float) + sizeof(int) + sizeof(float));
            sdfBuffers[c].SetData(sdfData);
        }
        
        // 合并所有SDF缓冲区到一个大缓冲区
        SDFData[] allSDFData = new SDFData[totalSize * sdfComponents.Count];
        for (int c = 0; c < sdfComponents.Count; c++)
        {
            SDFData[] componentData = new SDFData[totalSize];
            sdfBuffers[c].GetData(componentData);
            
            for (int i = 0; i < totalSize; i++)
            {
                allSDFData[c * totalSize + i] = componentData[i];
            }
            
            // 释放单个组件缓冲区
            sdfBuffers[c].Release();
        }
        
        // 创建合并后的缓冲区
        ComputeBuffer combinedBuffer = new ComputeBuffer(totalSize * sdfComponents.Count, sizeof(float) + sizeof(int) + sizeof(float));
        combinedBuffer.SetData(allSDFData);
        
        // 设置计算着色器参数
        int kernel = computeShader.FindKernel("CombineSDF");
        computeShader.SetBuffer(kernel, "ResultGrid", resultBuffer);
        computeShader.SetBuffer(kernel, "SDFGrids", combinedBuffer);
        computeShader.SetVector("GridSize", new Vector3(gridResolution.x, gridResolution.y, gridResolution.z));
        computeShader.SetInt("SDFCount", sdfComponents.Count);
        computeShader.SetVector("BoundsMin", boundsMin);
        computeShader.SetFloat("CellSize", cellSize);
        
        // 调度计算着色器
        computeShader.Dispatch(kernel, 
            Mathf.CeilToInt(gridResolution.x / 8f),
            Mathf.CeilToInt(gridResolution.y / 8f),
            Mathf.CeilToInt(gridResolution.z / 8f));
        
        // 获取结果
        resultBuffer.GetData(precomputedGrid);
        
        // 释放缓冲区
        combinedBuffer.Release();
    }

    private Vector3 GetWorldPositionFromGrid(int x, int y, int z)
    {
        return new Vector3(
            boundsMin.x + (x + 0.5f) * cellSize,
            boundsMin.y + (y + 0.5f) * cellSize,
            boundsMin.z + (z + 0.5f) * cellSize
        );
    }

    private int GetGridIndex(int x, int y, int z)
    {
        return x + gridResolution.x * (y + gridResolution.y * z);
    }

    public override float GetDistance(Vector3 point)
    {
        if (sdfComponents.Count == 0)
            return defaultDistance;
            
        if (usePrecomputed && isPrecomputed)
        {
            return GetPrecomputedDistance(point);
        }
        else
        {
            return CalculateDistanceDirectly(point);
        }
    }
    
    private float GetPrecomputedDistance(Vector3 point)
    {
        // 检查点是否在网格边界内
        if (point.x < boundsMin.x || point.y < boundsMin.y || point.z < boundsMin.z ||
            point.x > boundsMax.x || point.y > boundsMax.y || point.z > boundsMax.z)
        {
            return CalculateDistanceDirectly(point);
        }
        
        // 计算网格坐标
        Vector3 localPos = point - boundsMin;
        Vector3 gridPos = new Vector3(
            localPos.x / cellSize,
            localPos.y / cellSize,
            localPos.z / cellSize
        );
        
        // 三线性插值
        int x0 = Mathf.FloorToInt(gridPos.x);
        int y0 = Mathf.FloorToInt(gridPos.y);
        int z0 = Mathf.FloorToInt(gridPos.z);
        
        // 边界检查
        x0 = Mathf.Clamp(x0, 0, gridResolution.x - 2);
        y0 = Mathf.Clamp(y0, 0, gridResolution.y - 2);
        z0 = Mathf.Clamp(z0, 0, gridResolution.z - 2);
        
        float tx = gridPos.x - x0;
        float ty = gridPos.y - y0;
        float tz = gridPos.z - z0;
        
        int idx000 = GetGridIndex(x0, y0, z0);
        int idx100 = GetGridIndex(x0 + 1, y0, z0);
        int idx010 = GetGridIndex(x0, y0 + 1, z0);
        int idx110 = GetGridIndex(x0 + 1, y0 + 1, z0);
        int idx001 = GetGridIndex(x0, y0, z0 + 1);
        int idx101 = GetGridIndex(x0 + 1, y0, z0 + 1);
        int idx011 = GetGridIndex(x0, y0 + 1, z0 + 1);
        int idx111 = GetGridIndex(x0 + 1, y0 + 1, z0 + 1);

        return Mathf.Lerp(
            Mathf.Lerp(
                Mathf.Lerp(precomputedGrid[idx000], precomputedGrid[idx100], tx),
                Mathf.Lerp(precomputedGrid[idx010], precomputedGrid[idx110], tx),
                ty),
            Mathf.Lerp(
                Mathf.Lerp(precomputedGrid[idx001], precomputedGrid[idx101], tx),
                Mathf.Lerp(precomputedGrid[idx011], precomputedGrid[idx111], tx),
                ty),
            tz);
    }

    private float CalculateDistanceDirectly(Vector3 point)
    {
        if (sdfComponents.Count == 0)
            return defaultDistance;
            
        float result = sdfComponents[0].sdfObject.GetDistance(point);
        
        for (int i = 1; i < sdfComponents.Count; i++)
        {
            float distance = sdfComponents[i].sdfObject.GetDistance(point);
            CombineOperation operation = sdfComponents[i].operation;
            float smoothFactor = sdfComponents[i].smoothFactor;
            
            switch (operation)
            {
                case CombineOperation.Union:
                    result = Mathf.Min(result, distance);
                    break;
                    
                case CombineOperation.Intersection:
                    result = Mathf.Max(result, distance);
                    break;
                    
                case CombineOperation.Subtraction:
                    result = Mathf.Max(result, -distance);
                    break;
                    
                case CombineOperation.SmoothUnion:
                    float h = Mathf.Clamp01(0.5f + 0.5f * (distance - result) / smoothFactor);
                    result = Mathf.Lerp(distance, result, h) - smoothFactor * h * (1.0f - h);
                    break;
            }
        }
        
        return result;
    }

    public override Vector3 GetNormal(Vector3 point)
    {
        // 使用中心差分法计算法线
        float epsilon = 0.001f;
        Vector3 normal = new Vector3(
            GetDistance(point + new Vector3(epsilon, 0, 0)) - GetDistance(point - new Vector3(epsilon, 0, 0)),
            GetDistance(point + new Vector3(0, epsilon, 0)) - GetDistance(point - new Vector3(0, epsilon, 0)),
            GetDistance(point + new Vector3(0, 0, epsilon)) - GetDistance(point - new Vector3(0, 0, epsilon))
        );
        return normal.normalized;
    }

    // 添加组件的便捷方法
    public void AddComponent(SDFPrimitive sdf, CombineOperation operation = CombineOperation.Union, float smoothFactor = 0.1f)
    {
        SDFComponent component = new SDFComponent
        {
            sdfObject = sdf,
            operation = operation,
            smoothFactor = smoothFactor
        };
        
        sdfComponents.Add(component);
        
        // 如果已经预计算，则需要重新计算
        if (isPrecomputed)
        {
            PrecomputeSDF();
        }
    }
    
    // 导出预计算的SDF网格为资产
    public void ExportPrecomputedSDF(string assetPath)
    {
        if (!isPrecomputed)
        {
            PrecomputeSDF();
        }
        
        // 创建网格资产
        Mesh mesh = GenerateIsoSurfaceMesh(0f);
        
#if UNITY_EDITOR
        // 保存网格资产
        UnityEditor.AssetDatabase.CreateAsset(mesh, assetPath);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("预计算SDF网格已导出到: " + assetPath);
#else
        Debug.LogWarning("导出预计算SDF网格仅在编辑器模式下可用");
#endif
    }
    
    // 生成等值面网格
    public Mesh GenerateIsoSurfaceMesh(float isoLevel)
    {
        if (!isPrecomputed)
        {
            PrecomputeSDF();
        }
        
        // 将1D数组转换为3D数组以便于处理
        float[,,] sdfValues = new float[gridResolution.x, gridResolution.y, gridResolution.z];
        for (int x = 0; x < gridResolution.x; x++)
        for (int y = 0; y < gridResolution.y; y++)
        for (int z = 0; z < gridResolution.z; z++)
        {
            sdfValues[x, y, z] = precomputedGrid[GetGridIndex(x, y, z)];
        }
        
        // 使用Marching Cubes算法生成网格
        MarchingCubesWrapper marchingCubes = new MarchingCubesWrapper();
        Vector3 size = boundsMax - boundsMin;
        Mesh mesh = marchingCubes.GenerateMesh(sdfValues, isoLevel, size);
        
        // 调整网格位置
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] += boundsMin;
        }
        mesh.vertices = vertices;
        
        // 重新计算法线和边界
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
}

// Marching Cubes算法包装器
public class MarchingCubesWrapper
{
    // 三角形表 - 存储每种立方体配置的三角形顶点索引
    private static readonly int[] triangleTable = {
        // 这里应该是完整的Marching Cubes三角形表
        // 为简化，这里只提供部分数据
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        // ... 更多三角形表数据 ...
    };
    
    // 边缘表 - 存储每种立方体配置的活动边
    private static readonly int[] edgeTable = {
        0x0, 0x109, 0x203, 0x30a, 0x406, 0x50f, 0x605, 0x70c,
        0x80c, 0x905, 0xa0f, 0xb06, 0xc0a, 0xd03, 0xe09, 0xf00,
        // ... 更多边缘表数据 ...
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
            
            // 确定立方体配置索引
            int cubeIndex = 0;
            for (int i = 0; i < 8; i++)
                if (cubeValues[i] < isoLevel)
                    cubeIndex |= 1 << i;
                    
            // 如果立方体完全在等值面内部或外部，则跳过
            if (cubeIndex == 0 || cubeIndex == 255)
                continue;
                
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
            
            // 立方体边缘
            Vector3[] edgeVertices = new Vector3[12];
            
            // 计算边缘交点
            if ((edgeTable[cubeIndex] & 1) != 0)
                edgeVertices[0] = InterpolateVertex(cubePos[0], cubePos[1], cubeValues[0], cubeValues[1], isoLevel);
            if ((edgeTable[cubeIndex] & 2) != 0)
                edgeVertices[1] = InterpolateVertex(cubePos[1], cubePos[2], cubeValues[1], cubeValues[2], isoLevel);
            if ((edgeTable[cubeIndex] & 4) != 0)
                edgeVertices[2] = InterpolateVertex(cubePos[2], cubePos[3], cubeValues[2], cubeValues[3], isoLevel);
            if ((edgeTable[cubeIndex] & 8) != 0)
                edgeVertices[3] = InterpolateVertex(cubePos[3], cubePos[0], cubeValues[3], cubeValues[0], isoLevel);
            if ((edgeTable[cubeIndex] & 16) != 0)
                edgeVertices[4] = InterpolateVertex(cubePos[4], cubePos[5], cubeValues[4], cubeValues[5], isoLevel);
            if ((edgeTable[cubeIndex] & 32) != 0)
                edgeVertices[5] = InterpolateVertex(cubePos[5], cubePos[6], cubeValues[5], cubeValues[6], isoLevel);
            if ((edgeTable[cubeIndex] & 64) != 0)
                edgeVertices[6] = InterpolateVertex(cubePos[6], cubePos[7], cubeValues[6], cubeValues[7], isoLevel);
            if ((edgeTable[cubeIndex] & 128) != 0)
                edgeVertices[7] = InterpolateVertex(cubePos[7], cubePos[4], cubeValues[7], cubeValues[4], isoLevel);
            if ((edgeTable[cubeIndex] & 256) != 0)
                edgeVertices[8] = InterpolateVertex(cubePos[0], cubePos[4], cubeValues[0], cubeValues[4], isoLevel);
            if ((edgeTable[cubeIndex] & 512) != 0)
                edgeVertices[9] = InterpolateVertex(cubePos[1], cubePos[5], cubeValues[1], cubeValues[5], isoLevel);
            if ((edgeTable[cubeIndex] & 1024) != 0)
                edgeVertices[10] = InterpolateVertex(cubePos[2], cubePos[6], cubeValues[2], cubeValues[6], isoLevel);
            if ((edgeTable[cubeIndex] & 2048) != 0)
                edgeVertices[11] = InterpolateVertex(cubePos[3], cubePos[7], cubeValues[3], cubeValues[7], isoLevel);
            
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
        
        // 转换到世界坐标
        for (int i = 0; i < vertices.Count; i++)
        {
            vertices[i] = new Vector3(
                vertices[i].x / resX * size.x,
                vertices[i].y / resY * size.y,
                vertices[i].z / resZ * size.z
            );
        }
        
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        
        return mesh;
    }
    
    private Vector3 InterpolateVertex(Vector3 v1, Vector3 v2, float val1, float val2, float isoLevel)
    {
        if (Mathf.Abs(isoLevel - val1) < 0.00001f)
            return v1;
        if (Mathf.Abs(isoLevel - val2) < 0.00001f)
            return v2;
        if (Mathf.Abs(val1 - val2) < 0.00001f)
            return v1;
            
        float t = (isoLevel - val1) / (val2 - val1);
        return Vector3.Lerp(v1, v2, t);
    }
}