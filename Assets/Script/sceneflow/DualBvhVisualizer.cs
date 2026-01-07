using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Visualizes two BVH skeleton frames simultaneously in Game View
/// Renders Current Frame BVH (green) + Previous Frame BVH (red) for motion verification
/// Finds temp skeletons created by SceneFlowCalculator.OnShowSceneFlow()
/// Uses LineRenderer + Sphere primitives for URP compatibility (renders in Game View)
/// </summary>
public class DualBvhVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    [Tooltip("Show current frame skeleton (green)")]
    public bool showCurrentFrame = true;

    [Tooltip("Show previous frame skeleton (red)")]
    public bool showPreviousFrame = true;

    [Tooltip("Current frame color")]
    public Color currentFrameColor = Color.green;

    [Tooltip("Previous frame color")]
    public Color previousFrameColor = Color.red;

    [Tooltip("Joint sphere size")]
    [Range(0.001f, 0.1f)]
    public float jointSize = 0.02f;

    [Tooltip("Bone line width")]
    [Range(0.001f, 0.05f)]
    public float boneWidth = 0.015f;

    private Material currentFrameMaterial;
    private Material previousFrameMaterial;
    private Transform currentFrameContainer;
    private Transform previousFrameContainer;

    // Visualization GameObjects
    private GameObject currentFrameVisuals;
    private GameObject previousFrameVisuals;
    private readonly List<LineRenderer> currentBones = new();
    private readonly List<GameObject> currentJoints = new();
    private readonly List<LineRenderer> previousBones = new();
    private readonly List<GameObject> previousJoints = new();

    private void Start()
    {
        // Create materials for rendering
        currentFrameMaterial = CreateMaterial(currentFrameColor);
        previousFrameMaterial = CreateMaterial(previousFrameColor);
    }

    private void Update()
    {
        // Find temp skeleton containers created by SceneFlowCalculator
        GameObject sceneFlow = GameObject.Find("SceneFlow");
        if (sceneFlow != null)
        {
            Transform newCurrentContainer = sceneFlow.transform.Find("CurrentFrameBVH");
            Transform newPreviousContainer = sceneFlow.transform.Find("PreviousFrameBVH");

            // Rebuild visuals if containers changed
            if (newCurrentContainer != currentFrameContainer)
            {
                currentFrameContainer = newCurrentContainer;
                RebuildCurrentFrameVisuals();
            }

            if (newPreviousContainer != previousFrameContainer)
            {
                previousFrameContainer = newPreviousContainer;
                RebuildPreviousFrameVisuals();
            }
        }
        else
        {
            // SceneFlow not found - clear visuals
            if (currentFrameContainer != null)
            {
                currentFrameContainer = null;
                DestroyVisuals(ref currentFrameVisuals, currentBones, currentJoints);
            }
            if (previousFrameContainer != null)
            {
                previousFrameContainer = null;
                DestroyVisuals(ref previousFrameVisuals, previousBones, previousJoints);
            }
        }
    }

    private void LateUpdate()
    {
        // Update visual positions to follow skeleton transforms
        if (showCurrentFrame && currentFrameContainer != null)
        {
            UpdateVisuals(currentFrameContainer, currentBones, currentJoints);
        }

        if (showPreviousFrame && previousFrameContainer != null)
        {
            UpdateVisuals(previousFrameContainer, previousBones, previousJoints);
        }

        // Toggle visibility
        if (currentFrameVisuals != null)
        {
            currentFrameVisuals.SetActive(showCurrentFrame);
        }
        if (previousFrameVisuals != null)
        {
            previousFrameVisuals.SetActive(showPreviousFrame);
        }
    }

    private void RebuildCurrentFrameVisuals()
    {
        DestroyVisuals(ref currentFrameVisuals, currentBones, currentJoints);

        if (currentFrameContainer == null) return;

        Transform bvhRoot = GetBvhRoot(currentFrameContainer);
        if (bvhRoot == null) return;

        currentFrameVisuals = new GameObject("CurrentFrame_Visuals");
        currentFrameVisuals.transform.SetParent(transform);
        CreateSkeletonVisuals(bvhRoot, currentFrameVisuals.transform, currentFrameMaterial, currentBones, currentJoints);
    }

    private void RebuildPreviousFrameVisuals()
    {
        DestroyVisuals(ref previousFrameVisuals, previousBones, previousJoints);

        if (previousFrameContainer == null) return;

        Transform bvhRoot = GetBvhRoot(previousFrameContainer);
        if (bvhRoot == null) return;

        previousFrameVisuals = new GameObject("PreviousFrame_Visuals");
        previousFrameVisuals.transform.SetParent(transform);
        CreateSkeletonVisuals(bvhRoot, previousFrameVisuals.transform, previousFrameMaterial, previousBones, previousJoints);
    }

    private Transform GetBvhRoot(Transform container)
    {
        if (container == null || container.childCount == 0) return null;

        // Get temp skeleton root (first child: "TempBvhSkeleton_N" or similar)
        Transform tempSkeletonRoot = container.GetChild(0);
        if (tempSkeletonRoot == null || tempSkeletonRoot.childCount == 0) return null;

        // Get actual BVH root joint (first child of temp skeleton)
        Transform bvhRoot = tempSkeletonRoot.GetChild(0);
        return bvhRoot;
    }

    private void CreateSkeletonVisuals(Transform joint, Transform visualsParent, Material material,
        List<LineRenderer> bones, List<GameObject> joints)
    {
        if (joint == null) return;

        // Create sphere for this joint
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = $"Joint_{joint.name}";
        sphere.transform.SetParent(visualsParent);
        sphere.transform.position = joint.position;
        sphere.transform.localScale = Vector3.one * jointSize * 2f;

        // Remove collider
        if (sphere.TryGetComponent<Collider>(out var collider))
        {
            Destroy(collider);
        }

        // Set material
        Renderer renderer = sphere.GetComponent<Renderer>();
        renderer.material = material;
        joints.Add(sphere);

        // Create lines to all children
        foreach (Transform child in joint)
        {
            GameObject lineObj = new($"Bone_{joint.name}_to_{child.name}");
            lineObj.transform.SetParent(visualsParent);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.startWidth = boneWidth;
            lr.endWidth = boneWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.SetPosition(0, joint.position);
            lr.SetPosition(1, child.position);
            lr.material = material;

            bones.Add(lr);

            // Recurse to children
            CreateSkeletonVisuals(child, visualsParent, material, bones, joints);
        }
    }

    private void UpdateVisuals(Transform container, List<LineRenderer> bones, List<GameObject> joints)
    {
        Transform bvhRoot = GetBvhRoot(container);
        if (bvhRoot == null) return;

        int jointIndex = 0;
        int boneIndex = 0;
        UpdateVisualsRecursive(bvhRoot, ref jointIndex, ref boneIndex, bones, joints);
    }

    private void UpdateVisualsRecursive(Transform joint, ref int jointIndex, ref int boneIndex,
        List<LineRenderer> bones, List<GameObject> joints)
    {
        if (joint == null) return;

        // Update joint sphere position
        if (jointIndex < joints.Count)
        {
            joints[jointIndex].transform.position = joint.position;
            joints[jointIndex].transform.localScale = Vector3.one * jointSize * 2f;
            jointIndex++;
        }

        // Update bone lines to children
        foreach (Transform child in joint)
        {
            if (boneIndex < bones.Count)
            {
                LineRenderer lr = bones[boneIndex];
                lr.SetPosition(0, joint.position);
                lr.SetPosition(1, child.position);
                lr.startWidth = boneWidth;
                lr.endWidth = boneWidth;
                boneIndex++;
            }

            UpdateVisualsRecursive(child, ref jointIndex, ref boneIndex, bones, joints);
        }
    }

    private void DestroyVisuals(ref GameObject visualsRoot, List<LineRenderer> bones, List<GameObject> joints)
    {
        if (visualsRoot != null)
        {
            Destroy(visualsRoot);
            visualsRoot = null;
        }
        bones.Clear();
        joints.Clear();
    }

    private Material CreateMaterial(Color color)
    {
        // Use URP-compatible Unlit shader
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader == null)
        {
            Debug.LogWarning("[DualBvhVisualizer] URP Unlit shader not found, using Standard shader");
            shader = Shader.Find("Standard");
        }

        Material mat = new Material(shader);
        mat.hideFlags = HideFlags.HideAndDontSave;
        mat.color = color;

        // Set URP color property
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }

        return mat;
    }

    private void OnDestroy()
    {
        DestroyVisuals(ref currentFrameVisuals, currentBones, currentJoints);
        DestroyVisuals(ref previousFrameVisuals, previousBones, previousJoints);

        if (currentFrameMaterial != null) Destroy(currentFrameMaterial);
        if (previousFrameMaterial != null) Destroy(previousFrameMaterial);
    }
}
