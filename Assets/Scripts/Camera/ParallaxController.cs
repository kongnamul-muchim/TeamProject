using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndInk.ParallaxSystem
{
    /// <summary>
    /// 패럴랙스 중앙 제어기: 카메라 이동을 1회만 계산하고 모든 레이어에 동일한 delta를 전달한다.
    /// 
    /// 장점:
    /// - 카메라 delta 중복 계산 제거 (N회 → 1회)
    /// - 모든 레이어의 완벽한 동기화 보장
    /// - 일시정지, 속도 배율 등 중앙 제어 가능
    /// 
    /// 자동 탐색: Awake에서 씬/자식의 IParallaxLayer를 자동 수집한다.
    /// 수동 할당: [SerializeField] layers에 직접 등록할 수도 있다.
    /// 
    /// SRP: 카메라 추적 및 delta 발행만 담당
    /// DI: [SerializeField]로 카메라 참조, 레이어는 자동/수동 혼합 가능
    /// </summary>
    public sealed class ParallaxController : MonoBehaviour
    {
        // ── DI ──
        [Header("DI - 추적할 카메라 (미할당 시 Camera.main)")]
        [SerializeField] private Camera targetCamera;

        [Header("DI - 패럴랙스 레이어 (비어있으면 자동 탐색)")]
        [SerializeField] private List<ParallaxLayer> layers = new List<ParallaxLayer>();

        // ── 중앙 제어 ──
        [Header("전역 속도 배율 (0=정지, 1=정상, 2=2배속)")]
        [Range(0f, 5f)]
        [SerializeField] private float globalSpeedMultiplier = 1f;

        [Header("자동 탐색 범위")]
        [SerializeField] private AutoDiscoveryMode discoveryMode = AutoDiscoveryMode.Scene;

        // ── 공개 ──
        public event Action<Vector3> OnCameraMoved;
        public IReadOnlyList<ParallaxLayer> Layers => layers;
        public float GlobalSpeedMultiplier => globalSpeedMultiplier;
        public bool IsPaused => globalSpeedMultiplier <= 0f;

        // ── 내부 상태 ──
        private Vector3 _previousCameraPosition;
        private bool _isInitialized;

        // ── 열거형 ──
        public enum AutoDiscoveryMode
        {
            None,       // 자동 탐색 안 함 (수동 할당만)
            Children,   // 자식 오브젝트만
            Scene       // 씬 전체에서 탐색 (기본값)
        }

        // ── Unity 라이프사이클 ──

        private void Awake()
        {
            ResolveCamera();
            AutoDiscoverLayers();
        }

        private void Start()
        {
            ResolveCamera();

            if (targetCamera != null)
            {
                _previousCameraPosition = targetCamera.transform.position;
                _isInitialized = true;
            }

            // 모든 레이어 초기화 및 Controller 구독 모드 설정
            foreach (var layer in layers)
            {
                if (layer == null) continue;
                layer.Initialize();
                layer.SetControllerDriven(true);
            }
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
            }

            if (!_isInitialized) return;

            Vector3 currentPos = targetCamera.transform.position;
            Vector3 delta = (currentPos - _previousCameraPosition) * globalSpeedMultiplier;

            // CameraAnchored 모드에서는 매 프레임 위치 갱신이 필요하므로
            // delta가 0이어도 레이어 업데이트를 수행한다.
            // 이벤트는 의미 있는 이동이 있을 때만 발행한다.
            bool hasMovement = delta.sqrMagnitude >= 0.0001f;

            if (hasMovement)
                _previousCameraPosition = currentPos;

            // 모든 레이어에 카메라 위치 전달 (매 프레임)
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] != null)
                    layers[i].ApplyOffset(delta);
            }

            if (hasMovement)
                OnCameraMoved?.Invoke(delta);
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

        /// <summary>런타임에 레이어를 동적으로 추가한다.</summary>
        public void AddLayer(ParallaxLayer layer)
        {
            if (layer != null && !layers.Contains(layer))
            {
                layers.Add(layer);
                layer.Initialize();
                layer.SetControllerDriven(true);
            }
        }

        /// <summary>런타임에 레이어를 제거한다. 제거된 레이어는 독립 모드로 전환된다.</summary>
        public void RemoveLayer(ParallaxLayer layer)
        {
            if (layer != null && layers.Remove(layer))
            {
                layer.SetControllerDriven(false);
            }
        }

        // ── 내부 메서드 ──

        /// <summary>참조 카메라가 비활성이면 Camera.main으로 대체한다.</summary>
        private void ResolveCamera()
        {
            if (targetCamera != null && targetCamera.isActiveAndEnabled)
                return;

            var mainCam = Camera.main;
            if (mainCam != null)
                targetCamera = mainCam;
        }

        /// <summary>설정에 따라 레이어를 자동 탐색한다.</summary>
        private void AutoDiscoverLayers()
        {
            // 수동 할당이 있으면 자동 탐색 스킵
            if (layers.Count > 0)
            {
                // null 항목 제거
                layers.RemoveAll(l => l == null);
                return;
            }

            switch (discoveryMode)
            {
                case AutoDiscoveryMode.Children:
                    GetComponentsInChildren(layers);
                    break;

                case AutoDiscoveryMode.Scene:
                    foreach (var layer in FindObjectsOfType<ParallaxLayer>())
                    {
                        if (!layers.Contains(layer))
                            layers.Add(layer);
                    }
                    break;

                case AutoDiscoveryMode.None:
                default:
                    break;
            }

            // 자기 자신은 제외 (Controller가 Layer를 겸하는 경우 방지)
            layers.RemoveAll(l => l == this);
        }
    }

    // ── 유틸리티: List 확장 ──
    internal static class ListExtensions
    {
        public static void ForEach<T>(this List<T> list, Action<T> action)
        {
            for (int i = 0; i < list.Count; i++)
                action(list[i]);
        }
    }
}