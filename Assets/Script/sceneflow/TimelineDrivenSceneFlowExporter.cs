using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.InputSystem;
using System.Collections;
using System.IO;

/// <summary>
/// Timeline-driven scene flow exporter with frame navigation
/// Uses Timeline to control playback and SceneFlowCalculator to compute motion vectors
/// Exports PLY files with embedded motion vectors
///
/// Keyboard Navigation (when not exporting):
/// - Right Arrow: Next frame(s) (configurable skip amount)
/// - Left Arrow: Previous frame(s) (configurable skip amount)
/// - Page Down: Jump forward 10 frames
/// - Page Up: Jump backward 10 frames
/// - Home: Jump to start frame
/// - End: Jump to end frame
/// </summary>
public class TimelineDrivenSceneFlowExporter : MonoBehaviour
{
    // Singleton instance for access from BvhPlayableBehaviour
    public static TimelineDrivenSceneFlowExporter Instance { get; private set; }

    [Header("References")]
    [SerializeField] private SceneFlowCalculator sceneFlowCalculator;

    [Header("Navigation Settings")]
    [SerializeField]
    [Tooltip("Enable arrow key navigation to move through frames")]
    private bool enableArrowKeyNavigation = true;

    [SerializeField]
    [Tooltip("Number of frames to skip with arrow keys")]
    private int frameSkipAmount = 1;

    // Runtime references (auto-found, not assignable in Inspector)
    private MultiPointCloudView multiPointCloudView;
    private DatasetConfig datasetConfig;
    private PlayableDirector timeline;
    private bool isExporting = false;
    private float plyFrameRate;
    private int currentNavigationFrame = 0;
    private int lastExportedPlyFrame = -1; // Track last exported frame to avoid duplicates

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        // Get references
        datasetConfig = DatasetConfig.GetInstance();
        timeline = FindFirstObjectByType<PlayableDirector>();

        // Auto-find components if not assigned
        if (sceneFlowCalculator == null)
        {
            sceneFlowCalculator = FindFirstObjectByType<SceneFlowCalculator>();
        }

        if (multiPointCloudView == null)
        {
            multiPointCloudView = FindFirstObjectByType<MultiPointCloudView>();
        }

        // Extract PLY frame rate
        ExtractPlyFrameRate();
    }

    private void Update()
    {
        // Handle arrow key navigation (only when not exporting)
        if (!enableArrowKeyNavigation || isExporting)
            return;

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // Right Arrow: Next frame(s)
        if (keyboard.rightArrowKey.wasPressedThisFrame)
        {
            StartCoroutine(NavigateToFrameCoroutine(currentNavigationFrame + frameSkipAmount));
        }
        // Left Arrow: Previous frame(s)
        else if (keyboard.leftArrowKey.wasPressedThisFrame)
        {
            StartCoroutine(NavigateToFrameCoroutine(currentNavigationFrame - frameSkipAmount));
        }
        // Page Down: Jump forward 10 frames
        else if (keyboard.pageDownKey.wasPressedThisFrame)
        {
            StartCoroutine(NavigateToFrameCoroutine(currentNavigationFrame + 10));
        }
        // Page Up: Jump backward 10 frames
        else if (keyboard.pageUpKey.wasPressedThisFrame)
        {
            StartCoroutine(NavigateToFrameCoroutine(currentNavigationFrame - 10));
        }
        // Home: Go to start frame
        else if (keyboard.homeKey.wasPressedThisFrame)
        {
            StartCoroutine(NavigateToFrameCoroutine(datasetConfig != null ? datasetConfig.SceneFlowStartFrame : 0));
        }
        // End: Go to end frame
        else if (keyboard.endKey.wasPressedThisFrame)
        {
            StartCoroutine(NavigateToFrameCoroutine(datasetConfig != null ? datasetConfig.SceneFlowEndFrame : 0));
        }
    }

    /// <summary>
    /// Navigate to a specific PLY frame and update scene flow visualization (coroutine version)
    /// IMPORTANT: Must use coroutine to wait for Timeline to update before calculating scene flow
    /// </summary>
    private IEnumerator NavigateToFrameCoroutine(int targetFrame)
    {
        if (datasetConfig == null || timeline == null)
            yield break;

        // Clamp frame to valid range
        int startFrame = datasetConfig.SceneFlowStartFrame;
        int endFrame = datasetConfig.SceneFlowEndFrame;
        targetFrame = Mathf.Clamp(targetFrame, startFrame, endFrame);

        // Calculate timeline time for this frame
        float frameTime = targetFrame / plyFrameRate;

        // Seek timeline
        TimelineUtil.SeekToTime(frameTime);

        // CRITICAL: Wait one frame for Timeline to actually update!
        // Without this, OnShowSceneFlow() will read the OLD timeline time
        yield return null;

        // Update current navigation frame
        currentNavigationFrame = targetFrame;

        // Calculate scene flow for this frame (now Timeline has updated)
        if (sceneFlowCalculator != null)
        {
            // Use CalculateSceneFlow() for navigation (no Gizmo visualization overhead)
            // User can press "Show Scene Flow" button if they want Gizmo visualization
            sceneFlowCalculator.CalculateSceneFlow();

            // Get frame info for logging
            int currentBvhFrame = sceneFlowCalculator.GetLastCurrentBvhFrame();
            int previousBvhFrame = sceneFlowCalculator.GetLastPreviousBvhFrame();

            Debug.Log($"[TimelineDrivenSceneFlowExporter] Navigated to PLY Frame={targetFrame}, " +
                     $"Timeline Time={frameTime:F3}s, Current BVH Frame={currentBvhFrame}, " +
                     $"Previous BVH Frame={previousBvhFrame}");
        }
    }

    private void ExtractPlyFrameRate()
    {
        if (timeline == null || timeline.playableAsset == null)
        {
            Debug.LogError("[TimelineDrivenSceneFlowExporter] Timeline not found!");
            return;
        }

        var timelineAsset = timeline.playableAsset as UnityEngine.Timeline.TimelineAsset;
        foreach (var track in timelineAsset.GetOutputTracks())
        {
            if (track.name.Contains("Point Cloud") || track.GetType().Name.Contains("PointCloud"))
            {
                var clips = track.GetClips();
                foreach (var clip in clips)
                {
                    if (clip.asset is PointCloudPlayableAsset pcAsset)
                    {
                        // Extract private frameRate field via reflection
                        var frameRateField = typeof(PointCloudPlayableAsset).GetField(
                            "frameRate",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                        );

                        if (frameRateField != null)
                        {
                            plyFrameRate = (float)frameRateField.GetValue(pcAsset);
                            Debug.Log($"[TimelineDrivenSceneFlowExporter] PLY frame rate: {plyFrameRate} fps");
                            return;
                        }
                    }
                }
            }
        }

        Debug.LogError("[TimelineDrivenSceneFlowExporter] Could not extract PLY frame rate from Timeline!");
        plyFrameRate = 30f; // Fallback
    }

    /// <summary>
    /// Start batch export process
    /// </summary>
    public void StartExport()
    {
        if (isExporting)
        {
            Debug.LogWarning("[TimelineDrivenSceneFlowExporter] Export already in progress!");
            return;
        }

        if (datasetConfig == null)
        {
            Debug.LogError("[TimelineDrivenSceneFlowExporter] DatasetConfig not found!");
            return;
        }

        if (sceneFlowCalculator == null)
        {
            Debug.LogError("[TimelineDrivenSceneFlowExporter] SceneFlowCalculator not found!");
            return;
        }

        // Retry finding MultiPointCloudView (might not exist yet during Start())
        if (multiPointCloudView == null)
        {
            multiPointCloudView = FindFirstObjectByType<MultiPointCloudView>();
        }

        if (multiPointCloudView == null)
        {
            Debug.LogError("[TimelineDrivenSceneFlowExporter] MultiPointCloudView not found! Make sure the scene is fully initialized (wait 1-2 seconds after entering Play mode).");
            return;
        }

        StartCoroutine(ExportCoroutine());
    }

    /// <summary>
    /// Check if we should export the current frame (called from BvhPlayableBehaviour.PrepareFrame)
    /// </summary>
    public bool ShouldExportFrame(int bvhFrame)
    {
        return isExporting;
    }

    /// <summary>
    /// Export the current frame (called from BvhPlayableBehaviour.PrepareFrame)
    /// </summary>
    public void ExportCurrentFrame(int bvhFrame, float timelineTime)
    {
        // Calculate corresponding PLY frame
        int plyFrame = Mathf.FloorToInt(timelineTime * plyFrameRate);

        // Check if within export range
        if (plyFrame < datasetConfig.SceneFlowStartFrame || plyFrame > datasetConfig.SceneFlowEndFrame)
        {
            Debug.Log($"[TimelineDrivenSceneFlowExporter] Skipping frame {plyFrame} (outside range {datasetConfig.SceneFlowStartFrame}-{datasetConfig.SceneFlowEndFrame})");
            return;
        }

        // Avoid exporting the same PLY frame multiple times
        if (plyFrame == lastExportedPlyFrame)
        {
            Debug.Log($"[TimelineDrivenSceneFlowExporter] Skipping duplicate frame {plyFrame}");
            return;
        }

        lastExportedPlyFrame = plyFrame;

        // Check if already exported (skip existing)
        string filename = $"frame_{plyFrame:D4}.ply";
        string exportPath = GetExportPath();
        string fullPath = Path.Combine(exportPath, filename);

        if (datasetConfig.SceneFlowSkipExistingFiles && File.Exists(fullPath))
        {
            Debug.Log($"[TimelineDrivenSceneFlowExporter] Skipping existing file: {filename}");
            return;
        }

        // Perform export immediately - no wait needed since skeleton pool is persistent
        // SceneFlowCalculator now uses persistent skeletons (created once) instead of destroying/recreating
        PerformExport(bvhFrame, plyFrame, fullPath);
    }

    /// <summary>
    /// Core export logic (called by both normal and debug paths)
    /// </summary>
    private void PerformExport(int bvhFrame, int plyFrame, string fullPath)
    {
        // Calculate scene flow
        sceneFlowCalculator.CalculateSceneFlow();

        // Get motion vectors
        Vector3[] motionVectors = sceneFlowCalculator.GetCurrentMotionVectors();
        if (motionVectors == null || motionVectors.Length == 0)
        {
            Debug.LogError($"[TimelineDrivenSceneFlowExporter] Failed to calculate motion vectors for PLY frame {plyFrame}");
            return;
        }

        // Get current mesh
        Mesh currentMesh = multiPointCloudView.GetCurrentMesh();
        if (currentMesh == null)
        {
            Debug.LogError($"[TimelineDrivenSceneFlowExporter] No mesh available for PLY frame {plyFrame}");
            return;
        }

        // Export PLY
        if (datasetConfig.SceneFlowExportAsAscii)
            PlyExporter.ExportToPLY_ASCII(currentMesh, motionVectors, fullPath, null);
        else
            PlyExporter.ExportToPLY(currentMesh, motionVectors, fullPath, null);

        Debug.Log($"[TimelineDrivenSceneFlowExporter] Exported frame {plyFrame} (BVH frame {bvhFrame})");
    }

    /// <summary>
    /// Stop batch export process and pause Timeline
    /// </summary>
    public void StopExport()
    {
        isExporting = false;
        StopAllCoroutines();

        // Pause Timeline when export is stopped
        if (timeline != null)
        {
            timeline.Pause();
            Debug.Log("[TimelineDrivenSceneFlowExporter] Timeline paused");
        }

        Debug.Log("[TimelineDrivenSceneFlowExporter] Export cancelled");
    }

    private IEnumerator ExportCoroutine()
    {
        isExporting = true;
        lastExportedPlyFrame = -1; // Reset frame tracking
        float exportStartTime = Time.realtimeSinceStartup;

        // Get export settings from DatasetConfig
        int startFrame = datasetConfig.SceneFlowStartFrame;
        int endFrame = datasetConfig.SceneFlowEndFrame;
        int frameOffset = datasetConfig.SceneFlowFrameOffset;

        Debug.Log($"[TimelineDrivenSceneFlowExporter] Starting playback-based export from frame {startFrame} to {endFrame}");
        Debug.Log($"[TimelineDrivenSceneFlowExporter] PLY frame rate: {plyFrameRate} fps");
        Debug.Log($"[TimelineDrivenSceneFlowExporter] Scene flow frame offset: {frameOffset}");

        // Create export directory
        string exportPath = GetExportPath();
        Directory.CreateDirectory(exportPath);
        Debug.Log($"[TimelineDrivenSceneFlowExporter] Export path: {exportPath}");

        // Pause Timeline - we'll manually step through frames
        timeline.Pause();

        // Manually step through each frame
        int actualFramesExported = 0;
        for (int frame = startFrame; frame <= endFrame; frame++)
        {
            if (!isExporting)
                break;

            float frameTime = frame / plyFrameRate;

            Debug.Log($"[TimelineDrivenSceneFlowExporter] Seeking to frame {frame} (time {frameTime:F3}s)");

            // Seek Timeline to this specific frame
            timeline.time = frameTime;
            timeline.Evaluate();

            // Wait one frame for Timeline to update
            yield return null;

            // Export will be triggered automatically by BvhPlayableBehaviour.PrepareFrame
            // Wait for export to complete (track via lastExportedPlyFrame)
            int expectedPlyFrame = frame;
            float waitStartTime = Time.realtimeSinceStartup;
            while (lastExportedPlyFrame < expectedPlyFrame && isExporting)
            {
                yield return null;

                // Timeout after 30 seconds
                if (Time.realtimeSinceStartup - waitStartTime > 30f)
                {
                    Debug.LogWarning($"[TimelineDrivenSceneFlowExporter] Timeout waiting for frame {frame} export");
                    break;
                }
            }

            if (lastExportedPlyFrame == expectedPlyFrame)
            {
                actualFramesExported++;
            }
        }

        Debug.Log($"[TimelineDrivenSceneFlowExporter] Timeline kept paused at final time {timeline.time:F3}s");

        float totalTime = Time.realtimeSinceStartup - exportStartTime;
        int totalFrames = endFrame - startFrame + 1;
        Debug.Log($"[TimelineDrivenSceneFlowExporter] Export complete! {actualFramesExported}/{totalFrames} frames exported in {totalTime:F1}s");
        Debug.Log($"[TimelineDrivenSceneFlowExporter] Output directory: {exportPath}");

        isExporting = false;
    }

    private string GetExportPath()
    {
        string datasetFolder = datasetConfig.GetPointCloudRootDirectory();
        string exportDir = datasetConfig.SceneFlowExportDirectory;

        // Use SceneFlowExportDirectory from config, or default to "PLY_WithMotion"
        if (string.IsNullOrEmpty(exportDir))
        {
            exportDir = "PLY_WithMotion";
        }

        return Path.Combine(datasetFolder, exportDir);
    }
}
