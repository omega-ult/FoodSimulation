using UnityEngine;

public class FluidGrid
{
    public enum CellType
    {
        Air,
        Fluid,
        Boundary
    }
    
    private Vector3Int resolution;
    private float cellSize;
    private Vector3 origin;
    
    private Vector3[] velocityField;
    private float[] pressureField;
    private float[] densityField;
    private CellType[] cellTypes;
    
    public FluidGrid(Vector3Int resolution, float cellSize, Vector3 origin)
    {
        this.resolution = resolution;
        this.cellSize = cellSize;
        this.origin = origin;
        
        int totalCells = resolution.x * resolution.y * resolution.z;
        velocityField = new Vector3[totalCells];
        pressureField = new float[totalCells];
        densityField = new float[totalCells];
        cellTypes = new CellType[totalCells];
        
        // 初始化为空气
        for (int i = 0; i < totalCells; i++)
        {
            cellTypes[i] = CellType.Air;
        }
    }
    
    public FluidGrid Clone()
    {
        FluidGrid clone = new FluidGrid(resolution, cellSize, origin);
        velocityField.CopyTo(clone.velocityField, 0);
        pressureField.CopyTo(clone.pressureField, 0);
        densityField.CopyTo(clone.densityField, 0);
        cellTypes.CopyTo(clone.cellTypes, 0);
        return clone;
    }
    
    private int GetIndex(int x, int y, int z)
    {
        return x + resolution.x * (y + resolution.y * z);
    }
    
    public Vector3 GetCellPosition(int x, int y, int z)
    {
        return origin + new Vector3(
            (x + 0.5f) * cellSize,
            (y + 0.5f) * cellSize,
            (z + 0.5f) * cellSize
        );
    }
    
    public bool IsCellFluid(int x, int y, int z)
    {
        if (x < 0 || x >= resolution.x || y < 0 || y >= resolution.y || z < 0 || z >= resolution.z)
            return false;
        return cellTypes[GetIndex(x, y, z)] == CellType.Fluid;
    }
    
    public void SetCellType(int x, int y, int z, CellType type)
    {
        cellTypes[GetIndex(x, y, z)] = type;
    }
    
    public Vector3 GetVelocity(int x, int y, int z)
    {
        return velocityField[GetIndex(x, y, z)];
    }
    
    public void SetVelocity(int x, int y, int z, Vector3 velocity)
    {
        velocityField[GetIndex(x, y, z)] = velocity;
    }
    
    public void AddForce(int x, int y, int z, Vector3 force)
    {
        int index = GetIndex(x, y, z);
        velocityField[index] += force;
    }
    
    public float GetPressure(int x, int y, int z)
    {
        return pressureField[GetIndex(x, y, z)];
    }
    
    public void SetPressure(int x, int y, int z, float pressure)
    {
        pressureField[GetIndex(x, y, z)] = pressure;
    }
    
    public float GetDensity(int x, int y, int z)
    {
        return densityField[GetIndex(x, y, z)];
    }
    
    public void SetDensity(int x, int y, int z, float density)
    {
        densityField[GetIndex(x, y, z)] = density;
    }
    
    public float CalculateDivergence(int x, int y, int z)
    {
        Vector3 vRight = GetVelocity(x + 1, y, z);
        Vector3 vLeft = GetVelocity(x - 1, y, z);
        Vector3 vUp = GetVelocity(x, y + 1, z);
        Vector3 vDown = GetVelocity(x, y - 1, z);
        Vector3 vForward = GetVelocity(x, y, z + 1);
        Vector3 vBack = GetVelocity(x, y, z - 1);
        
        return (vRight.x - vLeft.x + vUp.y - vDown.y + vForward.z - vBack.z) * 0.5f;
    }
    
    public Vector3 SampleVelocity(Vector3 position)
    {
        // 转换到网格坐标
        Vector3 gridPos = (position - origin) / cellSize;
        
        // 获取最近的网格点
        int x0 = Mathf.FloorToInt(gridPos.x);
        int y0 = Mathf.FloorToInt(gridPos.y);
        int z0 = Mathf.FloorToInt(gridPos.z);
        
        // 边界检查
        x0 = Mathf.Clamp(x0, 0, resolution.x - 2);
        y0 = Mathf.Clamp(y0, 0, resolution.y - 2);
        z0 = Mathf.Clamp(z0, 0, resolution.z - 2);
        
        // 计算插值权重
        float tx = gridPos.x - x0;
        float ty = gridPos.y - y0;
        float tz = gridPos.z - z0;
        
        // 三线性插值
        return TrilinearInterpolation(
            GetVelocity(x0, y0, z0),
            GetVelocity(x0 + 1, y0, z0),
            GetVelocity(x0, y0 + 1, z0),
            GetVelocity(x0 + 1, y0 + 1, z0),
            GetVelocity(x0, y0, z0 + 1),
            GetVelocity(x0 + 1, y0, z0 + 1),
            GetVelocity(x0, y0 + 1, z0 + 1),
            GetVelocity(x0 + 1, y0 + 1, z0 + 1),
            tx, ty, tz
        );
    }
    
    public float SampleDensity(Vector3 position)
    {
        // 转换到网格坐标
        Vector3 gridPos = (position - origin) / cellSize;
        
        // 获取最近的网格点
        int x0 = Mathf.FloorToInt(gridPos.x);
        int y0 = Mathf.FloorToInt(gridPos.y);
        int z0 = Mathf.FloorToInt(gridPos.z);
        
        // 边界检查
        x0 = Mathf.Clamp(x0, 0, resolution.x - 2);
        y0 = Mathf.Clamp(y0, 0, resolution.y - 2);
        z0 = Mathf.Clamp(z0, 0, resolution.z - 2);
        
        // 计算插值权重
        float tx = gridPos.x - x0;
        float ty = gridPos.y - y0;
        float tz = gridPos.z - z0;
        
        // 三线性插值
        return TrilinearInterpolation(
            GetDensity(x0, y0, z0),
            GetDensity(x0 + 1, y0, z0),
            GetDensity(x0, y0 + 1, z0),
            GetDensity(x0 + 1, y0 + 1, z0),
            GetDensity(x0, y0, z0 + 1),
            GetDensity(x0 + 1, y0, z0 + 1),
            GetDensity(x0, y0 + 1, z0 + 1),
            GetDensity(x0 + 1, y0 + 1, z0 + 1),
            tx, ty, tz
        );
    }
    
    private Vector3 TrilinearInterpolation(
        Vector3 v000, Vector3 v100, Vector3 v010, Vector3 v110,
        Vector3 v001, Vector3 v101, Vector3 v011, Vector3 v111,
        float tx, float ty, float tz)
    {
        Vector3 c00 = Vector3.Lerp(v000, v100, tx);
        Vector3 c10 = Vector3.Lerp(v010, v110, tx);
        Vector3 c01 = Vector3.Lerp(v001, v101, tx);
        Vector3 c11 = Vector3.Lerp(v011, v111, tx);
        
        Vector3 c0 = Vector3.Lerp(c00, c10, ty);
        Vector3 c1 = Vector3.Lerp(c01, c11, ty);
        
        return Vector3.Lerp(c0, c1, tz);
    }
    
    private float TrilinearInterpolation(
        float v000, float v100, float v010, float v110,
        float v001, float v101, float v011, float v111,
        float tx, float ty, float tz)
    {
        float c00 = Mathf.Lerp(v000, v100, tx);
        float c10 = Mathf.Lerp(v010, v110, tx);
        float c01 = Mathf.Lerp(v001, v101, tx);
        float c11 = Mathf.Lerp(v011, v111, tx);
        
        float c0 = Mathf.Lerp(c00, c10, ty);
        float c1 = Mathf.Lerp(c01, c11, ty);
        
        return Mathf.Lerp(c0, c1, tz);
    }
    
    public void CopyVelocityFrom(FluidGrid other)
    {
        other.velocityField.CopyTo(velocityField, 0);
    }
    
    public void CopyDensityFrom(FluidGrid other)
    {
        other.densityField.CopyTo(densityField, 0);
    }
}