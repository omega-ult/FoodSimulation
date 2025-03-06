using UnityEngine;

public class MeshToSDF : SDFPrimitive
{
    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private float[,,] sdfGrid;
    private Vector3 gridSize = new Vector3(32, 32, 32);
    private Vector3 boundsMin;
    private Vector3 boundsMax;
    private float cellSize;

    private void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        triangles = mesh.triangles;
        InitializeSDFGrid();
    }

    private void InitializeSDFGrid()
    {
        Bounds bounds = mesh.bounds;
        boundsMin = transform.TransformPoint(bounds.min);
        boundsMax = transform.TransformPoint(bounds.max);
        
        cellSize = Mathf.Max(
            (boundsMax.x - boundsMin.x) / gridSize.x,
            (boundsMax.y - boundsMin.y) / gridSize.y,
            (boundsMax.z - boundsMin.z) / gridSize.z
        );

        sdfGrid = new float[(int)gridSize.x, (int)gridSize.y, (int)gridSize.z];
        GenerateSDFGrid();
    }

    private void GenerateSDFGrid()
    {
        for (int x = 0; x < gridSize.x; x++)
        for (int y = 0; y < gridSize.y; y++)
        for (int z = 0; z < gridSize.z; z++)
        {
            Vector3 worldPos = GetWorldPositionFromGrid(x, y, z);
            sdfGrid[x, y, z] = CalculateDistance(worldPos);
        }
    }

    private Vector3 GetWorldPositionFromGrid(int x, int y, int z)
    {
        return new Vector3(
            boundsMin.x + x * cellSize,
            boundsMin.y + y * cellSize,
            boundsMin.z + z * cellSize
        );
    }

    private float CalculateDistance(Vector3 point)
    {
        float minDist = float.MaxValue;
        
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v1 = transform.TransformPoint(vertices[triangles[i]]);
            Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 v3 = transform.TransformPoint(vertices[triangles[i + 2]]);
            
            float dist = PointTriangleDistance(point, v1, v2, v3);
            minDist = Mathf.Min(minDist, dist);
        }
        
        return minDist;
    }

    private float PointTriangleDistance(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 bc = c - b;
        Vector3 ca = a - c;

        Vector3 normal = Vector3.Cross(ab, -ca).normalized;
        float dist = Mathf.Abs(Vector3.Dot(p - a, normal));

        if (PointInTriangle(p, a, b, c, normal))
            return dist;

        float d1 = PointLineDistance(p, a, b);
        float d2 = PointLineDistance(p, b, c);
        float d3 = PointLineDistance(p, c, a);

        return Mathf.Min(d1, Mathf.Min(d2, d3));
    }

    private bool PointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
    {
        Vector3 ap = p - a;
        Vector3 bp = p - b;
        Vector3 cp = p - c;

        Vector3 ab = b - a;
        Vector3 bc = c - b;
        Vector3 ca = a - c;

        if (Vector3.Dot(Vector3.Cross(ab, ap), normal) < 0) return false;
        if (Vector3.Dot(Vector3.Cross(bc, bp), normal) < 0) return false;
        if (Vector3.Dot(Vector3.Cross(ca, cp), normal) < 0) return false;

        return true;
    }

    private float PointLineDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        Vector3 ap = p - a;
        
        float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Vector3.Dot(ab, ab));
        Vector3 closest = a + t * ab;
        
        return Vector3.Distance(p, closest);
    }

    public override float GetDistance(Vector3 point)
    {
        // 将世界坐标转换为网格坐标
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
        
        if (x0 < 0 || y0 < 0 || z0 < 0 || 
            x0 >= gridSize.x-1 || y0 >= gridSize.y-1 || z0 >= gridSize.z-1)
            return CalculateDistance(point);

        float tx = gridPos.x - x0;
        float ty = gridPos.y - y0;
        float tz = gridPos.z - z0;

        return Mathf.Lerp(
            Mathf.Lerp(
                Mathf.Lerp(sdfGrid[x0,y0,z0], sdfGrid[x0+1,y0,z0], tx),
                Mathf.Lerp(sdfGrid[x0,y0+1,z0], sdfGrid[x0+1,y0+1,z0], tx),
                ty),
            Mathf.Lerp(
                Mathf.Lerp(sdfGrid[x0,y0,z0+1], sdfGrid[x0+1,y0,z0+1], tx),
                Mathf.Lerp(sdfGrid[x0,y0+1,z0+1], sdfGrid[x0+1,y0+1,z0+1], tx),
                ty),
            tz);
    }
}