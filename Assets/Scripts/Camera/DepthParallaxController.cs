using System;
using UnityEngine;

namespace HideAndInk.ParallaxSystem
{
    /// <summary>
    /// Y축 깊이 패럴랙스 중앙 제어기.
    /// 
    /// 캐릭터의 Y축 위치를 읽고, 각 레이어의 깊이 비율에 따라
    /// 위치와 스케일을 조절하여 2D 게임에서 입체감을 표현한다.
    /// 
    /// SRP: 캐릭터 Y축 추적 및 깊이 오프셋 계산만 담당
    /// DI: [SerializeField]로 타겟 참조
    /// </summary>
    public sealed class DepthParallaxController : MonoBehaviour
    {
        // ── 열거형 ──

        /// <summary>깊이 패럴랙스 모드</summary>
        public enum DepthMode
        {
            /// <summary>직교 카메라: Y 위치와 스케일로 깊이감 표현</summary>
            Orthographic,

            /// <summary>원근 카메라: Z 위치로 깊이감 표현</summary>
            Perspective
        }

        // ── 레이어 데이터 ──

        [System.Serializable]
        public class DepthLayerData
        {
            [Tooltip("깊이 패럴랙스를 적용할 Transform")]
            public Transform target;

            [Header("깊이 비율 (0=원경, 1=전경)")]
            [Range(0f, 1f)]
            public float depthRatio = 0.5f;

            [Header("깊이 모드")]
            public DepthMode mode = DepthMode.Orthographic;

            [Header("Orthographic - Y축 깊이 이동 배율")]
            public float yDepthFactor = 0.3f;

            [Header("Orthographic - 깊이에 따른 스케일 증가량")]
            public float scaleBoost = 0.15f;

            [Header("Perspective - Z축 깊이 이동 배율")]
            public float zDepthFactor = 2f;

            [Header("X축에도 깊이 이동 적용")]
            public bool applyXDepth;

            [Header("X축 깊이 이동 배율")]
            public float xDepthFactor = 0.15f;

            // 런타임 전용
            [System.NonSerialized] public Vector3 originPos;
            [System.NonSerialized] public Vector3 originScale;
            [System.NonSerialized] public bool initialized;
        }

        // ── DI ──

        [Header("DI - 추적할 대상 (캐릭터 Transform)")]
        [SerializeField] private Transform target;

        [Header("깊이 패럴랙스 레이어 목록")]
        [SerializeField] private DepthLayerData[] layers = new DepthLayerData[0];

        [Header("원점 Y (캐릭터 시작 높이, 0이면 자동)")]
        [SerializeField] private float originY;

        [Header("전역 깊이 배율 (0=효과 없음, 1=정상, 2=강조)")]
        [Range(0f, 3f)]
        [SerializeField] private float globalDepthMultiplier = 1f;

        // ── 내부 상태 ──

        private bool _originSet;

        // ── Unity 라이프사이클 ──

        private void Start()
        {
            if (target != null && !_originSet)
            {
                originY = target.position.y;
                _originSet = true;
            }

            InitializeLayers();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float characterY = target.position.y;
            float deltaFromOrigin = (characterY - originY) * globalDepthMultiplier;

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer.target == null || !layer.initialized) continue;

                switch (layer.mode)
                {
                    case DepthMode.Orthographic:
                        ApplyOrthographic(layer, deltaFromOrigin);
                        break;

                    case DepthMode.Perspective:
                        ApplyPerspective(layer, deltaFromOrigin);
                        break;
                }
            }
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

        /// <summary>모든 레이어의 원점을 현재 위치로 재설정한다.</summary>
        public void ResetOrigins()
        {
            InitializeLayers();
        }

        // ── 내부 메서드 ──

        private void InitializeLayers()
        {
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer.target == null) continue;

                layer.originPos = layer.target.position;
                layer.originScale = layer.target.localScale;
                layer.initialized = true;
            }
        }

        private void ApplyOrthographic(DepthLayerData layer, float deltaFromOrigin)
        {
            float yOffset = deltaFromOrigin * layer.depthRatio * layer.yDepthFactor;
            float newY = layer.originPos.y + yOffset;

            float newX = layer.originPos.x;
            if (layer.applyXDepth)
                newX += deltaFromOrigin * layer.depthRatio * layer.xDepthFactor;

            float scaleMultiplier = 1f + layer.depthRatio * layer.scaleBoost * (deltaFromOrigin * 0.1f);
            scaleMultiplier = Mathf.Clamp(scaleMultiplier, 0.8f, 1.3f);

            layer.target.position = new Vector3(newX, newY, layer.originPos.z);
            layer.target.localScale = Vector3.Scale(layer.originScale, new Vector3(scaleMultiplier, scaleMultiplier, 1f));
        }

        private void ApplyPerspective(DepthLayerData layer, float deltaFromOrigin)
        {
            float zOffset = deltaFromOrigin * layer.depthRatio * layer.zDepthFactor;
            float newZ = layer.originPos.z + zOffset;

            float newX = layer.originPos.x;
            if (layer.applyXDepth)
                newX += deltaFromOrigin * layer.depthRatio * layer.xDepthFactor;

            layer.target.position = new Vector3(newX, layer.originPos.y, newZ);
            layer.target.localScale = layer.originScale;
        }
    }
}