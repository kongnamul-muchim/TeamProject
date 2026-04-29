using UnityEngine;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 의태 탐지기 설정
    /// 인스펙터 값을 DI 컨테이너로 전달하기 위한 Config 인터페이스
    /// </summary>
    public interface ICamouflageDetectorConfig
    {
        float DetectionRadius { get; }
        LayerMask LayerMask { get; }
    }
}
