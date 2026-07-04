using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Povet.MeshDestruction.Editor
{
    [CustomEditor(typeof(MeshSlicer))]
    public class MeshSlicerEditor : UnityEditor.Editor
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
            MeshSlicer slicer = (MeshSlicer)target;

            // 1. 마스터 세팅 렌더링
            EditorGUILayout.LabelField("Master Slice Settings", EditorStyles.boldLabel);
            SerializedProperty sliceMethodProp = serializedObject.FindProperty("sliceMethod");
            EditorGUILayout.PropertyField(sliceMethodProp);
            SliceMethod currentMethod = (SliceMethod)sliceMethodProp.enumValueIndex;
            // 글로벌 비율 슬라이더 노출
            EditorGUILayout.PropertyField(serializedObject.FindProperty("globalSliceRatio"));

            EditorGUILayout.Space();

            if (currentMethod == SliceMethod.SinglePlane)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("planePosition"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("planeRotation"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("planeSize"));
            }
            else
            {
                if (currentMethod != SliceMethod.VoronoiRandom)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("randomSeed"));

                if (currentMethod == SliceMethod.VoronoiRandom || currentMethod == SliceMethod.VoronoiFixedSeed)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("voronoiSeedCount"));
                else if (currentMethod == SliceMethod.Radial)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("impactPoint"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("radialRings"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("radialRays"));
                }
                else if (currentMethod == SliceMethod.Clustered)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("clusterCount"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("seedsPerCluster"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("clusterRadius"));
                }
                else if (currentMethod == SliceMethod.Splinter)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("voronoiSeedCount"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("splinterSpread"));
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Physics Settings", EditorStyles.boldLabel);

            SerializedProperty physicsModeProp = serializedObject.FindProperty("physicsMode");
            EditorGUILayout.PropertyField(physicsModeProp);
            ChunkPhysicsMode physicsMode = (ChunkPhysicsMode)physicsModeProp.enumValueIndex;

            if (physicsMode == ChunkPhysicsMode.Rigidbody)
            {
                SerializedProperty colliderTypeProp = serializedObject.FindProperty("colliderType");
                EditorGUILayout.PropertyField(colliderTypeProp);

                // 콜라이더가 None이 아닐 때만 스케일과 리지드바디 옵션을 보여줌
                if (colliderTypeProp.enumValueIndex != (int)ChunkColliderType.None)
                {
                    // MeshCollider는 스케일 조절이 불가능하므로 Box나 Sphere일 때만 스케일 슬라이더 노출
                    if (colliderTypeProp.enumValueIndex != (int)ChunkColliderType.MeshCollider)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("colliderScale"));
                    }
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("addRigidbody"));
                }
            }
            else if (physicsMode == ChunkPhysicsMode.DebrisBurst)
            {
                EditorGUILayout.HelpBox(
                    "파편에 콜라이더/리지드바디를 붙이지 않고, 파편 컨테이너에 DebrisBurst 컴포넌트가 부착됨.\n" +
                    "아래 세팅이 그 컴포넌트로 복사되며, 베이크 후 컨테이너에서 직접 수정해도 됨.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("debrisBurstSettings"), true);
            }

            // 2. 하이라키(자식 메쉬) 세팅 렌더링
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hierarchy Settings", EditorStyles.boldLabel);

            if (GUILayout.Button("Find All Child Meshes", GUILayout.Height(30)))
            {
                Undo.RecordObject(slicer, "Find Child Meshes");
                slicer.childSettings.Clear();

                // 자신을 포함한 모든 자식의 MeshFilter를 찾음
                MeshFilter[] filters = slicer.GetComponentsInChildren<MeshFilter>();
                foreach (MeshFilter f in filters)
                {
                    slicer.childSettings.Add(new ChildSliceSetting { targetFilter = f, enableSlice = true, sliceRatio = 1.0f });
                }
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("childSettings"), true);

            serializedObject.ApplyModifiedProperties();

            GUILayout.Space(15);
            if (GUILayout.Button("Slice All Objects", GUILayout.Height(40)))
            {
                Undo.RecordObject(slicer.gameObject, "Slice All Objects");
                slicer.Slice();
            }

            if (GUILayout.Button("Slice & Save Prefab", GUILayout.Height(30)))
            {
                GameObject container = slicer.Slice();
                if (container != null)
                    SaveFracturedPrefab(slicer, container);
            }
        }

        // 파편 메쉬들을 .asset(서브에셋)으로 저장하고, 컨테이너를 프리팹으로 저장함.
        // 저장된 프리팹은 FracturedSwap 등으로 폭발 시 원본과 교체해서 사용.
        private void SaveFracturedPrefab(MeshSlicer slicer, GameObject container)
        {
            string rootName = slicer.gameObject.name;
            string baseFolder = "Assets/FracturedCache";
            if (!AssetDatabase.IsValidFolder(baseFolder))
                AssetDatabase.CreateFolder("Assets", "FracturedCache");

            string folder = baseFolder + "/" + rootName;
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(baseFolder, rootName);

            // 파편 메쉬 전부를 하나의 .asset 파일에 서브에셋으로 저장
            string meshAssetPath = folder + "/" + rootName + "_Meshes.asset";
            AssetDatabase.DeleteAsset(meshAssetPath);

            MeshFilter[] filters = container.GetComponentsInChildren<MeshFilter>();
            bool mainCreated = false;
            int meshCount = 0;

            foreach (MeshFilter f in filters)
            {
                Mesh m = f.sharedMesh;
                if (m == null || AssetDatabase.Contains(m)) continue;

                m.name = f.gameObject.name;
                if (!mainCreated)
                {
                    AssetDatabase.CreateAsset(m, meshAssetPath);
                    mainCreated = true;
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(m, meshAssetPath);
                }
                meshCount++;
            }
            AssetDatabase.SaveAssets();

            string prefabPath = folder + "/" + container.name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(container, prefabPath, InteractionMode.UserAction);

            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[MeshSlicer] 파편 프리팹 저장 완료: {prefabPath} (메쉬 {meshCount}개)");
        }

        private void OnSceneGUI()
        {
            MeshSlicer slicer = (MeshSlicer)target;

            if (slicer.sliceMethod == SliceMethod.SinglePlane)
            {
                DrawSinglePlaneGUI(slicer);
            }
            else
            {
                DrawAdvancedVoronoiGUI(slicer);
            }
        }

        private void DrawAdvancedVoronoiGUI(MeshSlicer slicer)
        {
            if (slicer.sliceMethod == SliceMethod.Radial)
            {
                Vector3 worldImpact = slicer.transform.TransformPoint(slicer.impactPoint);
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldImpact = Handles.PositionHandle(worldImpact, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(slicer, "Move Impact Point");
                    slicer.impactPoint = slicer.transform.InverseTransformPoint(newWorldImpact);
                }
            }

            if (slicer.childSettings == null || slicer.childSettings.Count == 0) return;

            bool showSeeds = slicer.sliceMethod != SliceMethod.VoronoiRandom;
            if (showSeeds)
            {
                Random.InitState(slicer.randomSeed);
            }

            foreach (ChildSliceSetting setting in slicer.childSettings)
            {
                if (!setting.enableSlice || setting.targetFilter == null || setting.targetFilter.sharedMesh == null)
                    continue;

                Transform childT = setting.targetFilter.transform;
                Bounds bounds = setting.targetFilter.sharedMesh.bounds;

                Vector3 worldCenter = childT.TransformPoint(bounds.center);
                Vector3 worldSize = Vector3.Scale(bounds.size, childT.lossyScale);

                Handles.color = Color.yellow;
                Handles.DrawWireCube(worldCenter, worldSize);

                if (showSeeds)
                {
                    List<Vector3> seeds = new List<Vector3>();

                    // 씬 뷰 미리보기에도 글로벌 비율을 곱하여 실시간으로 점 개수가 변하도록 연동
                    float finalRatio = setting.sliceRatio * slicer.globalSliceRatio;

                    switch (slicer.sliceMethod)
                    {
                        case SliceMethod.VoronoiFixedSeed:
                            int effectiveSeeds = Mathf.Max(2, Mathf.RoundToInt(slicer.voronoiSeedCount * finalRatio));
                            seeds = slicer.GenerateUniformSeeds(setting.targetFilter, effectiveSeeds);
                            break;
                        case SliceMethod.Radial:
                            int effectiveRings = Mathf.Max(1, Mathf.RoundToInt(slicer.radialRings * finalRatio));
                            int effectiveRays = Mathf.Max(3, Mathf.RoundToInt(slicer.radialRays * finalRatio));
                            seeds = slicer.GenerateRadialSeeds(setting.targetFilter, effectiveRings, effectiveRays);
                            break;
                        case SliceMethod.Clustered:
                            int effectiveClusters = Mathf.Max(1, Mathf.RoundToInt(slicer.clusterCount * finalRatio));
                            int effectiveSeedsPerCluster = Mathf.Max(2, Mathf.RoundToInt(slicer.seedsPerCluster * finalRatio));
                            seeds = slicer.GenerateClusteredSeeds(setting.targetFilter, effectiveClusters, effectiveSeedsPerCluster);
                            break;
                        case SliceMethod.Splinter:
                            int effectiveSplinterSeeds = Mathf.Max(2, Mathf.RoundToInt(slicer.voronoiSeedCount * finalRatio));
                            seeds = slicer.GenerateSplinterSeeds(setting.targetFilter, effectiveSplinterSeeds);
                            break;
                    }

                    Handles.color = Color.red;
                    float handleSize = Mathf.Max(worldSize.x, worldSize.y, worldSize.z) * 0.02f;

                    foreach (Vector3 seed in seeds)
                    {
                        Vector3 worldSeed = childT.TransformPoint(seed);
                        Handles.SphereHandleCap(0, worldSeed, Quaternion.identity, handleSize, EventType.Repaint);
                    }
                }
            }
        }
    }
}
