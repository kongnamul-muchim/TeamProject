using UnityEngine;

namespace HideAndInk.CameraSystem
{
    /// <summary>
    /// 타겟을 카메라가 부드럽게 추적
    /// SRP: 카메라 이동만 담당, DI: [SerializeField]로 타겟 참조
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [Header("DI - 추적 대상")]
        [Tooltip("카메라가 추적할 타겟")]
        [SerializeField] private Transform target;

        [Header("추적 설정")]
        [Tooltip("카메라 추적 속도")]
        [SerializeField] private float smoothSpeed = 5f;
        [Tooltip("카메라 위치 오프셋")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

        [Header("이동 축 제한")]
        [Tooltip("X축 추적 활성화")]
        [SerializeField] private bool followX = true;
        [Tooltip("Y축 추적 활성화")]
        [SerializeField] private bool followY = true;

        [Header("Z축 깊이 추적")]
        [Tooltip("Z축 깊이 추적 활성화")]
        [SerializeField] private bool followZ;
        [Tooltip("Z축 깊이 추적 계수")]
        [SerializeField] private float zDepthFactor = 0.3f;
        [Tooltip("Z축 기본 오프셋")]
        [SerializeField] private float zBaseOffset = -10f;

        [Header("X축 흔들림 (Sway)")]
        [Tooltip("X축 흔들림 효과 활성화")]
        [SerializeField] private bool enableXSway = true;
        [Tooltip("흔들림 진폭")]
        [SerializeField] private float swayAmplitude = 0.15f;
        [Tooltip("흔들림 진동수")]
        [SerializeField] private float swayFrequency = 0.5f;

        private float _originTargetY;
        private float _swayTimer;
        private bool _paused;

        /// <summary>카메라 추적이 일시정지 중인지 여부</summary>
        public bool IsPaused => _paused;

        private void Start()
        {
            if (target != null)
                _originTargetY = target.position.y;
        }

        private void LateUpdate()
        {
            if (_paused || target == null) return;

            Vector3 targetPos = target.position + offset;

            if (!followX) targetPos.x = transform.position.x;
            if (!followY) targetPos.y = transform.position.y;

            // Z축 깊이: 캐릭터가 위로 올라가면 카메라가 앞으로 다가감
            if (followZ)
            {
                float deltaY = target.position.y - _originTargetY;
                targetPos.z = zBaseOffset + deltaY * zDepthFactor;
            }

            // X축 흔들림: 부드러운 사인파로 좌우 미세 이동
            if (enableXSway)
            {
                _swayTimer += Time.deltaTime;
                targetPos.x += Mathf.Sin(_swayTimer * swayFrequency * Mathf.PI * 2f) * swayAmplitude;
            }

            transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _originTargetY = target != null ? target.position.y : 0f;
        }

        /// <summary>
        /// 카메라 추적을 일시정지합니다. 트랜지션 중 카메라 수동 이동 시 사용.
        /// </summary>
        public void Pause()
        {
            _paused = true;
        }

        /// <summary>
        /// 카메라 추적을 재개합니다. 타겟의 현재 위치로 즉시 이동 후 추적 재개.
        /// </summary>
        /// <param name="snapToTarget">재개 시 타겟 위치로 즉시 이동할지 여부</param>
        public void Resume(bool snapToTarget = true)
        {
            _paused = false;

            if (snapToTarget && target != null)
            {
                // 타겟 위치로 즉시 스냅 (Lerp 지연 없이)
                Vector3 targetPos = target.position + offset;

                if (!followX) targetPos.x = transform.position.x;
                if (!followY) targetPos.y = transform.position.y;

                if (followZ)
                {
                    float deltaY = target.position.y - _originTargetY;
                    targetPos.z = zBaseOffset + deltaY * zDepthFactor;
                }

                transform.position = targetPos;
                _originTargetY = target.position.y;
            }
        }
    }
}
