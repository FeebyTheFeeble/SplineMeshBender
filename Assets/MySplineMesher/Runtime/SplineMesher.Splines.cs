using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace SC.Modeling.Splines
{
    public partial class SplineMesher
    {
        [SerializeField] List<SplinePointList> pointLists = new List<SplinePointList>();

        readonly List<SplineSampler> samplers = new List<SplineSampler>();

        /// <summary>Sampled data of each spline in the container (an entry is null if that spline has fewer than 2 knots).</summary>
        public IReadOnlyList<SplineSampler> Samplers { get { return samplers; } }

        /// <summary>Combined length of all splines, in the container's local units.</summary>
        public float TotalLength
        {
            get
            {
                float l = 0f;
                for (int i = 0; i < samplers.Count; i++)
                    if (samplers[i] != null) l += samplers[i].Length;
                return l;
            }
        }

        // ---------------------------------------------------------------- point data

        /// <summary>
        /// Makes sure there is one <see cref="PointData"/> per knot of every spline.
        /// Note: data is matched to knots by index. If you insert a knot in the middle of a spline,
        /// the data after it shifts by one; fix it up with the Roll/Scale/Vertex Color tools.
        /// </summary>
        public void EnsurePointData()
        {
            if (container == null) return;
            var splines = container.Splines;

            while (pointLists.Count < splines.Count) pointLists.Add(new SplinePointList());
            while (pointLists.Count > splines.Count) pointLists.RemoveAt(pointLists.Count - 1);

            for (int i = 0; i < splines.Count; i++)
            {
                var list = pointLists[i].points;
                int knots = splines[i].Count;
                while (list.Count < knots) list.Add(new PointData());
                while (list.Count > knots) list.RemoveAt(list.Count - 1);
            }
        }

        /// <summary>Returns the data of one knot (creates missing data when needed). Modify it, then call <see cref="SetDirty"/>.</summary>
        public PointData GetPointData(int splineIndex, int knotIndex)
        {
            EnsurePointData();
            if (splineIndex < 0 || splineIndex >= pointLists.Count) return null;
            var list = pointLists[splineIndex].points;
            if (knotIndex < 0 || knotIndex >= list.Count) return null;
            return list[knotIndex];
        }

        /// <summary>Resets scale, roll and color of every point.</summary>
        public void ResetPointData()
        {
            pointLists.Clear();
            EnsurePointData();
            SetDirty();
        }

        // ---------------------------------------------------------------- queries

        /// <summary>World-space frame (position + rotation looking along the spline) at a knot.</summary>
        public bool TryGetKnotFrame(int splineIndex, int knotIndex, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            worldPosition = default;
            worldRotation = Quaternion.identity;
            if (!EnsureSamplers()) return false;
            if (splineIndex < 0 || splineIndex >= samplers.Count) return false;
            var sm = samplers[splineIndex];
            if (sm == null || knotIndex < 0 || knotIndex >= sm.KnotSampleIndex.Length) return false;

            var s = sm.Samples[sm.KnotSampleIndex[knotIndex]];
            return ToWorld(s.position, s.tangent, s.up, out worldPosition, out worldRotation);
        }

        /// <summary>World-space frame at a distance along a spline (0 to the spline's length). Handy for moving objects along the road.</summary>
        public bool TryEvaluate(int splineIndex, float distance, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            worldPosition = default;
            worldRotation = Quaternion.identity;
            if (!EnsureSamplers()) return false;
            if (splineIndex < 0 || splineIndex >= samplers.Count || samplers[splineIndex] == null) return false;

            samplers[splineIndex].Evaluate(distance, out SplineSampler.Frame f);
            return ToWorld(f.position, f.tangent, f.up, out worldPosition, out worldRotation);
        }

        bool ToWorld(Vector3 p, Vector3 tangent, Vector3 up, out Vector3 worldPos, out Quaternion worldRot)
        {
            Transform ct = container.transform;
            worldPos = ct.TransformPoint(p);
            Vector3 f = ct.TransformDirection(tangent);
            Vector3 u = ct.TransformDirection(up);
            worldRot = f.sqrMagnitude > 1e-10f ? Quaternion.LookRotation(f, u) : Quaternion.identity;
            return true;
        }

        // ---------------------------------------------------------------- internals

        bool EnsureSamplers()
        {
            if (container == null) return false;
            if (samplers.Count != container.Splines.Count) BuildSamplers();
            return samplers.Count > 0;
        }

        void BuildSamplers()
        {
            samplers.Clear();
            if (container == null) return;
            EnsurePointData();

            // Optional: snap the sampled path onto the ground.
            Func<Vector3, Vector3> snap = null;
            if (settings.followGround)
            {
                Matrix4x4 l2w = container.transform.localToWorldMatrix;
                Matrix4x4 w2l = l2w.inverse;
                float rayHeight = settings.conformRayHeight;
                float rayLength = settings.conformRayHeight + settings.conformMaxDistance;
                float offset = settings.conformOffset;
                int mask = settings.conformMask;
                Physics.SyncTransforms();
                snap = p =>
                {
                    Vector3 w = l2w.MultiplyPoint3x4(p);
                    if (Physics.Raycast(w + Vector3.up * rayHeight, Vector3.down, out RaycastHit hit, rayLength, mask, QueryTriggerInteraction.Ignore))
                    {
                        w.y = hit.point.y + offset;
                        return w2l.MultiplyPoint3x4(w);
                    }
                    return p;
                };
            }

            var splines = container.Splines;
            for (int i = 0; i < splines.Count; i++)
            {
                samplers.Add(SplineSampler.Build(splines[i], pointLists[i].points,
                    settings.sampleSpacing, settings.upMode, settings.roll, snap));
            }
        }

        /// <summary>Cheap hash of everything that influences the result and lives outside this component.</summary>
        int ComputeSplineHash()
        {
            if (container == null) return 0;
            unchecked
            {
                int h = 17;
                Matrix4x4 m = transform.worldToLocalMatrix * container.transform.localToWorldMatrix;
                h = h * 31 + m.GetHashCode();

                var splines = container.Splines;
                h = h * 31 + splines.Count;
                for (int i = 0; i < splines.Count; i++)
                {
                    var sp = splines[i];
                    h = h * 31 + sp.Count;
                    h = h * 31 + (sp.Closed ? 1 : 0);
                    for (int k = 0; k < sp.Count; k++)
                    {
                        BezierKnot kn = sp[k];
                        h = h * 31 + kn.Position.GetHashCode();
                        h = h * 31 + kn.Rotation.GetHashCode();
                        h = h * 31 + kn.TangentIn.GetHashCode();
                        h = h * 31 + kn.TangentOut.GetHashCode();
                    }
                }
                return h;
            }
        }
    }

    /// <summary>
    /// Pre-sampled version of a spline: positions, orientation frames and per-point data at a fixed
    /// spacing, with cumulative distance for fast "distance along spline" lookups.
    /// </summary>
    public sealed class SplineSampler
    {
        public struct Sample
        {
            public Vector3 position;
            public Vector3 tangent;
            public Vector3 right;
            public Vector3 up;
            public float distance;
            public float scale;
            public Color color;
        }

        public struct Frame
        {
            public Vector3 position;
            public Vector3 tangent;
            public Vector3 right;
            public Vector3 up;
            public float scale;
            public Color color;
        }

        public Sample[] Samples;
        /// <summary>For each knot, the index into <see cref="Samples"/> where that knot sits.</summary>
        public int[] KnotSampleIndex;
        public float Length;
        public bool Closed;

        struct Raw
        {
            public Vector3 pos, tan, up;
            public float scale, roll;
            public Color color;
        }

        /// <param name="snap">Optional. Maps a spline-space point to a new spline-space point (used for ground following).</param>
        public static SplineSampler Build(Spline spline, List<PointData> data, float spacing, UpMode upMode, float globalRoll,
            Func<Vector3, Vector3> snap = null)
        {
            if (spline == null) return null;
            int n = spline.Count;
            if (n < 2) return null;

            int curveCount = spline.Closed ? n : n - 1;
            spacing = Mathf.Max(0.01f, spacing);

            // ---- pass 1: raw samples along the curves
            var raw = new List<Raw>(curveCount * 8);
            var knotIdx = new int[n];
            Vector3 lastTan = Vector3.forward;

            for (int c = 0; c < curveCount; c++)
            {
                BezierCurve curve = spline.GetCurve(c);
                float len = CurveUtility.CalculateLength(curve);
                int steps = Mathf.Clamp(Mathf.CeilToInt(len / spacing), 2, 2048);

                int k0 = c;
                int k1 = (c + 1) % n;
                knotIdx[k0] = raw.Count;

                Quaternion r0 = spline[k0].Rotation;
                Quaternion r1 = spline[k1].Rotation;
                PointData d0 = Get(data, k0);
                PointData d1 = Get(data, k1);

                int last = (c == curveCount - 1) ? steps : steps - 1;
                for (int s = 0; s <= last; s++)
                {
                    float t = s / (float)steps;

                    Vector3 pos = CurveUtility.EvaluatePosition(curve, t);
                    Vector3 tan = CurveUtility.EvaluateTangent(curve, t);
                    if (tan.sqrMagnitude < 1e-12f)
                    {
                        Vector3 chord = (Vector3)curve.P3 - (Vector3)curve.P0;
                        tan = chord.sqrMagnitude > 1e-12f ? chord : lastTan;
                    }
                    tan.Normalize();
                    lastTan = tan;

                    float st = t * t * (3f - 2f * t); // smoothstep: continuous across knots
                    raw.Add(new Raw
                    {
                        pos = pos,
                        tan = tan,
                        up = upMode == UpMode.WorldUp ? Vector3.up : Quaternion.Slerp(r0, r1, t) * Vector3.up,
                        scale = Mathf.Lerp(d0.scale, d1.scale, st),
                        roll = Mathf.Lerp(d0.roll, d1.roll, st) + globalRoll,
                        color = Color.Lerp(d0.color, d1.color, st)
                    });
                }
            }

            if (!spline.Closed) knotIdx[n - 1] = raw.Count - 1;

            // ---- optional ground snapping: move samples, then re-derive smoothed tangents
            if (snap != null && raw.Count > 1)
            {
                for (int i = 0; i < raw.Count; i++)
                {
                    Raw r = raw[i];
                    r.pos = snap(r.pos);
                    raw[i] = r;
                }

                int window = Mathf.Max(1, Mathf.RoundToInt(0.5f / spacing)); // ~1 m wide smoothing
                var tangents = new Vector3[raw.Count];
                for (int i = 0; i < raw.Count; i++)
                {
                    int a = Mathf.Max(0, i - window);
                    int b = Mathf.Min(raw.Count - 1, i + window);
                    Vector3 t = raw[b].pos - raw[a].pos;
                    tangents[i] = t.sqrMagnitude > 1e-12f ? t.normalized : raw[i].tan;
                }
                for (int i = 0; i < raw.Count; i++)
                {
                    Raw r = raw[i];
                    r.tan = tangents[i];
                    raw[i] = r;
                }
            }

            // ---- pass 2: frames, roll and cumulative distance
            var samples = new Sample[raw.Count];
            float dist = 0f;
            for (int i = 0; i < raw.Count; i++)
            {
                Raw r = raw[i];
                Orthonormalize(r.tan, r.up, out Vector3 right, out Vector3 upN);
                if (Mathf.Abs(r.roll) > 1e-5f)
                {
                    Quaternion q = Quaternion.AngleAxis(r.roll, r.tan);
                    right = q * right;
                    upN = q * upN;
                }

                if (i > 0) dist += Vector3.Distance(r.pos, raw[i - 1].pos);

                samples[i] = new Sample
                {
                    position = r.pos, tangent = r.tan, right = right, up = upN,
                    distance = dist, scale = r.scale, color = r.color
                };
            }

            var sampler = new SplineSampler
            {
                Samples = samples,
                KnotSampleIndex = knotIdx,
                Closed = spline.Closed
            };
            sampler.Length = samples[samples.Length - 1].distance;
            return sampler;
        }

        static readonly PointData DefaultPoint = new PointData();

        static PointData Get(List<PointData> data, int i)
        {
            return (data != null && i >= 0 && i < data.Count && data[i] != null) ? data[i] : DefaultPoint;
        }

        static void Orthonormalize(Vector3 tangent, Vector3 up, out Vector3 right, out Vector3 upOut)
        {
            right = Vector3.Cross(up, tangent);
            if (right.sqrMagnitude < 1e-8f)
                right = Vector3.Cross(Mathf.Abs(tangent.y) < 0.99f ? Vector3.up : Vector3.forward, tangent);
            right.Normalize();
            upOut = Vector3.Cross(tangent, right);
        }

        /// <summary>
        /// Frame at a distance along the spline. Distances outside [0, Length] extrapolate
        /// in a straight line along the end tangents.
        /// </summary>
        public void Evaluate(float distance, out Frame frame)
        {
            var s = Samples;

            if (distance <= 0f)
            {
                Sample a = s[0];
                frame = new Frame { position = a.position + a.tangent * distance, tangent = a.tangent, right = a.right, up = a.up, scale = a.scale, color = a.color };
                return;
            }

            if (distance >= Length)
            {
                Sample b = s[s.Length - 1];
                frame = new Frame { position = b.position + b.tangent * (distance - Length), tangent = b.tangent, right = b.right, up = b.up, scale = b.scale, color = b.color };
                return;
            }

            int lo = 0, hi = s.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (s[mid].distance <= distance) lo = mid; else hi = mid;
            }

            Sample p = s[lo];
            Sample q = s[hi];
            float span = q.distance - p.distance;
            float u = span > 1e-9f ? (distance - p.distance) / span : 0f;

            frame.position = Vector3.Lerp(p.position, q.position, u);
            frame.tangent = Vector3.Lerp(p.tangent, q.tangent, u).normalized;
            frame.right = Vector3.Lerp(p.right, q.right, u).normalized;
            frame.up = Vector3.Lerp(p.up, q.up, u).normalized;
            frame.scale = Mathf.Lerp(p.scale, q.scale, u);
            frame.color = Color.Lerp(p.color, q.color, u);
        }
    }
}
