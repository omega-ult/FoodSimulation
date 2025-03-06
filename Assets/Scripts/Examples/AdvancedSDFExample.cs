using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class AdvancedSDFExample : MonoBehaviour
{
    [Header("SDF设置")]
    public MeshFilter[] foodComponents;
    public CombinedSDF.CombineOperation[] operations;
    public bool usePrecomputed = true;
    public bool useComputeShader = true;
    public Vector3Int gridResolution = new Vector3Int(64, 64, 64);
    
    // 添加内外判断设置
    [Tooltip("是否使用射线检测判断内外")]
    public bool useInsideOutsideDetection = true;
    [Tooltip("射线检测方向数量")]
    public int rayDirectionCount = 6;
    
    [Header("可视化设置")]
    public bool visualizeSlice = true;
    public bool visualizeVolume = true;
    public float sliceHeight = 0f;
    public Vector3 volumeSize = new Vector3(10f, 10f, 10f);
    public int resolution = 100;
    public float isoLevel = 0f;
    
    [Header("性能测试")]
    public bool runPerformanceTest = false;
    public Text performanceText;
    
    // 添加运行时按钮控制
    [Header("运行时控制")]
    [Tooltip("点击Inspector中的按钮重新生成SDF")]
    public bool regenerateSDF = false;
    [Tooltip("点击Inspector中的按钮更新切片可视化")]
    public bool updateSliceVisualization = false;
    [Tooltip("点击Inspector中的按钮重新生成体积可视化")]
    public bool regenerateVolumeVisualization = false;
    
    private CombinedSDF combinedSDF;
    private GameObject sliceVisualizer;
    private GameObject volumeVisualizer;
    
    void Start()
    {
        // 创建组合SDF
        combinedSDF = gameObject.AddComponent<CombinedSDF>();
        combinedSDF.usePrecomputed = usePrecomputed;
        combinedSDF.useComputeShader = useComputeShader;
        combinedSDF.gridResolution = gridResolution;
        
        // 添加SDF组件和确保有Collider
        for (int i = 0; i < foodComponents.Length; i++)
        {
            GameObject foodObj = foodComponents[i].gameObject;
            
            // 确保有MeshCollider组件用于内外判断
            if (useInsideOutsideDetection)
            {
                MeshCollider collider = foodObj.GetComponent<MeshCollider>();
                if (collider == null)
                {
                    collider = foodObj.AddComponent<MeshCollider>();
                    collider.convex = false; // 非凸网格以保证准确性
                    collider.isTrigger = false; // 确保可以用于射线检测
                }
            }
            
            MeshToSDF meshSDF = foodObj.GetComponent<MeshToSDF>();
            if (meshSDF == null)
            {
                meshSDF = foodObj.AddComponent<MeshToSDF>();
            }
            
            CombinedSDF.CombineOperation operation = CombinedSDF.CombineOperation.Union;
            if (i < operations.Length)
            {
                operation = operations[i];
            }
            
            combinedSDF.AddComponent(meshSDF, operation);
        }
        
        // 预计算SDF
        if (usePrecomputed)
        {
            StartCoroutine(PrecomputeAndVisualize());
        }
        else
        {
            // 直接可视化
            if (visualizeSlice)
                CreateSDFSliceVisualizer();
                
            if (visualizeVolume)
                CreateSDFVolumeVisualizer();
        }
        
        // 运行性能测试
        if (runPerformanceTest)
        {
            StartCoroutine(RunPerformanceTest());
        }
    }
    
    // 添加Update方法检查按钮状态
    void Update()
    {
        // 检查是否需要重新生成SDF
        if (regenerateSDF)
        {
            regenerateSDF = false;
            RegenerateSDF();
        }
        
        // 检查是否需要更新切片可视化
        if (updateSliceVisualization)
        {
            updateSliceVisualization = false;
            if (sliceVisualizer != null)
                UpdateSDFSliceVisualizer();
            else
                CreateSDFSliceVisualizer();
        }
        
        // 检查是否需要重新生成体积可视化
        if (regenerateVolumeVisualization)
        {
            regenerateVolumeVisualization = false;
            CreateSDFVolumeVisualizer();
        }
    }
    
    // 添加重新生成SDF的方法
    public void RegenerateSDF()
    {
        Debug.Log("重新生成SDF...");
        
        // 更新组合SDF的设置
        combinedSDF.usePrecomputed = usePrecomputed;
        combinedSDF.useComputeShader = useComputeShader;
        combinedSDF.gridResolution = gridResolution;
        
        // 重新预计算
        if (usePrecomputed)
        {
            StartCoroutine(PrecomputeAndVisualize());
        }
        else
        {
            // 直接更新可视化
            if (visualizeSlice && sliceVisualizer != null)
                UpdateSDFSliceVisualizer();
                
            if (visualizeVolume)
                CreateSDFVolumeVisualizer();
        }
    }
    
    // 为编辑器添加菜单项
    [ContextMenu("重新生成SDF")]
    public void RegenerateSDF_ContextMenu()
    {
        RegenerateSDF();
    }
    
    [ContextMenu("更新切片可视化")]
    public void UpdateSliceVisualizer_ContextMenu()
    {
        if (sliceVisualizer != null)
            UpdateSDFSliceVisualizer();
        else
            CreateSDFSliceVisualizer();
    }
    
    [ContextMenu("重新生成体积可视化")]
    public void RegenerateVolumeVisualizer_ContextMenu()
    {
        CreateSDFVolumeVisualizer();
    }
    
    IEnumerator PrecomputeAndVisualize()
    {
        // 显示预计算进度
        Debug.Log("开始预计算SDF...");
        
        // 预计算SDF
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        
        combinedSDF.PrecomputeSDF();
        
        stopwatch.Stop();
        Debug.Log($"预计算SDF完成，耗时: {stopwatch.ElapsedMilliseconds}ms");
        
        yield return null;
        
        // 可视化
        if (visualizeSlice)
            CreateSDFSliceVisualizer();
            
        if (visualizeVolume)
            CreateSDFVolumeVisualizer();
    }
    
    IEnumerator RunPerformanceTest()
    {
        // 原有代码保持不变
        yield return new WaitForSeconds(1f); // 等待初始化完成
        
        int testPoints = 1000000;
        Vector3[] testPositions = new Vector3[testPoints];
        
        // 生成随机测试点
        for (int i = 0; i < testPoints; i++)
        {
            testPositions[i] = new Vector3(
                Random.Range(-volumeSize.x/2, volumeSize.x/2),
                Random.Range(-volumeSize.y/2, volumeSize.y/2),
                Random.Range(-volumeSize.z/2, volumeSize.z/2)
            );
        }
        
        // 测试直接计算性能
        combinedSDF.usePrecomputed = false;
        
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        
        for (int i = 0; i < testPoints; i++)
        {
            combinedSDF.GetDistance(testPositions[i]);
        }
        
        stopwatch.Stop();
        long directTime = stopwatch.ElapsedMilliseconds;
        
        yield return new WaitForSeconds(0.1f);
        
        // 测试预计算性能
        combinedSDF.usePrecomputed = true;
        
        stopwatch.Reset();
        stopwatch.Start();
        
        for (int i = 0; i < testPoints; i++)
        {
            combinedSDF.GetDistance(testPositions[i]);
        }
        
        stopwatch.Stop();
        long precomputedTime = stopwatch.ElapsedMilliseconds;
        
        // 显示性能测试结果
        string result = $"性能测试结果 ({testPoints:N0} 个点):\n" +
                       $"直接计算: {directTime}ms\n" +
                       $"预计算: {precomputedTime}ms\n" +
                       $"加速比: {directTime / (float)precomputedTime:F2}x";
                       
        Debug.Log(result);
        
        if (performanceText != null)
        {
            performanceText.text = result;
        }
    }
    
    void CreateSDFSliceVisualizer()
    {
        // 原有代码保持不变
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
            
            // 使用射线检测判断点是否在物体内部
            if (useInsideOutsideDetection)
            {
                bool isInside = IsPointInside(worldPos);
                distance = isInside ? -Mathf.Abs(distance) : Mathf.Abs(distance);
            }
            
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
    
    // 添加判断点是否在物体内部的方法
    private bool IsPointInside(Vector3 point)
    {
        // 使用多方向射线检测
        Vector3[] directions;
        
        if (rayDirectionCount <= 6)
        {
            // 使用6个主轴方向
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
            // 使用更多随机方向以提高准确性
            directions = new Vector3[rayDirectionCount];
            for (int i = 0; i < rayDirectionCount; i++)
            {
                directions[i] = Random.onUnitSphere;
            }
        }
        
        int insideCount = 0;
        
        foreach (var direction in directions)
        {
            int intersectionCount = 0;
            RaycastHit[] hits = Physics.RaycastAll(point, direction, 1000f);
            
            // 对每个食物组件计算相交次数
            foreach (var foodComponent in foodComponents)
            {
                int componentHits = 0;
                foreach (var hit in hits)
                {
                    if (hit.collider.gameObject == foodComponent.gameObject)
                    {
                        componentHits++;
                    }
                }
                
                // 如果与某个组件的相交次数为奇数，说明点在该组件内部
                if (componentHits % 2 == 1)
                {
                    intersectionCount++;
                    break; // 只要在任一组件内部即可
                }
            }
            
            if (intersectionCount > 0)
            {
                insideCount++;
            }
        }
        
        // 如果大多数方向都显示在内部，则认为点在物体内部
        return insideCount > directions.Length / 2;
    }
    
    // 修改体积可视化方法，使其也考虑内外判断
    void CreateSDFVolumeVisualizer()
    {
        if (volumeVisualizer != null)
            Destroy(volumeVisualizer);
            
        volumeVisualizer = new GameObject("SDF Volume Visualizer");
        volumeVisualizer.transform.SetParent(transform);
        volumeVisualizer.transform.localPosition = Vector3.zero;
        
        // 生成等值面网格，考虑内外判断
        Mesh mesh;
        mesh = combinedSDF.GenerateIsoSurfaceMesh(isoLevel);
        
        
        // 添加网格渲染器
        MeshFilter meshFilter = volumeVisualizer.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        
        MeshRenderer meshRenderer = volumeVisualizer.AddComponent<MeshRenderer>();
        Material material = new Material(Shader.Find("Standard"));
        material.color = Color.white;
        meshRenderer.material = material;
    }
}