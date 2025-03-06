using UnityEngine;

public class ColliderToSDF : SDFPrimitive
{
    private Collider targetCollider;
    private float[,,] sdfGrid;
    private Vector3 gridSize = new Vector3(32, 32, 32);
    private Vector3 boundsMin;
    private Vector3 boundsMax;
    private float cellSize;
    
    [Tooltip("用于射线检测的方向数量")]
    public int rayDirectionCount = 6;
    
    private void Awake()
    {
        targetCollider = GetComponent<Collider>();
        if (targetCollider == null)
        {
            Debug.LogError("ColliderToSDF需要一个Collider组件!");
            return;
        }
        InitializeSDFGrid();
    }
    
    private void InitializeSDFGrid()
    {
        Bounds bounds = targetCollider.bounds;
        boundsMin = bounds.min;
        boundsMax = bounds.max;
        
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
        // 使用Physics.ComputePenetration计算距离
        Vector3 direction;
        float distance;
        
        if (targetCollider.ClosestPoint(point) == point)
        {
            // 点在碰撞体外部
            distance = Vector3.Distance(point, targetCollider.ClosestPoint(point));
        }
        else
        {
            // 点在碰撞体内部
            distance = -DetermineInsideDistance(point);
        }
        
        return distance;
    }
    
    private float DetermineInsideDistance(Vector3 point)
    {
        Vector3[] directions;
        if (rayDirectionCount <= 6)
        {
            directions = new Vector3[] {
                Vector3.right,
                Vector3.left,
                Vector3.up,
                Vector3.down,
                Vector3.forward,
                Vector3.back
            };
        }
        else
        {
            directions = new Vector3[rayDirectionCount];
            for (int i = 0; i < rayDirectionCount; i++)
            {
                directions[i] = Random.onUnitSphere;
            }
        }
        
        float minDistance = float.MaxValue;
        foreach (var direction in directions)
        {
            RaycastHit hit;
            if (Physics.Raycast(point, direction, out hit))
            {
                if (hit.collider == targetCollider)
                {
                    minDistance = Mathf.Min(minDistance, hit.distance);
                }
            }
        }
        
        return minDistance;
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

        // 如果点在网格外，直接计算距离
        int x0 = Mathf.FloorToInt(gridPos.x);
        int y0 = Mathf.FloorToInt(gridPos.y);
        int z0 = Mathf.FloorToInt(gridPos.z);
        
        if (x0 < 0 || y0 < 0 || z0 < 0 || 
            x0 >= gridSize.x-1 || y0 >= gridSize.y-1 || z0 >= gridSize.z-1)
            return CalculateDistance(point);

        // 三线性插值
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