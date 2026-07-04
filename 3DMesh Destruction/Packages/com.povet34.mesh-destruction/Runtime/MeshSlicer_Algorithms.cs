using UnityEngine;
using System.Collections.Generic;

namespace Povet.MeshDestruction
{
    public partial class MeshSlicer : MonoBehaviour
    {
        private void SliceSinglePlane()
        {
            // (이전 SinglePlane 로직과 동일)
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter.sharedMesh == null) return;

            Vector3 localNormal = planeRotation * Vector3.up;
            Plane plane = new Plane(localNormal, planePosition);

            if (PerformSlice(filter.sharedMesh, plane, out MeshData posData, out MeshData negData))
            {
                CreateSlicedObject(gameObject.name + "_Positive", posData);
                CreateSlicedObject(gameObject.name + "_Negative", negData);
                gameObject.SetActive(false);
            }
        }

        public List<Vector3> GenerateUniformSeeds(MeshFilter filter, int seedCount)
        {
            Bounds bounds = filter.sharedMesh.bounds;
            List<Vector3> seeds = new List<Vector3>();
            for (int i = 0; i < seedCount; i++)
            {
                seeds.Add(new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(bounds.min.y, bounds.max.y),
                    Random.Range(bounds.min.z, bounds.max.z)
                ));
            }
            return seeds;
        }

        public List<Vector3> GenerateRadialSeeds(MeshFilter filter, int rings, int rays)
        {
            Bounds bounds = filter.sharedMesh.bounds;
            List<Vector3> seeds = new List<Vector3>();
            seeds.Add(impactPoint);

            float maxRadius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);

            for (int i = 1; i <= rings; i++)
            {
                float currentRadius = (maxRadius / rings) * i;
                for (int j = 0; j < rays; j++)
                {
                    Vector3 dir = Random.onUnitSphere;
                    float jitterRadius = currentRadius + Random.Range(-0.1f, 0.1f);
                    Vector3 pos = impactPoint + dir * jitterRadius;

                    pos.x = Mathf.Clamp(pos.x, bounds.min.x, bounds.max.x);
                    pos.y = Mathf.Clamp(pos.y, bounds.min.y, bounds.max.y);
                    pos.z = Mathf.Clamp(pos.z, bounds.min.z, bounds.max.z);
                    seeds.Add(pos);
                }
            }
            return seeds;
        }

        public List<Vector3> GenerateClusteredSeeds(MeshFilter filter, int clusters, int seedsPerCluster)
        {
            Bounds bounds = filter.sharedMesh.bounds;
            List<Vector3> seeds = new List<Vector3>();

            for (int i = 0; i < clusters; i++)
            {
                Vector3 clusterCenter = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(bounds.min.y, bounds.max.y),
                    Random.Range(bounds.min.z, bounds.max.z)
                );
                seeds.Add(clusterCenter);

                for (int j = 0; j < seedsPerCluster; j++)
                {
                    Vector3 childSeed = clusterCenter + Random.insideUnitSphere * clusterRadius;
                    childSeed.x = Mathf.Clamp(childSeed.x, bounds.min.x, bounds.max.x);
                    childSeed.y = Mathf.Clamp(childSeed.y, bounds.min.y, bounds.max.y);
                    childSeed.z = Mathf.Clamp(childSeed.z, bounds.min.z, bounds.max.z);
                    seeds.Add(childSeed);
                }
            }
            return seeds;
        }

        public List<Vector3> GenerateSplinterSeeds(MeshFilter filter, int seedCount)
        {
            Bounds bounds = filter.sharedMesh.bounds;
            List<Vector3> seeds = new List<Vector3>();

            for (int i = 0; i < seedCount; i++)
            {
                Vector3 pos = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    Mathf.Lerp(bounds.center.y, Random.Range(bounds.min.y, bounds.max.y), splinterSpread),
                    Random.Range(bounds.min.z, bounds.max.z)
                );
                seeds.Add(pos);
            }
            return seeds;
        }

        // --- 코어 실행 함수도 특정 MeshFilter를 타겟으로 잡도록 수정됨 ---
        private void ExecuteVoronoi(MeshFilter filter, List<Vector3> seeds)
        {
            List<Mesh> finalChunks = new List<Mesh>();

            for (int i = 0; i < seeds.Count; i++)
            {
                Mesh currentChunk = filter.sharedMesh;
                Vector3 currentSeed = seeds[i];
                bool chunkSurvived = true;

                for (int j = 0; j < seeds.Count; j++)
                {
                    if (i == j) continue;

                    Vector3 otherSeed = seeds[j];
                    Vector3 midPoint = (currentSeed + otherSeed) * 0.5f;
                    Vector3 normal = (otherSeed - currentSeed).normalized;
                    Plane slicePlane = new Plane(normal, midPoint);

                    if (PerformSlice(currentChunk, slicePlane, out MeshData posData, out MeshData negData))
                    {
                        if (negData.vertices.Count > 0)
                            currentChunk = negData.ToMesh();
                        else
                        {
                            chunkSurvived = false;
                            break;
                        }
                    }
                }

                if (chunkSurvived && currentChunk.vertexCount > 0)
                    finalChunks.Add(currentChunk);
            }

            for (int i = 0; i < finalChunks.Count; i++)
            {
                MeshData data = new MeshData();
                data.vertices = new List<Vector3>(finalChunks[i].vertices);
                data.normals = new List<Vector3>(finalChunks[i].normals);
                data.uvs = new List<Vector2>(finalChunks[i].uv);
                data.triangles = new List<int>(finalChunks[i].triangles);

                // 파편 이름에 원본 자식 오브젝트의 이름을 포함시켜 디버깅을 용이하게 함
                CreateSlicedObject(filter.gameObject.name + "_Chunk_" + i, data, filter);
            }
        }

        private void SliceSinglePlane(MeshFilter filter)
        {
            Vector3 localNormal = planeRotation * Vector3.up;
            Plane plane = new Plane(localNormal, planePosition);

            if (PerformSlice(filter.sharedMesh, plane, out MeshData posData, out MeshData negData))
            {
                CreateSlicedObject(filter.gameObject.name + "_Positive", posData, filter);
                CreateSlicedObject(filter.gameObject.name + "_Negative", negData, filter);
            }
        }

        // CreateSlicedObject도 원본 filter의 트랜스폼과 머티리얼을 따라가도록 수정
        public void CreateSlicedObject(string name, MeshData data, MeshFilter originalFilter)
        {
            if (data.vertices.Count == 0) return;

            GameObject go = new GameObject(name);
            go.transform.position = originalFilter.transform.position;
            go.transform.rotation = originalFilter.transform.rotation;
            go.transform.localScale = originalFilter.transform.lossyScale;
            go.transform.SetParent(chunkParent != null ? chunkParent : this.transform.parent, true);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = originalFilter.GetComponent<MeshRenderer>().sharedMaterial;

            Mesh mesh = data.ToMesh();
            filter.sharedMesh = mesh;

            // Rigidbody 모드가 아니면 물리 컴포넌트를 붙이지 않음
            // (DebrisBurst 모드는 컨테이너의 DebrisBurst가 트랜스폼 적분으로 구동)
            if (physicsMode != ChunkPhysicsMode.Rigidbody) return;

            switch (colliderType)
            {
                case ChunkColliderType.Box:
                    BoxCollider box = go.AddComponent<BoxCollider>();
                    box.center = mesh.bounds.center;
                    // bounds.size에 colliderScale을 곱하여 AABB의 크기를 최소한으로 줄임
                    box.size = mesh.bounds.size * colliderScale;
                    break;

                case ChunkColliderType.Sphere:
                    SphereCollider sphere = go.AddComponent<SphereCollider>();
                    sphere.center = mesh.bounds.center;
                    // X, Y, Z 중 가장 긴 축을 기준으로 반지름을 구하고 스케일을 곱함
                    float maxExtent = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.y, mesh.bounds.extents.z);
                    sphere.radius = maxExtent * colliderScale;
                    break;

                case ChunkColliderType.MeshCollider:
                    MeshCollider mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mesh;
                    mc.convex = true;
                    // MeshCollider는 메쉬 버텍스를 그대로 쓰므로 colliderScale 적용이 불가능함
                    break;
            }

            if (addRigidbody)
            {
                Rigidbody rb = go.AddComponent<Rigidbody>();
                float volume = mesh.bounds.size.x * mesh.bounds.size.y * mesh.bounds.size.z;
                rb.mass = Mathf.Clamp(volume * 10f, 0.1f, 10f);
            }
        }
    }
}
