using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshSlicer : MonoBehaviour
{
    public Vector3 planePosition = Vector3.zero;
    public Quaternion planeRotation = Quaternion.identity;
    public Vector2 planeSize = new Vector2(2f, 2f);

    public void Slice()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter.sharedMesh == null) return;

        Vector3 localNormal = planeRotation * Vector3.up;
        Plane plane = new Plane(localNormal, planePosition);

        MeshData positiveMesh = new MeshData();
        MeshData negativeMesh = new MeshData();
        List<Vector3> capVertices = new List<Vector3>();

        Vector3[] verts = filter.sharedMesh.vertices;
        Vector3[] norms = filter.sharedMesh.normals;
        Vector2[] uvs = filter.sharedMesh.uv;
        int[] tris = filter.sharedMesh.triangles;

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
                // 잘리지 않은 온전한 삼각형
                (side1 ? positiveMesh : negativeMesh).AddTriangle(v1, v2, v3, n1, n2, n3, uv1, uv2, uv3);
            }
            else
            {
                // 평면을 관통하여 잘리는 삼각형
                Vector3 aloneV, pairV1, pairV2;
                Vector2 aloneUV, pairUV1, pairUV2;
                Vector3 aloneN, pairN1, pairN2;
                bool aloneSide;

                // 혼자 다른 쪽에 있는 꼭짓점 찾기
                if (side1 != side2 && side1 != side3)
                {
                    aloneV = v1; pairV1 = v2; pairV2 = v3;
                    aloneUV = uv1; pairUV1 = uv2; pairUV2 = uv3;
                    aloneN = n1; pairN1 = n2; pairN2 = n3;
                    aloneSide = side1;
                }
                else if (side2 != side1 && side2 != side3)
                {
                    aloneV = v2; pairV1 = v3; pairV2 = v1;
                    aloneUV = uv2; pairUV1 = uv3; pairUV2 = uv1;
                    aloneN = n2; pairN1 = n3; pairN2 = n1;
                    aloneSide = side2;
                }
                else
                {
                    aloneV = v3; pairV1 = v1; pairV2 = v2;
                    aloneUV = uv3; pairUV1 = uv1; pairUV2 = uv2;
                    aloneN = n3; pairN1 = n1; pairN2 = n2;
                    aloneSide = side3;
                }

                // 교차점 계산 (Lerp)
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

                // 혼자 있는 쪽의 작은 삼각형 1개
                aloneMesh.AddTriangle(aloneV, intersect1, intersect2, aloneN, intersectN1, intersectN2, aloneUV, intersectUV1, intersectUV2);

                // 쌍으로 있는 쪽의 사각형을 삼각형 2개로 분할
                pairMesh.AddTriangle(intersect1, pairV1, pairV2, intersectN1, pairN1, pairN2, intersectUV1, pairUV1, pairUV2);
                pairMesh.AddTriangle(intersect1, pairV2, intersect2, intersectN1, pairN2, intersectN2, intersectUV1, pairUV2, intersectUV2);
            }
        }

        // 단면 메꾸기 (Capping)
        if (capVertices.Count > 0)
        {
            Vector3 center = Vector3.zero;
            foreach (Vector3 v in capVertices) center += v;
            center /= capVertices.Count;

            // 볼록 다면체(Convex) 기준 단순 부채꼴 삼각분할
            for (int i = 0; i < capVertices.Count; i += 2)
            {
                Vector3 v1 = capVertices[i];
                Vector3 v2 = capVertices[i + 1];

                // 노멀 방향을 맞춰주기 위한 외적 계산
                Vector3 cross = Vector3.Cross(v1 - center, v2 - center);
                if (Vector3.Dot(cross, localNormal) < 0)
                {
                    Vector3 temp = v1; v1 = v2; v2 = temp;
                }

                // // 단면의 UV는 임시로 0,0 처리 (추후 버텍스 컬러로 색상 제어 가능)
                // positiveMesh.AddTriangle(center, v1, v2, -localNormal, -localNormal, -localNormal, Vector2.zero, Vector2.zero, Vector2.zero);
                // negativeMesh.AddTriangle(center, v2, v1, localNormal, localNormal, localNormal, Vector2.zero, Vector2.zero, Vector2.zero);
                
                //와인딩 오더 오류로 반대로 그리겠음.
                positiveMesh.AddTriangle(center, v2, v1, -localNormal, -localNormal, -localNormal, Vector2.zero, Vector2.zero, Vector2.zero);
                negativeMesh.AddTriangle(center, v1, v2, localNormal, localNormal, localNormal, Vector2.zero, Vector2.zero, Vector2.zero);
            }
        }

        CreateSlicedObject(gameObject.name + "_Positive", positiveMesh);
        CreateSlicedObject(gameObject.name + "_Negative", negativeMesh);
        gameObject.SetActive(false);
    }

    private void CreateSlicedObject(string name, MeshData data)
    {
        GameObject go = new GameObject(name);
        go.transform.position = transform.position;
        go.transform.rotation = transform.rotation;
        go.transform.localScale = transform.localScale;

        MeshFilter filter = go.AddComponent<MeshFilter>();
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;

        Mesh mesh = new Mesh();
        mesh.vertices = data.vertices.ToArray();
        mesh.triangles = data.triangles.ToArray();
        mesh.normals = data.normals.ToArray();
        mesh.uv = data.uvs.ToArray();
        
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
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
    }
}