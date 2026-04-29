using UnityEngine;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// 백상아리 돌진에 맞은 오브젝트의 체력 관리
    /// HP 2 = 일반 → HP 1 = 약간 부서짐 → HP 0 = 완전 부서짐
    /// 오브젝트는 절대 Destroy/SetActive(false)되지 않음.
    /// 스프라이트만 3단계로 변경.
    /// </summary>
    public sealed class ObjectHP : MonoBehaviour
    {
        [Header("체력")]
        [SerializeField, Tooltip("최대 HP")] private int maxHP = 2;

        [Header("스프라이트")]
        [SerializeField, Tooltip("손상 상태 스프라이트")] private Sprite damagedSprite;
        [SerializeField, Tooltip("파괴 상태 스프라이트")] private Sprite destroyedSprite;

        // 상태
        private int _currentHP;
        private SpriteRenderer _spriteRenderer;
        private Sprite _originalSprite;

        // 프로퍼티
        public int CurrentHP => _currentHP;
        public int MaxHP => maxHP;
        public bool IsDestroyed => _currentHP <= 0;
        public bool IsDamaged => _currentHP < maxHP && _currentHP > 0;

        private void Awake()
        {
            _currentHP = maxHP;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
                _originalSprite = _spriteRenderer.sprite;
        }

        /// <summary>
        /// 데미지를 입습니다. 오브젝트는 절대 사라지지 않으며 스프라이트만 변경됩니다.
        /// </summary>
        /// <returns>
        ///   Damaged — HP만 깎임 (2→1), damagedSprite로 변경
        ///   Destroyed — 완전 부서짐 (1→0), destroyedSprite로 변경
        ///   AlreadyDead — 더 이상 깎일 HP 없음
        /// </returns>
        public HitResult TakeDamage()
        {
            if (IsDestroyed) return HitResult.AlreadyDead;

            _currentHP--;

            if (IsDestroyed)
            {
                if (destroyedSprite != null && _spriteRenderer != null)
                    _spriteRenderer.sprite = destroyedSprite;
                return HitResult.Destroyed;
            }

            if (damagedSprite != null && _spriteRenderer != null)
                _spriteRenderer.sprite = damagedSprite;
            return HitResult.Damaged;
        }

        /// <summary>
        /// ObjectHP 리셋 (풀링 등)
        /// </summary>
        public void ResetHP()
        {
            _currentHP = maxHP;
            if (_spriteRenderer != null && _originalSprite != null)
                _spriteRenderer.sprite = _originalSprite;
        }

        public enum HitResult
        {
            Damaged,      // HP 2→1
            Destroyed,    // HP 1→0
            AlreadyDead   // 이미 최종 상태
        }
    }
}
