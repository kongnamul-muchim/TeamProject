using UnityEngine;

namespace HideAndInk.ParallaxSystem
{
    /// <summary>
    /// 카메라 X 이동에 따라 레이어 좌표를 보간하여 원근감 생성
    /// 
    /// rate = 1 → 카메라와 동일하게 이동 → 화면상 움직임 0 (고정)
    /// rate = 0 → 전혀 이동 안함 → 최대 원근감
    /// rate = 0.5 → 카메라 이동의 절반 → 중간 원근감
    /// 
    /// 공식: layerX = originLayerX + (cameraX - originCameraX) * rate
    /// 
    /// SRP: 좌표 보간만 담당
    /// DI: [SerializeField]로 카메라 참조
    /// </summary>
    public sealed class ParallaxLayer : MonoBehaviour, IParallaxLayer
    {
        [Header("DI - 추적할 카메라")]
        [SerializeField] private UnityEngine.Camera targetCamera;

        [Header("보간 비율 (0=최대원근, 1=고정)")]
        [SerializeField] private float rate = 0.5f;

        [Header("Y축도 보간 적용")]
        [SerializeField] private bool applyY;

        private float _originLayerX;
        private float _originLayerY;
        private float _originCameraX;
        private float _originCameraY;
        private bool _isInitialized;

        public float SpeedRatio => rate;

        private void Start()
        {
            if (targetCamera == null)
                targetCamera = UnityEngine.Camera.main;

            if (targetCamera != null)
            {
                _originLayerX = transform.position.x;
                _originLayerY = transform.position.y;
                _originCameraX = targetCamera.transform.position.x;
                _originCameraY = targetCamera.transform.position.y;
                _isInitialized = true;
            }
        }

        private void LateUpdate()
        {
            if (!_isInitialized || targetCamera == null) return;

            float cameraDeltaX = targetCamera.transform.position.x - _originCameraX;
            float newX = _originLayerX + cameraDeltaX * rate;

            float newY = transform.position.y;
            if (applyY)
            {
                float cameraDeltaY = targetCamera.transform.position.y - _originCameraY;
                newY = _originLayerY + cameraDeltaY * rate;
            }

            transform.position = new Vector3(newX, newY, transform.position.z);
        }

        public void ApplyOffset(Vector3 delta) { }
    }
}
