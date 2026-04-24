using System.Collections;
using UnityEngine;
using HideAndInk.Core.Events;
using HideAndInk.Core.Environment;

namespace HideAndInk.Core.Enemy.Normal
{
    /// <summary>
    /// 성게 (Sea Urchin) 컨트롤러
    /// - 조류에 의해 X축으로 굴러다님
    /// - Ground Z 범위 내에서 랜덤 Z 위치에 소환
    /// - Player 접촉 시 성게가 달라붙어 속도 감소 시각화
    /// - 서서히 사라지면서 속도도 천천히 회복
    /// - Ground 이탈 시 제거
    /// </summary>
    public class SeaUrchinController : MonoBehaviour
    {
        [Header("둔부 설정")]
        [Tooltip("이동 속도 감소 비율 (0.5 = 50% 감소)")]
        [SerializeField] [Range(0f, 1f)] private float slowPercent = 0.5f;

        [Tooltip("둔부 지속 시간 = 성게가 사라지는 시간 (초)")]
        [SerializeField] private float slowDuration = 2f;

        [Header("충돌 설정")]
        [Tooltip("성게 간 충돌 시 튕겨내는 힘")]
        [SerializeField] private float bounceForce = 3f;

        [Header("스폰 설정")]
        [Tooltip("Camera로부터의 Z 거리 offset")]
        [SerializeField] private float cameraZOffset = 10f;

        [Tooltip("Ground Z 범위 최소값 (성게 소환 Z 위치)")]
        [SerializeField] private float spawnZMin = -3.0f;

        [Tooltip("Ground Z 범위 최대값 (성게 소환 Z 위치)")]
        [SerializeField] private float spawnZMax = 0.0f;

        [Header("속도 랜덤 설정")]
        [Tooltip("조류 힘에 곱해질 속도 배율 최소값 (0.5 = 절반 속도)")]
        [SerializeField] private float minSpeedMultiplier = 0.5f;

        [Tooltip("조류 힘에 곱해질 속도 배율 최대값 (1.5 = 1.5배 속도)")]
        [SerializeField] private float maxSpeedMultiplier = 1.5f;

        [Header("지면 감지 설정")]
        [Tooltip("지면 레이어 (Ground)")]
        [SerializeField] private LayerMask groundLayer = 1 << 8;

        [Tooltip("지면 탐색 최대 거리")]
        [SerializeField] private float groundCheckDistance = 5f;

        [Tooltip("Ground 이탈 후 제거 대기 시간 (초)")]
        [SerializeField] private float groundLossDelay = 1.0f;

        [Tooltip("지면 위로 띄울 오프셋 (메시가 땅에 파묻히는 현상 방지)")]
        [SerializeField] private float groundOffset = 0.15f;

        [Header("정지 감지 설정")]
        [Tooltip("정지 판정 속도 임계값 (m/s)")]
        [SerializeField] private float stationaryVelocityThreshold = 0.05f;

        [Tooltip("정지 후 풀 반환 대기 시간 (초)")]
        [SerializeField] private float stationaryDeactivateDelay = 2.5f;

        [Header("비활성화 설정")]
        [Tooltip("카메라 밖 타임아웃 (초). 이 시간 이상 보이지 않으면 강제 반환")]
        [SerializeField] private float cameraTimeoutDelay = 10f;

        // 컴포넌트
        private Rigidbody _rigidbody;
        private Animator _animator;
        private SeaUrchinPool _pool;
        private SpriteRenderer _spriteRenderer;
        private Collider _collider;

        // 상태 - 카메라/정지/Ground
        private bool _isOutsideCamera;
        private float _outsideTimer;
        private float _stationaryTimer;
        private float _groundLossTimer;

        // 상태 - Player 부착
        private bool _isAttached;
        private Coroutine _fadeCoroutine;

        // 캐싱
        private bool _hasRollingParam;
        private bool _hasSpriteRenderer;
        private Color _originalColor;
        private Vector3 _originalScale;
        private static readonly int IsRollingHash = Animator.StringToHash("IsRolling");

        public void SetPool(SeaUrchinPool pool) => _pool = pool;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            _collider = GetComponent<Collider>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _originalScale = transform.localScale;

            if (_rigidbody == null)
                _rigidbody = gameObject.AddComponent<Rigidbody>();

            // Rigidbody: Z축은 자유 (Ground Z 범위 내에서 랜덤 소환)
            _rigidbody.useGravity = false;
            _rigidbody.freezeRotation = false;
            _rigidbody.linearDamping = 0.5f;
            _rigidbody.constraints = RigidbodyConstraints.FreezePositionY;

            // Collider: Trigger 통일
            if (_collider != null)
            {
                _collider.isTrigger = true;
            }
            else
            {
                SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = 0.5f;
                _collider = sphere;
            }

            // SpriteRenderer 캐싱 (알파 페이드용)
            if (_spriteRenderer != null)
            {
                _hasSpriteRenderer = true;
                _originalColor = _spriteRenderer.color;
            }

            // Animator IsRolling 파라미터 캐싱
            if (_animator != null)
            {
                foreach (AnimatorControllerParameter param in _animator.parameters)
                {
                    if (param.name == "IsRolling" && param.type == AnimatorControllerParameterType.Bool)
                    {
                        _hasRollingParam = true;
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            if (_isAttached) return; // Player에 붙었으면 Update 스킵

            CheckGroundBelow();
            UpdateStationaryDetection();

            if (_isOutsideCamera)
            {
                _outsideTimer += Time.deltaTime;
                if (_outsideTimer >= cameraTimeoutDelay)
                    ReturnToPool();
            }
        }

        private void UpdateStationaryDetection()
        {
            float speed = _rigidbody.linearVelocity.magnitude;
            if (speed < stationaryVelocityThreshold)
            {
                _stationaryTimer += Time.deltaTime;
                if (_stationaryTimer >= stationaryDeactivateDelay)
                    ReturnToPool();
            }
            else
            {
                _stationaryTimer = 0f;
            }
        }

        private void CheckGroundBelow()
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out _, groundCheckDistance, groundLayer))
            {
                _groundLossTimer = 0f;
            }
            else
            {
                _groundLossTimer += Time.deltaTime;
                if (_groundLossTimer >= groundLossDelay)
                    ReturnToPool();
            }
        }

        /// <summary>
        /// 조류에 의한 소환 설정
        /// X = 카메라 가장자리, Z = Ground 범위 내 랜덤
        /// </summary>
        public void SetupForTide(TideDirection direction, float tideForce)
        {
            // 상태 초기화
            _isOutsideCamera = false;
            _outsideTimer = 0f;
            _stationaryTimer = 0f;
            _groundLossTimer = 0f;
            _isAttached = false;

            // 스케일/알파 리셋
            transform.localScale = _originalScale;
            if (_hasSpriteRenderer)
            {
                Color c = _originalColor;
                c.a = 1f;
                _spriteRenderer.color = c;
            }

            // Rigidbody 리셋
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            // 소환 위치: X는 카메라 가장자리, Z는 Ground 범위 내 랜덤
            Vector3 spawnPos = GetSpawnPositionOutsideCamera(direction);
            spawnPos.y = 0f;
            spawnPos.z = Random.Range(spawnZMin, spawnZMax);
            transform.position = spawnPos;

            // X축 랜덤 오프셋 (여러 마리 겹침 방지)
            Vector3 offsetPos = transform.position;
            offsetPos.x += Random.Range(-1.0f, 1.0f);
            transform.position = offsetPos;

            // 지면 정렬
            AlignToGround();

            // X축 속도 직접 설정 + 랜덤 배율 (성게마다 속도 달라서 예측 어려움)
            Vector3 forceDirection = direction == TideDirection.Right ? Vector3.right : Vector3.left;
            float speedMultiplier = Random.Range(minSpeedMultiplier, maxSpeedMultiplier);
            float impulseVelocity = (tideForce * speedMultiplier) / Mathf.Max(_rigidbody.mass, 0.001f);
            _rigidbody.linearVelocity += forceDirection * impulseVelocity;

#if UNITY_EDITOR
            Debug.Log($"[SeaUrchin] Spawned at ({transform.position.x:F1}, {transform.position.y:F1}, {transform.position.z:F1}) | Velocity: {_rigidbody.linearVelocity}");
#endif

            // 애니메이션
            if (_animator != null && _hasRollingParam)
                _animator.SetBool(IsRollingHash, true);
        }

        private void AlignToGround()
        {
            if (_collider == null) return;

            float bottomOffset = transform.position.y - _collider.bounds.min.y;
            Vector3 rayOrigin = transform.position + Vector3.up * (groundCheckDistance * 0.5f);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
            {
                Vector3 pos = transform.position;
                pos.y = hit.point.y + bottomOffset + groundOffset;
                transform.position = pos;
            }
        }

        private Vector3 GetSpawnPositionOutsideCamera(TideDirection direction)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return direction == TideDirection.Left
                    ? new Vector3(20f, 0f, 0f)
                    : new Vector3(-20f, 0f, 0f);
            }

            float viewportX = direction == TideDirection.Left ? 1.2f : -0.2f;
            float viewportY = Random.Range(0.1f, 0.9f);
            Vector3 viewportPos = new Vector3(viewportX, viewportY, cameraZOffset);
            Vector3 worldPos = mainCamera.ViewportToWorldPoint(viewportPos);
            worldPos.y = 0f;
            worldPos.z = 0f; // SetupForTide에서 덮어씀
            return worldPos;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isAttached) return;

            // Player 접촉 → 달라붙음 + 둔부
            if (other.CompareTag("Player"))
            {
                AttachToPlayer(other.transform);
                EnemyEvents.InvokePlayerSlowed(transform.position, slowPercent, slowDuration);
                return;
            }

            // 다른 성게 충돌 (X축으로만 튕김)
            SeaUrchinController otherUrchin = other.GetComponent<SeaUrchinController>();
            if (otherUrchin != null && !otherUrchin._isAttached)
            {
                Vector3 pushDir = (transform.position - otherUrchin.transform.position).normalized;
                pushDir.y = 0f;
                pushDir.z = 0f;
                if (pushDir.sqrMagnitude > 0f) pushDir.Normalize();

                _rigidbody.AddForce(pushDir * bounceForce, ForceMode.Impulse);
                otherUrchin._rigidbody.AddForce(-pushDir * bounceForce, ForceMode.Impulse);
            }
        }

        /// <summary>
        /// Player에 달라붙어 속도 감소 시각화
        /// </summary>
        private void AttachToPlayer(Transform playerTransform)
        {
            _isAttached = true;

            // 물리 정지
            _rigidbody.isKinematic = true;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            // Player 위에 부착
            transform.SetParent(playerTransform);
            transform.localPosition = new Vector3(0f, 0.5f, 0f);

            // 페이드 아웃 시작 (slowDuration 동안 서서히 사라짐)
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutOnPlayer());
        }

        /// <summary>
        /// slowDuration 동안 스케일 감소 + 알파 페이드
        /// </summary>
        private IEnumerator FadeOutOnPlayer()
        {
            float elapsed = 0f;

            while (elapsed < slowDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slowDuration;
                float eased = t * t; // ease-in: 처음엔 천천히, 나중에 빠르게

                // 스케일 감소
                transform.localScale = Vector3.Lerp(_originalScale, _originalScale * 0.3f, eased);

                // 알파 페이드
                if (_hasSpriteRenderer)
                {
                    Color c = _originalColor;
                    c.a = Mathf.Lerp(1f, 0f, eased);
                    _spriteRenderer.color = c;
                }

                yield return null;
            }

            _fadeCoroutine = null;
            ReturnToPool();
        }

        private void OnBecameInvisible()
        {
            if (!_isOutsideCamera && !_isAttached)
            {
                _isOutsideCamera = true;
                _outsideTimer = 0f;
            }
        }

        private void OnBecameVisible()
        {
            _isOutsideCamera = false;
            _outsideTimer = 0f;
        }

        private void ReturnToPool()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
            _pool?.Return(this);
        }

        public void ResetState()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            _isOutsideCamera = false;
            _outsideTimer = 0f;
            _stationaryTimer = 0f;
            _groundLossTimer = 0f;
            _isAttached = false;

            transform.localScale = _originalScale;

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            if (_hasSpriteRenderer)
            {
                Color c = _originalColor;
                c.a = 1f;
                _spriteRenderer.color = c;
            }

            if (_animator != null && _hasRollingParam)
                _animator.SetBool(IsRollingHash, false);
        }
    }
}
