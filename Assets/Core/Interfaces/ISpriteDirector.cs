using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 스프라이트 관리 인터페이스
    /// </summary>
    public interface ISpriteDirector
    {
        /// <summary>
        /// 방향별 스프라이트로 변경
        /// </summary>
        void ChangeSprite(MoveDirection direction);

        /// <summary>
        /// 기본 스프라이트로 변경
        /// </summary>
        void ChangeToDefaultSprite();

        /// <summary>
        /// 방향 업데이트 (스프라이트 변경)
        /// </summary>
        void UpdateDirection(MoveDirection direction);

        /// <summary>
        /// ColorPart 텍스처만 방향에 맞게 업데이트 (스프라이트는 유지)
        /// </summary>
        void UpdateColorPart(MoveDirection direction);
    }
}