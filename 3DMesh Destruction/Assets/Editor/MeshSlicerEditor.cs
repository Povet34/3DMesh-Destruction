using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(MeshSlicer))]
public class MeshSlicerEditor : Editor
{
    private void OnSceneGUI()
    {
        MeshSlicer slicer = (MeshSlicer)target;

        // 1. 핸들 조작 로직 (기존과 동일)
        Vector3 worldPos = slicer.transform.TransformPoint(slicer.planePosition);
        Quaternion worldRot = slicer.transform.rotation * slicer.planeRotation;

        EditorGUI.BeginChangeCheck();
        Vector3 newWorldPos = Handles.PositionHandle(worldPos, worldRot);
        Quaternion newWorldRot = Handles.RotationHandle(worldRot, worldPos);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(slicer, "Move Slice Plane");
            slicer.planePosition = slicer.transform.InverseTransformPoint(newWorldPos);
            slicer.planeRotation = Quaternion.Inverse(slicer.transform.rotation) * newWorldRot;
        }

        // 2. 가상 평면 그리기 (반투명 빨간색)
        Handles.color = new Color(1f, 0f, 0f, 0.15f);
        Vector3 halfSizeX = (worldRot * Vector3.right) * (slicer.planeSize.x * 0.5f);
        Vector3 halfSizeZ = (worldRot * Vector3.forward) * (slicer.planeSize.y * 0.5f);

        Vector3[] planeCorners = new Vector3[4];
        planeCorners[0] = worldPos - halfSizeX - halfSizeZ;
        planeCorners[1] = worldPos - halfSizeX + halfSizeZ;
        planeCorners[2] = worldPos + halfSizeX + halfSizeZ;
        planeCorners[3] = worldPos + halfSizeX - halfSizeZ;
        Handles.DrawSolidRectangleWithOutline(planeCorners, new Color(1f, 0f, 0f, 0.1f), Color.clear);

        // 3. 초록색 절취선(Intersection Line) 그리기 로직 추가
        DrawIntersectionLines(slicer);
    }

    private void DrawIntersectionLines(MeshSlicer slicer)
    {
        MeshFilter filter = slicer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;

        Mesh mesh = filter.sharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        // 로컬 스페이스 기준의 평면 정의
        Vector3 localPlaneNormal = slicer.planeRotation * Vector3.up;
        Plane localPlane = new Plane(localPlaneNormal, slicer.planePosition);

        Handles.color = Color.green;

        // 모든 삼각형을 순회하며 평면과의 교차점 계산
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 v1 = verts[tris[i]];
            Vector3 v2 = verts[tris[i + 1]];
            Vector3 v3 = verts[tris[i + 2]];

            // 각 버텍스에서 평면까지의 부호 있는 거리(Signed Distance) 계산
            float d1 = localPlane.GetDistanceToPoint(v1);
            float d2 = localPlane.GetDistanceToPoint(v2);
            float d3 = localPlane.GetDistanceToPoint(v3);

            // 세 점이 모두 평면의 같은 쪽에 있다면 교차하지 않는 것임
            if ((d1 > 0 && d2 > 0 && d3 > 0) || (d1 <= 0 && d2 <= 0 && d3 <= 0))
                continue;

            List<Vector3> intersectPoints = new List<Vector3>();

            // 선분 v1-v2 교차 확인
            if (Mathf.Sign(d1) != Mathf.Sign(d2)) 
                intersectPoints.Add(GetIntersectionPoint(v1, v2, d1, d2));
            
            // 선분 v2-v3 교차 확인
            if (Mathf.Sign(d2) != Mathf.Sign(d3)) 
                intersectPoints.Add(GetIntersectionPoint(v2, v3, d2, d3));
            
            // 선분 v3-v1 교차 확인
            if (Mathf.Sign(d3) != Mathf.Sign(d1)) 
                intersectPoints.Add(GetIntersectionPoint(v3, v1, d3, d1));

            // 교차점이 2개 나왔다면, 그 두 점을 잇는 선을 그림
            if (intersectPoints.Count == 2)
            {
                // 로컬 좌표를 월드 좌표로 변환하여 씬 뷰에 그림
                Vector3 worldPt1 = slicer.transform.TransformPoint(intersectPoints[0]);
                Vector3 worldPt2 = slicer.transform.TransformPoint(intersectPoints[1]);
                
                // 선 굵기를 3f로 주어 눈에 잘 띄게 만듦
                Handles.DrawLine(worldPt1, worldPt2, 3f);
            }
        }
    }

    // 두 버텍스 사이에서 평면과 정확히 교차하는 지점을 선형 보간(Lerp)으로 찾는 수학 함수
    private Vector3 GetIntersectionPoint(Vector3 p1, Vector3 p2, float d1, float d2)
    {
        // 거리의 비율을 계산하여 t값(0~1)을 구함
        float t = d1 / (d1 - d2);
        return Vector3.Lerp(p1, p2, t);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        MeshSlicer slicer = (MeshSlicer)target;

        GUILayout.Space(10);
        if (GUILayout.Button("Slice Object", GUILayout.Height(40)))
        {
            Undo.RecordObject(slicer.gameObject, "Slice Object");
            slicer.Slice();
        }
    }
}