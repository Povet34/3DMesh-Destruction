using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Povet.MeshDestruction;

namespace Povet.MeshDestruction.Tests
{
    // 절단면(캡)이 제대로 만들어졌는지 검증하는 테스트 모음.
    // 핵심 검사 두 가지:
    //  1) 수밀성(watertight): 절단 결과의 모든 에지가 정확히 2개의 삼각형에 공유되어야 함 (구멍/뒤집힘/누락 캡 검출)
    //  2) 부피 보존: 두 조각의 부피 합 == 원본 부피 (캡이 잘못 채워지면 어긋남)
    public class CapIntegrityTests
    {
        private GameObject host;
        private MeshSlicer slicer;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("CapTestHost");
            host.AddComponent<MeshFilter>();
            host.AddComponent<MeshRenderer>();
            slicer = host.AddComponent<MeshSlicer>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
                if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
            if (host != null) Object.DestroyImmediate(host);
        }

        // ---------- 헬퍼 ----------

        private Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            spawned.Add(go);
            return go.GetComponent<MeshFilter>().sharedMesh;
        }

        private static float MeshVolume(List<Vector3> verts, List<int> tris)
        {
            float volume = 0f;
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]];
                Vector3 b = verts[tris[i + 1]];
                Vector3 c = verts[tris[i + 2]];
                volume += Vector3.Dot(a, Vector3.Cross(b, c));
            }
            return volume / 6f;
        }

        private static float MeshVolume(MeshSlicer.MeshData data) => MeshVolume(data.vertices, data.triangles);

        private static float MeshVolume(Mesh mesh)
        {
            return MeshVolume(new List<Vector3>(mesh.vertices), new List<int>(mesh.triangles));
        }

        // 위치 기준으로 버텍스를 용접한 뒤, 모든 에지가 정확히 2개의 삼각형에 공유되는지 검사.
        // 닫힌 다양체(watertight manifold)가 아니면 실패 메시지 반환, 정상이면 null.
        private static string CheckWatertight(List<Vector3> verts, List<int> tris, string label)
        {
            // 스케일 비례 용접 셀
            Vector3 min = verts[0], max = verts[0];
            foreach (var v in verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
            float cell = Mathf.Max((max - min).magnitude * 1e-5f, 1e-7f);

            var lookup = new Dictionary<Vector3Int, int>();
            var points = new List<Vector3>();
            int WeldId(Vector3 p)
            {
                var baseKey = new Vector3Int(
                    Mathf.RoundToInt(p.x / cell), Mathf.RoundToInt(p.y / cell), Mathf.RoundToInt(p.z / cell));
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var key = new Vector3Int(baseKey.x + dx, baseKey.y + dy, baseKey.z + dz);
                    if (lookup.TryGetValue(key, out int id) && (points[id] - p).sqrMagnitude < cell * cell)
                        return id;
                }
                int newId = points.Count;
                points.Add(p);
                if (!lookup.ContainsKey(baseKey)) lookup[baseKey] = newId;
                return newId;
            }

            var edgeCount = new Dictionary<long, int>();
            long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = WeldId(verts[tris[i]]);
                int b = WeldId(verts[tris[i + 1]]);
                int c = WeldId(verts[tris[i + 2]]);
                if (a == b || b == c || c == a) continue; // 용접 후 퇴화 삼각형은 닫힘에 영향 없음

                foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
                {
                    long key = EdgeKey(p, q);
                    edgeCount.TryGetValue(key, out int count);
                    edgeCount[key] = count + 1;
                }
            }

            int bad = 0;
            foreach (var kvp in edgeCount)
                if (kvp.Value != 2) bad++;

            return bad == 0 ? null : $"{label}: 공유 삼각형이 2개가 아닌 에지가 {bad}개 있음 (전체 {edgeCount.Count}개) — 캡 누락/중복/뒤집힘 의심";
        }

        private static string CheckWatertight(MeshSlicer.MeshData data, string label)
            => CheckWatertight(data.vertices, data.triangles, label);

        // 가운데 구멍 뚫린 사각 링 (오목 + 다중 루프 단면용)
        private static Mesh CreateRingMesh(float outer, float inner, float h)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }

            AddQuad(new Vector3(outer, -h, -outer), new Vector3(outer, h, -outer), new Vector3(outer, h, outer), new Vector3(outer, -h, outer));
            AddQuad(new Vector3(-outer, -h, outer), new Vector3(-outer, h, outer), new Vector3(-outer, h, -outer), new Vector3(-outer, -h, -outer));
            AddQuad(new Vector3(outer, -h, outer), new Vector3(outer, h, outer), new Vector3(-outer, h, outer), new Vector3(-outer, -h, outer));
            AddQuad(new Vector3(-outer, -h, -outer), new Vector3(-outer, h, -outer), new Vector3(outer, h, -outer), new Vector3(outer, -h, -outer));
            AddQuad(new Vector3(inner, -h, inner), new Vector3(inner, h, inner), new Vector3(inner, h, -inner), new Vector3(inner, -h, -inner));
            AddQuad(new Vector3(-inner, -h, -inner), new Vector3(-inner, h, -inner), new Vector3(-inner, h, inner), new Vector3(-inner, -h, inner));
            AddQuad(new Vector3(-inner, -h, inner), new Vector3(-inner, h, inner), new Vector3(inner, h, inner), new Vector3(inner, -h, inner));
            AddQuad(new Vector3(inner, -h, -inner), new Vector3(inner, h, -inner), new Vector3(-inner, h, -inner), new Vector3(-inner, -h, -inner));
            AddQuad(new Vector3(-outer, h, outer), new Vector3(outer, h, outer), new Vector3(inner, h, inner), new Vector3(-inner, h, inner));
            AddQuad(new Vector3(outer, h, -outer), new Vector3(-outer, h, -outer), new Vector3(-inner, h, -inner), new Vector3(inner, h, -inner));
            AddQuad(new Vector3(outer, h, outer), new Vector3(outer, h, -outer), new Vector3(inner, h, -inner), new Vector3(inner, h, inner));
            AddQuad(new Vector3(-outer, h, -outer), new Vector3(-outer, h, outer), new Vector3(-inner, h, inner), new Vector3(-inner, h, -inner));
            AddQuad(new Vector3(outer, -h, outer), new Vector3(-outer, -h, outer), new Vector3(-inner, -h, inner), new Vector3(inner, -h, inner));
            AddQuad(new Vector3(-outer, -h, -outer), new Vector3(outer, -h, -outer), new Vector3(inner, -h, -inner), new Vector3(-inner, -h, -inner));
            AddQuad(new Vector3(outer, -h, -outer), new Vector3(outer, -h, outer), new Vector3(inner, -h, inner), new Vector3(inner, -h, -inner));
            AddQuad(new Vector3(-outer, -h, outer), new Vector3(-outer, -h, -outer), new Vector3(-inner, -h, -inner), new Vector3(-inner, -h, inner));

            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, new List<Vector2>(new Vector2[verts.Count]));
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // 절단 → 수밀성 + 부피 보존 공통 검증
        private void AssertSliceIntegrity(Mesh source, Plane plane, string label)
        {
            // 원본 자체가 닫힌 메쉬인지 먼저 확인 (테스트 전제 검증)
            string srcCheck = CheckWatertight(new List<Vector3>(source.vertices), new List<int>(source.triangles), label + " 원본");
            Assert.IsNull(srcCheck, srcCheck);

            float sourceVolume = MeshVolume(source);

            bool intersected = slicer.PerformSlice(source, plane, out var pos, out var neg);
            Assert.IsTrue(intersected, $"{label}: 평면이 메쉬와 교차해야 함");
            Assert.Greater(pos.vertices.Count, 0, $"{label}: positive 조각이 비어있음");
            Assert.Greater(neg.vertices.Count, 0, $"{label}: negative 조각이 비어있음");

            string posCheck = CheckWatertight(pos, label + " positive");
            string negCheck = CheckWatertight(neg, label + " negative");
            Assert.IsNull(posCheck, posCheck);
            Assert.IsNull(negCheck, negCheck);

            float total = MeshVolume(pos) + MeshVolume(neg);
            Assert.AreEqual(sourceVolume, total, Mathf.Abs(sourceVolume) * 1e-3f,
                $"{label}: 부피 보존 실패 (원본 {sourceVolume:F6}, 절단 후 합 {total:F6})");
        }

        // ---------- 프리미티브 절단 ----------

        private static readonly PrimitiveType[] Primitives =
        {
            PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cylinder
        };

        [Test]
        public void Slice_Primitives_HorizontalCut_IsWatertight([ValueSource(nameof(Primitives))] PrimitiveType type)
        {
            Mesh mesh = GetPrimitiveMesh(type);
            AssertSliceIntegrity(mesh, new Plane(Vector3.up, new Vector3(0f, 0.1f, 0f)), $"{type} 수평");
        }

        [Test]
        public void Slice_Primitives_DiagonalCut_IsWatertight([ValueSource(nameof(Primitives))] PrimitiveType type)
        {
            Mesh mesh = GetPrimitiveMesh(type);
            AssertSliceIntegrity(mesh, new Plane(new Vector3(1f, 1f, 0.5f).normalized, new Vector3(0.05f, -0.05f, 0f)), $"{type} 대각");
        }

        // ---------- 구멍 뚫린 메쉬 절단 ----------

        [Test]
        public void Slice_HoledRing_HorizontalCut_IsWatertight()
        {
            AssertSliceIntegrity(CreateRingMesh(1f, 0.5f, 0.25f), new Plane(Vector3.up, Vector3.zero), "링 수평");
        }

        [Test]
        public void Slice_HoledRing_VerticalCut_IsWatertight()
        {
            AssertSliceIntegrity(CreateRingMesh(1f, 0.5f, 0.25f), new Plane(Vector3.right, new Vector3(0.1f, 0f, 0f)), "링 수직");
        }

        [Test]
        public void Slice_HoledRing_DiagonalCut_IsWatertight()
        {
            AssertSliceIntegrity(CreateRingMesh(1f, 0.5f, 0.25f), new Plane(new Vector3(1f, 0.7f, 0.3f).normalized, new Vector3(0f, 0.05f, 0f)), "링 대각");
        }

        // ---------- 연속 절단 (보로노이 파이프라인 시뮬레이션) ----------
        // 실제 파괴에서는 이전 절단의 캡을 포함한 조각을 또 자름. 캡 품질이 나쁘면 여기서 무너짐.

        [Test]
        public void SequentialSlices_Sphere_StaysWatertight()
        {
            Mesh current = GetPrimitiveMesh(PrimitiveType.Sphere);
            var planes = new[]
            {
                new Plane(Vector3.up, Vector3.zero),
                new Plane(Vector3.right, new Vector3(0.05f, 0f, 0f)),
                new Plane(new Vector3(1f, 1f, 0f).normalized, new Vector3(0f, -0.08f, 0f)),
                new Plane(new Vector3(0f, 1f, 1f).normalized, new Vector3(0f, 0.06f, 0.02f)),
            };

            for (int step = 0; step < planes.Length; step++)
            {
                float beforeVolume = MeshVolume(current);
                bool intersected = slicer.PerformSlice(current, planes[step], out var pos, out var neg);
                Assert.IsTrue(intersected, $"단계 {step}: 교차해야 함");

                string posCheck = CheckWatertight(pos, $"단계 {step} positive");
                string negCheck = CheckWatertight(neg, $"단계 {step} negative");
                Assert.IsNull(posCheck, posCheck);
                Assert.IsNull(negCheck, negCheck);

                float total = MeshVolume(pos) + MeshVolume(neg);
                Assert.AreEqual(beforeVolume, total, Mathf.Abs(beforeVolume) * 2e-3f,
                    $"단계 {step}: 부피 보존 실패 (이전 {beforeVolume:F6}, 합 {total:F6})");

                // 보로노이와 동일하게 negative 쪽을 다음 입력으로 사용
                current = neg.ToMesh();
            }
        }
    }
}
