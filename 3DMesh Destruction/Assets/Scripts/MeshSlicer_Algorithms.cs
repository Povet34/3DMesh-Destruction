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

    private void SliceVoronoiRandom()
    {
        // 현재 시간을 기반으로 완벽한 무작위 시드 생성
        Random.InitState((int)System.DateTime.Now.Ticks);
        ExecuteVoronoi();
    }

    private void SliceVoronoiFixedSeed()
    {
        // 사용자가 입력한 고정 시드 적용
        Random.InitState(randomSeed);
        ExecuteVoronoi();
    }

    // 보로노이 파괴 공통 코어 로직
    private void ExecuteVoronoi()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter.sharedMesh == null) return;

        Bounds bounds = filter.sharedMesh.bounds;
        List<Vector3> seeds = new List<Vector3>();

        for (int i = 0; i < voronoiSeedCount; i++)
        {
            seeds.Add(new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            ));
        }

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

            CreateSlicedObject(gameObject.name + "_VoronoiChunk_" + i, data);
        }

        gameObject.SetActive(false);
    }
}