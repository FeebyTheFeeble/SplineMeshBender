using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;

namespace SC.Modeling.Splines
{
    /// <summary>
    /// Repeats and deforms a source mesh along the splines of a <see cref="SplineContainer"/>.
    /// The result is one continuous mesh on this object's MeshFilter.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Spline Mesher/Spline Mesher")]
    public partial class SplineMesher : MonoBehaviour
    {
        const string ColliderRootName = "__SplineMesher_Colliders";

        [Tooltip("The spline container whose splines the mesh follows. Defaults to the one on this object.")]
        [SerializeField] SplineContainer container;

        [Tooltip("The mesh that is repeated and bent along the spline. Needs Read/Write enabled in its import settings.")]
        [SerializeField] Mesh sourceMesh;

        [SerializeField] Settings settings = new Settings();

        [SerializeField, HideInInspector] Mesh generatedMesh;
        [SerializeField, HideInInspector] bool createdMeshCollider;

        /// <summary>Invoked (in the editor and at runtime) after the mesh has been regenerated.</summary>
        public UnityEvent onRebuilt = new UnityEvent();

        /// <summary>C# version of <see cref="onRebuilt"/>.</summary>
        public event Action<SplineMesher> Rebuilt;

        static readonly Dictionary<Mesh, SplineMesher> meshOwners = new Dictionary<Mesh, SplineMesher>();

        bool dirty = true;
        int lastHash;
        int vertexCount;
        MeshExtents lastExtents;

        // ---------------------------------------------------------------- public API

        public SplineContainer Container
        {
            get { return container; }
            set { container = value; SetDirty(); }
        }

        public Mesh SourceMesh
        {
            get { return sourceMesh; }
            set { sourceMesh = value; SetDirty(); }
        }

        /// <summary>The settings object. Modify values, then call <see cref="SetDirty"/>.</summary>
        public Settings Settings { get { return settings; } }

        /// <summary>The generated mesh (owned by this component).</summary>
        public Mesh GeneratedMesh { get { return generatedMesh; } }

        public int VertexCount { get { return vertexCount; } }

        /// <summary>Flags the mesh for regeneration; it is rebuilt at the end of the frame.</summary>
        public void SetDirty() { dirty = true; }

        /// <summary>Regenerates the mesh immediately.</summary>
        public void Rebuild()
        {
            dirty = false;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(this)) return; // prefab assets: nothing to build
#endif
            if (container == null) container = GetComponent<SplineContainer>();
            var mf = GetComponent<MeshFilter>();

            if (container == null || sourceMesh == null || mf == null)
            {
                ClearOutput(mf);
                lastHash = ComputeSplineHash();
                return;
            }

            // keep our own colliders out of the ground raycasts (path following and vertex conforming)
            var mc = GetComponent<MeshCollider>();
            bool mcEnabled = mc != null && mc.enabled;
            Transform colRoot = transform.Find(ColliderRootName);
            if (settings.conform || settings.followGround)
            {
                if (mc != null) mc.enabled = false;
                if (colRoot != null) colRoot.gameObject.SetActive(false);
            }

            BuildSamplers();
            EnsureGeneratedMesh();

            Matrix4x4 splineToLocal = transform.worldToLocalMatrix * container.transform.localToWorldMatrix;

            vertexCount = SplineMeshGenerator.Generate(generatedMesh, sourceMesh, samplers, splineToLocal,
                transform.localToWorldMatrix, settings, out lastExtents);

            if (mc != null) mc.enabled = mcEnabled;

            mf.sharedMesh = generatedMesh;
            UpdateColliders(splineToLocal);
            UpdateCaps(splineToLocal);

            lastHash = ComputeSplineHash();

            if (Rebuilt != null) Rebuilt(this);
            if (onRebuilt != null) onRebuilt.Invoke();
        }

        /// <summary>Rebuilds when flagged dirty or when the spline/transform changed. Called automatically.</summary>
        public void CheckForChanges()
        {
            if (!isActiveAndEnabled) return;
            if (dirty || (container != null && ComputeSplineHash() != lastHash))
                Rebuild();
        }

        // ---------------------------------------------------------------- Unity messages

        void Reset()
        {
            container = GetComponent<SplineContainer>();
            if (container == null) container = GetComponentInParent<SplineContainer>();
            dirty = true;
        }

        void OnEnable()
        {
            if (container == null) container = GetComponent<SplineContainer>();
            dirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += EditorTick;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
#endif
        }

        void OnDestroy()
        {
            if (generatedMesh != null)
            {
                SplineMesher owner;
                if (meshOwners.TryGetValue(generatedMesh, out owner) && owner == this) meshOwners.Remove(generatedMesh);
            }
        }

        void OnValidate()
        {
            dirty = true; // picked up by the editor tick / LateUpdate (never destroy objects inside OnValidate)
        }

        void LateUpdate()
        {
            CheckForChanges();
        }
        void OnDrawGizmosSelected()
        {
            if (!settings.showColliders || settings.colliderMode != ColliderMode.Boxes) return;

            Transform root = transform.Find(ColliderRootName);
            if (root == null) return;

            Gizmos.color = new Color(0.3f, 1f, 0.4f, 1f);
            for (int i = 0; i < root.childCount; i++)
            {
                var box = root.GetChild(i).GetComponent<BoxCollider>();
                if (box == null) continue;

                Transform t = box.transform;
                Gizmos.matrix = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#if UNITY_EDITOR
        void EditorTick()
        {
            if (this == null) { UnityEditor.EditorApplication.update -= EditorTick; return; }
            if (!Application.isPlaying) CheckForChanges();
        }
#endif

        // ---------------------------------------------------------------- mesh ownership

        void EnsureGeneratedMesh()
        {
            // A duplicated object shares the serialized mesh reference. Give the copy its own mesh.
            if (generatedMesh != null)
            {
                SplineMesher owner;
                if (meshOwners.TryGetValue(generatedMesh, out owner) && owner != null && owner != this)
                    generatedMesh = null;
            }

            if (generatedMesh == null)
                generatedMesh = new Mesh { name = "Spline Mesh" };

            meshOwners[generatedMesh] = this;
        }

        void ClearOutput(MeshFilter mf)
        {
            if (generatedMesh != null) generatedMesh.Clear();
            vertexCount = 0;
            samplers.Clear();
            DestroyColliderRoot();
            UpdateCaps(Matrix4x4.identity);
            if (mf != null && mf.sharedMesh == generatedMesh) mf.sharedMesh = generatedMesh;
        }

        // ---------------------------------------------------------------- colliders

        void UpdateColliders(Matrix4x4 m)
        {
            // MeshCollider
            var mc = GetComponent<MeshCollider>();
            if (settings.colliderMode == ColliderMode.Mesh)
            {
                if (mc == null) { mc = gameObject.AddComponent<MeshCollider>(); createdMeshCollider = true; }
                mc.sharedMesh = null;
                mc.sharedMesh = generatedMesh;
            }
            else if (mc != null && createdMeshCollider)
            {
                SafeDestroy(mc);
                createdMeshCollider = false;
            }

            // Boxes
            if (settings.colliderMode != ColliderMode.Boxes || vertexCount == 0)
            {
                DestroyColliderRoot();
                return;
            }

            Transform root = transform.Find(ColliderRootName);
            if (root == null)
            {
                var go = new GameObject(ColliderRootName);
                go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                root = go.transform;
                root.SetParent(transform, false);
            }
            root.gameObject.SetActive(true);
            root.gameObject.layer = gameObject.layer;

            float spacing = Mathf.Max(0.1f, settings.colliderSegmentLength);
            float lossy = Mathf.Max(1e-5f, Mathf.Abs(m.lossyScale.x));
            int used = 0;

            for (int si = 0; si < samplers.Count; si++)
            {
                SplineSampler sm = samplers[si];
                if (sm == null || sm.Length < 1e-4f) continue;

                float covered = SplineMeshGenerator.GetCoveredLength(sm.Length, lastExtents, settings);
                if (covered < 1e-4f) continue;

                int segs = Mathf.Max(1, Mathf.CeilToInt(covered / spacing));
                for (int k = 0; k < segs; k++)
                {
                    float d0 = covered * k / segs;
                    float d1 = covered * (k + 1) / segs;
                    sm.Evaluate(d0, out SplineSampler.Frame f0);
                    sm.Evaluate(d1, out SplineSampler.Frame f1);
                    sm.Evaluate((d0 + d1) * 0.5f, out SplineSampler.Frame fm);

                    Vector3 c0 = SectionCenter(f0);
                    Vector3 c1 = SectionCenter(f1);
                    Vector3 chordVec = c1 - c0;
                    float chord = chordVec.magnitude;
                    Vector3 fwd = m.MultiplyVector(chordVec);
                    Vector3 upv = m.MultiplyVector((f0.up + f1.up).normalized);
                    if (chord < 1e-4f || fwd.sqrMagnitude < 1e-10f) continue;

                    float sx = settings.scale.x * fm.scale;
                    float sy = settings.scale.y * fm.scale;

                    BoxCollider box = GetBox(root, used++);
                    var t = box.transform;
                    t.localPosition = m.MultiplyPoint3x4((c0 + c1) * 0.5f);
                    t.localRotation = Quaternion.LookRotation(fwd, upv);
                    t.localScale = Vector3.one;
                    box.center = Vector3.zero;
                    box.size = new Vector3(
                        Mathf.Max(0.001f, lastExtents.SideSize * Mathf.Abs(sx) * lossy),
                        Mathf.Max(0.001f, lastExtents.UpSize * Mathf.Abs(sy) * lossy),
                          (chord + 0.005f) * lossy);
                }
            }

            // remove leftovers
            for (int i = root.childCount - 1; i >= used; i--)
                SafeDestroy(root.GetChild(i).gameObject);
        }
        Vector3 SectionCenter(SplineSampler.Frame f)
        {
            return f.position
                   + f.right * (lastExtents.SideCenter * settings.scale.x * f.scale)
                   + f.up * (lastExtents.UpCenter * settings.scale.y * f.scale);
        }
        BoxCollider GetBox(Transform root, int index)
        {
            GameObject go;
            if (index < root.childCount)
            {
                go = root.GetChild(index).gameObject;
            }
            else
            {
                go = new GameObject("Box " + index);
                go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                go.transform.SetParent(root, false);
            }
            go.layer = gameObject.layer;
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            return box;
        }

        void DestroyColliderRoot()
        {
            Transform root = transform.Find(ColliderRootName);
            if (root != null) SafeDestroy(root.gameObject);

            var mc = GetComponent<MeshCollider>();
            if (mc != null && createdMeshCollider && settings.colliderMode != ColliderMode.Mesh)
            {
                SafeDestroy(mc);
                createdMeshCollider = false;
            }
        }

        static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }
    }
}
