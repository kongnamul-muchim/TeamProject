using UnityEngine;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// Billboard 스프라이트
    /// 스프라이트가 항상 카메라를 향하도록 함
    /// 원근 투영(Perspective) 카메라 대응
    /// </summary>
    public sealed class BillboardSprite : MonoBehaviour
    {
        [SerializeField] private Camera _targetCamera;

        private void Awake()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            if (_targetCamera == null) return;

            // 카메라 방향으로 회전 (Y축만 회전하여 스프라이트가 항상 카메라를 향함)
            transform.LookAt(
                transform.position + _targetCamera.transform.rotation * Vector3.forward,
                _targetCamera.transform.rotation * Vector3.up
            );
        }
    }
}