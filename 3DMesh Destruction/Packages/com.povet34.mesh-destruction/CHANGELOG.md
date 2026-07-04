# Changelog

## [1.0.1] - 2026-07-04

### Fixed
- Sphere/curved-mesh caps rendered with striped or missing faces, especially after repeated (Voronoi) slicing.
  - Cap winding is now decided once per loop instead of per triangle (sliver cross products flipped randomly).
  - Convex cap loops (circular cross-sections) use centroid-fan triangulation — no more sliver triangles.
  - Ear clipping uses a rotating cursor and scale-relative epsilon.
  - Cap loop welding distance is now relative to mesh scale; unclosed chains are closed instead of dropped.

### Added
- Cap integrity test suite: watertightness (every edge shared by exactly 2 triangles) and volume conservation
  across primitives (cube/sphere/capsule/cylinder), holed meshes, and sequential Voronoi-style slicing.

## [1.0.0] - 2026-07-04

### Added
- `MeshSlicer` component: single-plane, Voronoi (random/fixed), radial, clustered, splinter fracture.
- Loop-based cap filling with ear clipping — correct cuts for non-convex meshes and meshes with holes.
- `Slice & Save Prefab` editor button: bakes chunk meshes (sub-assets) + fractured prefab to `Assets/FracturedCache/`.
- `FracturedSwap` runtime component: swap intact model for baked fractured prefab with explosion force.
- Edit Mode test suite (volume conservation, cap loop reconstruction, ear clipping, seed generation).
- `Basic Destruction` sample.
