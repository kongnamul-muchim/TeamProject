using UnityEngine;

namespace HideAndInk.Core.Enemy.Boss
{
    /// <summary>
    /// 가자미(Flounder)가 남기는 모래 구덩이
    /// Player가 밟으면 의심도 증가, 일정 시간 후 소멸
    /// </summary>
    public class SandPit : MonoBehaviour
    {
        [Header("구덩이 설정")]
        [SerializeField, Tooltip("구덩이 지속 시간")] private float lifetime = 8f;
        [SerializeField, Tooltip("트리거 반경")] private float triggerRadius = 1.5f;

        /// <summary>
        /// Player가 구덩이를 밟았을 때 발생 (Controller에서 의심도 처리)
        /// </summary>
        public System.Action<Vector3> OnPlayerEnterPit;

        private Collider[] _overlapResult = new Collider[4];
        private bool _hasTriggered;

        private void Start()
        {
            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            if (_hasTriggered) return;

            // Tag 기반 감지 (LayerMask 없이 모든 Collider 검색 → Tag 필터)
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, triggerRadius, _overlapResult);

            for (int i = 0; i < count; i++)
            {
                if (_overlapResult[i].CompareTag("Player"))
                {
                    _hasTriggered = true;
                    OnPlayerEnterPit?.Invoke(transform.position);

                    // 밟힌 구덩이는 즉시 소멸 (같은 프레임 Update 재실행 방지)
                    enabled = false;
                    Destroy(gameObject);
                    return;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.6f, 0.2f, 0.3f);
            Gizmos.DrawSphere(transform.position, triggerRadius);
        }
    }
}
