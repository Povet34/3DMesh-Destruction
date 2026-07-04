# Changelog

## [1.0.0] - 2026-07-04

### Added
- `MeshSlicer` component: single-plane, Voronoi (random/fixed), radial, clustered, splinter fracture.
- Loop-based cap filling with ear clipping — correct cuts for non-convex meshes and meshes with holes.
- `Slice & Save Prefab` editor button: bakes chunk meshes (sub-assets) + fractured prefab to `Assets/FracturedCache/`.
- `FracturedSwap` runtime component: swap intact model for baked fractured prefab with explosion force.
- Edit Mode test suite (volume conservation, cap loop reconstruction, ear clipping, seed generation).
- `Basic Destruction` sample.
