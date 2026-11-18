using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SC.Modeling.Splines.Editing;

namespace SC.Modeling.Splines.Demo
{
    /// <summary>
    /// Builds simple procedural source meshes (road slab, fence) and menu items that set up
    /// ready-to-use demo objects, so the tool can be tried without any art.
    /// </summary>
    public static class DemoMeshFactory
    {
        // ------------------------------------------------------------ menu

        [MenuItem("GameObject/Spline Mesher/Demo/Road", false, 30)]
        static void CreateRoad()
        {
            var mesher = SplineMeshEditor.CreateSplineMesher("Demo Road", BuildRoad());
            mesher.Settings.upMode = UpMode.WorldUp;
            mesher.Settings.colliderMode = ColliderMode.Boxes;
            Tint(mesher, new Color(0.25f, 0.25f, 0.27f));
            mesher.Rebuild();
        }

        [MenuItem("GameObject/Spline Mesher/Demo/Fence", false, 31)]
        static void CreateFence()
        {
            var mesher = SplineMeshEditor.CreateSplineMesher("Demo Fence", BuildFence());
            mesher.Settings.upMode = UpMode.WorldUp;
            Tint(mesher, new Color(0.55f, 0.38f, 0.22f));
            mesher.Rebuild();
        }

        static void Tint(SplineMesher mesher, Color color)
        {
            var r = mesher.GetComponent<MeshRenderer>();
            if (r.sharedMaterial == null) return;
            var mat = new Material(r.sharedMaterial) { name = "Demo Material" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            r.sharedMaterial = mat;
        }

        // ------------------------------------------------------------ meshes (length along Z)

        /// <summary>4 m long, 6 m wide, 0.2 m thick slab with 16 loops along its length.</summary>
        public static Mesh BuildRoad()
        {
            var b = new Builder();
            b.AddBox(new Vector3(0f, -0.1f, 0f), new Vector3(6f, 0.2f, 4f), 4, 1, 16);
            return b.ToMesh("Demo Road Segment");
        }

        /// <summary>2 m long fence section: one post in the middle and two continuous rails.</summary>
        public static Mesh BuildFence()
        {
            var b = new Builder();
            b.AddBox(new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 1f, 0.12f), 1, 1, 1);        // post
            b.AddBox(new Vector3(0f, 0.85f, 0f), new Vector3(0.05f, 0.1f, 2f), 1, 1, 8);        // upper rail
            b.AddBox(new Vector3(0f, 0.45f, 0f), new Vector3(0.05f, 0.1f, 2f), 1, 1, 8);        // lower rail
            return b.ToMesh("Demo Fence Segment");
        }

        // ------------------------------------------------------------ tiny mesh builder

        sealed class Builder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();

            // Adds a subdivided quad grid spanned by u and v, oriented so the front face looks along 'normal'.
            void AddGrid(Vector3 origin, Vector3 u, Vector3 v, int uSeg, int vSeg, Vector3 normal)
            {
                int start = verts.Count;
                for (int j = 0; j <= vSeg; j++)
                    for (int i = 0; i <= uSeg; i++)
                    {
                        float fu = i / (float)uSeg, fv = j / (float)vSeg;
                        verts.Add(origin + u * fu + v * fv);
                        normals.Add(normal);
                        uvs.Add(new Vector2(fu, fv));
                    }

                // Unity front faces are clockwise; the face normal is cross(b - a, c - a).
                bool flip = Vector3.Dot(Vector3.Cross(v, u), normal) < 0f;
                int row = uSeg + 1;
                for (int j = 0; j < vSeg; j++)
                    for (int i = 0; i < uSeg; i++)
                    {
                        int p00 = start + j * row + i, p10 = p00 + 1, p01 = p00 + row, p11 = p01 + 1;
                        if (!flip) { Tri(p00, p01, p11); Tri(p00, p11, p10); }
                        else       { Tri(p00, p11, p01); Tri(p00, p10, p11); }
                    }
            }

            void Tri(int a, int b, int c) { tris.Add(a); tris.Add(b); tris.Add(c); }

            // Box with segments along x, y, z. Only the length (z) subdivisions matter for bending.
            public void AddBox(Vector3 c, Vector3 size, int sx, int sy, int sz)
            {
                Vector3 h = size * 0.5f;
                Vector3 X = new Vector3(size.x, 0, 0), Y = new Vector3(0, size.y, 0), Z = new Vector3(0, 0, size.z);

                AddGrid(c + new Vector3(-h.x,  h.y, -h.z), X, Z, sx, sz, Vector3.up);       // top
                AddGrid(c + new Vector3(-h.x, -h.y, -h.z), X, Z, sx, sz, Vector3.down);     // bottom
                AddGrid(c + new Vector3( h.x, -h.y, -h.z), Y, Z, sy, sz, Vector3.right);    // +x
                AddGrid(c + new Vector3(-h.x, -h.y, -h.z), Y, Z, sy, sz, Vector3.left);     // -x
                AddGrid(c + new Vector3(-h.x, -h.y,  h.z), X, Y, sx, sy, Vector3.forward);  // +z
                AddGrid(c + new Vector3(-h.x, -h.y, -h.z), X, Y, sx, sy, Vector3.back);     // -z
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(verts);
                m.SetNormals(normals);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
