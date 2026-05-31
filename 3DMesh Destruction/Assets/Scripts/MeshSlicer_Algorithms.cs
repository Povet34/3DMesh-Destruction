using UnityEngine;
using System.Collections.Generic;

public partial class MeshSlicer
{
    private void SliceSinglePlane()
    {
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
        else
        {
            Debug.LogWarning("평면이 메쉬를 관통하지 않습니다.");
        }
    }

    private void SliceVoronoi()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter.sharedMesh == null) return;

        Bounds bounds = filter.sharedMesh.bounds;
        List<Vector3> seeds = new List<Vector3>();

        // 1. 바운딩 박스 내부에 무작위 시드(Seed) 생성
        for (int i = 0; i < voronoiSeedCount; i++)
        {
            seeds.Add(new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            ));
        }

        List<Mesh> finalChunks = new List<Mesh>();

        // 2. 각 시드별로 보로노이 셀(Cell) 깎아내기
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
                
                // currentSeed에서 otherSeed를 향하는 방향이 노멀
                Vector3 normal = (otherSeed - currentSeed).normalized;
                Plane slicePlane = new Plane(normal, midPoint);

                // 메쉬를 자름
                if (PerformSlice(currentChunk, slicePlane, out MeshData posData, out MeshData negData))
                {
                    // normal이 otherSeed를 향하므로, currentSeed는 평면의 반대쪽(Negative)에 남음
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

        // 3. 깎여나간 최종 파편들을 씬에 생성
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