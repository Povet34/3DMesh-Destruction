using UnityEngine;
using System.Collections.Generic;

namespace Povet.MeshDestruction
{
    [System.Serializable]
    public class ChildSliceSetting
    {
        public MeshFilter targetFilter;
        public bool enableSlice = true;
        [Tooltip("마스터 설정값 대비 파편 개수 비율 (1 = 100%, 0.5 = 50%)")]
        [Range(0.1f, 5f)]
        public float sliceRatio = 1.0f;
    }

    public enum SliceMethod
    {
        SinglePlane,
        VoronoiRandom,
        VoronoiFixedSeed,
        Radial,      // 방사형 (유리창, 크레이터)
        Clustered,   // 군집형 (최적화용 덩어리 파괴)
        Splinter     // 나뭇결 (길쭉한 파괴)
    }

    public enum ChunkColliderType
    {
        None,
        Box,
        Sphere,
        MeshCollider
    }

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public partial class MeshSlicer : MonoBehaviour
    {
        [Header("Master Settings")]
        public SliceMethod sliceMethod = SliceMethod.SinglePlane;

        [Tooltip("모든 자식 메쉬의 파편 개수를 일괄 조절하는 글로벌 배율")]
        [Range(0.1f, 5f)]
        public float globalSliceRatio = 1.0f;

        [Header("Physics Settings")]
        public ChunkColliderType colliderType = ChunkColliderType.Box;

        [Tooltip("콜라이더의 크기 비율. 1보다 작게(예: 0.8) 설정해야 파편끼리 겹쳐서 폭발하는 현상을 막을 수 있음.")]
        [Range(0.1f, 1.0f)]
        public float colliderScale = 0.8f; // AABB를 축소시킬 비율

        public bool addMeshCollider = true;
        public bool addRigidbody = true;

        // Single Plane
        public Vector3 planePosition = Vector3.zero;
        public Quaternion planeRotation = Quaternion.identity;
        public Vector2 planeSize = new Vector2(2f, 2f);

        // Voronoi Common
        [Range(2, 50)] public int voronoiSeedCount = 5;
        public int randomSeed = 12345;

        // Radial Settings
        public Vector3 impactPoint = Vector3.zero;
        [Range(1, 5)] public int radialRings = 3;
        [Range(3, 15)] public int radialRays = 5;

        // Clustered Settings
        [Range(2, 10)] public int clusterCount = 3;
        [Range(2, 10)] public int seedsPerCluster = 4;
        public float clusterRadius = 0.5f;

        // Splinter Settings
        [Range(0f, 1f)] public float splinterSpread = 0.2f;

        // --- 새로 추가된 자식 메쉬 리스트 ---
        [Header("Hierarchy Settings")]
        public List<ChildSliceSetting> childSettings = new List<ChildSliceSetting>();

        // Slice 실행 중 파편들이 모일 컨테이너. 프리팹 저장/런타임 교체 단위가 됨.
        private Transform chunkParent;

        public GameObject Slice()
        {
            if (sliceMethod != SliceMethod.SinglePlane && sliceMethod != SliceMethod.VoronoiRandom)
                Random.InitState(randomSeed);
            else if (sliceMethod == SliceMethod.VoronoiRandom)
                Random.InitState((int)System.DateTime.Now.Ticks);

            // 파편을 담을 컨테이너를 원본과 동일한 월드 포즈로 생성.
            // 파편의 로컬 포즈가 원본 기준 상대값이 되므로, 런타임에 원본 위치에 Instantiate하면 그대로 맞음.
            GameObject container = new GameObject(gameObject.name + "_Fractured");
            container.transform.SetPositionAndRotation(transform.position, transform.rotation);
            container.transform.SetParent(transform.parent, true);
            chunkParent = container.transform;

            foreach (ChildSliceSetting setting in childSettings)
            {
                if (!setting.enableSlice || setting.targetFilter == null || setting.targetFilter.sharedMesh == null)
                    continue;

                // 자식 고유의 비율과 글로벌 비율을 곱하여 최종 비율 산출
                float finalRatio = setting.sliceRatio * globalSliceRatio;

                switch (sliceMethod)
                {
                    case SliceMethod.SinglePlane:
                        SliceSinglePlane(setting.targetFilter);
                        break;
                    case SliceMethod.VoronoiRandom:
                    case SliceMethod.VoronoiFixedSeed:
                        int effectiveSeeds = Mathf.Max(2, Mathf.RoundToInt(voronoiSeedCount * finalRatio));
                        ExecuteVoronoi(setting.targetFilter, GenerateUniformSeeds(setting.targetFilter, effectiveSeeds));
                        break;
                    case SliceMethod.Radial:
                        int effectiveRings = Mathf.Max(1, Mathf.RoundToInt(radialRings * finalRatio));
                        int effectiveRays = Mathf.Max(3, Mathf.RoundToInt(radialRays * finalRatio));
                        ExecuteVoronoi(setting.targetFilter, GenerateRadialSeeds(setting.targetFilter, effectiveRings, effectiveRays));
                        break;
                    case SliceMethod.Clustered:
                        int effectiveClusters = Mathf.Max(1, Mathf.RoundToInt(clusterCount * finalRatio));
                        int effectiveSeedsPerCluster = Mathf.Max(2, Mathf.RoundToInt(seedsPerCluster * finalRatio));
                        ExecuteVoronoi(setting.targetFilter, GenerateClusteredSeeds(setting.targetFilter, effectiveClusters, effectiveSeedsPerCluster));
                        break;
                    case SliceMethod.Splinter:
                        int effectiveSplinterSeeds = Mathf.Max(2, Mathf.RoundToInt(voronoiSeedCount * finalRatio));
                        ExecuteVoronoi(setting.targetFilter, GenerateSplinterSeeds(setting.targetFilter, effectiveSplinterSeeds));
                        break;
                }
            }

            chunkParent = null;

            // 모든 자식의 파괴가 끝난 후, 최상위 원본 오브젝트 자체를 비활성화함
            gameObject.SetActive(false);
            return container;
        }

        // 순수하게 메쉬 데이터와 평면을 받아 두 개의 MeshData로 쪼개주는 코어 수학 함수
        public bool PerformSlice(Mesh targetMesh, Plane plane, out MeshData positiveMesh, out MeshData negativeMesh)
        {
            positiveMesh = new MeshData();
            negativeMesh = new MeshData();
            List<Vector3> capVertices = new List<Vector3>();

            Vector3[] verts = targetMesh.vertices;
            Vector3[] norms = targetMesh.normals;
            Vector2[] uvs = targetMesh.uv;
            int[] tris = targetMesh.triangles;

            bool hasIntersection = false;

            for (int i = 0; i < tris.Length; i += 3)
            {
                int i1 = tris[i], i2 = tris[i + 1], i3 = tris[i + 2];
                Vector3 v1 = verts[i1], v2 = verts[i2], v3 = verts[i3];
                Vector2 uv1 = uvs[i1], uv2 = uvs[i2], uv3 = uvs[i3];
                Vector3 n1 = norms[i1], n2 = norms[i2], n3 = norms[i3];

                bool side1 = plane.GetSide(v1);
                bool side2 = plane.GetSide(v2);
                bool side3 = plane.GetSide(v3);

                if (side1 == side2 && side2 == side3)
                {
                    (side1 ? positiveMesh : negativeMesh).AddTriangle(v1, v2, v3, n1, n2, n3, uv1, uv2, uv3);
                }
                else
                {
                    hasIntersection = true;
                    Vector3 aloneV, pairV1, pairV2;
                    Vector2 aloneUV, pairUV1, pairUV2;
                    Vector3 aloneN, pairN1, pairN2;
                    bool aloneSide;

                    if (side1 != side2 && side1 != side3)
                    {
                        aloneV = v1; pairV1 = v2; pairV2 = v3;
                        aloneUV = uv1; pairUV1 = uv2; pairUV2 = uv3;
                        aloneN = n1; pairN1 = n2; pairN2 = n3; aloneSide = side1;
                    }
                    else if (side2 != side1 && side2 != side3)
                    {
                        aloneV = v2; pairV1 = v3; pairV2 = v1;
                        aloneUV = uv2; pairUV1 = uv3; pairUV2 = uv1;
                        aloneN = n2; pairN1 = n3; pairN2 = n1; aloneSide = side2;
                    }
                    else
                    {
                        aloneV = v3; pairV1 = v1; pairV2 = v2;
                        aloneUV = uv3; pairUV1 = uv1; pairUV2 = uv2;
                        aloneN = n3; pairN1 = n1; pairN2 = n2; aloneSide = side3;
                    }

                    float dAlone = plane.GetDistanceToPoint(aloneV);
                    float dPair1 = plane.GetDistanceToPoint(pairV1);
                    float dPair2 = plane.GetDistanceToPoint(pairV2);

                    float t1 = dAlone / (dAlone - dPair1);
                    float t2 = dAlone / (dAlone - dPair2);

                    Vector3 intersect1 = Vector3.Lerp(aloneV, pairV1, t1);
                    Vector3 intersect2 = Vector3.Lerp(aloneV, pairV2, t2);
                    Vector2 intersectUV1 = Vector2.Lerp(aloneUV, pairUV1, t1);
                    Vector2 intersectUV2 = Vector2.Lerp(aloneUV, pairUV2, t2);
                    Vector3 intersectN1 = Vector3.Lerp(aloneN, pairN1, t1);
                    Vector3 intersectN2 = Vector3.Lerp(aloneN, pairN2, t2);

                    capVertices.Add(intersect1);
                    capVertices.Add(intersect2);

                    MeshData aloneMesh = aloneSide ? positiveMesh : negativeMesh;
                    MeshData pairMesh = aloneSide ? negativeMesh : positiveMesh;

                    aloneMesh.AddTriangle(aloneV, intersect1, intersect2, aloneN, intersectN1, intersectN2, aloneUV, intersectUV1, intersectUV2);
                    pairMesh.AddTriangle(intersect1, pairV1, pairV2, intersectN1, pairN1, pairN2, intersectUV1, pairUV1, pairUV2);
                    pairMesh.AddTriangle(intersect1, pairV2, intersect2, intersectN1, pairN2, intersectN2, intersectUV1, pairUV2, intersectUV2);
                }
            }

            if (hasIntersection && capVertices.Count > 0)
            {
                FillCaps(capVertices, plane, positiveMesh, negativeMesh);
            }

            return hasIntersection;
        }

        // 절단면 채우기. 교차 세그먼트들을 연결해 닫힌 루프를 만들고, 루프별로 삼각화함.
        // 전체 평균점 하나로 부채꼴을 만들면 링처럼 오목한 메쉬에서 단면이 여러 루프로 나뉠 때
        // 빈 공간을 가로지르는 거대 삼각형이 생기므로, 반드시 루프 단위로 처리해야 함.
        internal static void FillCaps(List<Vector3> capSegments, Plane plane, MeshData positiveMesh, MeshData negativeMesh)
        {
            List<List<Vector3>> loops = BuildCapLoops(capSegments);
            if (loops.Count == 0) return;

            // 평면 위 2D 좌표계 기저
            Vector3 n = plane.normal;
            Vector3 axis = Mathf.Abs(n.y) < 0.99f ? Vector3.up : Vector3.right;
            Vector3 u = Vector3.Cross(n, axis).normalized;
            Vector3 v = Vector3.Cross(n, u);

            foreach (List<Vector3> loop in loops)
            {
                if (loop.Count < 3) continue;

                List<Vector2> polygon = new List<Vector2>(loop.Count);
                foreach (Vector3 p in loop)
                    polygon.Add(new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)));

                List<int> tris = TriangulatePolygon(polygon);

                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 a = loop[tris[i]];
                    Vector3 b = loop[tris[i + 1]];
                    Vector3 c = loop[tris[i + 2]];

                    // (a,b,c)가 +normal 방향을 바라보도록 정렬
                    if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f)
                    {
                        Vector3 tmp = b; b = c; c = tmp;
                    }

                    negativeMesh.AddTriangle(a, b, c, n, n, n, Vector2.zero, Vector2.zero, Vector2.zero);
                    positiveMesh.AddTriangle(a, c, b, -n, -n, -n, Vector2.zero, Vector2.zero, Vector2.zero);
                }
            }
        }

        // capSegments는 [시작, 끝] 쌍의 나열. 끝점이 일치하는 세그먼트끼리 이어 닫힌 루프들을 복원함.
        internal static List<List<Vector3>> BuildCapLoops(List<Vector3> capSegments)
        {
            const float weldDistance = 1e-4f;
            List<Vector3> points = new List<Vector3>();
            Dictionary<Vector3Int, int> lookup = new Dictionary<Vector3Int, int>();

            // 근접한 점들을 하나의 노드로 용접
            int GetPointId(Vector3 p)
            {
                Vector3Int baseKey = new Vector3Int(
                    Mathf.RoundToInt(p.x / weldDistance),
                    Mathf.RoundToInt(p.y / weldDistance),
                    Mathf.RoundToInt(p.z / weldDistance));

                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    Vector3Int key = new Vector3Int(baseKey.x + dx, baseKey.y + dy, baseKey.z + dz);
                    if (lookup.TryGetValue(key, out int id) && (points[id] - p).sqrMagnitude < weldDistance * weldDistance)
                        return id;
                }

                int newId = points.Count;
                points.Add(p);
                if (!lookup.ContainsKey(baseKey))
                    lookup[baseKey] = newId;
                return newId;
            }

            List<List<int>> adjacency = new List<List<int>>();

            for (int i = 0; i + 1 < capSegments.Count; i += 2)
            {
                if ((capSegments[i] - capSegments[i + 1]).sqrMagnitude < weldDistance * weldDistance)
                    continue; // 퇴화 세그먼트

                int a = GetPointId(capSegments[i]);
                int b = GetPointId(capSegments[i + 1]);
                if (a == b) continue;

                while (adjacency.Count < points.Count) adjacency.Add(new List<int>());
                adjacency[a].Add(b);
                adjacency[b].Add(a);
            }

            while (adjacency.Count < points.Count) adjacency.Add(new List<int>());

            List<List<Vector3>> loops = new List<List<Vector3>>();
            HashSet<long> usedEdges = new HashSet<long>();
            long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

            for (int start = 0; start < points.Count; start++)
            {
                foreach (int firstNext in adjacency[start])
                {
                    if (usedEdges.Contains(EdgeKey(start, firstNext))) continue;

                    List<Vector3> loop = new List<Vector3> { points[start] };
                    usedEdges.Add(EdgeKey(start, firstNext));
                    int current = firstNext;
                    int safety = points.Count + 2;

                    while (current != start && safety-- > 0)
                    {
                        loop.Add(points[current]);
                        int next = -1;
                        foreach (int cand in adjacency[current])
                        {
                            if (!usedEdges.Contains(EdgeKey(current, cand)))
                            {
                                next = cand;
                                break;
                            }
                        }
                        if (next == -1) break;
                        usedEdges.Add(EdgeKey(current, next));
                        current = next;
                    }

                    // 닫히지 못한 열린 체인은 버림 (거대 삼각형을 만드느니 구멍이 나음)
                    if (current == start && loop.Count >= 3)
                        loops.Add(loop);
                }
            }

            return loops;
        }

        // 이어클리핑(ear clipping) 기반 단순 다각형 삼각화. 오목 다각형도 처리 가능.
        internal static List<int> TriangulatePolygon(List<Vector2> polygon)
        {
            List<int> result = new List<int>();
            int n = polygon.Count;
            if (n < 3) return result;

            // CCW로 정규화
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = polygon[i];
                Vector2 q = polygon[(i + 1) % n];
                area += p.x * q.y - q.x * p.y;
            }

            List<int> indices = new List<int>(n);
            if (area >= 0f)
                for (int i = 0; i < n; i++) indices.Add(i);
            else
                for (int i = n - 1; i >= 0; i--) indices.Add(i);

            int guard = n * n + 8;
            while (indices.Count > 3 && guard-- > 0)
            {
                bool earFound = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int i0 = indices[(i + indices.Count - 1) % indices.Count];
                    int i1 = indices[i];
                    int i2 = indices[(i + 1) % indices.Count];

                    Vector2 a = polygon[i0], b = polygon[i1], c = polygon[i2];
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 0f)
                        continue; // 오목하거나 퇴화된 꼭짓점

                    bool containsOther = false;
                    foreach (int j in indices)
                    {
                        if (j == i0 || j == i1 || j == i2) continue;
                        if (PointInTriangle(polygon[j], a, b, c)) { containsOther = true; break; }
                    }
                    if (containsOther) continue;

                    result.Add(i0);
                    result.Add(i1);
                    result.Add(i2);
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                {
                    // 수치 오차로 귀를 못 찾으면 남은 부분을 부채꼴로 마감
                    for (int i = 1; i + 1 < indices.Count; i++)
                    {
                        result.Add(indices[0]);
                        result.Add(indices[i]);
                        result.Add(indices[i + 1]);
                    }
                    indices.Clear();
                }
            }

            if (indices.Count == 3)
            {
                result.Add(indices[0]);
                result.Add(indices[1]);
                result.Add(indices[2]);
            }

            return result;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos);
        }

        void CreateSlicedObject(string name, MeshData data)
        {
            if (data.vertices.Count == 0) return;

            GameObject go = new GameObject(name);
            go.transform.position = transform.position;
            go.transform.rotation = transform.rotation;
            go.transform.localScale = transform.localScale;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;

            Mesh mesh = data.ToMesh();
            filter.sharedMesh = mesh;

            // --- 물리 컴포넌트 자동 부착 로직 추가 ---
            if (addMeshCollider)
            {
                MeshCollider mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh; // 잘려진 새로운 메쉬를 콜라이더에 할당
                mc.convex = true;     // Rigidbody와 호환되도록 반드시 Convex를 켜줌
            }

            if (addRigidbody)
            {
                go.AddComponent<Rigidbody>();
            }
        }

        public class MeshData
        {
            public List<Vector3> vertices = new List<Vector3>();
            public List<Vector3> normals = new List<Vector3>();
            public List<Vector2> uvs = new List<Vector2>();
            public List<int> triangles = new List<int>();

            public void AddTriangle(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3, Vector2 uv1, Vector2 uv2, Vector2 uv3)
            {
                int index = vertices.Count;
                vertices.AddRange(new[] { v1, v2, v3 });
                normals.AddRange(new[] { n1, n2, n3 });
                uvs.AddRange(new[] { uv1, uv2, uv3 });
                triangles.AddRange(new[] { index, index + 1, index + 2 });
            }

            // 메모리 상에서 반복 연산을 하기 위해 MeshData를 Mesh로 변환하는 헬퍼 함수
            public Mesh ToMesh()
            {
                Mesh mesh = new Mesh();
                mesh.vertices = vertices.ToArray();
                mesh.triangles = triangles.ToArray();
                mesh.normals = normals.ToArray();
                mesh.uv = uvs.ToArray();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
