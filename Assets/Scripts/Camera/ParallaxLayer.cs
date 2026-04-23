using UnityEngine;

namespace HideAndInk.ParallaxSystem
{
    /// <summary>
    /// 패럴랙스 레이어: 카메라 이동에 따라 원근감 있게 배경을 이동시킨다.
    /// 
    /// 동작 모드:
    /// - Controller 구독 모드: ParallaxController가 있으면 이벤트로 delta를 받아 이동 (권장)
    /// - 독립 모드: Controller가 없으면 자체 LateUpdate에서 카메라를 추적 (Fallback)
    /// 
    /// SRP: 위치 계산만 담당, DI: [SerializeField]로 설정 참조
    /// OCP: IParallaxLayer 구현으로 새 레이어 타입 확장 가능
    /// </summary>
    public sealed class ParallaxLayer : MonoBehaviour, IParallaxLayer
    {
        [Header("DI - 추적할 카메라 (미할당 시 Camera.main)")]
        [SerializeField] private UnityEngine.Camera targetCamera;

        [Header("X축 보간 비율 (0=최대원근, 1=고정)")]
        [Range(0f, 1f)]
        [SerializeField] private float rate = 0.5f;

        [Header("Y축 설정")]
        [SerializeField] private bool applyY;
        [SerializeField] private float yRate = 0.2f;

        // ── IParallaxLayer 구현 ──
        public float SpeedRatio => rate;

        // ── 내부 상태 ──
        private Vector3 _originLayerPos;
        private Vector3 _originCameraPos;
        private bool _isInitialized;
        private bool _isControllerDriven;

        // ── 공개 메서드 ──

        /// <summary>초기 위치와 카메라 위치를 기록한다.</summary>
        public void Initialize()
        {
            if (targetCamera == null)
                targetCamera = UnityEngine.Camera.main;

            if (targetCamera != null)
            {
                _originLayerPos = transform.position;
                _originCameraPos = targetCamera.transform.position;
                _isInitialized = true;
            }
        }

        /// <summary>Controller로부터 delta를 받아 위치를 갱신한다.</summary>
        public void ApplyOffset(Vector3 delta)
        {
            if (!_isInitialized) return;

            // Controller 구독 모드: 누적 delta 대신 origin 기반 절대 위치 계산
            Vector3 cameraPos = targetCamera.transform.position;
            float newX = _originLayerPos.x + (cameraPos.x - _originCameraPos.x) * rate;
            float newY = transform.position.y;
            if (applyY)
                newY = _originLayerPos.y + (cameraPos.y - _originCameraPos.y) * yRate;

            transform.position = new Vector3(newX, newY, transform.position.z);
        }

        /// <summary>Controller가 이 레이어를 관리한다고 알린다. 독립 모드를 비활성화한다.</summary>
        public void SetControllerDriven(bool driven)
        {
            _isControllerDriven = driven;
        }

        // ── Unity 라이프사이클 ──

        private void Start()
        {
            Initialize();
        }

        private void LateUpdate()
        {
            // Controller 구독 모드면 자체 이동 로직 스킵
            if (_isControllerDriven) return;
            if (!_isInitialized || targetCamera == null) return;

            Vector3 cameraPos = targetCamera.transform.position;
            float newX = _originLayerPos.x + (cameraPos.x - _originCameraPos.x) * rate;
            float newY = transform.position.y;
            if (applyY)
                newY = _originLayerPos.y + (cameraPos.y - _originCameraPos.y) * yRate;

            transform.position = new Vector3(newX, newY, transform.position.z);
        }
    }
}