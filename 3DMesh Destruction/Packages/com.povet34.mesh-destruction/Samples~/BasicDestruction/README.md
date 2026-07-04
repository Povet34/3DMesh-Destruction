# Basic Destruction Sample

1. Open `BasicDestruction.unity`.
2. Enter Play mode.
3. Click the wall — the intact wall is swapped for the pre-baked fractured prefab (`Wall_Fractured.prefab`) and the chunks are blown away.

What to look at:

- **Wall** has a `FracturedSwap` component pointing at the baked prefab.
- **Main Camera** has the `ExplodeOnClick` demo script (raycasts the click and calls `FracturedSwap.Explode(hit.point)`).
- `Wall_Meshes.asset` holds all 14 chunk meshes as sub-assets — this is what `Slice & Save Prefab` produces.

To re-bake with different settings: add a `MeshSlicer` to any mesh, press **Find All Child Meshes**, tune the fracture pattern, then **Slice & Save Prefab**.
