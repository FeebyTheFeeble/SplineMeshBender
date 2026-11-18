using UnityEngine;

namespace SC.Modeling.Splines
{
    public partial class SplineMesher
    {
        [Header("End Caps")]
        [Tooltip("Prefab placed at the start of the (first) spline, e.g. a fence post or pipe flange.")]
        [SerializeField] GameObject startCap;

        [Tooltip("Prefab placed at the end of the (first) spline.")]
        [SerializeField] GameObject endCap;

        [Tooltip("Turn the start cap 180 degrees so it faces away from the spline.")]
        [SerializeField] bool flipStartCap = true;

        // The spawned instances (children of this object) and the prefab each one came from.
        [SerializeField, HideInInspector] GameObject startCapInstance;
        [SerializeField, HideInInspector] GameObject endCapInstance;
        [SerializeField, HideInInspector] GameObject startCapSource;
        [SerializeField, HideInInspector] GameObject endCapSource;

        public GameObject StartCap { get { return startCap; } set { startCap = value; SetDirty(); } }
        public GameObject EndCap { get { return endCap; } set { endCap = value; SetDirty(); } }

        /// <summary>The instantiated start cap, if any.</summary>
        public GameObject StartCapInstance { get { return startCapInstance; } }

        /// <summary>The instantiated end cap, if any.</summary>
        public GameObject EndCapInstance { get { return endCapInstance; } }

        void UpdateCaps(Matrix4x4 m)
        {
            UpdateCap(ref startCapInstance, ref startCapSource, startCap, true, m);
            UpdateCap(ref endCapInstance, ref endCapSource, endCap, false, m);
        }

        void UpdateCap(ref GameObject instance, ref GameObject source, GameObject prefab, bool atStart, Matrix4x4 m)
        {
            SplineSampler sm = samplers.Count > 0 ? samplers[0] : null;

            if (prefab == null || sm == null)
            {
                SafeDestroy(instance);
                instance = null;
                source = null;
                return;
            }

            if (instance == null || source != prefab)
            {
                SafeDestroy(instance);
                instance = SpawnCap(prefab, atStart ? "Start Cap" : "End Cap");
                source = prefab;
            }

            sm.Evaluate(atStart ? 0f : sm.Length, out SplineSampler.Frame f);

            Vector3 fwd = m.MultiplyVector(f.tangent);
            Vector3 up = m.MultiplyVector(f.up);
            Quaternion rot = fwd.sqrMagnitude > 1e-10f ? Quaternion.LookRotation(fwd, up) : Quaternion.identity;
            if (atStart && flipStartCap) rot *= Quaternion.Euler(0f, 180f, 0f);

            Transform t = instance.transform;
            t.localPosition = m.MultiplyPoint3x4(f.position);
            t.localRotation = rot;
        }

        GameObject SpawnCap(GameObject prefab, string capName)
        {
            GameObject go;
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(prefab))
                go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform);
            else
                go = Instantiate(prefab, transform);
#else
            go = Instantiate(prefab, transform);
#endif
            go.name = capName;
            return go;
        }
    }
}
