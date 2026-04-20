using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndInk.Parallax
{
    public sealed class ParallaxController : MonoBehaviour
    {
        [Header("DI - 추적할 카메라")]
        [SerializeField] private Camera targetCamera;

        [Header("DI - 패럴랙스 레이어 목록")]
        [SerializeField] private List<ParallaxLayer> layers = new List<ParallaxLayer>();

        public event Action<Vector3> OnCameraMoved;

        private Vector3 _previousCameraPosition;
        private bool _isInitialized;

        public IReadOnlyList<ParallaxLayer> Layers => layers;

        private void Start()
        {
            if (targetCamera != null)
            {
                _previousCameraPosition = targetCamera.transform.position;
                _isInitialized = true;
            }
        }

        private void LateUpdate()
        {
            if (!_isInitialized || targetCamera == null) return;

            Vector3 currentPos = targetCamera.transform.position;
            Vector3 delta = currentPos - _previousCameraPosition;

            if (delta.sqrMagnitude < 0.0001f) return;

            _previousCameraPosition = currentPos;
            OnCameraMoved?.Invoke(delta);
        }

        public void SetTargetCamera(Camera camera)
        {
            targetCamera = camera;
            if (camera != null)
            {
                _previousCameraPosition = camera.transform.position;
                _isInitialized = true;
            }
        }
    }
}
