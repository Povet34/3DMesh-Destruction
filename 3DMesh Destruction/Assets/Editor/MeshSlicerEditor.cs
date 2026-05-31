using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(MeshSlicer))]
public class MeshSlicerEditor : Editor
{
    private void OnSceneGUI()
    {
        MeshSlicer slicer = (MeshSlicer)target;

        // enum 상태에 따라 씬 뷰 GUI를 다르게 그림
        if (slicer.sliceMethod == SliceMethod.SinglePlane)
        {
            DrawSinglePlaneGUI(slicer);
        }
        else if (slicer.sliceMethod == SliceMethod.Voronoi)
        {
            DrawVoronoiGUI(slicer);
        }
    }

    private void DrawSinglePlaneGUI(MeshSlicer slicer)
    {
        // 1. 핸들 조작 로직
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

        // 3. 초록색 절취선 그리기
        DrawIntersectionLines(slicer);
    }

    private void DrawVoronoiGUI(MeshSlicer slicer)
    {
        MeshFilter filter = slicer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;

        // 보로노이 시드가 생성될 바운딩 박스 영역을 노란색 와이어프레임으로 표시
        Handles.color = Color.yellow;
        Bounds bounds = filter.sharedMesh.bounds;
        
        // 로컬 바운딩 박스를 월드 스페이스 기준으로 변환
        Vector3 worldCenter = slicer.transform.TransformPoint(bounds.center);
        Vector3 worldSize = Vector3.Scale(bounds.size, slicer.transform.lossyScale);

        Handles.DrawWireCube(worldCenter, worldSize);

        // 씬 뷰에 텍스트 라벨 띄우기
        GUIStyle labelStyle = new GUIStyle();
        labelStyle.normal.textColor = Color.yellow;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        
        Handles.Label(worldCenter + Vector3.up * (worldSize.y * 0.5f + 0.2f), $"Voronoi Bounds\nSeeds: {slicer.voronoiSeedCount}", labelStyle);
    }

    private void DrawIntersectionLines(MeshSlicer slicer)
    {
        MeshFilter filter = slicer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;

        Mesh mesh = filter.sharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        Vector3 localPlaneNormal = slicer.planeRotation * Vector3.up;
        Plane localPlane = new Plane(localPlaneNormal, slicer.planePosition);

        Handles.color = Color.green;

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 v1 = verts[tris[i]];
            Vector3 v2 = verts[tris[i + 1]];
            Vector3 v3 = verts[tris[i + 2]];

            float d1 = localPlane.GetDistanceToPoint(v1);
            float d2 = localPlane.GetDistanceToPoint(v2);
            float d3 = localPlane.GetDistanceToPoint(v3);

            if ((d1 > 0 && d2 > 0 && d3 > 0) || (d1 <= 0 && d2 <= 0 && d3 <= 0))
                continue;

            List<Vector3> intersectPoints = new List<Vector3>();

            if (Mathf.Sign(d1) != Mathf.Sign(d2)) 
                intersectPoints.Add(GetIntersectionPoint(v1, v2, d1, d2));
            
            if (Mathf.Sign(d2) != Mathf.Sign(d3)) 
                intersectPoints.Add(GetIntersectionPoint(v2, v3, d2, d3));
            
            if (Mathf.Sign(d3) != Mathf.Sign(d1)) 
                intersectPoints.Add(GetIntersectionPoint(v3, v1, d3, d1));

            if (intersectPoints.Count == 2)
            {
                Vector3 worldPt1 = slicer.transform.TransformPoint(intersectPoints[0]);
                Vector3 worldPt2 = slicer.transform.TransformPoint(intersectPoints[1]);
                Handles.DrawLine(worldPt1, worldPt2, 3f);
            }
        }
    }

    private Vector3 GetIntersectionPoint(Vector3 p1, Vector3 p2, float d1, float d2)
    {
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