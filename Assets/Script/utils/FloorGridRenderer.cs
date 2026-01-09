using UnityEngine;

/// <summary>
/// Renders a floor grid overlay for point cloud visualization.
/// Used to provide spatial reference when exporting point cloud frames.
/// </summary>
public class FloorGridRenderer : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private bool showGrid = true;
    [SerializeField] private float gridSize = 10f;
    [SerializeField] private float gridSpacing = 0.5f;
    [SerializeField] private Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
    [SerializeField] private Color centerLineColor = new Color(1f, 0f, 0f, 1f);
    [SerializeField] private float gridHeight = 0f;
    [SerializeField] private float lineWidth = 1f;

    [Header("Material Settings")]
    [SerializeField] private Material gridMaterial;
    [SerializeField] private bool useCustomMaterial = false;

    private Mesh gridMesh;
    private Material runtimeMaterial;

    void Start()
    {
        GenerateGridMesh();
        SetupMaterial();
    }

    void OnEnable()
    {
        if (gridMesh == null)
        {
            GenerateGridMesh();
        }
        if (runtimeMaterial == null)
        {
            SetupMaterial();
        }
    }

    private void SetupMaterial()
    {
        if (useCustomMaterial && gridMaterial != null)
        {
            runtimeMaterial = new Material(gridMaterial);
        }
        else
        {
            // Create default material with transparent rendering
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            runtimeMaterial = new Material(shader);
            runtimeMaterial.color = gridColor;
        }

        // Enable alpha blending for transparency
        runtimeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        runtimeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        runtimeMaterial.SetInt("_ZWrite", 0);
        runtimeMaterial.DisableKeyword("_ALPHATEST_ON");
        runtimeMaterial.EnableKeyword("_ALPHABLEND_ON");
        runtimeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        runtimeMaterial.renderQueue = 3000;
    }

    private void GenerateGridMesh()
    {
        if (gridMesh != null)
        {
            Destroy(gridMesh);
        }

        gridMesh = new Mesh();
        gridMesh.name = "FloorGrid";

        int lineCount = Mathf.CeilToInt(gridSize / gridSpacing) * 2 + 1;
        int vertexCount = lineCount * 4; // 2 lines (X and Z) * 2 vertices per line
        int indexCount = lineCount * 4; // 2 lines * 2 indices per line

        Vector3[] vertices = new Vector3[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] indices = new int[indexCount];

        float halfSize = gridSize / 2f;
        int vertexIndex = 0;
        int indexIndex = 0;

        // Generate grid lines along Z-axis
        for (float x = -halfSize; x <= halfSize; x += gridSpacing)
        {
            Color lineColor = Mathf.Abs(x) < 0.01f ? centerLineColor : gridColor;

            vertices[vertexIndex] = new Vector3(x, gridHeight, -halfSize);
            vertices[vertexIndex + 1] = new Vector3(x, gridHeight, halfSize);
            colors[vertexIndex] = lineColor;
            colors[vertexIndex + 1] = lineColor;

            indices[indexIndex] = vertexIndex;
            indices[indexIndex + 1] = vertexIndex + 1;

            vertexIndex += 2;
            indexIndex += 2;
        }

        // Generate grid lines along X-axis
        for (float z = -halfSize; z <= halfSize; z += gridSpacing)
        {
            Color lineColor = Mathf.Abs(z) < 0.01f ? centerLineColor : gridColor;

            vertices[vertexIndex] = new Vector3(-halfSize, gridHeight, z);
            vertices[vertexIndex + 1] = new Vector3(halfSize, gridHeight, z);
            colors[vertexIndex] = lineColor;
            colors[vertexIndex + 1] = lineColor;

            indices[indexIndex] = vertexIndex;
            indices[indexIndex + 1] = vertexIndex + 1;

            vertexIndex += 2;
            indexIndex += 2;
        }

        gridMesh.vertices = vertices;
        gridMesh.colors = colors;
        gridMesh.SetIndices(indices, MeshTopology.Lines, 0);
        gridMesh.RecalculateBounds();
    }

    void OnRenderObject()
    {
        if (!showGrid || gridMesh == null || runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetPass(0);
        Graphics.DrawMeshNow(gridMesh, transform.localToWorldMatrix);
    }

    /// <summary>
    /// Manually draw the grid to a specific camera's render texture.
    /// Call this before Camera.Render() when capturing to a RenderTexture.
    /// </summary>
    /// <param name="camera">The camera to draw the grid for</param>
    public void DrawGridForCamera(Camera camera)
    {
        if (!showGrid || gridMesh == null || runtimeMaterial == null || camera == null)
        {
            return;
        }

        // Set up the camera's matrices for rendering
        GL.PushMatrix();
        GL.LoadProjectionMatrix(camera.projectionMatrix);
        GL.modelview = camera.worldToCameraMatrix * transform.localToWorldMatrix;

        runtimeMaterial.SetPass(0);
        Graphics.DrawMeshNow(gridMesh, Matrix4x4.identity);

        GL.PopMatrix();
    }

    /// <summary>
    /// Draw the grid to the currently active RenderTexture.
    /// Call this after setting up the render texture but before reading pixels.
    /// </summary>
    public void DrawGridToActiveRenderTexture(Camera camera)
    {
        if (!showGrid || gridMesh == null || runtimeMaterial == null || camera == null)
        {
            return;
        }

        // Draw mesh using Graphics.DrawMesh which renders in the current frame
        Graphics.DrawMesh(gridMesh, transform.localToWorldMatrix, runtimeMaterial, 0, camera);
    }

    void OnDestroy()
    {
        if (gridMesh != null)
        {
            Destroy(gridMesh);
        }
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    // Public API for runtime control
    public void SetGridVisible(bool visible)
    {
        showGrid = visible;
    }

    public void SetGridSize(float size)
    {
        gridSize = size;
        GenerateGridMesh();
    }

    public void SetGridSpacing(float spacing)
    {
        gridSpacing = spacing;
        GenerateGridMesh();
    }

    public void SetGridColor(Color color)
    {
        gridColor = color;
        if (runtimeMaterial != null && !useCustomMaterial)
        {
            runtimeMaterial.color = color;
        }
    }

    public void SetGridHeight(float height)
    {
        gridHeight = height;
        GenerateGridMesh();
    }

    public bool IsGridVisible()
    {
        return showGrid;
    }

    // Editor preview
    void OnDrawGizmosSelected()
    {
        if (!showGrid) return;

        Gizmos.color = gridColor;
        float halfSize = gridSize / 2f;

        // Draw grid preview in editor
        for (float x = -halfSize; x <= halfSize; x += gridSpacing)
        {
            Vector3 start = transform.TransformPoint(new Vector3(x, gridHeight, -halfSize));
            Vector3 end = transform.TransformPoint(new Vector3(x, gridHeight, halfSize));
            Gizmos.DrawLine(start, end);
        }

        for (float z = -halfSize; z <= halfSize; z += gridSpacing)
        {
            Vector3 start = transform.TransformPoint(new Vector3(-halfSize, gridHeight, z));
            Vector3 end = transform.TransformPoint(new Vector3(halfSize, gridHeight, z));
            Gizmos.DrawLine(start, end);
        }

        // Draw center lines
        Gizmos.color = centerLineColor;
        Gizmos.DrawLine(
            transform.TransformPoint(new Vector3(0, gridHeight, -halfSize)),
            transform.TransformPoint(new Vector3(0, gridHeight, halfSize))
        );
        Gizmos.DrawLine(
            transform.TransformPoint(new Vector3(-halfSize, gridHeight, 0)),
            transform.TransformPoint(new Vector3(halfSize, gridHeight, 0))
        );
    }
}
