# 3D Mesh Destruction

Mesh slicing & fracturing toolkit for Unity.

- **Fracture patterns**: Single plane, Voronoi (random / fixed seed), Radial (glass, crater), Clustered (chunky debris), Splinter (wood grain)
- **Non-convex cap filling**: cut cross-sections are reconstructed as closed loops and triangulated with ear clipping — meshes with holes (rings, torus-like shapes) are cut correctly without giant spanning triangles
- **Prefab baking**: fracture once in the editor, save all chunk meshes + prefab to `Assets/FracturedCache/`, and reuse at runtime
- **Runtime swap**: `FracturedSwap` component swaps the intact model for the pre-baked fractured prefab and applies explosion force
- **Two physics modes**: full `Rigidbody` physics per chunk, or `DebrisBurst` — collider-free transform integration with a logical ground plane (bounce + friction), near-zero physics cost

## Installation (UPM)

Open `Packages/manifest.json` and add:

```json
{
  "dependencies": {
    "com.povet34.mesh-destruction": "https://github.com/Povet34/3DMesh-Destruction.git#upm"
  }
}
```

Or via **Window > Package Manager > + > Add package from git URL...** with:

```
https://github.com/Povet34/3DMesh-Destruction.git#upm
```

The `upm` branch contains only the package (created with `git subtree split`).

## Quick Start

### 1. Bake a fractured prefab (editor)

1. Add `MeshSlicer` to the model you want to destroy.
2. Click **Find All Child Meshes** to register child meshes.
3. Pick a `SliceMethod` and tune seed counts. Scene view shows bounds/seed previews.
4. Click **Slice & Save Prefab** — chunk meshes and a `<name>_Fractured` prefab are saved under `Assets/FracturedCache/<name>/`.

### 2. Swap on explosion (runtime)

```csharp
using Povet.MeshDestruction;

var swap = gameObject.AddComponent<FracturedSwap>();
swap.fracturedPrefab = fracturedPrefab; // baked prefab
swap.explosionForce = 400f;
swap.Explode(hitPoint); // original is disabled, chunks fly
```

### 3. Collider-free debris (DebrisBurst)

Set `Physics Mode` to **DebrisBurst** on the `MeshSlicer` before baking — chunks get no colliders or rigidbodies, and the fractured container gets a `DebrisBurst` component with the settings you configured on the slicer. `FracturedSwap.Explode()` detects it automatically.

`DebrisBurst` also works standalone — add it to any object whose children have Renderers and call `Burst()`:

```csharp
using Povet.MeshDestruction;

var debris = fracturedRoot.AddComponent<DebrisBurst>();
debris.settings.impulse = 8f;
debris.settings.useGroundPlane = true;  // logical floor: pieces bounce, no colliders involved
debris.settings.bounciness = 0.4f;
debris.Burst(explosionCenter);
```

### 4. Slice purely in code

```csharp
using Povet.MeshDestruction;

var slicer = GetComponent<MeshSlicer>();
GameObject container = slicer.Slice(); // chunks are grouped under <name>_Fractured
```

## Samples

Import **Basic Destruction** from the Package Manager Samples tab: a pre-baked wall you can click to explode.

## Tests

Edit Mode tests are included (`Povet.MeshDestruction.Editor.Tests`). Add the package to `testables` in your project `manifest.json` to see them in the Test Runner:

```json
{
  "testables": ["com.povet34.mesh-destruction"]
}
```

Coverage includes volume-conservation checks that verify caps stay watertight, including multi-loop cross-sections on meshes with holes.

## License

MIT
