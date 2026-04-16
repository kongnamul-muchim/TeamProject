using UnityEngine;
using System.Collections.Generic;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 시야 감지 유형
    /// </summary>
    public enum VisionPatternType
    {
        /// <summary>
        /// 순찰형: 빠르게 이동, 좁은 시야 (30-45°)
        /// </summary>
        Patrol,

        /// <summary>
        /// 관찰형: 천천히 이동, 넓은 시야 (90-120°)
        /// </summary>
        Observe,

        /// <summary>
        /// 경계형: 한 구역 내, 빠르고 넓은 시야 (120-180°)
        /// </summary>
        Guard
    }

    /// <summary>
    /// 시야 감지 시스템 인터페이스
    /// </summary>
    public interface IVisionSensor
    {
        /// <summary>
        /// 시야 감지 원점 (일반적으로 적의 위치)
        /// </summary>
        Vector3 Origin { get; }

        /// <summary>
        /// 시야 반경
        /// </summary>
        float ViewRadius { get; }

        /// <summary>
        /// 시야 각도 (부채꼴 범위, 도)
        /// </summary>
        float ViewAngle { get; }

        /// <summary>
        /// 시야 패턴 유형
        /// </summary>
        VisionPatternType PatternType { get; }

        /// <summary>
        /// 특정 대상이 시야 내에 있는지 확인
        /// </summary>
        /// <param name="target">확인할 대상</param>
        /// <returns>시야 내 감지 여부</returns>
        bool CanSee(GameObject target);

        /// <summary>
        /// 시야 내 모든 감지 가능한 대상 가져오기
        /// </summary>
        /// <returns>감지된 대상 리스트</returns>
        List<GameObject> GetAllVisibleTargets();

        /// <summary>
        /// 시야 방향 가져오기 (전방 벡터)
        /// </summary>
        Vector3 GetViewDirection();

        /// <summary>
        /// 대상이 시야 각도 내에 있는지 확인
        /// </summary>
        bool IsWithinViewAngle(Vector3 targetPosition);

        /// <summary>
        /// 대상까지의 시야가 가려졌는지 확인 (장애물 체크)
        /// </summary>
        bool IsBlockedByObstacle(Vector3 targetPosition);
    }
}
