using UnityEngine;
using UnityEngine.InputSystem;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using HideAndInk.Core.Player;

namespace HideAndInk.Player
{
    /// <summary>
    /// 플레이어 이동 시스템 Unity 어댑터
    /// InputSystem 연동 및 Transform 이동 적용
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovementAdapter : MonoBehaviour
    {
        [Header("이동 속도 설정")]
        [SerializeField] private float horizontalSpeed = 5.0f;
        [SerializeField] private float verticalSpeed = 4.0f;
        [SerializeField] private float acceleration = 10.0f;
        [SerializeField] private float friction = 0.9f;

        private IPlayerMovement _playerMovement;
        private CharacterController _characterController;
        private Vector2 _moveInput;

        /// <summary>
        /// DI용 생성자 (실제 사용 시 GameManager에서 주입)
        /// </summary>
        public PlayerMovementAdapter()
        {
        }

        /// <summary>
        /// 테스트용 생성자
        /// </summary>
        public PlayerMovementAdapter(
            float horizontalSpeed,
            float verticalSpeed,
            float acceleration,
            float friction)
        {
            this.horizontalSpeed = horizontalSpeed;
            this.verticalSpeed = verticalSpeed;
            this.acceleration = acceleration;
            this.friction = friction;
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            
            // DI 컨테이너에서 해결하거나 직접 생성
            if (GameManager.Container != null && GameManager.Container.IsRegistered<IPlayerMovement>())
            {
                _playerMovement = GameManager.Container.Resolve<IPlayerMovement>();
            }
            else
            {
                _playerMovement = new PlayerMovement(
                    horizontalSpeed,
                    verticalSpeed,
                    acceleration,
                    friction);
            }
        }

        private void Update()
        {
            // 이동 입력 전달
            _playerMovement.Move(_moveInput);

            // 이동 시스템 업데이트
            if (_playerMovement is PlayerMovement movement)
            {
                movement.Update(Time.deltaTime);
            }

            // 캐릭터 컨트롤러로 이동 적용
            Vector2 velocity = _playerMovement.Velocity;
            Vector3 moveDirection = new Vector3(velocity.x, 0f, velocity.y);
            
            _characterController.Move(moveDirection * Time.deltaTime);
        }

        /// <summary>
        /// InputSystem 콜백 - 이동 입력
        /// </summary>
        public void OnMove(InputAction.CallbackContext context)
        {
            _moveInput = context.ReadValue<Vector2>();
        }

        /// <summary>
        /// 현재 이동 상태 확인
        /// </summary>
        public bool IsMoving => _playerMovement?.IsMoving ?? false;

        /// <summary>
        /// 현재 속도 확인
        /// </summary>
        public Vector2 CurrentVelocity => _playerMovement?.Velocity ?? Vector2.zero;
    }
}