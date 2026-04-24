using UnityEngine;

namespace Player.Visual
{
    /// <summary>
    /// 부모 오브젝트의 이동 방향을 감지하여 파티클의 방출 방향(Z축 회전)을 반대로 자동 조절합니다.
    /// 주로 캐릭터가 회전하지 않고 스프라이트만 교체되는 방식에서 사용합니다.
    /// </summary>
    public class InkDirectionController : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("속도가 이 값보다 작으면 방향을 업데이트하지 않습니다.")]
        [SerializeField] private float threshold = 0.1f;
        
        [Tooltip("반대 방향으로 회전할지 여부 (먹물의 경우 뒤로 뿜어야 하므로 true)")]
        [SerializeField] private bool flipDirection = true;

        private Rigidbody _parentRigidbody;

        private void Start()
        {
            // 부모 또는 최상위 부모로부터 Rigidbody(3D)를 찾습니다.
            _parentRigidbody = GetComponentInParent<Rigidbody>();
            
            if (_parentRigidbody == null)
            {
                Debug.LogWarning($"[InkDirectionController] 부모 오브젝트에서 Rigidbody를 찾을 수 없습니다. {gameObject.name}");
            }
        }

        private void Update()
        {
            if (_parentRigidbody == null) return;

            // 이동 속도 벡터를 가져옵니다. (Unity 6의 linearVelocity 지원)
            Vector3 velocity = _parentRigidbody.linearVelocity;

            // 속도가 임계값보다 클 때만 방향을 계산합니다.
            // XZ 평면 이동이므로 X축과 Z축의 속도 합을 체크합니다.
            Vector2 velocityXZ = new Vector2(velocity.x, velocity.z);

            if (velocityXZ.sqrMagnitude > threshold * threshold)
            {
                float multiplier = flipDirection ? -1f : 1f;
                
                // XZ 평면에서의 각도를 계산합니다. (Atan2의 인자 순서: Z, X)
                float angle = Mathf.Atan2(velocityXZ.x * multiplier, velocityXZ.y * multiplier) * Mathf.Rad2Deg;

                // 파티클 시스템 오브젝트의 Y축 회전값을 업데이트합니다.
                // 바닥면을 따라 회전하므로 Y축을 기준으로 돕니다.
                transform.rotation = Quaternion.Euler(0, angle, 0);
            }
        }
    }
}
