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
        /// 의태 시작
        /// </summary>
        /// <param name="target">타겟 오브젝트</param>
        void StartCamouflage(GameObject target);

        /// <summary>
        /// 의태 해제
        /// </summary>
        void CancelCamouflage();

        /// <summary>
        /// 업데이트 (매 프레임 호출)
        /// </summary>
        /// <param name="deltaTime">경과 시간</param>
        /// <param name="isMoving">현재 움직이는 중인지</param>
        void Update(float deltaTime, bool isMoving);

        /// <summary>
        /// Lock 시간 완료 여부
        /// </summary>
        bool IsLockComplete { get; }
    }
}