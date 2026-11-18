using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SC.Modeling.Splines
{
    /// <summary>Bounds of the source mesh expressed in "side / up / along" coordinates.</summary>
    public struct MeshExtents
    {
        public float sideMin, sideMax, upMin, upMax, alongMin, alongMax;

        public float AlongLength { get { return alongMax - alongMin; } }
        public float SideSize { get { return sideMax - sideMin; } }
        public float UpSize { get { return upMax - upMin; } }
        public float SideCenter { get { return (sideMax + sideMin) * 0.5f; } }
        public float UpCenter { get { return (upMax + upMin) * 0.5f; } }
        public bool IsValid { get { return alongMax - alongMin > 1e-5f; } }
    }

    /// <summary>
    /// Core algorithm. Every vertex of the source mesh is mapped to a distance along the spline
    /// (from its position along the forward axis), then placed relative to the spline frame at that distance.
    /// The mesh is repeated as often as the repeat mode demands.
    /// </summary>
    public static class SplineMeshGenerator
    {
        public const int MaxVertices = 4000000;

        /// <summary>Splits a mesh-space vector into (side, up, along) components. Cyclic axis permutations keep handedness.</summary>
        public static void Decompose(Vector3 v, ForwardAxis axis, bool flip, out float side, out float up, out float along)
        {
            switch (axis)
            {
                case ForwardAxis.X: side = v.y; up = v.z; along = v.x; break;
                case ForwardAxis.Y: side = v.z; up = v.x; along = v.y; break;
                default:            side = v.x; up = v.y; along = v.z; break;
            }
            if (flip) { side = -side; along = -along; } // 180 degree turn around "up"
        }

        public static MeshExtents GetExtents(Vector3[] vertices, ForwardAxis axis, bool flip)
        {
            var e = new MeshExtents
            {
                sideMin = float.MaxValue, upMin = float.MaxValue, alongMin = float.MaxValue,
                sideMax = float.MinValue, upMax = float.MinValue, alongMax = float.MinValue
            };
            for (int i = 0; i < vertices.Length; i++)
            {
                Decompose(vertices[i], axis, flip, out float s, out float u, out float a);
                if (s < e.sideMin) e.sideMin = s; if (s > e.sideMax) e.sideMax = s;
                if (u < e.upMin) e.upMin = u;     if (u > e.upMax) e.upMax = u;
                if (a < e.alongMin) e.alongMin = a; if (a > e.alongMax) e.alongMax = a;
            }
            return e;
        }

        /// <summary>
        /// Builds the deformed mesh into <paramref name="output"/>.
        /// </summary>
        /// <param name="splineToLocal">Matrix from spline (container local) space to the mesher's local space.</param>
        /// <param name="localToWorld">Mesher's local-to-world matrix (used for ground conforming).</param>
        /// <returns>Number of generated vertices (0 when nothing was generated).</returns>
        public static int Generate(Mesh output, Mesh source, IReadOnlyList<SplineSampler> samplers,
            Matrix4x4 splineToLocal, Matrix4x4 localToWorld, Settings s, out MeshExtents extents)
        {
            extents = default;
            output.Clear();
            if (source == null || s == null || samplers == null) return 0;

            if (!source.isReadable)
            {
                Debug.LogWarning("Spline Mesher: the source mesh '" + source.name + "' is not readable. Enable 'Read/Write' in its import settings.", source);
                return 0;
            }

            Vector3[] sv = source.vertices;
            int vc = sv.Length;
            if (vc == 0) return 0;

            extents = GetExtents(sv, s.forwardAxis, s.flipForward);
            if (!extents.IsValid)
            {
                Debug.LogWarning("Spline Mesher: the source mesh has no length along the selected forward axis.", source);
                return 0;
            }
            float meshLen = extents.AlongLength;

            // ---- source data, pre-split into side/up/along
            Vector3[] sn = source.normals;
            Vector4[] st = source.tangents;
            Vector2[] suv = source.uv;
            Color[] sc = source.colors;
            bool hasN = sn != null && sn.Length == vc;
            bool hasT = st != null && st.Length == vc;
            bool hasUV = suv != null && suv.Length == vc;
            bool hasC = sc != null && sc.Length == vc;

            var vSide = new float[vc]; var vUp = new float[vc]; var vAlong = new float[vc];
            for (int i = 0; i < vc; i++)
                Decompose(sv[i], s.forwardAxis, s.flipForward, out vSide[i], out vUp[i], out vAlong[i]);

            float[] nS = null, nU = null, nA = null;
            if (hasN)
            {
                nS = new float[vc]; nU = new float[vc]; nA = new float[vc];
                for (int i = 0; i < vc; i++)
                    Decompose(sn[i], s.forwardAxis, s.flipForward, out nS[i], out nU[i], out nA[i]);
            }

            float[] tS = null, tU = null, tA = null;
            if (hasT)
            {
                tS = new float[vc]; tU = new float[vc]; tA = new float[vc];
                for (int i = 0; i < vc; i++)
                    Decompose(new Vector3(st[i].x, st[i].y, st[i].z), s.forwardAxis, s.flipForward, out tS[i], out tU[i], out tA[i]);
            }

            int subCount = Mathf.Max(1, source.subMeshCount);
            int[][] subIdx = new int[subCount][];
            for (int j = 0; j < subCount; j++)
            {
                subIdx[j] = source.GetTopology(j) == MeshTopology.Triangles ? source.GetIndices(j) : new int[0];
            }

            // ---- repetition plan per spline
            int count = samplers.Count;
            var reps = new int[count];
            var segLen = new float[count];
            float nominal = Mathf.Max(1e-5f, meshLen * Mathf.Abs(s.scale.z));
            long totalVerts = 0;

            for (int i = 0; i < count; i++)
            {
                SplineSampler sm = samplers[i];
                if (sm == null || sm.Length < 1e-4f) continue;

                int n;
                switch (s.repeatMode)
                {
                    case RepeatMode.Stretch:
                        n = 1; segLen[i] = sm.Length; break;
                    case RepeatMode.RepeatFixed:
                        n = Mathf.Max(1, Mathf.FloorToInt(sm.Length / nominal)); segLen[i] = nominal; break;
                    default:
                        n = Mathf.Max(1, Mathf.RoundToInt(sm.Length / nominal)); segLen[i] = sm.Length / n; break;
                }
                n = Mathf.Min(n, Mathf.Max(1, s.maxRepeats));
                reps[i] = n;
                totalVerts += (long)n * vc;
            }

            if (totalVerts == 0) return 0;
            if (totalVerts > MaxVertices)
            {
                Debug.LogWarning("Spline Mesher: generating " + totalVerts + " vertices exceeds the limit of " + MaxVertices +
                                 ". Use a longer/simpler source mesh or fewer repeats.");
                return 0;
            }
            int total = (int)totalVerts;

            // ---- output buffers
            var oVerts = new List<Vector3>(total);
            var oNormals = hasN && s.normalMode == NormalMode.Transform ? new List<Vector3>(total) : null;
            var oTangents = hasT && s.normalMode == NormalMode.Transform ? new List<Vector4>(total) : null;
            var oUV = hasUV ? new List<Vector2>(total) : null;
            var oColors = (s.useVertexColors || hasC) ? new List<Color>(total) : null;
            var oIdx = new List<int>[subCount];
            for (int j = 0; j < subCount; j++) oIdx[j] = new List<int>(subIdx[j].Length * 2);

            Matrix4x4 normalM = splineToLocal.inverse.transpose;
            Matrix4x4 worldToLocal = localToWorld.inverse;
            float zSign = s.scale.z < 0f ? -1f : 1f;
            bool flipWinding = (s.scale.x * s.scale.y * s.scale.z < 0f) ^ (splineToLocal.determinant < 0f);
            bool conform = s.conform;
            if (conform) Physics.SyncTransforms();

            int baseVertex = 0;
            for (int si = 0; si < count; si++)
            {
                SplineSampler sm = samplers[si];
                int n = reps[si];
                if (sm == null || n == 0) continue;

                float sl = segLen[si];
                float along = (sl / meshLen) * zSign; // stretch factor along the spline (signed)

                for (int r = 0; r < n; r++)
                {
                    for (int v = 0; v < vc; v++)
                    {
                        float u = (vAlong[v] - extents.alongMin) / meshLen;
                        if (zSign < 0f) u = 1f - u;
                        float d = (r + u) * sl;

                        sm.Evaluate(d, out SplineSampler.Frame f);

                        float sx = Nonzero(s.scale.x * f.scale);
                        float sy = Nonzero(s.scale.y * f.scale);

                        // position
                        Vector3 basePos = f.position + f.right * (vSide[v] * sx);
                        Vector3 local;
                        if (conform)
                        {
                            Vector3 lb = splineToLocal.MultiplyPoint3x4(basePos);
                            Vector3 wb = localToWorld.MultiplyPoint3x4(lb);
                            if (Physics.Raycast(wb + Vector3.up * s.conformRayHeight, Vector3.down, out RaycastHit hit,
                                    s.conformRayHeight + s.conformMaxDistance, s.conformMask, QueryTriggerInteraction.Ignore))
                            {
                                wb.y = hit.point.y + s.conformOffset;
                                lb = worldToLocal.MultiplyPoint3x4(wb);
                            }
                            local = lb + splineToLocal.MultiplyVector(f.up) * (vUp[v] * sy);
                        }
                        else
                        {
                            local = splineToLocal.MultiplyPoint3x4(basePos + f.up * (vUp[v] * sy));
                        }
                        oVerts.Add(local);

                        // normal (inverse-transpose of the local stretch, then rotated into the frame)
                        if (oNormals != null)
                        {
                            Vector3 nv = f.right * (nS[v] / sx) + f.up * (nU[v] / sy) + f.tangent * (nA[v] / Nonzero(along));
                            oNormals.Add(normalM.MultiplyVector(nv).normalized);
                        }

                        // tangent
                        if (oTangents != null)
                        {
                            Vector3 tv = f.right * (tS[v] * sx) + f.up * (tU[v] * sy) + f.tangent * (tA[v] * along);
                            tv = splineToLocal.MultiplyVector(tv).normalized;
                            oTangents.Add(new Vector4(tv.x, tv.y, tv.z, flipWinding ? -st[v].w : st[v].w));
                        }

                        if (oUV != null)
                        {
                            Vector2 uv = suv[v];
                            oUV.Add(new Vector2(uv.x * s.uvScale.x + s.uvOffset.x, uv.y * s.uvScale.y + s.uvOffset.y));
                        }

                        if (oColors != null)
                        {
                            Color c = hasC ? sc[v] : Color.white;
                            oColors.Add(s.useVertexColors ? c * f.color : c);
                        }
                    }

                    // indices
                    for (int j = 0; j < subCount; j++)
                    {
                        int[] idx = subIdx[j];
                        List<int> dst = oIdx[j];
                        int off = baseVertex + r * vc;
                        if (flipWinding)
                        {
                            for (int t = 0; t + 2 < idx.Length; t += 3)
                            {
                                dst.Add(off + idx[t]); dst.Add(off + idx[t + 2]); dst.Add(off + idx[t + 1]);
                            }
                        }
                        else
                        {
                            for (int t = 0; t < idx.Length; t++) dst.Add(off + idx[t]);
                        }
                    }
                }

                baseVertex += n * vc;
            }

            // ---- upload
            if (Application.isPlaying) output.MarkDynamic();
            output.indexFormat = total > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            output.SetVertices(oVerts);
            if (oNormals != null) output.SetNormals(oNormals);
            if (oTangents != null) output.SetTangents(oTangents);
            if (oUV != null) output.SetUVs(0, oUV);
            if (oColors != null) output.SetColors(oColors);

            output.subMeshCount = subCount;
            for (int j = 0; j < subCount; j++) output.SetTriangles(oIdx[j], j, false);
            output.RecalculateBounds();

            if (s.normalMode == NormalMode.Recalculate || !hasN)
            {
                output.RecalculateNormals();
                if (hasT && hasUV) output.RecalculateTangents();
            }

#if UNITY_EDITOR
            if (s.generateLightmapUVs && !Application.isPlaying)
                UnityEditor.Unwrapping.GenerateSecondaryUVSet(output);
#endif
            return total;
        }
        public static float GetCoveredLength(float splineLength, MeshExtents extents, Settings s)
        {
            if (splineLength < 1e-4f) return 0f;
            if (s.repeatMode != RepeatMode.RepeatFixed) return splineLength;

            float nominal = Mathf.Max(1e-5f, extents.AlongLength * Mathf.Abs(s.scale.z));
            int n = Mathf.Max(1, Mathf.FloorToInt(splineLength / nominal));
            n = Mathf.Min(n, Mathf.Max(1, s.maxRepeats));
            return n * nominal;
        }
        static float Nonzero(float v)
        {
            if (Mathf.Abs(v) >= 1e-5f) return v;
            return v < 0f ? -1e-5f : 1e-5f;
        }
    }
}
