using UnityEngine;

namespace Povet.MeshDestruction
{
    // DebrisBurst 동작 파라미터. MeshSlicer가 이 세팅을 들고 있다가 베이크 시
    // 파편 컨테이너의 DebrisBurst 컴포넌트로 복사해줌. 단독 사용 시엔 컴포넌트에서 직접 편집.
    [System.Serializable]
    public class DebrisBurstSettings
    {
        [Header("Impulse")]
        [Tooltip("중심에서 바깥으로 방사되는 초기 속도")]
        public float impulse = 7f;
        [Tooltip("방사 속도에 더해지는 랜덤 속도")]
        public float randomImpulse = 2.5f;
        [Tooltip("위로 띄우는 정도")]
        public float upwardBias = 4f;
        public float gravity = 18f;
        [Tooltip("파편 회전 속도 (도/초)")]
        public float angularSpeed = 540f;

        [Header("Ground Plane (논리적 바닥)")]
        [Tooltip("콜라이더 대신 논리적 평면에서 파편이 튕기게 함")]
        public bool useGroundPlane = true;
        [Tooltip("버스트 시점의 루트 Y 기준 바닥 오프셋")]
        public float groundYOffset = 0f;
        [Range(0f, 1f)] public float bounciness = 0.35f;
        [Tooltip("접지 시 수평 속도/회전 감쇠율")]
        [Range(0f, 1f)] public float groundFriction = 0.5f;

        [Header("Lifetime")]
        [Tooltip("버스트 후 파편 유지 시간. 0이면 무제한")]
        public float lifetime = 0f;
        [Tooltip("수명 끝에서 파편이 줄어들며 사라지는 시간")]
        public float shrinkDuration = 0.5f;

        public DebrisBurstSettings Clone() => (DebrisBurstSettings)MemberwiseClone();
    }

    // Rigidbody/Collider 없이 트랜스폼 적분으로 자식 파편들을 날리는 컴포넌트.
    // 파편끼리 밀어내는 상호작용이 없는 대신 물리 비용이 0에 가깝고, 논리적 바닥 평면에서 튕김.
    // MeshSlicer의 physicsMode = DebrisBurst로 베이크하면 컨테이너에 자동으로 붙지만,
    // 자식에 Renderer가 있는 아무 오브젝트에 수동으로 붙여 단독 사용해도 됨.
    public class DebrisBurst : MonoBehaviour
    {
        public DebrisBurstSettings settings = new DebrisBurstSettings();

        [Tooltip("Start 시점에 자동으로 버스트 (FracturedSwap 없이 단독 사용할 때)")]
        public bool burstOnStart = false;

        private Transform[] _pieces;
        private Renderer[] _renderers;
        private Vector3[] _velocities;
        private Vector3[] _axes;
        private float[] _spins;
        private Vector3[] _localPositions;
        private Quaternion[] _localRotations;
        private Vector3[] _localScales;
        private bool _burst;
        private float _groundY;
        private float _elapsed;

        private void Awake() => CachePieces();

        private void Start()
        {
            if (burstOnStart) Burst();
        }

        // 자식 Renderer들을 파편으로 등록하고 원래 로컬 포즈를 기억함
        public void CachePieces()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            int count = _renderers.Length;
            _pieces = new Transform[count];
            _velocities = new Vector3[count];
            _axes = new Vector3[count];
            _spins = new float[count];
            _localPositions = new Vector3[count];
            _localRotations = new Quaternion[count];
            _localScales = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                Transform t = _renderers[i].transform;
                _pieces[i] = t;
                _localPositions[i] = t.localPosition;
                _localRotations[i] = t.localRotation;
                _localScales[i] = t.localScale;
            }
        }

        public void Burst() => Burst(transform.position);

        // explosionCenter는 월드 좌표. 파편은 중심에서 바깥 방향으로 날아감.
        public void Burst(Vector3 explosionCenter)
        {
            if (_pieces == null) CachePieces();

            for (int i = 0; i < _pieces.Length; i++)
            {
                Transform t = _pieces[i];
                if (t == null) continue;
                t.gameObject.SetActive(true);

                Vector3 dir = _renderers[i].bounds.center - explosionCenter;
                dir = dir.sqrMagnitude < 1e-4f ? Random.onUnitSphere : dir.normalized;
                _velocities[i] = dir * settings.impulse
                               + Vector3.up * settings.upwardBias
                               + Random.insideUnitSphere * settings.randomImpulse;
                _axes[i] = Random.onUnitSphere;
                _spins[i] = settings.angularSpeed * Random.Range(0.7f, 1.3f);
            }

            _groundY = transform.position.y + settings.groundYOffset;
            _elapsed = 0f;
            _burst = true;
        }

        // 파편을 원래 로컬 포즈로 되돌리고 시뮬레이션 중지
        public void Restore()
        {
            _burst = false;
            if (_pieces == null) return;
            for (int i = 0; i < _pieces.Length; i++)
            {
                Transform t = _pieces[i];
                if (t == null) continue;
                t.gameObject.SetActive(true);
                t.localPosition = _localPositions[i];
                t.localRotation = _localRotations[i];
                t.localScale = _localScales[i];
            }
        }

        private void Update() => Tick(Time.deltaTime);

        // 적분 스텝. 테스트에서 고정 deltaTime으로 호출할 수 있도록 Update와 분리함.
        public void Tick(float dt)
        {
            if (!_burst || dt <= 0f) return;

            for (int i = 0; i < _pieces.Length; i++)
            {
                Transform t = _pieces[i];
                if (t == null || !t.gameObject.activeSelf) continue;

                _velocities[i] += Vector3.down * (settings.gravity * dt);
                t.position += _velocities[i] * dt;
                t.Rotate(_axes[i], _spins[i] * dt, Space.World);

                if (!settings.useGroundPlane) continue;

                float penetration = _groundY - _renderers[i].bounds.min.y;
                if (penetration <= 0f || _velocities[i].y > 0f) continue;

                t.position += Vector3.up * penetration;
                Vector3 v = _velocities[i];
                v.y = -v.y * settings.bounciness;
                if (v.y < 0.4f) v.y = 0f; // 미세 바운스 제거
                v.x *= 1f - settings.groundFriction;
                v.z *= 1f - settings.groundFriction;
                _velocities[i] = v;
                _spins[i] *= 1f - settings.groundFriction;
            }

            if (settings.lifetime > 0f)
            {
                _elapsed += dt;
                float remaining = settings.lifetime - _elapsed;
                if (remaining <= 0f)
                {
                    FinishBurst();
                }
                else if (settings.shrinkDuration > 0f && remaining < settings.shrinkDuration)
                {
                    // 수명 끝자락에서 스케일을 줄여 자연스럽게 사라지게 함
                    float scale = remaining / settings.shrinkDuration;
                    for (int i = 0; i < _pieces.Length; i++)
                    {
                        if (_pieces[i] == null) continue;
                        _pieces[i].localScale = _localScales[i] * scale;
                    }
                }
            }
        }

        private void FinishBurst()
        {
            _burst = false;
            for (int i = 0; i < _pieces.Length; i++)
            {
                if (_pieces[i] == null) continue;
                _pieces[i].gameObject.SetActive(false);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (settings == null || !settings.useGroundPlane) return;

            float y = Application.isPlaying && _burst ? _groundY : transform.position.y + settings.groundYOffset;
            float size = 8f;
            Renderer[] rs = GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                size = Mathf.Max(b.size.x, b.size.z) * 2.5f + 2f;
            }

            Vector3 center = transform.position;
            center.y = y;
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.2f);
            Gizmos.DrawCube(center, new Vector3(size, 0.01f, size));
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(center, new Vector3(size, 0.01f, size));
        }
#endif
    }
}
