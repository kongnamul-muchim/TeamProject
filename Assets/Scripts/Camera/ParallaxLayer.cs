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
    /// 위치 기준:
    /// - CameraAnchored (기본): 카메라 기준 위치. 배경이 항상 화면에 유지됨.
    ///   rate=0 → 카메라와 동일 속도 (원경, 오프셋만 다름)
    ///   rate=1 → 카메라 중심에 고정 (전경)
    /// 
    /// - WorldAnchored: 월드 기준 위치. 전경 오브젝트에 적합.
    ///   rate=0 → 월드에 고정 (배경이 화면 밖으로 사라질 수 있음)
    ///   rate=1 → 카메라와 1:1 이동
    /// 
    /// SRP: 위치 계산만 담당, DI: [SerializeField]로 설정 참조
    /// OCP: IParallaxLayer 구현으로 새 레이어 타입 확장 가능
    /// </summary>
    public sealed class ParallaxLayer : MonoBehaviour, IParallaxLayer
    {
        // ── 열거형 ──

        /// <summary>패럴랙스 위치 기준 모드</summary>
        public enum AnchorMode
        {
            /// <summary>카메라 기준: 배경이 항상 화면에 유지됨 (배경 레이어용)</summary>
            CameraAnchored,

            /// <summary>월드 기준: 월드 좌표에 고정 (전경 오브젝트용)</summary>
            WorldAnchored
        }

        // ── DI ──

        [Header("DI - 추적할 카메라 (미할당 시 Camera.main)")]
        [SerializeField] private UnityEngine.Camera targetCamera;

        [Header("위치 기준 모드")]
        [SerializeField] private AnchorMode anchorMode = AnchorMode.CameraAnchored;

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
            ResolveCamera();

            if (targetCamera != null)
            {
                _originLayerPos = transform.position;
                _originCameraPos = targetCamera.transform.position;
                _isInitialized = true;

                Debug.Log($"[ParallaxLayer] '{name}' 초기화 | " +
                          $"mode={anchorMode} | rate={rate:F2} | " +
                          $"originLayer={_originLayerPos} | originCamera={_originCameraPos} | " +
                          $"camera={targetCamera.name}");
            }
            else
            {
                Debug.LogWarning($"[ParallaxLayer] '{name}' 카메라를 찾을 수 없음!");
            }
        }

        /// <summary>참조 카메라가 비활성이면 Camera.main으로 대체한다.</summary>
        private void ResolveCamera()
        {
            if (targetCamera != null && targetCamera.isActiveAndEnabled)
                return;

            var mainCam = UnityEngine.Camera.main;
            if (mainCam != null)
                targetCamera = mainCam;
        }

        /// <summary>카메라 위치에 따라 레이어 위치를 계산한다.</summary>
        private Vector3 CalculatePosition(Vector3 cameraPos)
        {
            float originOffsetX = _originLayerPos.x - _originCameraPos.x;
            float originOffsetY = _originLayerPos.y - _originCameraPos.y;

            float newX;
            float newY;

            switch (anchorMode)
            {
                // 카메라 기준: 배경이 항상 화면에 유지됨
                // rate=0 → 카메라 위치 + 전체 오프셋 (원경, 카메라와 같은 속도로 이동)
                // rate=1 → 카메라 위치 (전경, 카메라 중심에 고정)
                case AnchorMode.CameraAnchored:
                    newX = cameraPos.x + originOffsetX * (1f - rate);
                    newY = applyY
                        ? cameraPos.y + originOffsetY * (1f - yRate)
                        : transform.position.y;
                    break;

                // 월드 기준: 기존 동작 (전경 오브젝트용)
                // rate=0 → 월드에 고정 (배경이 화면 밖으로 사라질 수 있음)
                // rate=1 → 카메라와 1:1 이동
                case AnchorMode.WorldAnchored:
                default:
                    newX = _originLayerPos.x + (cameraPos.x - _originCameraPos.x) * rate;
                    newY = applyY
                        ? _originLayerPos.y + (cameraPos.y - _originCameraPos.y) * yRate
                        : transform.position.y;
                    break;
            }

            return new Vector3(newX, newY, transform.position.z);
        }

        // ── 디버그 로그 ──

        private float _logInterval;
        private float _lastLogTime;

        private void LogPosition(Vector3 cameraPos, Vector3 newPos)
        {
            if (Time.time - _lastLogTime < _logInterval) return;
            _lastLogTime = Time.time;

            float originOffsetX = _originLayerPos.x - _originCameraPos.x;
            Debug.Log($"[ParallaxLayer] '{name}' | mode={anchorMode} | rate={rate:F2} | " +
                      $"camX={cameraPos.x:F2} | originCamX={_originCameraPos.x:F2} | " +
                      $"offsetX={originOffsetX:F2} | resultX={newPos.x:F2}");
        }

        /// <summary>Controller로부터 delta를 받아 위치를 갱신한다.</summary>
        public void ApplyOffset(Vector3 delta)
        {
            if (!_isInitialized) return;

            Vector3 cameraPos = targetCamera.transform.position;
            transform.position = CalculatePosition(cameraPos);
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
            _logInterval = 1f; // 1초마다 로그 출력
        }

        private void LateUpdate()
        {
            // Controller 구독 모드면 자체 이동 로직 스킵
            if (_isControllerDriven) return;

            // 참조 카메라가 비활성이면 활성 카메라로 전환
            if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            {
                ResolveCamera();
                if (targetCamera == null) return;

                // 카메라가 전환되었으므로 원점 재설정
                _originLayerPos = transform.position;
                _originCameraPos = targetCamera.transform.position;
                Debug.Log($"[ParallaxLayer] '{name}' 카메라 전환 → {targetCamera.name} | origin 재설정");
            }

            if (!_isInitialized) return;

            Vector3 cameraPos = targetCamera.transform.position;
            Vector3 newPos = CalculatePosition(cameraPos);
            transform.position = newPos;

            LogPosition(cameraPos, newPos);
        }
    }
}