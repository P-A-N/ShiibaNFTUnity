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
    [Tooltip("Enable automatic scene flow calculation when navigating with arrow keys")]
    private bool autoCalculateOnFrameStep = true;

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
    private int resolvedStartFrame = -1; // Resolved start frame for export (set in ExportCoroutine)
    private int resolvedEndFrame = -1; // Resolved end frame for export (set in ExportCoroutine)

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        // Subscribe to TimelineController's frame step event
        TimelineController.OnFrameStepped += OnFrameStepped;
    }

    private void OnDisable()
    {
        // Unsubscribe from event
        TimelineController.OnFrameStepped -= OnFrameStepped;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Called when TimelineController steps to a new frame (arrow key navigation)
    /// </summary>
    private void OnFrameStepped()
    {
        if (!autoCalculateOnFrameStep || isExporting)
            return;

        // Calculate scene flow for the new frame
        if (sceneFlowCalculator != null)
        {
            sceneFlowCalculator.CalculateSceneFlow();
        }
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

        // Initialize currentNavigationFrame from timeline position
        SyncNavigationFrameFromTimeline();
    }


    /// <summary>
    /// Synchronize currentNavigationFrame with the actual timeline position
    /// </summary>
    private void SyncNavigationFrameFromTimeline()
    {
        if (timeline == null || plyFrameRate <= 0)
            return;

        // Use RoundToInt to avoid floating-point precision issues
        currentNavigationFrame = Mathf.RoundToInt((float)timeline.time * plyFrameRate);
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

        // Search ALL tracks for PointCloudPlayableAsset clips (don't filter by track name)
        foreach (var track in timelineAsset.GetOutputTracks())
        {
            foreach (var clip in track.GetClips())
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
                        Debug.Log($"[TimelineDrivenSceneFlowExporter] PLY frame rate: {plyFrameRate} fps (from track: {track.name})");
                        return;
                    }
                }
            }
        }

        Debug.LogWarning("[TimelineDrivenSceneFlowExporter] Could not find PointCloudPlayableAsset in Timeline. Using default 30 fps.");
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
        // Use RoundToInt instead of FloorToInt to avoid floating-point precision issues
        // (e.g., 493/30*30 = 492.999... which floors to 492)
        int plyFrame = Mathf.RoundToInt(timelineTime * plyFrameRate);

        // Check if within export range (use resolved values set in ExportCoroutine)
        if (plyFrame < resolvedStartFrame || plyFrame > resolvedEndFrame)
        {
            Debug.Log($"[TimelineDrivenSceneFlowExporter] Skipping frame {plyFrame} (outside range {resolvedStartFrame}-{resolvedEndFrame})");
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
        string filename = $"{datasetConfig.DatasetName}_sf_{plyFrame:D6}.ply";
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

        // Build header comments with frame metadata
        string[] headerComments = BuildHeaderComments(plyFrame, bvhFrame);

        // Export PLY
        if (datasetConfig.SceneFlowExportAsAscii)
            PlyExporter.ExportToPLY_ASCII(currentMesh, motionVectors, fullPath, headerComments);
        else
            PlyExporter.ExportToPLY(currentMesh, motionVectors, fullPath, headerComments);

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

        // Resolve endFrame = 0 to total frame count
        if (endFrame <= 0)
        {
            var manager = FindFirstObjectByType<MultiCameraPointCloudManager>();
            if (manager != null)
            {
                int totalFrameCount = manager.GetTotalFrameCount();
                Debug.Log($"[TimelineDrivenSceneFlowExporter] GetTotalFrameCount() returned: {totalFrameCount}");
                if (totalFrameCount > 0)
                {
                    endFrame = totalFrameCount - 1; // Frame indices are 0-based, but we start from 1
                    Debug.Log($"[TimelineDrivenSceneFlowExporter] Resolved endFrame=0 to total frame count: {endFrame}");
                }
                else
                {
                    Debug.LogError("[TimelineDrivenSceneFlowExporter] Cannot determine total frame count (got 0 or negative)!");
                    isExporting = false;
                    yield break;
                }
            }
            else
            {
                Debug.LogError("[TimelineDrivenSceneFlowExporter] MultiCameraPointCloudManager not found!");
                isExporting = false;
                yield break;
            }
        }

        // Store resolved values for ExportCurrentFrame to use
        resolvedStartFrame = startFrame;
        resolvedEndFrame = endFrame;

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

    /// <summary>
    /// Build header comments array for PLY export with frame metadata
    /// </summary>
    private string[] BuildHeaderComments(int plyFrame, int bvhFrame)
    {
        Vector3? torso7Pos = GetTorso7GlobalPosition();

        if (torso7Pos.HasValue)
        {
            return new string[]
            {
                $"PointCloudFrame: {plyFrame}",
                $"BvhFrame: {bvhFrame}",
                $"torso_7_global_position: {torso7Pos.Value.x} {torso7Pos.Value.y} {torso7Pos.Value.z}"
            };
        }
        else
        {
            return new string[]
            {
                $"PointCloudFrame: {plyFrame}",
                $"BvhFrame: {bvhFrame}"
            };
        }
    }

    /// <summary>
    /// Get torso_7 joint global position from SceneFlowCalculator's CurrentFrameBVH skeleton
    /// </summary>
    private Vector3? GetTorso7GlobalPosition()
    {
        if (sceneFlowCalculator == null)
            return null;

        // Find CurrentFrameBVH container under SceneFlowCalculator
        Transform currentFrameContainer = sceneFlowCalculator.transform.Find("CurrentFrameBVH");
        if (currentFrameContainer == null)
            return null;

        // Navigate to torso_7 joint: CurrentFrameBVH > TempBvhSkeleton_N > root > ... > torso_7
        Transform torso7 = FindJointRecursive(currentFrameContainer, "torso_7");
        if (torso7 == null)
            return null;

        return torso7.position;
    }

    /// <summary>
    /// Recursively search for a joint by name in transform hierarchy
    /// </summary>
    private Transform FindJointRecursive(Transform parent, string jointName)
    {
        if (parent.name == jointName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform found = FindJointRecursive(child, jointName);
            if (found != null)
                return found;
        }

        return null;
    }
}
