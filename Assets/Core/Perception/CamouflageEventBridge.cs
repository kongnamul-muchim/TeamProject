using UnityEngine;
using HideAndInk.Core.Events;
using HideAndInk.Core.VFX;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 이벤트를 VFX 시스템에 연결하는 브릿지 (반장 역할)
    /// VFX 프리팹을 Instantiate하여 의태 시작/해제/완료 시각 효과 재생
    /// </summary>
    public class CamouflageEventBridge : MonoBehaviour
    {
        [Header("VFX 프리팹")]
        [Tooltip("의태 시작 시 재생할 VFX 프리팹")]
        [SerializeField] private GameObject camouflageStartVFX;

        [Tooltip("의태 해제 시 재생할 VFX 프리팹")]
        [SerializeField] private GameObject camouflageEndVFX;

        [Tooltip("의태 완료 시 바닥에 생성할 InkMark VFX 프리팹들 (랜덤 선택)")]
        [SerializeField] private GameObject[] inkMarkVFXs;

        [Header("InkMark 설정")]
        [Tooltip("InkMark 생성 시 Y축 오프셋 (바닥에 깔리도록)")]
        [SerializeField] private float inkMarkYOffset = -0.5f;

        [Tooltip("InkMark Sorting Order (바닥보다 위에 표시)")]
        [SerializeField] private int inkMarkSortingOrder = -1;

        [Header("VFX 재생 속도")]
        [Tooltip("의태 시간에 비례한 VFX 재생 속도 배수 (1 = 기본 속도)")]
        [SerializeField] private float vfxSpeedMultiplier = 1f;

        [Header("기존 효과 시스템 참조 (Member C가 작성한 컴포넌트 할당)")]
        [Tooltip("의태 시작/종료 시 사운드를 재생하는 컴포넌트")]
        [SerializeField] private MonoBehaviour soundEffect;

        private Transform _playerTransform;

        private void Awake()
        {
            _playerTransform = transform;
        }

        private void OnEnable()
        {
            // 의태 이벤트 구독
            CamouflageEvents.OnCamouflageStart += HandleCamouflageStart;
            CamouflageEvents.OnCamouflageComplete += HandleCamouflageComplete;
            CamouflageEvents.OnCamouflageEnd += HandleCamouflageEnd;
        }

        private void OnDisable()
        {
            // 의태 이벤트 구독 해제 (메모리 누수 방지)
            CamouflageEvents.OnCamouflageStart -= HandleCamouflageStart;
            CamouflageEvents.OnCamouflageComplete -= HandleCamouflageComplete;
            CamouflageEvents.OnCamouflageEnd -= HandleCamouflageEnd;
        }

        /// <summary>
        /// 의태 시작 시 호출 (None → Attached)
        /// </summary>
        private void HandleCamouflageStart(GameObject target)
        {
            // Start VFX 생성
            if (camouflageStartVFX != null)
            {
                GameObject vfx = Instantiate(camouflageStartVFX, _playerTransform.position, Quaternion.identity);
                ApplyVFXSpeed(vfx);
            }

            // Member C의 사운드 시스템 호출
            if (soundEffect != null)
            {
                soundEffect.SendMessage("PlayAttachSound", SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// 완벽 의태 달성 시 호출 (Perfect 도달)
        /// </summary>
        private void HandleCamouflageComplete(GameObject target)
        {
            // InkMark 랜덤 선택 후 생성
            if (inkMarkVFXs != null && inkMarkVFXs.Length > 0)
            {
                GameObject selectedMark = inkMarkVFXs[Random.Range(0, inkMarkVFXs.Length)];

                Vector3 spawnPos = _playerTransform.position;
                spawnPos.y += inkMarkYOffset;

                GameObject inkMark = Instantiate(selectedMark, spawnPos, Quaternion.identity);

                // Sorting Order 설정 (바닥보다 위에 표시)
                SpriteRenderer sr = inkMark.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingOrder = inkMarkSortingOrder;
                }
            }

            if (soundEffect != null)
            {
                soundEffect.SendMessage("PlayPerfectSound", SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// 의태 해제 시 호출 (→ None)
        /// </summary>
        private void HandleCamouflageEnd(GameObject target)
        {
            // End VFX 생성
            if (camouflageEndVFX != null)
            {
                GameObject vfx = Instantiate(camouflageEndVFX, _playerTransform.position, Quaternion.identity);
                ApplyVFXSpeed(vfx);
            }

            if (soundEffect != null)
            {
                soundEffect.SendMessage("PlayDetachSound", SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// VFX의 Animator 속도를 의태 시간에 비례하여 조정
        /// </summary>
        private void ApplyVFXSpeed(GameObject vfx)
        {
            Animator animator = vfx.GetComponent<Animator>();
            if (animator != null)
            {
                animator.speed = vfxSpeedMultiplier;
            }
        }
    }
}
