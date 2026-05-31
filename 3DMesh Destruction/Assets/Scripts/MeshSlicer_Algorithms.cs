using UnityEngine;
using System.Collections.Generic;

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

        // 1. 기본 보로노이 (Uniform)
    public List<Vector3> GenerateUniformSeeds()
    {
        Bounds bounds = GetComponent<MeshFilter>().sharedMesh.bounds;
        List<Vector3> seeds = new List<Vector3>();
        for (int i = 0; i < voronoiSeedCount; i++)
        {
            seeds.Add(new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            ));
        }
        return seeds;
    }

    // 2. 방사형 파괴 (Radial) - 삼각함수를 이용한 동심원 배치
    public List<Vector3> GenerateRadialSeeds()
    {
        Bounds bounds = GetComponent<MeshFilter>().sharedMesh.bounds;
        List<Vector3> seeds = new List<Vector3>();
        
        seeds.Add(impactPoint); // 정중앙 타격점

        // 3D 공간이므로 X, Y, Z 중 가장 긴 축을 기준으로 삼음
        float maxRadius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);

        for (int i = 1; i <= radialRings; i++)
        {
            float currentRadius = (maxRadius / radialRings) * i;
            for (int j = 0; j < radialRays; j++)
            {
                // XZ 평면이 아닌, 3D 구(Sphere) 표면 방향으로 무작위 벡터 생성
                Vector3 dir = Random.onUnitSphere; 
                
                float jitterRadius = currentRadius + Random.Range(-0.1f, 0.1f);
                Vector3 pos = impactPoint + dir * jitterRadius;

                // 바운딩 박스를 벗어나지 않도록 클램핑
                pos.x = Mathf.Clamp(pos.x, bounds.min.x, bounds.max.x);
                pos.y = Mathf.Clamp(pos.y, bounds.min.y, bounds.max.y);
                pos.z = Mathf.Clamp(pos.z, bounds.min.z, bounds.max.z);

                seeds.Add(pos);
            }
        }
        return seeds;
    }

    // 3. 클러스터 파괴 (Clustered) - 구체 내부 랜덤 배치
    public List<Vector3> GenerateClusteredSeeds()
    {
        Bounds bounds = GetComponent<MeshFilter>().sharedMesh.bounds;
        List<Vector3> seeds = new List<Vector3>();

        for (int i = 0; i < clusterCount; i++)
        {
            // 부모 시드 (큰 덩어리의 중심)
            Vector3 clusterCenter = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );
            seeds.Add(clusterCenter);

            // 자식 시드 (부모 주변에 뭉쳐있는 자잘한 파편들)
            for (int j = 0; j < seedsPerCluster; j++)
            {
                Vector3 childSeed = clusterCenter + Random.insideUnitSphere * clusterRadius;
                // 바운딩 박스를 벗어나지 않도록 클램핑
                childSeed.x = Mathf.Clamp(childSeed.x, bounds.min.x, bounds.max.x);
                childSeed.y = Mathf.Clamp(childSeed.y, bounds.min.y, bounds.max.y);
                childSeed.z = Mathf.Clamp(childSeed.z, bounds.min.z, bounds.max.z);
                seeds.Add(childSeed);
            }
        }
        return seeds;
    }

    // 4. 스플린터 파괴 (Splinter) - 특정 축 압축
    public List<Vector3> GenerateSplinterSeeds()
    {
        Bounds bounds = GetComponent<MeshFilter>().sharedMesh.bounds;
        List<Vector3> seeds = new List<Vector3>();

        for (int i = 0; i < voronoiSeedCount; i++)
        {
            // 나뭇결(세로로 긴 파편)이 되려면, 자르는 면이 세로로 서야 함.
            // 즉, 시드들은 XZ 평면으로 넓게 퍼지고, Y축으로는 높이 차이가 거의 없어야 함.
            Vector3 pos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                // Y축을 중심부로 강하게 압축 (splinterSpread가 0에 가까울수록 더 길쭉해짐)
                Mathf.Lerp(bounds.center.y, Random.Range(bounds.min.y, bounds.max.y), splinterSpread), 
                Random.Range(bounds.min.z, bounds.max.z)
            );
            seeds.Add(pos);
        }
        return seeds;
    }

    // 공통 코어 로직 (매개변수로 seeds 리스트를 받도록 수정됨)
    private void ExecuteVoronoi(List<Vector3> seeds)
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter.sharedMesh == null) return;

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
                    {
                        currentChunk = negData.ToMesh();
                    }
                    else
                    {
                        chunkSurvived = false;
                        break;
                    }
                }
            }

            if (chunkSurvived && currentChunk.vertexCount > 0)
            {
                finalChunks.Add(currentChunk);
            }
        }

        for (int i = 0; i < finalChunks.Count; i++)
        {
            MeshData data = new MeshData();
            data.vertices = new List<Vector3>(finalChunks[i].vertices);
            data.normals = new List<Vector3>(finalChunks[i].normals);
            data.uvs = new List<Vector2>(finalChunks[i].uv);
            data.triangles = new List<int>(finalChunks[i].triangles);

            CreateSlicedObject(gameObject.name + "_Chunk_" + i, data);
        }

        gameObject.SetActive(false);
    }
}