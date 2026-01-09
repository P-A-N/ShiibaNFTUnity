using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Exports PNG images of every point cloud frame with an optional floor grid overlay.
/// Integrates with Timeline for automatic frame-by-frame capture during playback.
/// </summary>
public class PointCloudFrameExporter : MonoBehaviour
{
    [Header("Export Settings")]
    [SerializeField] private string exportFolderName = "PointCloudExport";
    [SerializeField] private bool useAbsolutePath = false;
    [SerializeField] private string absoluteExportPath = "";
    [SerializeField] private int imageWidth = 1920;
    [SerializeField] private int imageHeight = 1080;
    [SerializeField] private bool exportWithAlpha = false;
    [SerializeField] private bool flipHorizontally = false;

    [Header("Camera Settings")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool findCameraAutomatically = true;

    [Header("Camera Rotation (Orbit around target)")]
    [SerializeField] private bool enableCameraRotation = false;
    [SerializeField] private string lookAtJointName = "torso_7";
    [SerializeField] private float startAngle = 0f;
    [SerializeField] private float endAngle = 360f;
    [Tooltip("Number of full rotations during export (1.0 = one rotation, 2.0 = two rotations)")]
    [SerializeField] [Range(0.5f, 5f)] private float rotationCycles = 1f;
    [Tooltip("Orbit radius and height are calculated from camera's initial position at export start")]
    [SerializeField] private bool useInitialCameraDistance = true;

    [Header("Camera Smoothing")]
    [SerializeField] private bool enableCameraSmoothing = true;
    [SerializeField] [Range(0.01f, 1f)] private float smoothingFactor = 0.3f; // Lower = smoother (more lag), Higher = faster response

    [Header("Floor Grid")]
    [SerializeField] private bool showFloorGrid = true;
    [SerializeField] private FloorGridRenderer floorGrid;
    [SerializeField] private bool createGridAutomatically = true;

    [Header("Export Control")]
    [SerializeField] private bool exportOnPlaybackOnly = true;
    [SerializeField] private bool exportEveryFrame = false;
    [SerializeField] private int frameSkip = 0; // Export every Nth frame (0 = export all)
    [SerializeField] private float frameWaitTime = 0.3f; // Wait time after seeking before capture
    [SerializeField] private int additionalRenderFrames = 3; // Additional frames to wait for rendering

    [Header("Timeline Integration")]
    [SerializeField] private PlayableDirector timeline;
    [SerializeField] private bool findTimelineAutomatically = true;

    [Header("Status")]
    [SerializeField] private bool isExporting = false;
    [SerializeField] private int exportedFrameCount = 0;
    [SerializeField] private string lastExportPath = "";

    private RenderTexture renderTexture;
    private Texture2D screenshotTexture;
    private string currentExportDirectory;
    private int frameCounter = 0;
    private bool wasPlayingLastFrame = false;

    // Camera rotation state
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private int totalFramesForRotation = 0;
    private float calculatedOrbitRadius = 0f;
    private float calculatedOrbitHeight = 0f;

    // Camera smoothing state (low-pass filter)
    private Vector3 smoothedLookAtPosition;
    private bool isFirstFrame = true;

    void Start()
    {
        SetupCamera();
        SetupFloorGrid();
        SetupTimeline();
        SetupRenderTextures();
    }

    private void SetupCamera()
    {
        if (targetCamera == null && findCameraAutomatically)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
        }

        if (targetCamera == null)
        {
            Debug.LogError("[PointCloudFrameExporter] No camera found! Please assign a camera.");
        }
    }

    private void SetupFloorGrid()
    {
        if (showFloorGrid)
        {
            if (floorGrid == null)
            {
                floorGrid = FindFirstObjectByType<FloorGridRenderer>();
            }

            if (floorGrid == null && createGridAutomatically)
            {
                GameObject gridObj = new GameObject("FloorGrid");
                gridObj.transform.parent = transform;
                gridObj.transform.localPosition = Vector3.zero;
                floorGrid = gridObj.AddComponent<FloorGridRenderer>();
                Debug.Log("[PointCloudFrameExporter] Created FloorGridRenderer automatically");
            }

            if (floorGrid != null)
            {
                floorGrid.SetGridVisible(true);
            }
        }
    }

    private void SetupTimeline()
    {
        if (timeline == null && findTimelineAutomatically)
        {
            timeline = FindFirstObjectByType<PlayableDirector>();
        }
    }

    private void SetupRenderTextures()
    {
        // Clean up existing textures if they exist
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
        if (screenshotTexture != null)
        {
            Destroy(screenshotTexture);
        }

        TextureFormat format = exportWithAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24;
        screenshotTexture = new Texture2D(imageWidth, imageHeight, format, false);
        renderTexture = new RenderTexture(imageWidth, imageHeight, 24);
        renderTexture.antiAliasing = 1;

        Debug.Log($"[PointCloudFrameExporter] Render textures created: {imageWidth}x{imageHeight}");
    }

    void Update()
    {
        if (!isExporting) return;

        if (exportOnPlaybackOnly && timeline != null)
        {
            bool isPlayingNow = (timeline.state == PlayState.Playing);

            // Export only during playback
            if (isPlayingNow && ShouldExportThisFrame())
            {
                StartCoroutine(CaptureFrameCoroutine());
            }

            wasPlayingLastFrame = isPlayingNow;
        }
        else if (exportEveryFrame && ShouldExportThisFrame())
        {
            StartCoroutine(CaptureFrameCoroutine());
        }
    }

    private bool ShouldExportThisFrame()
    {
        if (frameSkip <= 0)
        {
            return true;
        }
        return (frameCounter % (frameSkip + 1)) == 0;
    }

    /// <summary>
    /// Start exporting frames
    /// </summary>
    [ContextMenu("Start Export")]
    public void StartExport()
    {
        if (isExporting)
        {
            Debug.LogWarning("[PointCloudFrameExporter] Export already in progress");
            return;
        }

        if (targetCamera == null)
        {
            Debug.LogError("[PointCloudFrameExporter] Cannot start export: No camera assigned");
            return;
        }

        // Recreate render textures with current resolution settings
        SetupRenderTextures();

        // Create export directory
        currentExportDirectory = GetExportDirectory();
        if (!Directory.Exists(currentExportDirectory))
        {
            Directory.CreateDirectory(currentExportDirectory);
        }

        isExporting = true;
        exportedFrameCount = 0;
        frameCounter = 0;
        lastExportPath = currentExportDirectory;

        // Enable floor grid if configured
        if (showFloorGrid && floorGrid != null)
        {
            floorGrid.SetGridVisible(true);
        }

        Debug.Log($"[PointCloudFrameExporter] Started export to: {currentExportDirectory}");
    }

    /// <summary>
    /// Stop exporting frames
    /// </summary>
    [ContextMenu("Stop Export")]
    public void StopExport()
    {
        if (!isExporting)
        {
            Debug.LogWarning("[PointCloudFrameExporter] No export in progress");
            return;
        }

        isExporting = false;
        Debug.Log($"[PointCloudFrameExporter] Stopped export. Total frames exported: {exportedFrameCount}");
    }

    /// <summary>
    /// Export a single frame immediately
    /// </summary>
    [ContextMenu("Export Current Frame")]
    public void ExportCurrentFrame()
    {
        if (targetCamera == null)
        {
            Debug.LogError("[PointCloudFrameExporter] Cannot export: No camera assigned");
            return;
        }

        // Recreate render textures with current resolution settings
        SetupRenderTextures();

        // Create export directory if needed
        if (string.IsNullOrEmpty(currentExportDirectory) || !Directory.Exists(currentExportDirectory))
        {
            currentExportDirectory = GetExportDirectory();
            if (!Directory.Exists(currentExportDirectory))
            {
                Directory.CreateDirectory(currentExportDirectory);
            }
        }

        StartCoroutine(CaptureFrameCoroutine(true));
    }

    private IEnumerator CaptureFrameCoroutine(bool isSingleFrame = false)
    {
        // Wait for end of frame to ensure all rendering is complete
        yield return new WaitForEndOfFrame();

        // Set up render texture
        RenderTexture previousRT = targetCamera.targetTexture;
        targetCamera.targetTexture = renderTexture;

        // Queue grid draw for this camera (will be rendered during Camera.Render())
        if (showFloorGrid && floorGrid != null)
        {
            floorGrid.DrawGridToActiveRenderTexture(targetCamera);
        }

        // Render camera to texture
        targetCamera.Render();

        // Read pixels from render texture
        RenderTexture.active = renderTexture;
        screenshotTexture.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        screenshotTexture.Apply();

        // Restore previous render texture
        targetCamera.targetTexture = previousRT;
        RenderTexture.active = null;

        // Flip horizontally if enabled
        if (flipHorizontally)
        {
            FlipTextureHorizontally(screenshotTexture);
        }

        // Generate filename with frame number
        string filename = GenerateFilename();
        string filepath = Path.Combine(currentExportDirectory, filename);

        // Encode and save
        byte[] bytes = screenshotTexture.EncodeToPNG();
        File.WriteAllBytes(filepath, bytes);

        exportedFrameCount++;
        frameCounter++;

        if (isSingleFrame)
        {
            Debug.Log($"[PointCloudFrameExporter] Frame exported: {filepath}");
        }
    }

    private string GenerateFilename()
    {
        // Get frame number from timeline if available
        int frameNumber = frameCounter;
        if (timeline != null)
        {
            // Estimate frame number from timeline time (assuming 30 FPS)
            float timelineFPS = 30f;

            // Try to get actual FPS from MultiCameraPointCloudManager
            var pointCloudManager = FindFirstObjectByType<MultiCameraPointCloudManager>();
            if (pointCloudManager != null)
            {
                int fps = pointCloudManager.GetFpsFromHeader();
                if (fps > 0)
                {
                    timelineFPS = fps;
                }
            }

            frameNumber = Mathf.FloorToInt((float)timeline.time * timelineFPS);
        }

        return $"frame_{frameNumber:D6}.png";
    }

    private string GetExportDirectory()
    {
        if (useAbsolutePath && !string.IsNullOrEmpty(absoluteExportPath))
        {
            return absoluteExportPath;
        }

        // Use project-relative path
        string projectPath = Application.dataPath;
        string exportPath = Path.Combine(projectPath, "..", "Exports", exportFolderName);

        // Add timestamp to create unique export directory
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        exportPath = Path.Combine(exportPath, timestamp);

        return Path.GetFullPath(exportPath);
    }

    /// <summary>
    /// Flip texture horizontally (mirror effect)
    /// </summary>
    private void FlipTextureHorizontally(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;

        Color32[] pixels = texture.GetPixels32();
        Color32[] flippedPixels = new Color32[pixels.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sourceIndex = y * width + x;
                int flippedX = width - 1 - x;
                int targetIndex = y * width + flippedX;
                flippedPixels[targetIndex] = pixels[sourceIndex];
            }
        }

        texture.SetPixels32(flippedPixels);
        texture.Apply();
    }

    /// <summary>
    /// Update camera position to orbit around the target joint
    /// </summary>
    /// <param name="currentFrame">Current frame index</param>
    /// <param name="totalFrames">Total number of frames for angle interpolation</param>
    private void UpdateCameraOrbit(int currentFrame, int totalFrames)
    {
        if (!enableCameraRotation || targetCamera == null)
            return;

        // Find the look-at target (torso_7 or specified joint)
        Vector3 rawLookAtPosition = GetLookAtTargetPosition();
        if (rawLookAtPosition == Vector3.zero)
        {
            Debug.LogWarning($"[PointCloudFrameExporter] Could not find joint '{lookAtJointName}' for camera orbit");
            return;
        }

        // Apply smoothing (low-pass filter) to the look-at position
        Vector3 lookAtPosition;
        if (enableCameraSmoothing)
        {
            if (isFirstFrame)
            {
                // Initialize smoothed position on first frame
                smoothedLookAtPosition = rawLookAtPosition;
                isFirstFrame = false;
            }
            else
            {
                // Low-pass filter: smoothed = smoothed + factor * (raw - smoothed)
                // This is equivalent to: smoothed = lerp(smoothed, raw, factor)
                smoothedLookAtPosition = Vector3.Lerp(smoothedLookAtPosition, rawLookAtPosition, smoothingFactor);
            }
            lookAtPosition = smoothedLookAtPosition;
        }
        else
        {
            lookAtPosition = rawLookAtPosition;
        }

        // Calculate angle for this frame
        float angle;
        if (totalFrames > 1)
        {
            // Interpolate angle based on frame progress, multiplied by rotation cycles
            float progress = (float)currentFrame / (totalFrames - 1);
            float angleRange = (endAngle - startAngle) * rotationCycles;
            angle = startAngle + angleRange * progress;
        }
        else
        {
            // Use start angle (for single frame)
            angle = startAngle;
        }

        // Use calculated orbit radius and height from initial camera position
        float radius = calculatedOrbitRadius;
        float height = calculatedOrbitHeight;

        // Calculate camera position on orbit circle around the target
        // Negate angle to rotate in opposite direction (clockwise when viewed from above)
        float angleRad = -angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(
            Mathf.Sin(angleRad) * radius,
            height,
            Mathf.Cos(angleRad) * radius
        );

        Vector3 newCameraPosition = lookAtPosition + offset;
        targetCamera.transform.position = newCameraPosition;

        // Look at the smoothed target position
        targetCamera.transform.LookAt(lookAtPosition);
    }

    /// <summary>
    /// Get the world position of the look-at target joint
    /// </summary>
    private Vector3 GetLookAtTargetPosition()
    {
        // Use BvhJointUtility to find the joint
        Vector3 position = BvhJointUtility.GetJointWorldPosition(lookAtJointName);
        if (position != Vector3.zero)
        {
            return position;
        }

        // Fallback: Try to find BVH_Character directly
        GameObject bvhCharacter = GameObject.Find("BVH_Character");
        if (bvhCharacter == null)
        {
            return Vector3.zero;
        }

        // Search for the joint in the hierarchy
        Transform joint = BvhJointUtility.FindTransformRecursive(bvhCharacter.transform, lookAtJointName);
        if (joint != null)
        {
            return joint.position;
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Save original camera transform and calculate orbit parameters from initial position
    /// </summary>
    private void SaveOriginalCameraTransform()
    {
        if (targetCamera == null)
            return;

        originalCameraPosition = targetCamera.transform.position;
        originalCameraRotation = targetCamera.transform.rotation;

        // Reset smoothing state for new export
        isFirstFrame = true;
        smoothedLookAtPosition = Vector3.zero;

        // Calculate orbit radius and height from initial camera position relative to target
        if (useInitialCameraDistance)
        {
            Vector3 targetPos = GetLookAtTargetPosition();
            if (targetPos != Vector3.zero)
            {
                Vector3 offset = originalCameraPosition - targetPos;
                // Orbit radius is horizontal distance (XZ plane)
                calculatedOrbitRadius = new Vector2(offset.x, offset.z).magnitude;
                // Orbit height is vertical offset
                calculatedOrbitHeight = offset.y;

                Debug.Log($"[PointCloudFrameExporter] Calculated orbit from initial camera position: radius={calculatedOrbitRadius:F2}, height={calculatedOrbitHeight:F2}");
            }
        }
    }

    /// <summary>
    /// Restore original camera transform after orbit export
    /// </summary>
    private void RestoreOriginalCameraTransform()
    {
        if (targetCamera != null)
        {
            targetCamera.transform.position = originalCameraPosition;
            targetCamera.transform.rotation = originalCameraRotation;
        }
    }

    void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (screenshotTexture != null)
        {
            Destroy(screenshotTexture);
        }
    }

    // Public API
    public void SetExportResolution(int width, int height)
    {
        imageWidth = width;
        imageHeight = height;
        SetupRenderTextures();
    }

    public void SetFloorGridVisible(bool visible)
    {
        showFloorGrid = visible;
        if (floorGrid != null)
        {
            floorGrid.SetGridVisible(visible);
        }
    }

    public void SetFlipHorizontally(bool flip)
    {
        flipHorizontally = flip;
    }

    public bool IsExporting()
    {
        return isExporting;
    }

    public int GetExportedFrameCount()
    {
        return exportedFrameCount;
    }

    public string GetLastExportPath()
    {
        return lastExportPath;
    }

    // Camera rotation API
    public void SetCameraRotationEnabled(bool enabled)
    {
        enableCameraRotation = enabled;
    }

    public void SetOrbitAngles(float startAngleDeg, float endAngleDeg)
    {
        startAngle = startAngleDeg;
        endAngle = endAngleDeg;
    }

    public void SetLookAtJoint(string jointName)
    {
        lookAtJointName = jointName;
    }

    public void SetCameraSmoothing(bool enabled, float factor = 0.1f)
    {
        enableCameraSmoothing = enabled;
        smoothingFactor = Mathf.Clamp(factor, 0.01f, 1f);
    }

    /// <summary>
    /// Preview orbit at a specific angle (for testing in editor)
    /// Uses current camera distance as orbit radius/height
    /// </summary>
    [ContextMenu("Preview Orbit At Start Angle")]
    public void PreviewOrbitPosition()
    {
        if (targetCamera == null)
        {
            Debug.LogError("[PointCloudFrameExporter] No camera assigned for preview");
            return;
        }

        Vector3 targetPos = GetLookAtTargetPosition();
        if (targetPos == Vector3.zero)
        {
            Debug.LogError($"[PointCloudFrameExporter] Could not find joint '{lookAtJointName}' for preview");
            return;
        }

        // Calculate orbit parameters from current camera position
        Vector3 offset = targetCamera.transform.position - targetPos;
        float radius = new Vector2(offset.x, offset.z).magnitude;
        float height = offset.y;

        // Position camera at start angle using the calculated radius/height
        // Negate angle to match export rotation direction
        float angleRad = -startAngle * Mathf.Deg2Rad;
        Vector3 newOffset = new Vector3(
            Mathf.Sin(angleRad) * radius,
            height,
            Mathf.Cos(angleRad) * radius
        );

        targetCamera.transform.position = targetPos + newOffset;
        targetCamera.transform.LookAt(targetPos);

        Debug.Log($"[PointCloudFrameExporter] Camera positioned at orbit angle {startAngle}°, radius={radius:F2}, height={height:F2}, looking at {lookAtJointName} at {targetPos}");
    }

    /// <summary>
    /// Batch export all frames by stepping through timeline
    /// </summary>
    [ContextMenu("Batch Export All Frames")]
    public void BatchExportAllFrames()
    {
        if (timeline == null)
        {
            Debug.LogError("[PointCloudFrameExporter] Cannot batch export: No timeline assigned");
            return;
        }

        StartCoroutine(BatchExportCoroutine());
    }

    private IEnumerator BatchExportCoroutine()
    {
        Debug.Log("[PointCloudFrameExporter] Starting batch export...");

        // Recreate render textures with current resolution settings
        SetupRenderTextures();

        // Prepare export
        currentExportDirectory = GetExportDirectory();
        if (!Directory.Exists(currentExportDirectory))
        {
            Directory.CreateDirectory(currentExportDirectory);
        }

        exportedFrameCount = 0;
        frameCounter = 0;
        lastExportPath = currentExportDirectory;

        // Get frame count from MultiCameraPointCloudManager
        var pointCloudManager = FindFirstObjectByType<MultiCameraPointCloudManager>();
        if (pointCloudManager == null)
        {
            Debug.LogError("[PointCloudFrameExporter] MultiCameraPointCloudManager not found!");
            yield break;
        }

        int totalFrames = pointCloudManager.GetTotalFrameCount();
        if (totalFrames <= 0)
        {
            Debug.LogError("[PointCloudFrameExporter] Invalid frame count from manager");
            yield break;
        }

        Debug.Log($"[PointCloudFrameExporter] Exporting {totalFrames} frames...");

        // Save total frames for rotation calculation
        totalFramesForRotation = totalFrames;

        // Save original camera transform if rotation is enabled
        if (enableCameraRotation)
        {
            SaveOriginalCameraTransform();
            Debug.Log($"[PointCloudFrameExporter] Camera rotation enabled: orbit radius={calculatedOrbitRadius:F2}, height={calculatedOrbitHeight:F2}, angle {startAngle}° to {endAngle}°");
        }

        // Enable floor grid
        if (showFloorGrid && floorGrid != null)
        {
            floorGrid.SetGridVisible(true);
        }

        // Stop timeline if playing
        bool wasPlaying = timeline.state == PlayState.Playing;
        if (wasPlaying)
        {
            timeline.Pause();
        }

        // Step through each frame
        for (int i = 0; i < totalFrames; i++)
        {
            // Skip frames if configured
            if (frameSkip > 0 && (i % (frameSkip + 1)) != 0)
            {
                frameCounter++;
                continue;
            }

            // Seek to frame first (this updates both point cloud AND skeleton)
            pointCloudManager.SeekToFrame(i);

            // Wait for frame processing (point cloud loading and skeleton update)
            yield return new WaitForSeconds(frameWaitTime);

            // Update camera orbit position AFTER seeking
            // This ensures the skeleton is at the correct position for lookAt target
            if (enableCameraRotation)
            {
                UpdateCameraOrbit(i, totalFrames);
            }

            // Wait additional frames to ensure rendering is complete from new camera position
            for (int f = 0; f < additionalRenderFrames; f++)
            {
                yield return new WaitForEndOfFrame();
            }

            // Capture frame
            yield return StartCoroutine(CaptureFrameCoroutine());

            // Progress log every 10 frames
            if (i % 10 == 0)
            {
                Debug.Log($"[PointCloudFrameExporter] Progress: {i}/{totalFrames} frames");
            }
        }

        // Restore original camera transform if rotation was enabled
        if (enableCameraRotation)
        {
            RestoreOriginalCameraTransform();
            Debug.Log("[PointCloudFrameExporter] Camera transform restored");
        }

        Debug.Log($"[PointCloudFrameExporter] Batch export complete! Exported {exportedFrameCount} frames to: {currentExportDirectory}");

        // Restore playback state
        if (wasPlaying)
        {
            timeline.Play();
        }

        // Open export folder
#if UNITY_EDITOR
        UnityEditor.EditorUtility.RevealInFinder(currentExportDirectory);
#else
        Application.OpenURL("file://" + currentExportDirectory);
#endif
    }
}
