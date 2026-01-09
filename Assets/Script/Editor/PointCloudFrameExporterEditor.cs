using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor UI for PointCloudFrameExporter
/// Provides intuitive controls for configuring and executing frame exports
/// </summary>
[CustomEditor(typeof(PointCloudFrameExporter))]
public class PointCloudFrameExporterEditor : Editor
{
    private SerializedProperty exportFolderName;
    private SerializedProperty useAbsolutePath;
    private SerializedProperty absoluteExportPath;
    private SerializedProperty imageWidth;
    private SerializedProperty imageHeight;
    private SerializedProperty exportWithAlpha;
    private SerializedProperty flipHorizontally;
    private SerializedProperty targetCamera;
    private SerializedProperty findCameraAutomatically;

    // Camera Rotation
    private SerializedProperty enableCameraRotation;
    private SerializedProperty lookAtJointName;
    private SerializedProperty startAngle;
    private SerializedProperty endAngle;
    private SerializedProperty useInitialCameraDistance;

    // Camera Smoothing
    private SerializedProperty enableCameraSmoothing;
    private SerializedProperty smoothingFactor;

    private SerializedProperty showFloorGrid;
    private SerializedProperty floorGrid;
    private SerializedProperty createGridAutomatically;
    private SerializedProperty exportOnPlaybackOnly;
    private SerializedProperty exportEveryFrame;
    private SerializedProperty frameSkip;
    private SerializedProperty frameWaitTime;
    private SerializedProperty additionalRenderFrames;
    private SerializedProperty timeline;
    private SerializedProperty findTimelineAutomatically;

    private void OnEnable()
    {
        exportFolderName = serializedObject.FindProperty("exportFolderName");
        useAbsolutePath = serializedObject.FindProperty("useAbsolutePath");
        absoluteExportPath = serializedObject.FindProperty("absoluteExportPath");
        imageWidth = serializedObject.FindProperty("imageWidth");
        imageHeight = serializedObject.FindProperty("imageHeight");
        exportWithAlpha = serializedObject.FindProperty("exportWithAlpha");
        flipHorizontally = serializedObject.FindProperty("flipHorizontally");
        targetCamera = serializedObject.FindProperty("targetCamera");
        findCameraAutomatically = serializedObject.FindProperty("findCameraAutomatically");

        // Camera Rotation
        enableCameraRotation = serializedObject.FindProperty("enableCameraRotation");
        lookAtJointName = serializedObject.FindProperty("lookAtJointName");
        startAngle = serializedObject.FindProperty("startAngle");
        endAngle = serializedObject.FindProperty("endAngle");
        useInitialCameraDistance = serializedObject.FindProperty("useInitialCameraDistance");

        // Camera Smoothing
        enableCameraSmoothing = serializedObject.FindProperty("enableCameraSmoothing");
        smoothingFactor = serializedObject.FindProperty("smoothingFactor");

        showFloorGrid = serializedObject.FindProperty("showFloorGrid");
        floorGrid = serializedObject.FindProperty("floorGrid");
        createGridAutomatically = serializedObject.FindProperty("createGridAutomatically");
        exportOnPlaybackOnly = serializedObject.FindProperty("exportOnPlaybackOnly");
        exportEveryFrame = serializedObject.FindProperty("exportEveryFrame");
        frameSkip = serializedObject.FindProperty("frameSkip");
        frameWaitTime = serializedObject.FindProperty("frameWaitTime");
        additionalRenderFrames = serializedObject.FindProperty("additionalRenderFrames");
        timeline = serializedObject.FindProperty("timeline");
        findTimelineAutomatically = serializedObject.FindProperty("findTimelineAutomatically");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        PointCloudFrameExporter exporter = (PointCloudFrameExporter)target;

        // Header
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Point Cloud Frame Exporter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Export PNG images of every point cloud frame with optional floor grid overlay.", MessageType.Info);
        EditorGUILayout.Space();

        // Export Settings
        EditorGUILayout.LabelField("Export Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(exportFolderName);
        EditorGUILayout.PropertyField(useAbsolutePath);
        if (useAbsolutePath.boolValue)
        {
            EditorGUILayout.PropertyField(absoluteExportPath);
            if (GUILayout.Button("Browse..."))
            {
                string path = EditorUtility.OpenFolderPanel("Select Export Folder", absoluteExportPath.stringValue, "");
                if (!string.IsNullOrEmpty(path))
                {
                    absoluteExportPath.stringValue = path;
                }
            }
        }
        EditorGUILayout.Space();

        // Image Settings
        EditorGUILayout.LabelField("Image Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(imageWidth, new GUIContent("Width"));
        EditorGUILayout.PropertyField(imageHeight, new GUIContent("Height"));
        EditorGUILayout.EndHorizontal();

        // Resolution presets
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("1920x1080")) { imageWidth.intValue = 1920; imageHeight.intValue = 1080; }
        if (GUILayout.Button("1280x720")) { imageWidth.intValue = 1280; imageHeight.intValue = 720; }
        if (GUILayout.Button("3840x2160")) { imageWidth.intValue = 3840; imageHeight.intValue = 2160; }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(exportWithAlpha);
        EditorGUILayout.PropertyField(flipHorizontally, new GUIContent("Flip Horizontally", "Mirror the exported image horizontally"));
        EditorGUILayout.Space();

        // Camera Settings
        EditorGUILayout.LabelField("Camera Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(findCameraAutomatically);
        if (!findCameraAutomatically.boolValue)
        {
            EditorGUILayout.PropertyField(targetCamera);
        }
        EditorGUILayout.Space();

        // Camera Rotation Settings
        EditorGUILayout.LabelField("Camera Rotation (Orbit around target)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enableCameraRotation, new GUIContent("Enable Camera Rotation"));
        if (enableCameraRotation.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(lookAtJointName, new GUIContent("Look At Joint", "Target joint to orbit around (e.g., torso_7)"));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(startAngle, new GUIContent("Start Angle (°)"));
            EditorGUILayout.PropertyField(endAngle, new GUIContent("End Angle (°)"));
            EditorGUILayout.EndHorizontal();

            // Angle presets
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("0° → 360°")) { startAngle.floatValue = 0f; endAngle.floatValue = 360f; }
            if (GUILayout.Button("0° → 180°")) { startAngle.floatValue = 0f; endAngle.floatValue = 180f; }
            if (GUILayout.Button("-90° → 90°")) { startAngle.floatValue = -90f; endAngle.floatValue = 90f; }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Orbit radius and height are calculated from the camera's initial position when export starts.", MessageType.Info);
            EditorGUI.indentLevel--;

            // Preview button (only in Play mode)
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Preview Orbit At Start Angle"))
            {
                exporter.PreviewOrbitPosition();
            }
            GUI.enabled = true;

            // Camera Smoothing (sub-section of Camera Rotation)
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Camera Smoothing (Low-Pass Filter)", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(enableCameraSmoothing, new GUIContent("Enable Smoothing", "Apply low-pass filter to smooth camera following"));
            if (enableCameraSmoothing.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(smoothingFactor, new GUIContent("Smoothing Factor", "Lower = smoother (more lag), Higher = faster response"));
                EditorGUILayout.HelpBox("0.05 = Very smooth (high lag)\n0.1 = Smooth (default)\n0.3 = Moderate\n0.5 = Responsive\n1.0 = No smoothing", MessageType.None);
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.Space();

        // Floor Grid Settings
        EditorGUILayout.LabelField("Floor Grid Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(showFloorGrid);
        if (showFloorGrid.boolValue)
        {
            EditorGUILayout.PropertyField(createGridAutomatically);
            if (!createGridAutomatically.boolValue)
            {
                EditorGUILayout.PropertyField(floorGrid);
            }
        }
        EditorGUILayout.Space();

        // Export Control Settings
        EditorGUILayout.LabelField("Export Control", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(exportOnPlaybackOnly, new GUIContent("Export On Playback Only", "Only export frames during timeline playback"));
        EditorGUILayout.PropertyField(exportEveryFrame, new GUIContent("Export Every Frame", "Export continuously every frame (use with caution)"));
        EditorGUILayout.PropertyField(frameSkip, new GUIContent("Frame Skip", "Export every Nth frame (0 = export all frames)"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Timing (Reduce flickering)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(frameWaitTime, new GUIContent("Frame Wait Time (s)", "Wait time after seeking before capture (increase if flickering)"));
        EditorGUILayout.PropertyField(additionalRenderFrames, new GUIContent("Additional Render Frames", "Extra frames to wait for rendering to complete"));
        EditorGUILayout.Space();

        // Timeline Settings
        EditorGUILayout.LabelField("Timeline Integration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(findTimelineAutomatically);
        if (!findTimelineAutomatically.boolValue)
        {
            EditorGUILayout.PropertyField(timeline);
        }
        EditorGUILayout.Space();

        // Status Display
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        bool isExporting = exporter.IsExporting();
        EditorGUILayout.LabelField("Export Status:", isExporting ? "EXPORTING" : "Idle");
        EditorGUILayout.LabelField("Frames Exported:", exporter.GetExportedFrameCount().ToString());

        string lastPath = exporter.GetLastExportPath();
        if (!string.IsNullOrEmpty(lastPath))
        {
            EditorGUILayout.LabelField("Last Export:", lastPath);
            if (GUILayout.Button("Open Export Folder"))
            {
                EditorUtility.RevealInFinder(lastPath);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        // Action Buttons
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use export functions.", MessageType.Warning);
        }

        GUI.enabled = Application.isPlaying;

        EditorGUILayout.BeginHorizontal();

        if (!isExporting)
        {
            if (GUILayout.Button("Start Export", GUILayout.Height(40)))
            {
                exporter.StartExport();
            }
        }
        else
        {
            if (GUILayout.Button("Stop Export", GUILayout.Height(40)))
            {
                exporter.StopExport();
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Export Current Frame", GUILayout.Height(30)))
        {
            exporter.ExportCurrentFrame();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (GUILayout.Button("Batch Export All Frames", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog(
                "Batch Export All Frames",
                "This will export all frames in the timeline. This may take several minutes depending on the frame count. Continue?",
                "Yes", "Cancel"))
            {
                exporter.BatchExportAllFrames();
            }
        }

        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "USAGE:\n" +
            "1. Start Export: Begin exporting frames during timeline playback\n" +
            "2. Export Current Frame: Export the currently visible frame\n" +
            "3. Batch Export All Frames: Automatically step through and export all frames\n\n" +
            "Exported images will be saved to: Exports/{FolderName}/{Timestamp}/",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }
}
