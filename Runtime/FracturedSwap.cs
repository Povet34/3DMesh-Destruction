using UnityEngine;

namespace Povet.MeshDestruction
{
    // 미리 구워둔(캐싱된) 파편 프리팹을 폭발 시점에 원본과 교체하는 컴포넌트.
    // MeshSlicer 인스펙터의 "Slice & Save Prefab"으로 저장한 프리팹을 연결해서 사용함.
    public class FracturedSwap : MonoBehaviour
    {
        [Tooltip("Slice & Save Prefab으로 구워둔 파편 프리팹")]
        public GameObject fracturedPrefab;

        [Header("Explosion")]
        public float explosionForce = 300f;
        public float explosionRadius = 3f;
        public float upwardsModifier = 0.3f;

        [Tooltip("파편이 자동으로 사라지기까지의 시간. 0이면 유지")]
        public float chunkLifetime = 0f;

        public GameObject Explode()
        {
            return Explode(transform.position);
        }

        // explosionCenter는 월드 좌표
        public GameObject Explode(Vector3 explosionCenter)
        {
            if (fracturedPrefab == null)
            {
                Debug.LogWarning($"[FracturedSwap] {name}: fracturedPrefab이 비어 있음", this);
                return null;
            }

            // 파편 프리팹은 원본과 동일한 포즈로 구워졌으므로, 현재 포즈에 그대로 인스턴스화하면 이어짐
            GameObject instance = Instantiate(fracturedPrefab, transform.position, transform.rotation, transform.parent);

            foreach (Rigidbody rb in instance.GetComponentsInChildren<Rigidbody>())
                rb.AddExplosionForce(explosionForce, explosionCenter, explosionRadius, upwardsModifier);

            if (chunkLifetime > 0f)
                Destroy(instance, chunkLifetime);

            gameObject.SetActive(false);
            return instance;
        }
    }
}
