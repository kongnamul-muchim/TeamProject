using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndInk.ParallaxSystem
{
    /// <summary>
    /// Y축 깊이 패럴랙스 중앙 제어기.
    /// 
    /// 캐릭터의 Y축 위치를 1회만 읽고, 모든 레이어에 깊이 오프셋을 전달한다.
    /// 
    /// ┌──────────────────────────────────────────────────────────┐
    /// │  작동 구조                                               │
    /// │                                                          │
    /// │  Character (Y축 이동)                                    │
    /// │       │                                                   │
    /// │       ▼                                                   │
    /// │  DepthParallaxController                                  │
    /// │   ├─ characterY 계산 (1회)                               │
    /// │   ├─ deltaFromOrigin 계산 (1회)                          │
    /// │   │                                                      │
    /// │   ├─→ Layer 0 (원경, depthRatio=0.1)                     │
    /// │   ├─→ Layer 1 (중경, depthRatio=0.4)                     │
    /// │   ├─→ Layer 2 (근경, depthRatio=0.7)                     │
    /// │   └─→ Layer 3 (전경, depthRatio=1.0)                     │
    /// │                                                          │
    /// │  각 레이어가 자신의 depthRatio에 따라                      │
    /// │  Y위치, 스케일(Ortho) 또는 Z위치(Persp)를 조절            │
    /// └──────────────────────────────────────────────────────────┘
    /// 
    /// SRP: 캐릭터 Y축 추적 및 깊이 오프셋 발행만 담당
    /// DI: [SerializeField]로 타겟 참조, 레이어는 자동/수동 혼합 가능
    /// </summary>
    public sealed class DepthParallaxController : MonoBehaviour
    {
        // ── DI ──

        [Header("DI - 추적할 대상 (캐릭터 Transform)")]
        [SerializeField] private Transform target;

        [Header("DI - 깊이 패럴랙스 레이어 (비어있으면 자동 탐색)")]
        [SerializeField] private List<DepthParallaxLayer> layers = new List<DepthParallaxLayer>();

        // ── 설정 ──

        [Header("원점 Y (캐릭터 시작 높이, 0이면 자동)")]
        [SerializeField] private float originY;

        [Header("전역 깊이 배율 (0=효과 없음, 1=정상, 2=강조)")]
        [Range(0f, 3f)]
        [SerializeField] private float globalDepthMultiplier = 1f;

        [Header("자동 탐색 범위")]
        [SerializeField] private AutoDiscoveryMode discoveryMode = AutoDiscoveryMode.Scene;

        // ── 공개 ──

        public event Action<float, float> OnDepthChanged;
        public IReadOnlyList<DepthParallaxLayer> Layers => layers;
        public float GlobalDepthMultiplier => globalDepthMultiplier;

        // ── 내부 상태 ──

        private bool _originSet;
        private bool _isInitialized;

        // ── 열거형 ──

        public enum AutoDiscoveryMode
        {
            None,
            Children,
            Scene
        }

        // ── Unity 라이프사이클 ──

        private void Awake()
        {
            AutoDiscoverLayers();
        }

        private void Start()
        {
            if (target != null && !_originSet)
            {
                originY = target.position.y;
                _originSet = true;
            }

            foreach (var layer in layers)
            {
                if (layer == null) continue;
                layer.Initialize();
                layer.SetControllerDriven(true);
            }

            _isInitialized = true;
        }

        private void LateUpdate()
        {
            if (!_isInitialized || target == null) return;

            float characterY = target.position.y;
            float deltaFromOrigin = (characterY - originY) * globalDepthMultiplier;

            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] != null)
                    layers[i].ApplyDepthOffset(characterY, deltaFromOrigin);
            }

            OnDepthChanged?.Invoke(characterY, deltaFromOrigin);
        }

        // ── 공개 메서드 ──

        /// <summary>추적 대상을 설정한다.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (newTarget != null && !_originSet)
            {
                originY = newTarget.position.y;
                _originSet = true;
            }
        }

        /// <summary>원점 Y를 수동으로 설정한다.</summary>
        public void SetOriginY(float y)
        {
            originY = y;
            _originSet = true;
        }

        /// <summary>전역 깊이 배율을 설정한다.</summary>
        public void SetDepthMultiplier(float multiplier)
        {
            globalDepthMultiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>런타임에 레이어를 동적으로 추가한다.</summary>
        public void AddLayer(DepthParallaxLayer layer)
        {
            if (layer != null && !layers.Contains(layer))
            {
                layers.Add(layer);
                layer.Initialize();
                layer.SetControllerDriven(true);
            }
        }

        /// <summary>런타임에 레이어를 제거한다.</summary>
        public void RemoveLayer(DepthParallaxLayer layer)
        {
            if (layer != null && layers.Remove(layer))
                layer.SetControllerDriven(false);
        }

        // ── 내부 메서드 ──

        private void AutoDiscoverLayers()
        {
            if (layers.Count > 0)
            {
                layers.RemoveAll(l => l == null);
                return;
            }

            switch (discoveryMode)
            {
                case AutoDiscoveryMode.Children:
                    GetComponentsInChildren(layers);
                    break;

                case AutoDiscoveryMode.Scene:
                    foreach (var layer in FindObjectsOfType<DepthParallaxLayer>())
                    {
                        if (!layers.Contains(layer))
                            layers.Add(layer);
                    }
                    break;

                case AutoDiscoveryMode.None:
                default:
                    break;
            }

            layers.RemoveAll(l => l == this);
        }
    }
}