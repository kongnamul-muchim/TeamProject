using UnityEngine;
using System.Collections.Generic;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Perception;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HideAndInk.Player
{
    /// <summary>
    /// 스프라이트 관리 시스템
    /// 스프라이트 로딩, 캐싱, 방향 업데이트 담당
    /// </summary>
    public sealed class SpriteDirector : MonoBehaviour, ISpriteDirector
    {
        [SerializeField] private string spritePath = "Art/1_Characters/Player/Spr_Player_Idle_";
        [SerializeField] private string maskPath = "Art/1_Characters/Player/Mask_Player_Idle_";

        private SpriteRenderer _spriteRenderer;
        private Dictionary<MoveDirection, Sprite> _spriteCache = new();
        private Dictionary<MoveDirection, Sprite> _shadowCache = new();
        private MoveDirection _lastDirection = MoveDirection.Down;
        private bool _isFlippedX = false;

        /// <summary>
        ///Resources 경로 설정
        /// </summary>
        /// <param name="path">Resources 상대 경로</param>
        public void SetSpritePath(string path)
        {
            spritePath = path;
        }

        /// <summary>
        /// 스프라이트 렌더러 설정
        /// </summary>
        /// <param name="spriteRenderer">대상 스프라이트 렌더러</param>
        public void SetSpriteRenderer(SpriteRenderer spriteRenderer)
        {
            _spriteRenderer = spriteRenderer;
        }

        /// <summary>
        /// 스프라이트 방향 업데이트
        /// </summary>
        /// <param name="direction">이동 방향</param>
        public void UpdateDirection(MoveDirection direction)
        {
            if (_spriteRenderer == null) return;
            if (direction == _lastDirection) return;

            _lastDirection = direction;

            Sprite targetSprite = GetSprite(direction);
            Sprite targetShadowSprite = GetShadow(direction);

            if (targetSprite != null)
            {
                _spriteRenderer.sprite = targetSprite;

                Material mat = _spriteRenderer.material;
                if (mat != null)
                {
                    Texture2D mainTex = targetSprite.texture;
                    Texture2D colorPartTex = targetShadowSprite != null ? targetShadowSprite.texture : mainTex;

                    mat.SetTexture("_MainTex", mainTex);
                    mat.SetTexture("_ColorPart", colorPartTex);
                }
            }
        }

        /// <summary>
        /// 기본 스프라이트로 변경 (의태 시 사용)
        /// </summary>
        public void ChangeToDefaultSprite()
        {
            if (_spriteRenderer == null) return;

            Sprite defaultSprite = LoadSprite($"{spritePath}Player");
            if (defaultSprite != null)
            {
                _spriteRenderer.sprite = defaultSprite;
                Debug.Log("[SpriteDirector] Changed sprite to default Player");
            }
            else
            {
                Debug.LogWarning($"[SpriteDirector] Default Player sprite not found at {spritePath}Player");
            }
        }

        /// <summary>
        /// 방향에 따라 ColorPart 텍스처 업데이트 (의태 시 사용)
        /// </summary>
        /// <param name="direction">이동 방향</param>
        public void UpdateColorPart(MoveDirection direction)
        {
            if (_spriteRenderer == null) return;

            Sprite shadowSprite = GetShadow(direction);
            Material mat = _spriteRenderer.material;
            if (mat != null && shadowSprite != null)
            {
                mat.SetTexture("_ColorPart", shadowSprite.texture);
            }
        }

        /// <summary>
        /// 이동 방향에 따른 스프라이트 가져오기 (캐시)
        /// </summary>
        private Sprite GetSprite(MoveDirection direction)
        {
            if (_spriteCache.TryGetValue(direction, out Sprite cached))
                return cached;

            string path = direction switch
            {
                MoveDirection.Down => $"{spritePath}Front",
                MoveDirection.Left => $"{spritePath}Left",
                MoveDirection.Right => $"{spritePath}Right",
                MoveDirection.Up => $"{spritePath}Back",
                _ => $"{spritePath}Front"
            };

            Sprite sprite = LoadSprite(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[SpriteDirector] Sprite not found at path: {path}");
                return null;
            }
            _spriteCache[direction] = sprite;
            return sprite;
        }

        /// <summary>
        /// 이동 방향에 따른 그림자 스프라이트 가져오기 (캐시)
        /// </summary>
        private Sprite GetShadow(MoveDirection direction)
        {
            if (_shadowCache.TryGetValue(direction, out Sprite cached))
                return cached;

            string path = direction switch
            {
                MoveDirection.Down => $"{maskPath}Front",
                MoveDirection.Left => $"{maskPath}Left",
                MoveDirection.Right => $"{maskPath}Right",
                MoveDirection.Up => $"{maskPath}Back",
                _ => $"{maskPath}Front"
            };

            Sprite sprite = LoadSprite(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[SpriteDirector] Shadow sprite not found at path: {path}");
                return null;
            }
            _shadowCache[direction] = sprite;
            return sprite;
        }

        private Sprite LoadSprite(string path)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Sprite>(path + ".png");
#else
            return Resources.Load<Sprite>(path);
#endif
        }
    }
}