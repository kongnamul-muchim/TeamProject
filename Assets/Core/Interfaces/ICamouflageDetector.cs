using System.Collections.Generic;
using UnityEngine;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 의태 탐지 시스템 인터페이스
    /// 반경 내 오브젝트 탐지
    /// </summary>
    public interface ICamouflageDetector
    {
        /// <summary>
        /// 탐지 반경
        /// </summary>
        float DetectionRadius { get; }

        /// <summary>
        /// 반경 내 의태 가능 오브젝트들 탐지
        /// </summary>
        /// <param name="position">탐� 기준 위치</param>
        /// <returns">탐지된 오브젝트 목록</param>
        List<GameObject> DetectCandidates(Vector3 position);

        /// <summary>
        /// 가장 가까운 의태 가능 오브젝트 탐지
        /// </summary>
        /// <param name="position">탐색 기준 위치</param>
        /// <returns">가장 가까운 오브젝트, 없으면 null</returns>
        GameObject FindNearestCandidate(Vector3 position);

        /// <summary>
        /// 특정 레이어만 탐지하도록 설정
        /// </summary>
        /// <param name="layerMask">탐지할 레이어</param>
        void SetLayerMask(LayerMask layerMask);
    }
}