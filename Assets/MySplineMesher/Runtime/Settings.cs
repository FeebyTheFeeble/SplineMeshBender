using System;
using System.Collections.Generic;
using UnityEngine;

namespace SC.Modeling.Splines
{
    /// <summary>Which local axis of the source mesh runs along the spline.</summary>
    public enum ForwardAxis
    {
        /// <summary>Mesh length runs along Z (X = width, Y = height). The usual Unity convention.</summary>
        Z,
        /// <summary>Mesh length runs along X (Y = width, Z = height).</summary>
        X,
        /// <summary>Mesh length runs along Y (Z = width, X = height).</summary>
        Y
    }

    /// <summary>How the source mesh is repeated over the spline's length.</summary>
    public enum RepeatMode
    {
        /// <summary>Repeat a whole number of times, stretching each copy slightly so the spline is filled exactly.</summary>
        RepeatFit,
        /// <summary>Repeat at the natural (scaled) mesh length. A remainder shorter than one copy is left empty.</summary>
        RepeatFixed,
        /// <summary>Do not repeat. One copy is stretched over the entire spline.</summary>
        Stretch
    }

    /// <summary>Where the "up" direction of the mesh comes from.</summary>
    public enum UpMode
    {
        /// <summary>Use the rotation of each spline knot (what Unity's spline tools set).</summary>
        KnotRotation,
        /// <summary>Always use world/container up. Good for roads, fences, anything that must stay level.</summary>
        WorldUp
    }

    /// <summary>How normals are produced for the deformed mesh.</summary>
    public enum NormalMode
    {
        /// <summary>Transform the source normals along with the deformation. Seamless between repeats.</summary>
        Transform,
        /// <summary>Let Unity recalculate normals. Better after ground conforming, but can show seams.</summary>
        Recalculate
    }

    /// <summary>Automatic collider generation.</summary>
    public enum ColliderMode
    {
        None,
        /// <summary>A chain of oriented BoxColliders on a hidden child object. Cheap and stable.</summary>
        Boxes,
        /// <summary>A MeshCollider using the generated mesh. Accurate, more expensive to update.</summary>
        Mesh
    }

    /// <summary>Per-knot data, stored in the same order as the spline's knots.</summary>
    [Serializable]
    public class PointData
    {
        [Tooltip("Multiplier for the mesh cross-section (width and height) at this point.")]
        public float scale = 1f;

        [Tooltip("Roll angle in degrees around the spline direction at this point.")]
        public float roll = 0f;

        [Tooltip("Vertex color at this point (multiplied with the source mesh colors). Interpolated along the spline.")]
        public Color color = Color.white;

        public PointData Clone() { return (PointData)MemberwiseClone(); }
    }

    /// <summary>Serializable wrapper so Unity can store a list of lists.</summary>
    [Serializable]
    public class SplinePointList
    {
        public List<PointData> points = new List<PointData>();
    }

    /// <summary>All user-facing generation settings of a <see cref="SplineMesher"/>.</summary>
    [Serializable]
    public class Settings
    {
        [Header("Mesh")]
        [Tooltip("The axis of the source mesh that is laid along the spline.")]
        public ForwardAxis forwardAxis = ForwardAxis.Z;

        [Tooltip("Reverse the mesh direction (rotates it 180 degrees around its up axis).")]
        public bool flipForward = false;

        public RepeatMode repeatMode = RepeatMode.RepeatFit;

        [Tooltip("X = width, Y = height, Z = length multiplier. Negative values mirror the mesh (e.g. guardrails on both sides of a road).")]
        public Vector3 scale = Vector3.one;

        [Tooltip("Roll angle in degrees applied along the whole spline, on top of the per-point roll.")]
        public float roll = 0f;

        public UpMode upMode = UpMode.KnotRotation;

        [Tooltip("Safety limit for how many copies of the mesh may be generated per spline.")]
        [Min(1)] public int maxRepeats = 2000;

        [Header("Quality")]
        [Tooltip("Distance in meters between spline samples. Lower is smoother but slower. The mesh itself needs enough edge loops to bend nicely.")]
        [Min(0.01f)] public float sampleSpacing = 0.25f;

        public NormalMode normalMode = NormalMode.Transform;

        [Header("UV & Color")]
        public Vector2 uvScale = Vector2.one;
        public Vector2 uvOffset = Vector2.zero;

        [Tooltip("Write per-point vertex colors into the mesh (for shaders).")]
        public bool useVertexColors = false;

        [Tooltip("Generate a secondary UV set for baked lighting. Editor only; costs time on every rebuild.")]
        public bool generateLightmapUVs = false;

        [Header("Collider")]
        public ColliderMode colliderMode = ColliderMode.None;

        [Tooltip("Length in meters of each box when using box colliders.")]
        [Min(0.1f)] public float colliderSegmentLength = 1f;

        [Tooltip("Draw the box colliders in the Scene view while this object is selected.")]
        public bool showColliders = false;

        [Header("Ground Conforming")]
        [Tooltip("Snap the spline PATH itself onto the ground (one raycast per spline sample). Posts, rails, colliders and end caps all follow the terrain, so nothing floats. Uses the mask, ray and offset values below.")]
        public bool followGround = false;

        [Tooltip("Additionally snap every mesh vertex downward onto colliders (raycasts per vertex; slower).")]
        public bool conform = false;

        public LayerMask conformMask = ~0;

        [Tooltip("How far above each vertex the ray starts.")]
        [Min(0f)] public float conformRayHeight = 5f;

        [Tooltip("How far below each vertex the ray may travel.")]
        [Min(0f)] public float conformMaxDistance = 20f;

        [Tooltip("Vertical offset above the ground that is hit.")]
        public float conformOffset = 0.02f;
    }
}
