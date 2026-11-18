# My Spline Mesher

Repeats and deforms a mesh along a Unity spline. Think roads, fences, pipes, cables and paths.
Built on Unity's native Splines package (`com.unity.splines`), so it combines with the other spline tools and your own code.

## Install
Copy the `MySplineMesher` folder into your project's `Packages` folder (embedded package).
Requires Unity 2022.3+ and Splines 2.x (installed automatically as a dependency).

## Quick start
1. `GameObject > Spline Mesher > Spline Mesher`, or right-click a **Spline Container** > **Add Spline Mesher**.
2. Assign a **Source Mesh** (enable *Read/Write* in its import settings).
3. Edit the spline with Unity's spline tools. The mesh follows live.
4. Try the demo: `GameObject > Spline Mesher > Demo > Road / Fence`.

## Features
- One continuous mesh, no GameObject clutter
- Repeat modes: fit (whole copies stretched to fill), fixed length, or stretch a single copy
- Forward axis selection, flip, per-axis scale (negative values mirror)
- Per-point **Roll**, **Scale** and **Vertex Color**, with scene tools
- Prefab **start / end caps**
- **Follow Ground**: snaps the spline path onto the terrain so nothing floats; optional per-vertex **Conform** on top
- Automatic **box** or **mesh** colliders
- UV scale/offset, multiple materials (sub-meshes), multiple splines per container
- Lightmap UV generation (editor), seamless normals
- Rebuild events: `onRebuilt` (UnityEvent) and `Rebuilt` (C# event)
- Runtime generation: modify the spline and the mesh updates at the end of the frame

## Scripting
```csharp
var mesher = GetComponent<SplineMesher>();
mesher.Settings.scale = new Vector3(2f, 1f, 1f);   // widen
mesher.SetDirty();                                  // rebuild at end of frame (or mesher.Rebuild())
mesher.Rebuilt += m => Debug.Log("Rebuilt: " + m.VertexCount + " vertices");

var p = mesher.GetPointData(0, 2);                  // spline 0, knot 2
p.roll = 30f; p.scale = 1.5f; p.color = Color.red;
mesher.SetDirty();

if (mesher.TryEvaluate(0, 12.5f, out var pos, out var rot)) { /* place something 12.5 m along the spline */ }
```

## Limitations
- Meshes are saved in the scene. To use one in a prefab, export it to FBX (Unity's FBX Exporter package).
- Regenerating complex geometry every frame (animated splines) can be slow.
- Non-uniformly scaled spline containers are not supported.
- Per-point data is matched to knots by index; inserting a knot shifts the data after it.
- Box colliders do not follow ground conforming; use the Mesh collider mode for that.
- End caps are placed on the first spline of the container only.
- The source mesh needs enough edge loops along its length to bend smoothly.

## Folder layout
```
Editor/    inspector, scene tools, help window, menus
Runtime/   SplineMesher component, generator, settings
_Demo/     procedural road and fence demo (editor only)
```
