using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 탐지기 설정 구현체
    /// CamouflageAdapter가 인스펙터 값으로 생성하여 DI 컨테이너에 등록
    /// </summary>
    public sealed class CamouflageDetectorConfig : ICamouflageDetectorConfig
    {
        public float DetectionRadius { get; }
        public LayerMask LayerMask { get; }

        public CamouflageDetectorConfig(float detectionRadius, LayerMask layerMask)
        {
            DetectionRadius = detectionRadius;
            LayerMask = layerMask;
        }
    }
}
