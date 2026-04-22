using UnityEngine;

namespace HideAndInk.ParallaxSystem
{
    public sealed class ParallaxLayer : MonoBehaviour
    {
        [Header("DI - 추적할 카메라")]
        [SerializeField] private UnityEngine.Camera targetCamera;

        [Header("보간 비율 (0=최대원근, 1=고정)")]
        [Range(0, 1)] // 슬라이더로 조절하기 쉽게 추가
        [SerializeField] private float rate = 0.5f;

        [Header("Y축 설정")]
        [SerializeField] private bool applyY;
        [SerializeField] private float yRate = 0.2f; // Y축은 보통 더 적게 움직이는게 자연스러움

        private Vector3 _originLayerPos;
        private Vector3 _originCameraPos;
        private bool _isInitialized;

        private void Start()
        {
            Initialize();
        }

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

        private void LateUpdate()
        {
            if (!_isInitialized || targetCamera == null) return;

            Vector3 cameraPos = targetCamera.transform.position;
            Vector3 currentPos = transform.position;

            // X축 계산
            float cameraDeltaX = cameraPos.x - _originCameraPos.x;
            float newX = _originLayerPos.x + (cameraDeltaX * rate);

            // Y축 계산
            float newY = currentPos.y;
            if (applyY)
            {
                float cameraDeltaY = cameraPos.y - _originCameraPos.y;
                newY = _originLayerPos.y + (cameraDeltaY * yRate);
            }

            transform.position = new Vector3(newX, newY, currentPos.z);
        }

        // 에디터에서 값을 바꿀 때 즉시 확인 가능하게 함
        private void OnValidate()
        {
            if (Application.isPlaying && _isInitialized)
            {
                // 게임 실행 중 수치 조정 시 즉각 반응
            }
        }
    }
}
