using UnityEngine;
using UnityEngine.InputSystem;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Perception;
using HideAndInk.Core.Managers;

namespace HideAndInk.Player
{
    /// <summary>
    /// 의태 시스템 Unity 어댑터
    /// InputSystem 연동 및 실제 의태 효과 적용
    /// </summary>
    [RequireComponent(typeof(PlayerMovementAdapter))]
    public sealed class CamouflageAdapter : MonoBehaviour
    {
        [Header("의태 탐지 설정")]
        [SerializeField] private float detectionRadius = 1.0f;
        [SerializeField] private LayerMask camouflageLayer = -1; // Everything

        [Header("의태 시간 설정")]
        [SerializeField] private float lockTime = 0.4f;
        [SerializeField] private float blendTime = 1.0f;
        [SerializeField] private float perfectTime = 2.0f;

        [Header("의태 키")]
        [SerializeField] private InputActionReference camouflageAction;

        private ICamouflageDetector _detector;
        private ICamouflageStateMachine _stateMachine;
        private IMaterialCloner _materialCloner;
        private PlayerMovementAdapter _playerMovement;
        private Renderer _playerRenderer;

        private Vector3 _originalPosition;
        private bool _isCamouflageInputPressed;
        private GameObject _currentTarget;

        private void Awake()
        {
            _playerMovement = GetComponent<PlayerMovementAdapter>();
            _playerRenderer = GetComponent<Renderer>();

            // DI 컨테이너에서 해결하거나 직접 생성
            if (GameManager.Container != null && GameManager.Container.IsRegistered<ICamouflageDetector>())
            {
                _detector = GameManager.Container.Resolve<ICamouflageDetector>();
            }
            else
            {
                _detector = new CamouflageDetector(detectionRadius);
                _detector.SetLayerMask(camouflageLayer);
            }

            if (GameManager.Container != null && GameManager.Container.IsRegistered<ICamouflageStateMachine>())
            {
                _stateMachine = GameManager.Container.Resolve<ICamouflageStateMachine>();
            }
            else
            {
                _stateMachine = new CamouflageStateMachine(lockTime, blendTime, perfectTime);
            }

            if (_playerRenderer != null)
            {
                _materialCloner = new MaterialCloner(_playerRenderer);
            }
        }

        private void OnEnable()
        {
            if (camouflageAction != null)
            {
                camouflageAction.action.performed += OnCamouflagePerformed;
                camouflageAction.action.canceled += OnCamouflageCanceled;
            }
        }

        private void OnDisable()
        {
            if (camouflageAction != null)
            {
                camouflageAction.action.performed -= OnCamouflagePerformed;
                camouflageAction.action.canceled -= OnCamouflageCanceled;
            }
        }

        private void Update()
        {
            // Lock 시간中は移動入力を無視
            if (_stateMachine.CurrentState == CamouflageState.Locked && !_stateMachine.IsLockComplete)
            {
                return;
            }

            // 移動状态確認
            bool isMoving = _playerMovement != null && _playerMovement.IsMoving;

            // 상태 시스템 업데이트
            _stateMachine.Update(Time.deltaTime, isMoving);

            // 색상 보간 업데이트
            UpdateBlend();

            // 의태 상태에 따른 위치 조정
            UpdatePosition();
        }

        /// <summary>
        /// 색상 보간 업데이트
        /// </summary>
        private void UpdateBlend()
        {
            if (_stateMachine.TargetObject == null) return;

            switch (_stateMachine.CurrentState)
            {
                case CamouflageState.Partial:
                    if (_stateMachine is CamouflageStateMachine stateMachineImpl)
                    {
                        _materialCloner?.BlendToTarget(
                            _stateMachine.TargetObject,
                            stateMachineImpl.BlendProgress);
                    }
                    break;

                case CamouflageState.Perfect:
                    // Perfect에서는 완전히 타겟 색상
                    _materialCloner?.BlendToTarget(_stateMachine.TargetObject, 1f);
                    break;

                case CamouflageState.None:
                    // 원본 색상으로 복원
                    _materialCloner?.RestoreOriginalColor();
                    break;
            }
        }

        /// <summary>
        /// 위치 스냅 업데이트
        /// </summary>
        private void UpdatePosition()
        {
            switch (_stateMachine.CurrentState)
            {
                case CamouflageState.Locked:
                    // Lock 중에는 원본 위치 저장
                    if (_originalPosition == Vector3.zero)
                    {
                        _originalPosition = transform.position;
                    }
                    break;

                case CamouflageState.Approaching:
                case CamouflageState.Partial:
                case CamouflageState.Perfect:
                    // 오브젝트 위치로 스냅
                    if (_stateMachine.TargetObject != null)
                    {
                        transform.position = _stateMachine.TargetObject.transform.position;
                    }
                    break;

                case CamouflageState.None:
                    // 원본 위치 복원 (필요시)
                    _originalPosition = Vector3.zero;
                    break;
            }
        }

        /// <summary>
        /// 의태 키 입력 처리
        /// </summary>
        private void OnCamouflagePerformed(InputAction.CallbackContext context)
        {
            if (_isCamouflageInputPressed) return;
            _isCamouflageInputPressed = true;

            // 이미 의태 중이면 무시
            if (_stateMachine.CurrentState != CamouflageState.None) return;

            // 반경 내 가장 가까운 오브젝트 탐지
            GameObject nearest = _detector.FindNearestCandidate(transform.position);

            if (nearest != null)
            {
                _currentTarget = nearest;
                _originalPosition = transform.position;
                _stateMachine.StartCamouflage(nearest);
            }
        }

        /// <summary>
        /// 의태 키 해제 처리
        /// </summary>
        private void OnCamouflageCanceled(InputAction.CallbackContext context)
        {
            _isCamouflageInputPressed = false;

            // Perfect 상태가 아니면 의태 해제
            if (_stateMachine.CurrentState != CamouflageState.Perfect)
            {
                _stateMachine.CancelCamouflage();
                _materialCloner?.RestoreOriginalColor();
            }
        }

        /// <summary>
        /// 현재 의태 상태 확인 (외부 참조용)
        /// </summary>
        public CamouflageState CurrentState => _stateMachine.CurrentState;

        /// <summary>
        /// 의태 가능한 오브젝트 탐지 (디버그/UI용)
        /// </summary>
        public GameObject FindNearestCamouflageable()
        {
            return _detector.FindNearestCandidate(transform.position);
        }
    }
}