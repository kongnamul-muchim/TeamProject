using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 의태 상태 제공자 인터페이스
    /// Core 레이어가 Scripts 레이어(CamouflageAdapter)를 직접 의존하지 않도록 하는 역전 인터페이스
    /// </summary>
    public interface ICamouflageStateProvider
    {
        /// <summary>
        /// 현재 의태 상태
        /// </summary>
        CamouflageState CurrentState { get; }

        /// <summary>
        /// 의태 중인지 여부 (None이 아닌 상태)
        /// </summary>
        bool IsCamouflaging { get; }

        /// <summary>
        /// 완벽 의태 여부
        /// </summary>
        bool IsPerfect { get; }
    }
}
