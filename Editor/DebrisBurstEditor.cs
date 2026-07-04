using UnityEngine;
using UnityEditor;

namespace Povet.MeshDestruction.Editor
{
    [CustomEditor(typeof(DebrisBurst))]
    public class DebrisBurstEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "트랜스폼 적분은 플레이 모드에서만 동작함.\n" +
                    "플레이 중 아래 버튼으로 테스트하거나, FracturedSwap.Explode() / Burst()를 호출하면 됨.",
                    MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                DebrisBurst debris = (DebrisBurst)target;

                if (GUILayout.Button("Burst", GUILayout.Height(30)))
                    debris.Burst();

                if (GUILayout.Button("Restore", GUILayout.Height(24)))
                    debris.Restore();
            }
        }
    }
}
