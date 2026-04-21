using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Player.Visual
{
    /// <summary>
    /// 플레이어 애니메이션 브릿지 (아티스트 파트 전용)
    /// 기존 코어 스크립트를 수정하지 않고 이동 정보를 애니메이터에 전달합니다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerVisualBridge : MonoBehaviour
    {
        [Header("연결 컴포넌트 (자동 할당)")]
        [SerializeField] private PlayerMovementAdapter movementAdapter;
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private Material _material;

        [Header("마스크 시트 설정")]
        [SerializeField] private Sprite[] maskSprites; // 마스크 시트 슬라이스 파일들을 여기에 드래그 앤 드롭
        private int _lastSpriteIndex = -1;

        [Header("애니메이터 파라미터 이름")]
        [SerializeField] private string moveXParam = "MoveX";
        [SerializeField] private string moveYParam = "MoveY";
        [SerializeField] private string isMovingParam = "isMoving";
        [SerializeField] private string isEscapingParam = "isEscaping";

        private bool _debugIsEscaping = false;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            
            // 'Visual' 자식 오브젝트에서 SpriteRenderer를 먼저 찾습니다. (Scale 0.2 이슈 해결)
            Transform visualTransform = transform.Find("Visual");
            if (visualTransform != null)
            {
                _spriteRenderer = visualTransform.GetComponent<SpriteRenderer>();
            }
            else
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
            
            if (_spriteRenderer != null)
            {
                _material = _spriteRenderer.material;
            }

            if (movementAdapter == null)
            {
                movementAdapter = GetComponentInParent<PlayerMovementAdapter>() ?? GetComponent<PlayerMovementAdapter>();
            }
        }

        private void Update()
        {
            if (movementAdapter == null || _animator == null) return;

            // 1. 이동 정보 가져오기
            Vector2 velocity = movementAdapter.CurrentVelocity;
            bool isMoving = movementAdapter.IsMoving;
            MoveDirection direction = movementAdapter.Direction;

            // 2. 애니메이터 파라미터 업데이트
            UpdateAnimatorParameters(velocity, isMoving, direction);

            // 3. 도망 모드 디버그 테스트 (Space Key)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _debugIsEscaping = !_debugIsEscaping;
                _animator.SetBool(isEscapingParam, _debugIsEscaping);
                Debug.Log($"[PlayerVisualBridge] Escape Mode: {_debugIsEscaping}");
            }
        }

        private void LateUpdate()
        {
            // 4. 마스크 시트 동기화 로직
            SyncMaskWithMainSprite();
        }

        /// <summary>
        /// 마스크 스프라이트의 텍스처를 머티리얼에 동기화하여 적용합니다.
        /// </summary>
        private void SyncMaskWithMainSprite()
        {
            if (_spriteRenderer == null || _material == null || maskSprites == null || maskSprites.Length == 0) return;

            // 복잡한 UV 계산을 제거하고, 마스크 시트 전체 텍스처를 머티리얼에 한 번 할당합니다.
            // 셰이더 내부에서 메인 UV를 공유하므로 시트 레이아웃(Full Rect 등)이 같다면 자동으로 맞게 됩니다.
            if (maskSprites[0] != null)
            {
                Texture currentMaskTexture = _material.GetTexture("_ColorPart");
                Texture targetMaskTexture = maskSprites[0].texture;

                if (currentMaskTexture != targetMaskTexture)
                {
                    _material.SetTexture("_ColorPart", targetMaskTexture);
                }
            }
        }

        private void UpdateAnimatorParameters(Vector2 velocity, bool isMoving, MoveDirection direction)
        {
            _animator.SetBool(isMovingParam, isMoving);

            if (isMoving)
            {
                // 이동 중일 때 속도 방향 전달
                _animator.SetFloat(moveXParam, velocity.normalized.x);
                _animator.SetFloat(moveYParam, velocity.normalized.y);
            }
            else
            {
                // 정지 시 마지막 방향 유지하기 위한 로직 (필요 시 direction 기반으로 설정 가능)
                switch (direction)
                {
                    case MoveDirection.Up:    _animator.SetFloat(moveYParam, 1);  _animator.SetFloat(moveXParam, 0); break;
                    case MoveDirection.Down:  _animator.SetFloat(moveYParam, -1); _animator.SetFloat(moveXParam, 0); break;
                    case MoveDirection.Left:  _animator.SetFloat(moveXParam, -1); _animator.SetFloat(moveYParam, 0); break;
                    case MoveDirection.Right: _animator.SetFloat(moveXParam, 1);  _animator.SetFloat(moveYParam, 0); break;
                }
            }
        }
    }
}
