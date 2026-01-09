# Point Cloud Frame Export System

This system allows you to export PNG images of every point cloud frame with an optional floor grid overlay for spatial reference.

## Features

- **Frame-by-frame PNG export** of point cloud visualization
- **Floor grid overlay** for spatial reference and measurements
- **Multiple export modes**:
  - Manual export of current frame
  - Automatic export during timeline playback
  - Batch export of all frames
- **Configurable image resolution** with presets (720p, 1080p, 4K)
- **Timeline integration** for synchronized frame export
- **Custom editor UI** for easy configuration and control

## Components

### 1. FloorGridRenderer
Renders a customizable floor grid overlay on the point cloud.

**Key Settings:**
- `Grid Size`: Total size of the grid (default: 10 units)
- `Grid Spacing`: Distance between grid lines (default: 0.5 units)
- `Grid Color`: Color of regular grid lines
- `Center Line Color`: Color of center axis lines (X/Z axes)
- `Grid Height`: Y-position of the grid (default: 0)

### 2. PointCloudFrameExporter
Main component that handles frame capture and PNG export.

**Key Settings:**
- `Export Folder Name`: Name of the output folder (default: "PointCloudExport")
- `Image Width/Height`: Resolution of exported images (default: 1920x1080)
- `Export With Alpha`: Include alpha channel in PNG export
- `Flip Horizontally`: Mirror the exported image horizontally
- `Target Camera`: Camera to capture from (auto-detects Main Camera)
- `Show Floor Grid`: Enable/disable floor grid overlay
- `Export On Playback Only`: Only export during timeline playback
- `Frame Skip`: Export every Nth frame (0 = export all frames)

## Setup Instructions

### Quick Setup (Recommended)

1. **Add PointCloudFrameExporter to your scene:**
   - Select any GameObject (e.g., the "world" GameObject)
   - Add Component → Point Cloud Frame Exporter
   - The component will automatically find the camera and timeline

2. **Configure floor grid (optional):**
   - Enable "Show Floor Grid" in the inspector
   - Enable "Create Grid Automatically"
   - Adjust grid size and spacing as needed

3. **Set export resolution:**
   - Use preset buttons (1920x1080, 1280x720, 3840x2160)
   - Or manually set custom width/height

4. **Choose export mode:**
   - Enable "Export On Playback Only" for automatic export during playback
   - Or use manual/batch export buttons

### Manual Setup

1. **Create FloorGridRenderer (if you want a custom grid):**
   ```
   GameObject → Create Empty → Name: "FloorGrid"
   Add Component → Floor Grid Renderer
   Position: (0, 0, 0) or adjust to your scene
   ```

2. **Add PointCloudFrameExporter:**
   ```
   Select GameObject → Add Component → Point Cloud Frame Exporter
   ```

3. **Assign references:**
   - Drag Camera to "Target Camera" field (or use auto-find)
   - Drag FloorGridRenderer to "Floor Grid" field (or use auto-create)
   - Drag PlayableDirector to "Timeline" field (or use auto-find)

## Usage

### Method 1: Export Current Frame

1. Enter Play Mode
2. Navigate to the desired frame using timeline scrubbing or arrow keys
3. In the PointCloudFrameExporter inspector, click **"Export Current Frame"**
4. The frame will be saved to `Exports/{FolderName}/{Timestamp}/frame_XXXXXX.png`

### Method 2: Export During Playback

1. Enter Play Mode
2. In the PointCloudFrameExporter inspector, click **"Start Export"**
3. Play the timeline (Space bar)
4. Frames will be automatically exported during playback
5. Click **"Stop Export"** to end capture
6. Find exported images in `Exports/{FolderName}/{Timestamp}/`

### Method 3: Batch Export All Frames (Recommended)

1. Enter Play Mode
2. In the PointCloudFrameExporter inspector, click **"Batch Export All Frames"**
3. Confirm the dialog
4. The system will:
   - Automatically step through each frame
   - Export PNG image for each frame
   - Show progress in console every 10 frames
   - Open the export folder when complete
5. Exported images: `Exports/{FolderName}/{Timestamp}/frame_000000.png`, `frame_000001.png`, etc.

## Export Settings

### Image Resolution

Choose appropriate resolution based on your needs:

| Resolution | Preset | Use Case |
|------------|--------|----------|
| 1280x720 | 720p | Quick preview, testing |
| 1920x1080 | 1080p | Standard quality, presentations |
| 3840x2160 | 4K | High quality, publications |

### Frame Skip

Use "Frame Skip" to reduce export count:
- `0`: Export all frames
- `1`: Export every 2nd frame (half the frames)
- `4`: Export every 5th frame (1/5th the frames)

Example: Dataset with 1000 frames, Frame Skip = 4 → Export 200 frames

### Export Path Options

**Relative Path (Default):**
- Exports to: `{ProjectRoot}/Exports/{FolderName}/{Timestamp}/`
- Automatically creates timestamped folders
- Easy to manage and version control

**Absolute Path:**
- Enable "Use Absolute Path"
- Click "Browse..." to select custom folder
- Exports to: `{AbsolutePath}/`
- Useful for external drives or specific locations

## Floor Grid Configuration

### Default Grid Settings

The default grid is a 10x10 meter grid with 0.5m spacing:
- **Grid Size**: 10 units
- **Grid Spacing**: 0.5 units (20 lines)
- **Grid Color**: Gray (50% opacity)
- **Center Lines**: Red (X-axis) and Z-axis

### Customizing the Grid

Adjust grid settings in the FloorGridRenderer component:

1. **Change grid size:**
   - Increase `Grid Size` for larger area coverage
   - Example: 20 units for a 20x20 meter grid

2. **Change grid spacing:**
   - Decrease `Grid Spacing` for finer resolution (e.g., 0.1 units)
   - Increase `Grid Spacing` for coarser grid (e.g., 1.0 units)

3. **Adjust colors:**
   - Set `Grid Color` alpha channel for transparency
   - Change `Center Line Color` for axis visualization

4. **Adjust height:**
   - Set `Grid Height` to align with floor level
   - Example: -4.747 to match world GameObject offset

### Hiding/Showing the Grid

- **In Inspector**: Toggle "Show Grid" in FloorGridRenderer
- **At Runtime**: Use `SetFloorGridVisible(bool)` in PointCloudFrameExporter
- **For Export**: Enable/disable "Show Floor Grid" in PointCloudFrameExporter

## Image Transformation Options

### Flip Horizontally

The "Flip Horizontally" option mirrors the exported image along the vertical axis, creating a left-right flipped version of the point cloud visualization.

**Use Cases:**
- **Mirror correction**: Correct camera orientation issues
- **Coordinate system alignment**: Match different coordinate system conventions (e.g., left-hand vs right-hand coordinate systems)
- **Comparison views**: Create mirrored views for stereo or comparison purposes
- **Data alignment**: Align exported images with external datasets that use flipped coordinates

**How to Enable:**
1. In the PointCloudFrameExporter Inspector, check "Flip Horizontally"
2. All exported images will be horizontally flipped
3. Can be toggled at runtime using `SetFlipHorizontally(bool)`

**Technical Details:**
- The flip is applied after rendering but before PNG encoding
- Performance impact is minimal (~0.01-0.02 seconds per frame)
- The flip preserves all image quality and properties
- Floor grid overlay is also flipped if enabled

**Example:**
```csharp
PointCloudFrameExporter exporter = GetComponent<PointCloudFrameExporter>();
exporter.SetFlipHorizontally(true);  // Enable horizontal flip
exporter.ExportCurrentFrame();       // Export flipped image
```

## Integration with Existing Workflow

### Timeline Synchronization

The exporter automatically syncs with your Timeline:
- Frame numbers are calculated from timeline time
- Uses actual FPS from `MultiCameraPointCloudManager`
- Respects timeline playback state
- Arrow key navigation works during export

### Point Cloud Data Sources

Works with all processing modes:
- **PLY Mode**: Exports pre-loaded PLY frames
- **PLY_WITH_MOTION**: Exports with motion vectors
- **CPU/GPU/ONESHADER**: Exports real-time processed frames

### BVH Skeleton Integration

If you have BVH skeleton visualization:
- The skeleton will be included in exported images
- Adjust `BVH Position Offset` in DatasetConfig to align with grid
- Use BVH drift correction for accurate frame-by-frame alignment

## Output File Format

Exported files are named sequentially:
```
frame_000000.png
frame_000001.png
frame_000002.png
...
frame_000999.png
```

Frame numbers correspond to timeline frame indices calculated as:
```
frameNumber = floor(timelineTime * FPS)
```

## Performance Considerations

### Batch Export Performance

- **Export time**: ~0.1-0.2 seconds per frame
- **1000 frames**: ~2-3 minutes
- **Disk space**: ~1-2 MB per frame (1080p PNG)

### Tips for Faster Export

1. **Use frame skip**: Export every Nth frame to reduce total count
2. **Lower resolution**: Use 720p for quick previews
3. **Disable alpha**: Uncheck "Export With Alpha" for smaller files
4. **Close other applications**: Free up system resources

## Troubleshooting

### "No camera found" Error

**Solution:**
- Enable "Find Camera Automatically" in inspector
- Or manually assign Main Camera to "Target Camera" field

### "MultiCameraPointCloudManager not found" Error (Batch Export)

**Solution:**
- Ensure `MultiCameraPointCloudManager` component exists in scene
- Check that Timeline has `PointCloudPlayableAsset` configured
- Verify DatasetConfig is properly assigned

### Floor Grid Not Visible

**Solution:**
- Check "Show Floor Grid" is enabled in PointCloudFrameExporter
- Verify FloorGridRenderer component exists and is enabled
- Adjust `Grid Height` to match your scene's floor level
- Increase grid line opacity (Grid Color alpha channel)

### Exported Images Are Black

**Solution:**
- Verify camera is rendering point cloud correctly in Game view
- Check camera's "Target Texture" is set to None (not RenderTexture)
- Ensure point cloud mesh has proper material and is visible
- Try exporting a single frame first to test

### Frame Numbers Don't Match Timeline

**Solution:**
- Frame numbers are calculated from timeline time × FPS
- Verify FPS is correct in `MultiCameraPointCloudManager`
- For precise frame matching, use batch export mode

### Export Folder Not Opening

**Solution:**
- Check "Last Export Path" in inspector status section
- Manually navigate to: `{ProjectRoot}/Exports/{FolderName}/{Timestamp}/`
- Verify export folder name and timestamp in logs

## Example Workflows

### Workflow 1: Quick Preview Export

```
1. Enter Play Mode
2. Click "Export Current Frame" to test camera view
3. Adjust camera position/grid settings
4. Use "Start Export" + playback to export sample frames
5. Review exported images in Exports folder
```

### Workflow 2: Full Dataset Export

```
1. Configure DatasetConfig with all frame data
2. Add PointCloudFrameExporter to scene
3. Set resolution to 1920x1080
4. Enable floor grid with appropriate spacing
5. Enter Play Mode
6. Click "Batch Export All Frames"
7. Wait for completion (progress in console)
8. Export folder opens automatically
```

### Workflow 3: High-Quality Publication Export

```
1. Set resolution to 3840x2160 (4K)
2. Configure floor grid with fine spacing (0.1 units)
3. Adjust camera to optimal viewing angle
4. Use Frame Skip = 0 to export all frames
5. Batch export with high quality settings
6. Post-process images as needed for publication
```

## API Reference

### PointCloudFrameExporter Public Methods

```csharp
// Start/stop automatic export during playback
void StartExport()
void StopExport()

// Export single frame immediately
void ExportCurrentFrame()

// Batch export all frames (with progress logging)
void BatchExportAllFrames()

// Configure export settings at runtime
void SetExportResolution(int width, int height)
void SetFloorGridVisible(bool visible)
void SetFlipHorizontally(bool flip)

// Query export status
bool IsExporting()
int GetExportedFrameCount()
string GetLastExportPath()
```

### FloorGridRenderer Public Methods

```csharp
// Control grid visibility
void SetGridVisible(bool visible)
bool IsGridVisible()

// Adjust grid parameters at runtime
void SetGridSize(float size)
void SetGridSpacing(float spacing)
void SetGridColor(Color color)
void SetGridHeight(float height)
```

## Advanced Usage

### Custom Camera Setup

For specific viewing angles:
```csharp
// Adjust camera in Start() or via Inspector
Camera targetCamera = GetComponent<PointCloudFrameExporter>().targetCamera;
targetCamera.transform.position = new Vector3(5, 3, -5);
targetCamera.transform.LookAt(Vector3.zero);
```

### Dynamic Grid Configuration

```csharp
FloorGridRenderer grid = FindFirstObjectByType<FloorGridRenderer>();
grid.SetGridSize(20f);        // 20x20 meter grid
grid.SetGridSpacing(0.25f);   // 0.25m spacing
grid.SetGridHeight(-4.747f);  // Align with point cloud floor
```

### Programmatic Batch Export

```csharp
PointCloudFrameExporter exporter = GetComponent<PointCloudFrameExporter>();
exporter.SetExportResolution(3840, 2160); // 4K
exporter.SetFloorGridVisible(true);
exporter.BatchExportAllFrames();
```

## See Also

- [BVH_TIMELINE_USAGE.md](../timeline/BVH_TIMELINE_USAGE.md) - BVH skeleton integration
- [CLAUDE.md](/CLAUDE.md) - Overall project documentation
- [DebugImageExporter.cs](./DebugImageExporter.cs) - Utility for exporting sensor debug images
