using UnityEngine;

namespace HideAndInk.ParallaxSystem
{
    /// <summary>
    /// Y축 깊이 패럴랙스 레이어 인터페이스.
    /// 캐릭터의 Y 위치 변화에 따라 레이어의 깊이감을 표현한다.
    /// </summary>
    public interface IDepthParallaxLayer
    {
        /// <summary>깊이 비율 (0=가장 먼 원경, 1=가장 가까운 전경)</summary>
        float DepthRatio { get; }

        /// <summary>캐릭터의 Y축 변화량을 받아 레이어 위치/스케일을 갱신한다.</summary>
        void ApplyDepthOffset(float characterY, float deltaFromOrigin);
    }
}