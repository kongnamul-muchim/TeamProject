using UnityEngine;

namespace HideAndInk.Camera
{
    /// <summary>
    /// 타겟을 카메라가 부드럽게 추적
    /// SRP: 카메라 이동만 담당, DI: [SerializeField]로 타겟 참조
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [Header("DI - 추적 대상")]
        [SerializeField] private Transform target;

        [Header("추적 설정")]
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

        [Header("이동 축 제한")]
        [SerializeField] private bool followX = true;
        [SerializeField] private bool followY = true;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 targetPos = target.position + offset;

            if (!followX) targetPos.x = transform.position.x;
            if (!followY) targetPos.y = transform.position.y;

            transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
