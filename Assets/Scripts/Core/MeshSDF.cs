using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class MeshSDF : SDFPrimitive
{
    [Header("SDF 设置")]
    public int resolution = 32;
    public float boundaryPadding = 1.0f;
    
    private float[,,] distanceField;
    private Vector3 gridOrigin;
    private float cellSize;
    private Bounds meshBounds;
    
    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            GenerateDistanceField(meshFilter.sharedMesh);
        }
    }
    
    private void GenerateDistanceField(Mesh mesh)
    {
        // 计算网格边界
        meshBounds = mesh.bounds;
        Vector3 size = meshBounds.size * (1 + boundaryPadding);
        gridOrigin = meshBounds.center - size * 0.5f;
        cellSize = Mathf.Max(size.x, size.y, size.z) / resolution;
        
        // 初始化距离场
        int gridX = Mathf.CeilToInt(size.x / cellSize);
        int gridY = Mathf.CeilToInt(size.y / cellSize);
        int gridZ = Mathf.CeilToInt(size.z / cellSize);
        distanceField = new float[gridX, gridY, gridZ];
        
        // 获取网格数据
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        
        // 计算每个网格点的距离
        for (int x = 0; x < gridX; x++)
        for (int y = 0; y < gridY; y++)
        for (int z = 0; z < gridZ; z++)
        {
            Vector3 point = GetWorldPosition(x, y, z);
            float minDistance = float.MaxValue;
            
            // 计算到所有三角形的最短距离
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v1 = transform.TransformPoint(vertices[triangles[i]]);
                Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 v3 = transform.TransformPoint(vertices[triangles[i + 2]]);
                
                float dist = PointTriangleDistance(point, v1, v2, v3);
                minDistance = Mathf.Min(minDistance, dist);
            }
            
            // 确定内外部
            bool isInside = IsPointInsideMesh(point, mesh);
            distanceField[x, y, z] = isInside ? -minDistance : minDistance;
        }
    }
    
    public override float GetDistance(Vector3 point)
    {
        // 转换到局部坐标
        point = transform.InverseTransformPoint(point);
        
        // 获取网格坐标
        Vector3 gridPos = (point - gridOrigin) / cellSize;
        int x = Mathf.FloorToInt(gridPos.x);
        int y = Mathf.FloorToInt(gridPos.y);
        int z = Mathf.FloorToInt(gridPos.z);
        
        // 检查边界
        if (x < 0 || x >= distanceField.GetLength(0) - 1 ||
            y < 0 || y >= distanceField.GetLength(1) - 1 ||
            z < 0 || z >= distanceField.GetLength(2) - 1)
        {
            return meshBounds.SqrDistance(point);
        }
        
        // 三线性插值
        Vector3 t = gridPos - new Vector3(x, y, z);
        return TrilinearInterpolation(x, y, z, t);
    }
    
    private float TrilinearInterpolation(int x, int y, int z, Vector3 t)
    {
        float c000 = distanceField[x, y, z];
        float c100 = distanceField[x + 1, y, z];
        float c010 = distanceField[x, y + 1, z];
        float c110 = distanceField[x + 1, y + 1, z];
        float c001 = distanceField[x, y, z + 1];
        float c101 = distanceField[x + 1, y, z + 1];
        float c011 = distanceField[x, y + 1, z + 1];
        float c111 = distanceField[x + 1, y + 1, z + 1];
        
        return Mathf.Lerp(
            Mathf.Lerp(
                Mathf.Lerp(c000, c100, t.x),
                Mathf.Lerp(c010, c110, t.x),
                t.y),
            Mathf.Lerp(
                Mathf.Lerp(c001, c101, t.x),
                Mathf.Lerp(c011, c111, t.x),
                t.y),
            t.z);
    }
    
    private Vector3 GetWorldPosition(int x, int y, int z)
    {
        return gridOrigin + new Vector3(x, y, z) * cellSize;
    }
    
    private bool IsPointInsideMesh(Vector3 point, Mesh mesh)
    {
        // 使用光线投射法判断点是否在网格内部
        int intersections = 0;
        Vector3 rayStart = point;
        Vector3 rayDir = Vector3.right;
        
        Ray ray = new Ray(rayStart, rayDir);
        RaycastHit[] hits = Physics.RaycastAll(ray);
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject == gameObject)
            {
                intersections++;
            }
        }
        
        return (intersections % 2) == 1;
    }
    
    private float PointTriangleDistance(Vector3 point, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        // 计算点到三角形的最短距离
        Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;
        float planeDistance = Vector3.Dot(point - v1, normal);
        Vector3 projectedPoint = point - planeDistance * normal;
        
        // 检查投影点是否在三角形内
        if (IsPointInTriangle(projectedPoint, v1, v2, v3))
        {
            return Mathf.Abs(planeDistance);
        }
        
        // 如果不在三角形内，计算到边的最短距离
        float d1 = PointLineDistance(point, v1, v2);
        float d2 = PointLineDistance(point, v2, v3);
        float d3 = PointLineDistance(point, v3, v1);
        
        return Mathf.Min(d1, Mathf.Min(d2, d3));
    }
    
    private bool IsPointInTriangle(Vector3 point, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        // 使用重心坐标判断点是否在三角形内
        Vector3 u = v2 - v1;
        Vector3 v = v3 - v1;
        Vector3 w = point - v1;
        
        Vector3 vCrossW = Vector3.Cross(v, w);
        Vector3 vCrossU = Vector3.Cross(v, u);
        
        if (Vector3.Dot(vCrossW, vCrossU) < 0)
            return false;
            
        Vector3 uCrossW = Vector3.Cross(u, w);
        Vector3 uCrossV = Vector3.Cross(u, v);
        
        if (Vector3.Dot(uCrossW, uCrossV) < 0)
            return false;
            
        float denom = uCrossV.magnitude;
        float r = vCrossW.magnitude / denom;
        float t = uCrossW.magnitude / denom;
        
        return r + t <= 1;
    }
    
    private float PointLineDistance(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 line = end - start;
        float len = line.magnitude;
        line.Normalize();
        
        float t = Vector3.Dot(point - start, line);
        t = Mathf.Clamp(t, 0, len);
        
        Vector3 projection = start + line * t;
        return Vector3.Distance(point, projection);
    }
}