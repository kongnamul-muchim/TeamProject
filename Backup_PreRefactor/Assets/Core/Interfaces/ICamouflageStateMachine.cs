using UnityEngine;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 의태 시스템 상태
    /// </summary>
    public enum CamouflageState
    {
        /// <summary>
        /// 기본 상태, 의태 안 됨
        /// </summary>
        None,
        
        /// <summary>
        /// 오브젝트에 달라붙은 상태 (위치 스냅, 0.2~0.3초 움직임 잠김)
        /// </summary>
        Attached,
        
        /// <summary>
        /// Lock 시간 (키 입력 후 움직임 불가)
        /// </summary>
        Locked,
        
        /// <summary>
        /// 의태 접근 중
        /// </summary>
        Approaching,
        
        /// <summary>
        /// 의태 중 (색상 보간 중)
        /// </summary>
        Partial,
        
        /// <summary>
        /// 완벽 의태 (완전히 동화됨)
        /// </summary>
        Perfect
    }

    /// <summary>
    /// 의태 상태 시스템 인터페이스
    /// </summary>
    public interface ICamouflageStateMachine
    {
        /// <summary>
        /// 현재 상태
        /// </summary>
        CamouflageState CurrentState { get; }

        /// <summary>
        /// 현재 타겟 오브젝트
        /// </summary>
        GameObject TargetObject { get; }

        /// <summary>
        /// 의태 시작 (Attached 상태로)
        /// </summary>
        /// <param name="target">타겟 오브젝트</param>
        void StartAttach(GameObject target);

        /// <summary>
        /// 키가 떼어진 시점 기록 (Perfect 도달 여부 확인용)
        /// </summary>
        void RecordKeyRelease();

        /// <summary>
        /// 의태 해제
        /// </summary>
        /// <param name="force">강제 취소 (이동으로 인한 취소가 아닌 경우)</param>
        void CancelCamouflage(bool force = false);

        /// <summary>
        /// 업데이트 (매 프레임 호출)
        /// </summary>
        /// <param name="deltaTime">경과 시간</param>
        /// <param name="isMoving">현재 움직이는 중인지</param>
        void Update(float deltaTime, bool isMoving);

        /// <summary>
        /// Attached 상태 완료 여부 (0.2~0.3초 경과)
        /// </summary>
        bool IsAttachedComplete { get; }

        /// <summary>
        /// Lock 시간 완료 여부
        /// </summary>
        bool IsLockComplete { get; }

        /// <summary>
        /// Perfect 도달 여부
        /// </summary>
        bool IsPerfectReached { get; }
    }
}