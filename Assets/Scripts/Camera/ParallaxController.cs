using System;
using UnityEngine;

namespace HideAndInk.ParallaxSystem
{
    /// <summary>
    /// 패럴랙스 중앙 제어기: 카메라 이동에 따라 배경 레이어들의 위치를 계산한다.
    /// 
    /// CameraAnchored 모드: 배경이 항상 화면에 유지되며 원근감 표현
    /// WorldAnchored 모드: 월드 좌표 기준 (전경 오브젝트용)
    /// 
    /// 각 레이어는 이 컨트롤러의 자식 오브젝트로 설정하고,
    /// Inspector에서 rate, anchorMode 등을 개별 조정한다.
    /// 
    /// SRP: 카메라 추적 및 레이어 위치 계산만 담당
    /// DI: [SerializeField]로 카메라 참조
    /// </summary>
    public sealed class ParallaxController : MonoBehaviour
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

        // ── 레이어 데이터 ──

        [System.Serializable]
        public class ParallaxLayerData
        {
            [Tooltip("패럴랙스를 적용할 Transform")]
            public Transform target;

            [Header("위치 기준 모드")]
            public AnchorMode anchorMode = AnchorMode.CameraAnchored;

            [Header("X축 보간 비율 (0=최대원경, 1=고정)")]
            [Range(0f, 1f)]
            public float rate = 0.3f;

            [Header("Y축 설정")]
            public bool applyY;
            [Range(0f, 1f)]
            public float yRate = 0.2f;

            // 런타임 전용
            [System.NonSerialized] public Vector3 originPos;
            [System.NonSerialized] public bool initialized;
        }

        // ── DI ──

        [Header("DI - 추적할 카메라 (미할당 시 Camera.main)")]
        [SerializeField] private Camera targetCamera;

        [Header("패럴랙스 레이어 목록")]
        [SerializeField] private ParallaxLayerData[] layers = new ParallaxLayerData[0];

        [Header("전역 속도 배율 (0=정지, 1=정상, 2=2배속)")]
        [Range(0f, 5f)]
        [SerializeField] private float globalSpeedMultiplier = 1f;

        // ── 내부 상태 ──

        private Vector3 _previousCameraPosition;
        private bool _isInitialized;

        // ── Unity 라이프사이클 ──

        private void Awake()
        {
            ResolveCamera();
        }

        private void Start()
        {
            ResolveCamera();

            if (targetCamera != null)
            {
                _previousCameraPosition = targetCamera.transform.position;
                _isInitialized = true;
            }

            InitializeLayers();
        }

        private void LateUpdate()
        {
            // 참조 카메라가 비활성이면 활성 카메라로 전환
            if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            {
                ResolveCamera();
                if (targetCamera == null) return;

                _previousCameraPosition = targetCamera.transform.position;
                _isInitialized = true;
                InitializeLayers();
            }

            if (!_isInitialized) return;

            Vector3 cameraPos = targetCamera.transform.position;

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer.target == null || !layer.initialized) continue;

                float originOffsetX = layer.originPos.x - _previousCameraPosition.x;
                float originOffsetY = layer.originPos.y - _previousCameraPosition.y;

                float newX;
                float newY;

                switch (layer.anchorMode)
                {
                    case AnchorMode.CameraAnchored:
                        newX = cameraPos.x + originOffsetX * (1f - layer.rate);
                        newY = layer.applyY
                            ? cameraPos.y + originOffsetY * (1f - layer.yRate)
                            : layer.target.position.y;
                        break;

                    case AnchorMode.WorldAnchored:
                    default:
                        newX = layer.originPos.x + (cameraPos.x - _previousCameraPosition.x) * layer.rate;
                        newY = layer.applyY
                            ? layer.originPos.y + (cameraPos.y - _previousCameraPosition.y) * layer.yRate
                            : layer.target.position.y;
                        break;
                }

                layer.target.position = new Vector3(newX, newY, layer.target.position.z);
            }

            _previousCameraPosition = cameraPos;
        }

        // ── 공개 메서드 ──

        /// <summary>카메라를 수동으로 설정한다.</summary>
        public void SetTargetCamera(Camera camera)
        {
            targetCamera = camera;
            if (camera != null)
            {
                _previousCameraPosition = camera.transform.position;
                _isInitialized = true;
                InitializeLayers();
            }
        }

        /// <summary>패럴랙스를 일시정지/재개한다.</summary>
        public void SetPaused(bool paused)
        {
            globalSpeedMultiplier = paused ? 0f : 1f;
        }

        /// <summary>전역 속도 배율을 설정한다.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            globalSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>모든 레이어의 원점을 현재 위치로 재설정한다.</summary>
        public void ResetOrigins()
        {
            InitializeLayers();
        }

        // ── 내부 메서드 ──

        private void ResolveCamera()
        {
            if (targetCamera != null && targetCamera.isActiveAndEnabled)
                return;

            var mainCam = Camera.main;
            if (mainCam != null)
                targetCamera = mainCam;
        }

        private void InitializeLayers()
        {
            if (targetCamera == null) return;

            Vector3 camPos = targetCamera.transform.position;

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer.target == null) continue;

                layer.originPos = layer.target.position;
                layer.initialized = true;

                Debug.Log($"[ParallaxController] Layer '{layer.target.name}' | " +
                          $"mode={layer.anchorMode} | rate={layer.rate:F2} | " +
                          $"origin={layer.originPos} | cam={camPos}");
            }
        }
    }
}