using UnityEngine;
using HideAndInk.Core.Perception;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 스프라이트 관리 시스템 인터페이스
    /// 스프라이트 로딩, 캐싱, 방향 업데이트 담당
    /// </summary>
    public interface ISpriteDirector
    {
        /// <summary>
        /// 스프라이트 방향 업데이트
        /// </summary>
        /// <param name="direction">이동 방향</param>
        void UpdateDirection(MoveDirection direction);

        /// <summary>
        /// 기본 스프라이트로 변경 (의태 시 사용)
        /// </summary>
        void ChangeToDefaultSprite();

        /// <summary>
        /// 스프라이트 렌더러 설정
        /// </summary>
        /// <param name="spriteRenderer">대상 스프라이트 렌더러</param>
        void SetSpriteRenderer(SpriteRenderer spriteRenderer);

        /// <summary>
        ///Resources 경로 설정
        /// </summary>
        /// <param name="path">Resources 상대 경로</param>
        void SetSpritePath(string path);
    }
}