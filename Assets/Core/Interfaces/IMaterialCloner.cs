using UnityEngine;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 메테리얼 복사 시스템 인터페이스
    /// PropertyBlock을 사용하여 색상만 변경
    /// </summary>
    public interface IMaterialCloner
    {
        /// <summary>
        /// 타겟 오브젝트의 색상을 가져옴
        /// </summary>
        /// <param name="target">타겟 오브젝트</param>
        /// <returns">타겟의 메인 색상</returns>
        Color GetTargetColor(GameObject target);

        /// <summary>
        /// 플레이어 색상을 타겟으로 보간
        /// </summary>
        /// <param name="target">타겟 오브젝트</param>
        /// <param name="progress">보간 진행도 (0~1)</param>
        void BlendToTarget(GameObject target, float progress);

        /// <summary>
        /// 원본 색상으로 복원
        /// </summary>
        void RestoreOriginalColor();

        /// <summary>
        /// 원본 색상으로 천천히 복원 (보간)
        /// </summary>
        /// <param name="progress">보간 진행도 (0~1)</param>
        void BlendToOriginal(float progress);

        /// <summary>
        /// 원본 색상 설정
        /// </summary>
        /// <param name="originalColor">원본 색상</param>
        void SetOriginalColor(Color originalColor);

        /// <summary>
        /// Octopus Material로 전환 (의태 시 사용)
        /// </summary>
        void ApplyOctopusMaterial();

        /// <summary>
        /// Default Material로 복원 (의태 해제 시 사용)
        /// </summary>
        void RestoreDefaultMaterial();

        /// <summary>
        /// Octopus Material 사용 중인지 확인
        /// </summary>
        bool IsUsingOctopusMaterial { get; }
    }
}