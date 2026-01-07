#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TimelineDrivenSceneFlowExporter))]
public class TimelineDrivenSceneFlowExporterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TimelineDrivenSceneFlowExporter exporter = (TimelineDrivenSceneFlowExporter)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Export Settings (from DatasetConfig)", EditorStyles.boldLabel);

        // Get DatasetConfig to display settings (check if ConfigManager exists in scene first)
        ConfigManager configManager = Object.FindFirstObjectByType<ConfigManager>();
        DatasetConfig config = null;

        if (configManager != null)
        {
            config = ConfigManager.GetDatasetConfig();
        }

        if (config != null)
        {
            EditorGUILayout.LabelField($"Start Frame: {config.SceneFlowStartFrame}");
            EditorGUILayout.LabelField($"End Frame: {config.SceneFlowEndFrame}");
            EditorGUILayout.LabelField($"Export Directory: {config.SceneFlowExportDirectory}");
            EditorGUILayout.LabelField($"Export Format: {(config.SceneFlowExportAsAscii ? "ASCII" : "Binary")}");
            EditorGUILayout.LabelField($"Skip Existing Files: {config.SceneFlowSkipExistingFiles}");
            EditorGUILayout.LabelField($"Frame Offset: {config.SceneFlowFrameOffset}");
        }
        else
        {
            EditorGUILayout.HelpBox("DatasetConfig not found in scene!", MessageType.Warning);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Export Control", EditorStyles.boldLabel);

        // Start Export button
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Start Export", GUILayout.Height(40)))
        {
            if (config == null)
            {
                EditorUtility.DisplayDialog("Error", "DatasetConfig not found! Please ensure ConfigManager is in the scene with DatasetConfig assigned.", "OK");
                return;
            }

            if (EditorUtility.DisplayDialog(
                "Confirm Export",
                $"Export PLY files with motion vectors from frame {config.SceneFlowStartFrame} to {config.SceneFlowEndFrame}?\n\n" +
                $"Export Directory: {config.SceneFlowExportDirectory}\n" +
                $"This will use Timeline evaluation and SceneFlowCalculator's working motion vector calculation.",
                "Start Export",
                "Cancel"))
            {
                exporter.StartExport();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        // Stop Export button
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Stop Export", GUILayout.Height(30)))
        {
            exporter.StopExport();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Timeline-Driven Scene Flow Exporter\n\n" +
            "How it works:\n" +
            "1. Uses Timeline.SeekToTime() to step through PLY frames\n" +
            "2. Calls SceneFlowCalculator.OnShowSceneFlow() (the working method!)\n" +
            "3. Extracts motion vectors and exports to PLY files\n\n" +
            "Requirements:\n" +
            "• Timeline with BVH and PointCloud tracks\n" +
            "• SceneFlowCalculator in scene\n" +
            "• DualBvhVisualizer in scene (for visual verification)\n" +
            "• DatasetConfig with correct settings",
            MessageType.Info
        );
    }
}
#endif
