using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Povet.MeshDestruction;

namespace Povet.MeshDestruction.Tests
{
    public class MeshSlicerTests
    {
        private GameObject go;
        private MeshSlicer slicer;

        [SetUp]
        public void SetUp()
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slicer = go.AddComponent<MeshSlicer>();
        }

        [TearDown]
        public void TearDown()
        {
            // 테스트 중 생성된 오브젝트 전부 정리
            foreach (var obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (obj != null && obj.transform.parent == null &&
                    (obj.name.Contains("_Chunk_") || obj.name.Contains("_Fractured") ||
                     obj.name.Contains("_Positive") || obj.name.Contains("_Negative") ||
                     obj.name.StartsWith("TestRing") || obj == go))
                {
                    Object.DestroyImmediate(obj);
                }
            }
        }

        // ---------- 헬퍼 ----------

        // 닫힌 메쉬의 부피 (signed volume 합)
        private static float MeshVolume(MeshSlicer.MeshData data)
        {
            float volume = 0f;
            for (int i = 0; i < data.triangles.Count; i += 3)
            {
                Vector3 a = data.vertices[data.triangles[i]];
                Vector3 b = data.vertices[data.triangles[i + 1]];
                Vector3 c = data.vertices[data.triangles[i + 2]];
                volume += Vector3.Dot(a, Vector3.Cross(b, c));
            }
            return volume / 6f;
        }

        private static float MeshVolume(Mesh mesh)
        {
            var data = new MeshSlicer.MeshData();
            data.vertices.AddRange(mesh.vertices);
            data.triangles.AddRange(mesh.triangles);
            return MeshVolume(data);
        }

        private static void AddQuad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        // 가운데 구멍이 뚫린 사각 링(도넛) 메쉬. 오목 형상 + 다중 캡 루프 테스트용.
        // outer: 바깥 반폭, inner: 구멍 반폭, h: 절반 높이. 부피 = (4*outer^2 - 4*inner^2) * 2h
        private static Mesh CreateRingMesh(float outer, float inner, float h)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            // 바깥 옆면 4개 (법선 바깥쪽)
            AddQuad(verts, tris, new Vector3(outer, -h, -outer), new Vector3(outer, h, -outer), new Vector3(outer, h, outer), new Vector3(outer, -h, outer));       // +X
            AddQuad(verts, tris, new Vector3(-outer, -h, outer), new Vector3(-outer, h, outer), new Vector3(-outer, h, -outer), new Vector3(-outer, -h, -outer));   // -X
            AddQuad(verts, tris, new Vector3(outer, -h, outer), new Vector3(outer, h, outer), new Vector3(-outer, h, outer), new Vector3(-outer, -h, outer));       // +Z
            AddQuad(verts, tris, new Vector3(-outer, -h, -outer), new Vector3(-outer, h, -outer), new Vector3(outer, h, -outer), new Vector3(outer, -h, -outer));   // -Z

            // 안쪽(구멍) 옆면 4개 (법선 구멍 안쪽 = 메쉬 바깥)
            AddQuad(verts, tris, new Vector3(inner, -h, inner), new Vector3(inner, h, inner), new Vector3(inner, h, -inner), new Vector3(inner, -h, -inner));       // 구멍 +X벽
            AddQuad(verts, tris, new Vector3(-inner, -h, -inner), new Vector3(-inner, h, -inner), new Vector3(-inner, h, inner), new Vector3(-inner, -h, inner));   // 구멍 -X벽
            AddQuad(verts, tris, new Vector3(-inner, -h, inner), new Vector3(-inner, h, inner), new Vector3(inner, h, inner), new Vector3(inner, -h, inner));       // 구멍 +Z벽
            AddQuad(verts, tris, new Vector3(inner, -h, -inner), new Vector3(inner, h, -inner), new Vector3(-inner, h, -inner), new Vector3(-inner, -h, -inner));   // 구멍 -Z벽

            // 윗면 4개 (법선 +Y)
            AddQuad(verts, tris, new Vector3(-outer, h, outer), new Vector3(outer, h, outer), new Vector3(inner, h, inner), new Vector3(-inner, h, inner));         // +Z 스트립
            AddQuad(verts, tris, new Vector3(outer, h, -outer), new Vector3(-outer, h, -outer), new Vector3(-inner, h, -inner), new Vector3(inner, h, -inner));     // -Z 스트립
            AddQuad(verts, tris, new Vector3(outer, h, outer), new Vector3(outer, h, -outer), new Vector3(inner, h, -inner), new Vector3(inner, h, inner));         // +X 스트립
            AddQuad(verts, tris, new Vector3(-outer, h, -outer), new Vector3(-outer, h, outer), new Vector3(-inner, h, inner), new Vector3(-inner, h, -inner));     // -X 스트립

            // 아랫면 4개 (법선 -Y)
            AddQuad(verts, tris, new Vector3(outer, -h, outer), new Vector3(-outer, -h, outer), new Vector3(-inner, -h, inner), new Vector3(inner, -h, inner));
            AddQuad(verts, tris, new Vector3(-outer, -h, -outer), new Vector3(outer, -h, -outer), new Vector3(inner, -h, -inner), new Vector3(-inner, -h, -inner));
            AddQuad(verts, tris, new Vector3(outer, -h, -outer), new Vector3(outer, -h, outer), new Vector3(inner, -h, inner), new Vector3(inner, -h, -inner));
            AddQuad(verts, tris, new Vector3(-outer, -h, outer), new Vector3(-outer, -h, -outer), new Vector3(-inner, -h, -inner), new Vector3(-inner, -h, inner));

            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, new List<Vector2>(new Vector2[verts.Count]));
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- PerformSlice 기본 동작 ----------

        [Test]
        public void PerformSlice_CubeCenterCut_ReturnsBothHalves()
        {
            Mesh cube = go.GetComponent<MeshFilter>().sharedMesh;
            var plane = new Plane(Vector3.up, Vector3.zero);

            bool result = slicer.PerformSlice(cube, plane, out var pos, out var neg);

            Assert.IsTrue(result, "중심을 지나는 평면은 교차해야 함");
            Assert.Greater(pos.vertices.Count, 0, "양쪽 절반이 모두 비어있으면 안 됨 (positive)");
            Assert.Greater(neg.vertices.Count, 0, "양쪽 절반이 모두 비어있으면 안 됨 (negative)");
        }

        [Test]
        public void PerformSlice_PlaneOutsideMesh_ReturnsFalse()
        {
            Mesh cube = go.GetComponent<MeshFilter>().sharedMesh;
            var plane = new Plane(Vector3.up, new Vector3(0f, 100f, 0f));

            bool result = slicer.PerformSlice(cube, plane, out _, out _);

            Assert.IsFalse(result, "메쉬 밖을 지나는 평면은 교차하지 않아야 함");
        }

        [Test]
        public void PerformSlice_CubeCenterCut_ConservesVolume()
        {
            Mesh cube = go.GetComponent<MeshFilter>().sharedMesh; // 1x1x1, 부피 1
            var plane = new Plane(new Vector3(0.3f, 1f, 0.2f).normalized, new Vector3(0f, 0.1f, 0f));

            slicer.PerformSlice(cube, plane, out var pos, out var neg);

            float total = MeshVolume(pos) + MeshVolume(neg);
            Assert.AreEqual(1f, total, 1e-3f, "절단 후 두 조각의 부피 합이 원본과 같아야 함 (캡이 닫혀있어야 성립)");
        }

        // ---------- 오목/다중 루프 캡 (핵심 회귀 테스트) ----------

        [Test]
        public void PerformSlice_RingHorizontalCut_ConservesVolume()
        {
            // 구멍 뚫린 링을 수평으로 자르면 단면이 바깥 루프 + 구멍 루프 두 개로 나뉨.
            // 캡이 구멍을 덮어버리면(예전 부채꼴 방식) 부피가 부풀어 이 테스트가 실패함.
            Mesh ring = CreateRingMesh(1f, 0.5f, 0.25f);
            float expected = (4f - 1f) * 0.5f; // 1.5
            Assert.AreEqual(expected, MeshVolume(ring), 1e-4f, "링 메쉬 자체 검증");

            var plane = new Plane(Vector3.up, Vector3.zero);
            bool result = slicer.PerformSlice(ring, plane, out var pos, out var neg);

            Assert.IsTrue(result);
            float total = MeshVolume(pos) + MeshVolume(neg);
            Assert.AreEqual(expected, total, 1e-3f, "구멍이 있는 단면에서도 부피가 보존되어야 함 (구멍을 캡으로 덮으면 안 됨)");
        }

        [Test]
        public void PerformSlice_RingVerticalCut_ConservesVolume()
        {
            // 수직으로 자르면 한쪽 단면에 분리된 사각형 루프가 2개 생김
            Mesh ring = CreateRingMesh(1f, 0.5f, 0.25f);
            float expected = 1.5f;

            var plane = new Plane(Vector3.right, Vector3.zero);
            bool result = slicer.PerformSlice(ring, plane, out var pos, out var neg);

            Assert.IsTrue(result);
            Assert.AreEqual(expected / 2f, MeshVolume(pos), 1e-3f, "대칭 절단이므로 각 절반은 원본의 절반");
            Assert.AreEqual(expected / 2f, MeshVolume(neg), 1e-3f, "대칭 절단이므로 각 절반은 원본의 절반");
        }

        // ---------- 캡 유틸 단위 테스트 ----------

        [Test]
        public void BuildCapLoops_TwoDisjointSquares_ReturnsTwoLoops()
        {
            var segments = new List<Vector3>();
            void AddSquare(float offset)
            {
                var corners = new[]
                {
                    new Vector3(offset, 0, 0), new Vector3(offset + 1, 0, 0),
                    new Vector3(offset + 1, 0, 1), new Vector3(offset, 0, 1)
                };
                for (int i = 0; i < 4; i++)
                {
                    segments.Add(corners[i]);
                    segments.Add(corners[(i + 1) % 4]);
                }
            }
            AddSquare(0f);
            AddSquare(5f);

            var loops = MeshSlicer.BuildCapLoops(segments);

            Assert.AreEqual(2, loops.Count, "떨어져 있는 두 사각형은 두 개의 루프로 복원되어야 함");
            Assert.AreEqual(4, loops[0].Count);
            Assert.AreEqual(4, loops[1].Count);
        }

        [Test]
        public void TriangulatePolygon_Square_ReturnsTwoTriangles()
        {
            var square = new List<Vector2>
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
            };

            var tris = MeshSlicer.TriangulatePolygon(square);

            Assert.AreEqual(6, tris.Count, "사각형은 삼각형 2개(인덱스 6개)로 나뉘어야 함");
        }

        [Test]
        public void TriangulatePolygon_ConcaveLShape_PreservesArea()
        {
            // L자 오목 다각형, 면적 = 3
            var lShape = new List<Vector2>
            {
                new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 1),
                new Vector2(1, 1), new Vector2(1, 2), new Vector2(0, 2)
            };

            var tris = MeshSlicer.TriangulatePolygon(lShape);

            Assert.AreEqual((lShape.Count - 2) * 3, tris.Count, "n각형은 n-2개의 삼각형으로 나뉘어야 함");

            float triArea = 0f;
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector2 a = lShape[tris[i]], b = lShape[tris[i + 1]], c = lShape[tris[i + 2]];
                triArea += Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * 0.5f;
            }
            Assert.AreEqual(3f, triArea, 1e-4f, "삼각화 결과의 총 면적이 원본 다각형 면적과 같아야 함");
        }

        // ---------- 시드 생성 ----------

        [Test]
        public void GenerateUniformSeeds_ReturnsRequestedCountInsideBounds()
        {
            var filter = go.GetComponent<MeshFilter>();
            var seeds = slicer.GenerateUniformSeeds(filter, 10);

            Assert.AreEqual(10, seeds.Count);
            Bounds bounds = filter.sharedMesh.bounds;
            foreach (var seed in seeds)
                Assert.IsTrue(bounds.Contains(seed), $"시드 {seed}가 메쉬 바운드 안에 있어야 함");
        }

        // ---------- Slice 통합 (컨테이너 생성) ----------

        [Test]
        public void Slice_Voronoi_CreatesFracturedContainerAndDisablesOriginal()
        {
            slicer.sliceMethod = SliceMethod.VoronoiFixedSeed;
            slicer.voronoiSeedCount = 5;
            slicer.randomSeed = 42;
            slicer.colliderType = ChunkColliderType.None;
            slicer.addRigidbody = false;
            slicer.childSettings.Add(new ChildSliceSetting
            {
                targetFilter = go.GetComponent<MeshFilter>(),
                enableSlice = true,
                sliceRatio = 1f
            });

            GameObject container = slicer.Slice();

            Assert.IsNotNull(container, "Slice는 파편 컨테이너를 반환해야 함");
            Assert.IsTrue(container.name.EndsWith("_Fractured"));
            Assert.Greater(container.transform.childCount, 1, "파편이 2개 이상 생성되어야 함");
            Assert.IsFalse(go.activeSelf, "원본은 비활성화되어야 함");

            Object.DestroyImmediate(container);
        }
    }
}
