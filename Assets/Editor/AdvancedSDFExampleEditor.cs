using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AdvancedSDFExample))]
public class AdvancedSDFExampleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 绘制默认Inspector
        DrawDefaultInspector();
        
        // 获取目标组件
        AdvancedSDFExample sdfExample = (AdvancedSDFExample)target;
        
        // 只在运行时显示按钮
        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("运行时控制", EditorStyles.boldLabel);
            
            // 添加重新生成SDF按钮
            if (GUILayout.Button("重新生成SDF"))
            {
                sdfExample.RegenerateSDF();
            }
            
            // 添加更新切片可视化按钮
            if (GUILayout.Button("更新切片可视化"))
            {
                sdfExample.UpdateSliceVisualizer_ContextMenu();
            }
            
            // 添加重新生成体积可视化按钮
            if (GUILayout.Button("重新生成体积可视化"))
            {
                sdfExample.RegenerateVolumeVisualizer_ContextMenu();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("在运行时将显示控制按钮", MessageType.Info);
        }
    }
}