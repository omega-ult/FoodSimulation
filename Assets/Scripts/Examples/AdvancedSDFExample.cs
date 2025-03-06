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
        
        // 添加SDF组件
        for (int i = 0; i < foodComponents.Length; i++)
        {
            MeshToSDF meshSDF = foodComponents[i].gameObject.GetComponent<MeshToSDF>();
            if (meshSDF == null)
            {
                meshSDF = foodComponents[i].gameObject.AddComponent<MeshToSDF>();
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
    
    void CreateSDFVolumeVisualizer()
    {
        if (volumeVisualizer != null)
            Destroy(volumeVisualizer);
            
        volumeVisualizer = new GameObject("SDF Volume Visualizer");
        volumeVisualizer.transform.SetParent(transform);
        volumeVisualizer.transform.localPosition = Vector3.zero;
        
        // 生成等值面网格
        Mesh mesh = combinedSDF.GenerateIsoSurfaceMesh(isoLevel);
        
        // 添加网格渲染器
        MeshFilter meshFilter = volumeVisualizer.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        
        MeshRenderer meshRenderer = volumeVisualizer.AddComponent<MeshRenderer>();
        Material material = new Material(Shader.Find("Standard"));
        material.color = Color.white;
        meshRenderer.material = material;
    }
}