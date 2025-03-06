using UnityEngine;
using System.Collections.Generic;

public class FluidSimulator : MonoBehaviour
{
    [Header("模拟参数")]
    public Vector3Int gridResolution = new Vector3Int(64, 64, 64);
    public float cellSize = 0.1f;
    public float timeStep = 0.016f;
    public int solverIterations = 20;
    public float viscosity = 0.1f;
    public float density = 1.0f;
    
    [Header("边界条件")]
    public List<SDFPrimitive> boundarySDFs = new List<SDFPrimitive>();
    public float boundaryFriction = 0.5f;
    
    [Header("可视化")]
    public bool visualizeVelocity = true;
    public bool visualizePressure = false;
    public bool visualizeDensity = true;
    public float visualScale = 1.0f;
    
    // 流体网格数据
    private FluidGrid fluidGrid;
    private Vector3 gridOrigin;
    
    // 可视化对象
    private GameObject velocityVisualizer;
    private GameObject densityVisualizer;
    
    // 计算着色器
    private ComputeShader fluidComputeShader;
    
    private void Start()
    {
        InitializeFluidGrid();
        InitializeComputeShader();
        
        if (visualizeVelocity || visualizePressure || visualizeDensity)
        {
            CreateVisualizers();
        }
    }
    
    private void Update()
    {
        // 每帧更新流体模拟
        SimulateFluid(timeStep);
        
        // 更新可视化
        if (visualizeVelocity || visualizePressure || visualizeDensity)
        {
            UpdateVisualizers();
        }
    }
    
    private void InitializeFluidGrid()
    {
        // 计算网格原点（居中于对象位置）
        gridOrigin = transform.position - new Vector3(
            gridResolution.x * cellSize * 0.5f,
            gridResolution.y * cellSize * 0.5f,
            gridResolution.z * cellSize * 0.5f
        );
        
        // 创建流体网格
        fluidGrid = new FluidGrid(gridResolution, cellSize, gridOrigin);
    }
    
    private void InitializeComputeShader()
    {
        fluidComputeShader = Resources.Load<ComputeShader>("FluidCompute");
        if (fluidComputeShader == null)
        {
            Debug.LogError("无法加载流体计算着色器，将使用CPU计算");
        }
    }
    
    private void SimulateFluid(float dt)
    {
        // 1. 应用外力（如重力、用户交互等）
        ApplyForces(dt);
        
        // 2. 处理边界条件（使用SDF）
        ApplyBoundaryConditions();
        
        // 3. 计算压力
        SolvePressure(dt);
        
        // 4. 更新速度场
        UpdateVelocityField(dt);
        
        // 5. 平流（移动流体）
        AdvectVelocity(dt);
        AdvectDensity(dt);
    }
    
    private void ApplyForces(float dt)
    {
        // 应用重力和其他外力
        Vector3 gravity = new Vector3(0, -9.81f, 0);
        
        for (int x = 0; x < gridResolution.x; x++)
        for (int y = 0; y < gridResolution.y; y++)
        for (int z = 0; z < gridResolution.z; z++)
        {
            // 只对流体单元应用力
            if (fluidGrid.IsCellFluid(x, y, z))
            {
                fluidGrid.AddForce(x, y, z, gravity * dt);
            }
        }
        
        // 这里可以添加用户交互力
    }
    
    private void ApplyBoundaryConditions()
    {
        // 使用SDF处理边界条件
        for (int x = 0; x < gridResolution.x; x++)
        for (int y = 0; y < gridResolution.y; y++)
        for (int z = 0; z < gridResolution.z; z++)
        {
            Vector3 cellPos = fluidGrid.GetCellPosition(x, y, z);
            
            // 检查每个SDF边界
            foreach (var sdf in boundarySDFs)
            {
                float distance = sdf.GetDistance(cellPos);
                
                // 如果单元格在SDF内部或接近表面
                if (distance < cellSize)
                {
                    // 获取SDF表面法线
                    Vector3 normal = sdf.GetNormal(cellPos);
                    
                    // 调整速度（反射或摩擦）
                    Vector3 velocity = fluidGrid.GetVelocity(x, y, z);
                    
                    // 计算反射速度
                    float vDotN = Vector3.Dot(velocity, normal);
                    if (vDotN < 0)
                    {
                        // 反射速度分量
                        Vector3 reflectedVel = velocity - 2 * vDotN * normal;
                        
                        // 应用摩擦
                        reflectedVel *= (1.0f - boundaryFriction);
                        
                        // 设置调整后的速度
                        fluidGrid.SetVelocity(x, y, z, reflectedVel);
                    }
                    
                    // 标记为边界单元
                    fluidGrid.SetCellType(x, y, z, FluidGrid.CellType.Boundary);
                }
            }
        }
    }
    
    private void SolvePressure(float dt)
    {
        // 使用雅可比迭代法求解压力泊松方程
        float scale = dt / (density * cellSize * cellSize);
        
        // 初始化压力为0
        for (int x = 0; x < gridResolution.x; x++)
        for (int y = 0; y < gridResolution.y; y++)
        for (int z = 0; z < gridResolution.z; z++)
        {
            fluidGrid.SetPressure(x, y, z, 0);
        }
        
        // 迭代求解
        for (int iter = 0; iter < solverIterations; iter++)
        {
            for (int x = 1; x < gridResolution.x - 1; x++)
            for (int y = 1; y < gridResolution.y - 1; y++)
            for (int z = 1; z < gridResolution.z - 1; z++)
            {
                if (fluidGrid.IsCellFluid(x, y, z))
                {
                    // 计算散度
                    float divergence = fluidGrid.CalculateDivergence(x, y, z);
                    
                    // 计算相邻单元的压力和
                    float pressureSum = 0;
                    int validNeighbors = 0;
                    
                    if (fluidGrid.IsCellFluid(x+1, y, z)) { pressureSum += fluidGrid.GetPressure(x+1, y, z); validNeighbors++; }
                    if (fluidGrid.IsCellFluid(x-1, y, z)) { pressureSum += fluidGrid.GetPressure(x-1, y, z); validNeighbors++; }
                    if (fluidGrid.IsCellFluid(x, y+1, z)) { pressureSum += fluidGrid.GetPressure(x, y+1, z); validNeighbors++; }
                    if (fluidGrid.IsCellFluid(x, y-1, z)) { pressureSum += fluidGrid.GetPressure(x, y-1, z); validNeighbors++; }
                    if (fluidGrid.IsCellFluid(x, y, z+1)) { pressureSum += fluidGrid.GetPressure(x, y, z+1); validNeighbors++; }
                    if (fluidGrid.IsCellFluid(x, y, z-1)) { pressureSum += fluidGrid.GetPressure(x, y, z-1); validNeighbors++; }
                    
                    // 更新压力
                    if (validNeighbors > 0)
                    {
                        float newPressure = (pressureSum - divergence * scale) / validNeighbors;
                        fluidGrid.SetPressure(x, y, z, newPressure);
                    }
                }
            }
        }
    }
    
    private void UpdateVelocityField(float dt)
    {
        // 使用压力梯度更新速度场
        float scale = dt / (density * cellSize);
        
        for (int x = 1; x < gridResolution.x - 1; x++)
        for (int y = 1; y < gridResolution.y - 1; y++)
        for (int z = 1; z < gridResolution.z - 1; z++)
        {
            if (fluidGrid.IsCellFluid(x, y, z))
            {
                Vector3 velocity = fluidGrid.GetVelocity(x, y, z);
                
                // 计算压力梯度
                float pRight = fluidGrid.GetPressure(x+1, y, z);
                float pLeft = fluidGrid.GetPressure(x-1, y, z);
                float pUp = fluidGrid.GetPressure(x, y+1, z);
                float pDown = fluidGrid.GetPressure(x, y-1, z);
                float pForward = fluidGrid.GetPressure(x, y, z+1);
                float pBack = fluidGrid.GetPressure(x, y, z-1);
                
                Vector3 pressureGradient = new Vector3(
                    pRight - pLeft,
                    pUp - pDown,
                    pForward - pBack
                ) * 0.5f;
                
                // 更新速度
                velocity -= pressureGradient * scale;
                fluidGrid.SetVelocity(x, y, z, velocity);
            }
        }
    }
    
    private void AdvectVelocity(float dt)
    {
        // 使用半拉格朗日方法平流速度场
        FluidGrid tempGrid = fluidGrid.Clone();
        
        for (int x = 1; x < gridResolution.x - 1; x++)
        for (int y = 1; y < gridResolution.y - 1; y++)
        for (int z = 1; z < gridResolution.z - 1; z++)
        {
            if (fluidGrid.IsCellFluid(x, y, z))
            {
                Vector3 pos = fluidGrid.GetCellPosition(x, y, z);
                Vector3 vel = fluidGrid.GetVelocity(x, y, z);
                
                // 回溯粒子位置
                Vector3 backPos = pos - vel * dt;
                
                // 从回溯位置采样速度（线性插值）
                Vector3 sampledVel = fluidGrid.SampleVelocity(backPos);
                
                // 更新速度
                tempGrid.SetVelocity(x, y, z, sampledVel);
            }
        }
        
        // 更新主网格
        fluidGrid.CopyVelocityFrom(tempGrid);
    }
    
    private void AdvectDensity(float dt)
    {
        // 使用半拉格朗日方法平流密度场
        FluidGrid tempGrid = fluidGrid.Clone();
        
        for (int x = 1; x < gridResolution.x - 1; x++)
        for (int y = 1; y < gridResolution.y - 1; y++)
        for (int z = 1; z < gridResolution.z - 1; z++)
        {
            if (fluidGrid.IsCellFluid(x, y, z))
            {
                Vector3 pos = fluidGrid.GetCellPosition(x, y, z);
                Vector3 vel = fluidGrid.GetVelocity(x, y, z);
                
                // 回溯粒子位置
                Vector3 backPos = pos - vel * dt;
                
                // 从回溯位置采样密度（线性插值）
                float sampledDensity = fluidGrid.SampleDensity(backPos);
                
                // 更新密度
                tempGrid.SetDensity(x, y, z, sampledDensity);
            }
        }
        
        // 更新主网格
        fluidGrid.CopyDensityFrom(tempGrid);
    }
    
    // 可视化方法
    private void CreateVisualizers()
    {
        // 创建速度场可视化
        if (visualizeVelocity)
        {
            velocityVisualizer = new GameObject("Velocity Visualizer");
            velocityVisualizer.transform.SetParent(transform);
            velocityVisualizer.transform.localPosition = Vector3.zero;
        }
        
        // 创建密度场可视化
        if (visualizeDensity)
        {
            densityVisualizer = new GameObject("Density Visualizer");
            densityVisualizer.transform.SetParent(transform);
            densityVisualizer.transform.localPosition = Vector3.zero;
        }
    }
    
    private void UpdateVisualizers()
    {
        // 更新速度场可视化
        if (visualizeVelocity && velocityVisualizer != null)
        {
            UpdateVelocityVisualizer();
        }
        
        // 更新密度场可视化
        if (visualizeDensity && densityVisualizer != null)
        {
            UpdateDensityVisualizer();
        }
    }
    private void UpdateDensityVisualizer()
    {
        // 清除旧的可视化
        foreach (Transform child in densityVisualizer.transform)
        {
            Destroy(child.gameObject);
        }
        
        int step = Mathf.Max(1, gridResolution.x / 32);
        
        for (int x = step; x < gridResolution.x; x += step)
        for (int y = step; y < gridResolution.y; y += step)
        for (int z = step; z < gridResolution.z; z += step)
        {
            float density = fluidGrid.GetDensity(x, y, z);
            if (density > 0.1f)
            {
                GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                point.transform.SetParent(densityVisualizer.transform);
                point.transform.position = fluidGrid.GetCellPosition(x, y, z);
                
                // 根据密度设置大小和颜色
                float size = Mathf.Lerp(0.05f, 0.2f, density);
                point.transform.localScale = Vector3.one * size;
                
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(1, 1, 1, Mathf.Clamp01(density));
                point.GetComponent<Renderer>().material = mat;
            }
        }
    }
        private void UpdateVelocityVisualizer()
    {
        // 清除旧的可视化
        foreach (Transform child in velocityVisualizer.transform)
        {
            Destroy(child.gameObject);
        }
        
        // 创建速度场箭头
        int step = Mathf.Max(1, gridResolution.x / 16); // 减少箭头数量以提高性能
        
        for (int x = step; x < gridResolution.x; x += step)
        for (int y = step; y < gridResolution.y; y += step)
        for (int z = step; z < gridResolution.z; z += step)
        {
            if (fluidGrid.IsCellFluid(x, y, z))
            {
                Vector3 pos = fluidGrid.GetCellPosition(x, y, z);
                Vector3 vel = fluidGrid.GetVelocity(x, y, z);
                
                if (vel.magnitude > 0.01f)
                {
                    // 创建箭头
                    GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    arrow.transform.SetParent(velocityVisualizer.transform);
                    
                    // 设置箭头位置和方向
                    arrow.transform.position = pos;
                    arrow.transform.up = vel.normalized;
                    
                    // 设置箭头大小
                    float length = vel.magnitude * visualScale;
                    arrow.transform.localScale = new Vector3(0.1f, length, 0.1f);
                    
                    // 设置箭头颜色
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = Color.Lerp(Color.blue, Color.red, vel.magnitude / 10f);
                    arrow.GetComponent<Renderer>().material = mat;
                    
                    // 添加箭头头部
                    GameObject arrowHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    arrowHead.transform.SetParent(arrow.transform);
                    arrowHead.transform.localPosition = new Vector3(0, 0.5f, 0);
                    arrowHead.transform.localRotation = Quaternion.identity;
                    arrowHead.transform.localScale = new Vector3(2f, 1f, 2f);
                    arrowHead.GetComponent<Renderer>().material = mat;
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        // 清理可视化对象
        if (velocityVisualizer != null)
            Destroy(velocityVisualizer);
            
        if (densityVisualizer != null)
            Destroy(densityVisualizer);
    }
}