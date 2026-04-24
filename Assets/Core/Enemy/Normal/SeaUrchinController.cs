using UnityEngine;
using HideAndInk.Core.Events;
using HideAndInk.Core.Environment;

namespace HideAndInk.Core.Enemy.Normal
{
    /// <summary>
    /// 성게 (Sea Urchin) 컨트롤러
    /// 조류에 의해 굴러다니며 Player 접촉 시 둔부 효과
    /// 카메라 밖으로 나가면 일정 시간 후 풀로 반환
    /// </summary>
    public class SeaUrchinController : MonoBehaviour
    {
        [Header("둔부 설정")]
        [Tooltip("이동 속도 감소 비율 (0.5 = 50% 감소)")]
        [SerializeField] [Range(0f, 1f)] private float slowPercent = 0.5f;

        [Tooltip("둔부 지속 시간 (초)")]
        [SerializeField] private float slowDuration = 2f;

        [Tooltip("둔부 중첩 최대치")]
        [SerializeField] private int slowStackMax = 3;

        [Header("충돌 설정")]
        [Tooltip("성게 간 충돌 시 튕겨내는 힘")]
        [SerializeField] private float bounceForce = 3f;

        [Header("스폰 설정")]
        [Tooltip("Camera가 Player로부터 떨어진 Z 거리 (CameraFollow.offset.z의 절대값)")]
        [SerializeField] private float cameraZOffset = 10f;

        [Header("지면 감지 설정")]
        [Tooltip("지면 레이어 (Ground)")]
        [SerializeField] private LayerMask groundLayer = 1 << 8;

        [Tooltip("지면 탐색 최대 거리 (공중 소환 시 높이 고려)")]
        [SerializeField] private float groundCheckDistance = 100f;

        [Header("비활성화 설정")]
        [Tooltip("카메라 밖에서 비활성화까지 지연 시간 (초)")]
        [SerializeField] private float deactivateDelay = 2.5f;

        [Tooltip("X값 변동 임계값 (m). 이보다 작으면 정지로 간주")]
        [SerializeField] private float xThreshold = 0.1f;

        // 컴포넌트
        private Rigidbody _rigidbody;
        private Animator _animator;
        private SeaUrchinPool _pool;

        // 상태
        private bool _isOutsideCamera;
        private float _outsideTimer;
        private float _lastX;
        private float _stationaryTimer;
        private bool _isStationary;

        /// <summary>
        /// 풀 참조 설정
        /// </summary>
        public void SetPool(SeaUrchinPool pool)
        {
            _pool = pool;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();

            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
                _rigidbody.useGravity = false;
                _rigidbody.freezeRotation = false;
                _rigidbody.constraints = RigidbodyConstraints.FreezePositionY;
            }

            // Rigidbody가 이미 있든 새로 추가했든 항상 Damping 설정 (prefab 기본값 0 방지)
            _rigidbody.linearDamping = 2f;

            // Collider가 없으면 SphereCollider 자동 추가 (Trigger)
            Collider existingCollider = GetComponent<Collider>();
            if (existingCollider == null)
            {
                SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = 0.5f;
            }
        }

        private void Update()
        {
            // 카메라 밖 체크
            if (_isOutsideCamera)
            {
                _outsideTimer += Time.deltaTime;

                // X값 변동 체크
                float xDelta = Mathf.Abs(transform.position.x - _lastX);
                if (xDelta < xThreshold)
                {
                    _stationaryTimer += Time.deltaTime;
                }
                else
                {
                    _stationaryTimer = 0f;
                    _lastX = transform.position.x;
                }

                // 지연 시간 경과 또는 정지 상태면 풀로 반환
                if (_outsideTimer >= deactivateDelay || _stationaryTimer >= deactivateDelay)
                {
                    ReturnToPool();
                }
            }
            else
            {
                _outsideTimer = 0f;
                _stationaryTimer = 0f;
                _lastX = transform.position.x;
            }
        }

        /// <summary>
        /// 조류에 의한 소환 설정
        /// </summary>
        public void SetupForTide(TideDirection direction, float tideForce)
        {
            // 상태 초기화
            _isOutsideCamera = false;
            _outsideTimer = 0f;
            _stationaryTimer = 0f;
            _isStationary = false;

            // Z 위치를 게임 평면(0)으로 리셋 (풀 재사용 시 이전 Z값이 남아있는 문제 방지)
            Vector3 resetPos = transform.position;
            resetPos.z = 0f;
            transform.position = resetPos;
            _lastX = transform.position.x;

            // 카메라 밖에서 소환
            Vector3 spawnPos = GetSpawnPositionOutsideCamera(direction);
            spawnPos.y = 0f; // Y 초기화 후 AlignToGround에서 보정
            transform.position = spawnPos;

            // Rigidbody 초기화
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            // 지면에 맞춰 Y 위치 조정 (공중 생성 방지, Rigidbody freeze 전에 실행)
            AlignToGround();

            // 조류 방향에 따라 힘 적용
            // Left: 오른쪽→왼쪽 흐름 (왼쪽 방향으로 힘)
            // Right: 왼쪽→오른쪽 흐름 (오른쪽 방향으로 힘)
            Vector3 forceDirection = direction == TideDirection.Right ? Vector3.right : Vector3.left;
            _rigidbody.AddForce(forceDirection * tideForce, ForceMode.Impulse);

            // 애니메이션 재생 (구르기) - Animator + 파라미터 존재 시만
            if (_animator != null)
            {
                // Animator에 IsRolling 파라미터가 있을 때만 설정
                foreach (AnimatorControllerParameter param in _animator.parameters)
                {
                    if (param.name == "IsRolling" && param.type == AnimatorControllerParameterType.Bool)
                    {
                        _animator.SetBool("IsRolling", true);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Raycast로 지면을 감지하여 성게의 Y 위치를 지면에 맞춤
        /// useGravity=false + FreezePositionY 상태에서도 지면에 붙어있도록 보장
        /// </summary>
        private void AlignToGround()
        {
            Collider col = GetComponent<Collider>();
            if (col == null) return;

            // 콜라이더 하단에서 지면까지의 오프셋 계산
            float bottomOffset = transform.position.y - col.bounds.min.y;

            // 성게 위에서 아래로 Raycast
            Vector3 rayOrigin = transform.position + Vector3.up * (groundCheckDistance * 0.5f);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
            {
                Vector3 pos = transform.position;
                pos.y = hit.point.y + bottomOffset;
                transform.position = pos;

#if UNITY_EDITOR
                Debug.Log($"[SeaUrchin] Aligned to ground: Y={pos.y:F2} (hit ground at Y={hit.point.y:F2})");
#endif
            }
            else
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[SeaUrchin] No ground found below spawn position! Y={transform.position.y:F2}");
#endif
            }
        }

        /// <summary>
        /// 카메라 밖 소환 위치 계산
        /// 조류 흐름 방향의 반대쪽 가장자리에서 생성되어 흘러감
        /// </summary>
        private Vector3 GetSpawnPositionOutsideCamera(TideDirection direction)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                // 카메라 없으면 기본 위치 (조류 반대쪽에서 스폰)
                return direction == TideDirection.Left
                    ? new Vector3(20f, 0f, 0f)
                    : new Vector3(-20f, 0f, 0f);
            }

            // 조류 흐름 방향의 반대쪽 가장자리에서 생성
            // Left(오른쪽→왼쪽 흐름): 오른쪽 가장자리(viewport 1.2)에서 생성
            // Right(왼쪽→오른쪽 흐름): 왼쪽 가장자리(viewport -0.2)에서 생성
            float viewportX = direction == TideDirection.Left ? 1.2f : -0.2f;

            // Y 위치 랜덤화 (다양한 높이에서 생성되어 굴러감)
            float viewportY = Random.Range(0.1f, 0.9f);

            // Z 거리: Camera는 항상 Player로부터 offset.z = -10만큼 떨어져 있음
            // ViewportToWorldPoint의 Z 파라미터는 카메라 전방 기준 거리
            // gamePlaneZ = cameraZ + cameraZOffset, 거리 = |cameraZ - gamePlaneZ| = cameraZOffset
            Vector3 viewportPos = new Vector3(viewportX, viewportY, cameraZOffset);
            Vector3 worldPos = mainCamera.ViewportToWorldPoint(viewportPos);
            worldPos.y = 0f; // Y는 AlignToGround에서 보정, 여기선 0으로 초기화

            return worldPos;
        }

        /// <summary>
        /// 접촉 감지 (Trigger Collider)
        /// - Player: 둔부 효과
        /// - 다른 성게: 튕겨냄
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            // Player 접촉 → 둔부
            if (other.CompareTag("Player"))
            {
                EnemyEvents.InvokePlayerSlowed(transform.position, slowPercent, slowDuration);

#if UNITY_EDITOR
                Debug.Log($"[SeaUrchin] Player slowed! Percent: {slowPercent}, Duration: {slowDuration}s");
#endif
                return;
            }

            // 다른 성게 접촉 → 튕겨냄
            SeaUrchinController otherUrchin = other.GetComponent<SeaUrchinController>();
            if (otherUrchin != null)
            {
                Vector3 pushDirection = (transform.position - otherUrchin.transform.position).normalized;
                pushDirection.y = 0f;

                _rigidbody.AddForce(pushDirection * bounceForce, ForceMode.Impulse);
                otherUrchin._rigidbody.AddForce(-pushDirection * bounceForce, ForceMode.Impulse);
            }
        }

        /// <summary>
        /// 물리 충돌 시 튕겨냄 (Trigger가 아닌 Collider용 fallback)
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            SeaUrchinController otherUrchin = collision.gameObject.GetComponent<SeaUrchinController>();
            if (otherUrchin != null)
            {
                Vector3 pushDirection = (transform.position - otherUrchin.transform.position).normalized;
                pushDirection.y = 0f;

                _rigidbody.AddForce(pushDirection * bounceForce, ForceMode.Impulse);
                otherUrchin._rigidbody.AddForce(-pushDirection * bounceForce, ForceMode.Impulse);
            }
        }

        /// <summary>
        /// 카메라 렌더러 가시성 체크
        /// </summary>
        private void OnBecameInvisible()
        {
            if (!_isOutsideCamera)
            {
                _isOutsideCamera = true;
                _outsideTimer = 0f;
                _lastX = transform.position.x;
            }
        }

        private void OnBecameVisible()
        {
            _isOutsideCamera = false;
            _outsideTimer = 0f;
            _stationaryTimer = 0f;
        }

        /// <summary>
        /// 풀로 반환
        /// </summary>
        private void ReturnToPool()
        {
            if (_pool != null)
            {
                _pool.Return(this);
            }
            else
            {
                // 풀이 없으면 비활성화
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 상태 초기화 (풀 반환 시 호출)
        /// </summary>
        public void ResetState()
        {
            _isOutsideCamera = false;
            _outsideTimer = 0f;
            _stationaryTimer = 0f;
            _isStationary = false;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            if (_animator != null)
            {
                // Animator에 IsRolling 파라미터가 있을 때만 설정
                foreach (AnimatorControllerParameter param in _animator.parameters)
                {
                    if (param.name == "IsRolling" && param.type == AnimatorControllerParameterType.Bool)
                    {
                        _animator.SetBool("IsRolling", false);
                        break;
                    }
                }
            }
        }
    }
}
