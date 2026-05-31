using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(MeshSlicer))]
public class MeshSlicerEditor : Editor
{
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

    private void DrawVoronoiGUI(MeshSlicer slicer, bool showSeeds)
    {
        MeshFilter filter = slicer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;

        Bounds bounds = filter.sharedMesh.bounds;
        Vector3 worldCenter = slicer.transform.TransformPoint(bounds.center);
        Vector3 worldSize = Vector3.Scale(bounds.size, slicer.transform.lossyScale);

        Handles.color = Color.yellow;
        Handles.DrawWireCube(worldCenter, worldSize);

        string labelText = $"Voronoi Bounds\nSeeds: {slicer.voronoiSeedCount}";

        // showSeeds가 true일 때만 빨간 점을 계산하고 그림
        if (showSeeds)
        {
            Random.InitState(slicer.randomSeed);
            Handles.color = Color.red;
            float handleSize = Mathf.Max(worldSize.x, worldSize.y, worldSize.z) * 0.02f;

            for (int i = 0; i < slicer.voronoiSeedCount; i++)
            {
                Vector3 localSeed = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(bounds.min.y, bounds.max.y),
                    Random.Range(bounds.min.z, bounds.max.z)
                );
                Vector3 worldSeed = slicer.transform.TransformPoint(localSeed);
                Handles.SphereHandleCap(0, worldSeed, Quaternion.identity, handleSize, EventType.Repaint);
            }
            labelText += $"\nFixed Seed: {slicer.randomSeed}";
        }
        else
        {
            labelText += "\nMode: Random (Seeds Hidden)";
        }

        GUIStyle labelStyle = new GUIStyle();
        labelStyle.normal.textColor = Color.yellow;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        Handles.Label(worldCenter + Vector3.up * (worldSize.y * 0.5f + 0.2f), labelText, labelStyle);
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
        serializedObject.Update();

        SerializedProperty sliceMethodProp = serializedObject.FindProperty("sliceMethod");
        EditorGUILayout.PropertyField(sliceMethodProp);

        SliceMethod currentMethod = (SliceMethod)sliceMethodProp.enumValueIndex;

        EditorGUILayout.Space();

        // 선택된 모드에 따라 인스펙터에 보여줄 변수를 다르게 처리함
        if (currentMethod == SliceMethod.SinglePlane)
        {
            EditorGUILayout.LabelField("Single Plane Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("planePosition"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("planeRotation"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("planeSize"));
        }
        else
        {
            EditorGUILayout.LabelField("Voronoi Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("voronoiSeedCount"));
            
            // 고정 시드 모드일 때만 randomSeed 변수를 노출함
            if (currentMethod == SliceMethod.VoronoiFixedSeed)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("randomSeed"));
            }
        }

        serializedObject.ApplyModifiedProperties();

        GUILayout.Space(15);
        if (GUILayout.Button("Slice Object", GUILayout.Height(40)))
        {
            MeshSlicer slicer = (MeshSlicer)target;
            Undo.RecordObject(slicer.gameObject, "Slice Object");
            slicer.Slice();
        }
    }

    private void OnSceneGUI()
    {
        MeshSlicer slicer = (MeshSlicer)target;

        if (slicer.sliceMethod == SliceMethod.SinglePlane)
        {
            DrawSinglePlaneGUI(slicer);
        }
        else if (slicer.sliceMethod == SliceMethod.VoronoiRandom)
        {
            // 무작위 모드는 빨간 점(Seed)을 그리지 않음 (false 전달)
            DrawVoronoiGUI(slicer, false);
        }
        else if (slicer.sliceMethod == SliceMethod.VoronoiFixedSeed)
        {
            // 고정 시드 모드는 빨간 점(Seed)을 그림 (true 전달)
            DrawVoronoiGUI(slicer, true);
        }
    }
}