using UnityEngine;

namespace HideAndInk.CameraSystem
{
    /// <summary>
    /// 오브젝트의 로컬 X축을 사인파로 미세하게 흔들어 입체감을 준다.
    /// 카메라의 자식 오브젝트에 부착하여 사용.
    /// 
    /// 사용법: Main Camera의 자식 오브젝트에 부착 후
    /// Inspector에서 amplitude, frequency를 개별 조정
    /// </summary>
    public sealed class LocalSway : MonoBehaviour
    {
        [Header("흔들림 설정")]
        [Tooltip("흔들림 효과 활성화")]
        [SerializeField] private bool enableSway = true;

        [Tooltip("흔들림 폭 (클수록 넓게 흔들림)")]
        [SerializeField] private float amplitude = 0.1f;

        [Tooltip("흔들림 속도 Hz (클수록 빠르게 흔들림)")]
        [SerializeField] private float frequency = 0.5f;

        [Tooltip("시작 오프셋 (각 오브젝트마다 다른 위상으로 흔들리게)")]
        [SerializeField] private float phaseOffset;

        [Header("부드러움")]
        [Tooltip("Lerp 보간 강도 (0=부드러움, 1=즉시 반응)")]
        [SerializeField] private float smoothness = 5f;

        private float _timer;
        private Vector3 _originLocalPos;

        private void Start()
        {
            _originLocalPos = transform.localPosition;
        }

        private void LateUpdate()
        {
            if (!enableSway) return;

            _timer += Time.deltaTime;

            float swayX = Mathf.Sin((_timer * frequency + phaseOffset) * Mathf.PI * 2f) * amplitude;
            Vector3 targetPos = new Vector3(
                _originLocalPos.x + swayX,
                _originLocalPos.y,
                _originLocalPos.z
            );

            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, smoothness * Time.deltaTime);
        }

        /// <summary>흔들림 활성화/비활성화</summary>
        public void SetSwayEnabled(bool enabled) => enableSway = enabled;

        /// <summary>흔들림 폭 변경</summary>
        public void SetAmplitude(float newAmplitude) => amplitude = newAmplitude;

        /// <summary>흔들림 속도 변경</summary>
        public void SetFrequency(float newFrequency) => frequency = newFrequency;
    }
}