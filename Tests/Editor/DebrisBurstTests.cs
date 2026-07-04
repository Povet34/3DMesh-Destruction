using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Povet.MeshDestruction;

namespace Povet.MeshDestruction.Tests
{
    public class DebrisBurstTests
    {
        private const float Dt = 1f / 60f;

        private GameObject root;
        private DebrisBurst burst;
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("DebrisRoot");
            cleanup.Add(root);
            root.transform.position = Vector3.zero; // 논리 바닥 = y 0

            for (int i = 0; i < 4; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = $"Piece_{i}";
                piece.transform.SetParent(root.transform);
                piece.transform.localPosition = new Vector3((i - 1.5f) * 0.5f, 1f, 0f);
                piece.transform.localScale = Vector3.one * 0.3f;
            }

            burst = root.AddComponent<DebrisBurst>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in cleanup)
                if (go != null) Object.DestroyImmediate(go);
            cleanup.Clear();
        }

        private void Simulate(float seconds)
        {
            int steps = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
                burst.Tick(Dt);
        }

        [Test]
        public void Burst_MovesPieces()
        {
            Random.InitState(1);
            var before = new List<Vector3>();
            foreach (Transform child in root.transform) before.Add(child.position);

            burst.Burst();
            Simulate(0.5f);

            int moved = 0;
            int index = 0;
            foreach (Transform child in root.transform)
            {
                if ((child.position - before[index]).sqrMagnitude > 0.01f) moved++;
                index++;
            }
            Assert.AreEqual(before.Count, moved, "버스트 후 모든 파편이 이동해야 함");
        }

        [Test]
        public void GroundPlane_KeepsPiecesAboveLogicalFloor()
        {
            Random.InitState(7);
            burst.settings.useGroundPlane = true;
            burst.settings.groundYOffset = 0f;
            burst.settings.impulse = 5f;
            burst.settings.upwardBias = 3f;

            burst.Burst();
            Simulate(10f); // 충분히 정착시킴

            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                Assert.GreaterOrEqual(r.bounds.min.y, -0.02f,
                    $"{r.name}: 논리 바닥(y=0) 아래로 뚫고 내려가면 안 됨 (min.y={r.bounds.min.y:F4})");
            }
        }

        [Test]
        public void GroundPlane_Disabled_PiecesFallThrough()
        {
            Random.InitState(7);
            burst.settings.useGroundPlane = false;
            burst.settings.impulse = 0f;
            burst.settings.upwardBias = 0f;
            burst.settings.randomImpulse = 0f;

            burst.Burst();
            Simulate(3f);

            foreach (var r in root.GetComponentsInChildren<Renderer>())
                Assert.Less(r.bounds.min.y, -1f, "바닥이 꺼져 있으면 파편이 계속 낙하해야 함");
        }

        [Test]
        public void Restore_ResetsLocalPose()
        {
            var originalPos = new List<Vector3>();
            var originalRot = new List<Quaternion>();
            foreach (Transform child in root.transform)
            {
                originalPos.Add(child.localPosition);
                originalRot.Add(child.localRotation);
            }

            Random.InitState(3);
            burst.Burst();
            Simulate(1f);
            burst.Restore();

            int index = 0;
            foreach (Transform child in root.transform)
            {
                Assert.Less((child.localPosition - originalPos[index]).magnitude, 1e-4f,
                    $"{child.name}: Restore 후 로컬 위치가 복원되어야 함");
                Assert.Less(Quaternion.Angle(child.localRotation, originalRot[index]), 0.01f,
                    $"{child.name}: Restore 후 로컬 회전이 복원되어야 함");
                Assert.IsTrue(child.gameObject.activeSelf);
                index++;
            }
        }

        [Test]
        public void Lifetime_DeactivatesPiecesAfterExpiry()
        {
            Random.InitState(5);
            burst.settings.lifetime = 0.5f;
            burst.settings.shrinkDuration = 0.2f;

            burst.Burst();
            Simulate(1f);

            foreach (Transform child in root.transform)
                Assert.IsFalse(child.gameObject.activeSelf, "수명이 지나면 파편이 비활성화되어야 함");
        }

        // ---------- MeshSlicer 통합 ----------

        [Test]
        public void Slice_DebrisBurstMode_AttachesConfiguredComponentWithoutPhysics()
        {
            var host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cleanup.Add(host);
            var slicer = host.AddComponent<MeshSlicer>();

            slicer.sliceMethod = SliceMethod.VoronoiFixedSeed;
            slicer.voronoiSeedCount = 4;
            slicer.randomSeed = 42;
            slicer.physicsMode = ChunkPhysicsMode.DebrisBurst;
            slicer.debrisBurstSettings.impulse = 12.5f;
            slicer.debrisBurstSettings.useGroundPlane = true;
            slicer.childSettings.Add(new ChildSliceSetting
            {
                targetFilter = host.GetComponent<MeshFilter>(),
                enableSlice = true,
                sliceRatio = 1f
            });

            GameObject container = slicer.Slice();
            cleanup.Add(container);

            var debris = container.GetComponent<DebrisBurst>();
            Assert.IsNotNull(debris, "DebrisBurst 모드면 컨테이너에 DebrisBurst가 붙어야 함");
            Assert.AreEqual(12.5f, debris.settings.impulse, 1e-5f, "슬라이서의 세팅이 복사되어야 함");
            Assert.IsTrue(debris.settings.useGroundPlane);

            // 세팅은 복사본이어야 함 (슬라이서 쪽을 나중에 바꿔도 구운 결과에 영향 없어야 함)
            slicer.debrisBurstSettings.impulse = 999f;
            Assert.AreEqual(12.5f, debris.settings.impulse, 1e-5f, "세팅은 깊은 복사여야 함");

            Assert.AreEqual(0, container.GetComponentsInChildren<Collider>(true).Length,
                "DebrisBurst 모드에서는 파편에 콜라이더가 없어야 함");
            Assert.AreEqual(0, container.GetComponentsInChildren<Rigidbody>(true).Length,
                "DebrisBurst 모드에서는 파편에 리지드바디가 없어야 함");
            Assert.Greater(container.transform.childCount, 1, "파편이 생성되어야 함");
        }

        [Test]
        public void Slice_RigidbodyMode_StillAttachesPhysics()
        {
            var host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cleanup.Add(host);
            var slicer = host.AddComponent<MeshSlicer>();

            slicer.sliceMethod = SliceMethod.VoronoiFixedSeed;
            slicer.voronoiSeedCount = 4;
            slicer.randomSeed = 42;
            slicer.physicsMode = ChunkPhysicsMode.Rigidbody;
            slicer.colliderType = ChunkColliderType.Box;
            slicer.addRigidbody = true;
            slicer.childSettings.Add(new ChildSliceSetting
            {
                targetFilter = host.GetComponent<MeshFilter>(),
                enableSlice = true,
                sliceRatio = 1f
            });

            GameObject container = slicer.Slice();
            cleanup.Add(container);

            Assert.IsNull(container.GetComponent<DebrisBurst>(), "Rigidbody 모드에서는 DebrisBurst가 없어야 함");
            Assert.Greater(container.GetComponentsInChildren<Collider>(true).Length, 0);
            Assert.Greater(container.GetComponentsInChildren<Rigidbody>(true).Length, 0);
        }
    }
}
