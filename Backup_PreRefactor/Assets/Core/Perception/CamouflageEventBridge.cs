using UnityEngine;
using HideAndInk.Core.Events;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.VFX;
using System.Collections.Generic;

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

        [Tooltip("의태 시작 VFX 스케일")]
        [SerializeField] private Vector3 camouflageStartScale = Vector3.one;

        [Tooltip("의태 해제 시 재생할 VFX 프리팹")]
        [SerializeField] private GameObject camouflageEndVFX;

        [Tooltip("의태 해제 VFX 스케일")]
        [SerializeField] private Vector3 camouflageEndScale = Vector3.one;

        [Tooltip("의태 완료 시 바닥에 생성할 InkMark VFX 프리팹들 (랜덤 선택)")]
        [SerializeField] private GameObject[] inkMarkVFXs;

        [Header("InkMark 설정")]
        [Tooltip("InkMark 생성 시 Y축 오프셋 (바닥에 깔리도록)")]
        [SerializeField] private float inkMarkYOffset = -0.5f;

        [Tooltip("InkMark Y축 미세 보정 (Z-fighting 방지)")]
        [SerializeField] private float inkMarkYMicroOffset = 0.01f;

        [Tooltip("InkMark Sorting Order (바닥보다 위에 표시)")]
        [SerializeField] private int inkMarkSortingOrder = -1;

        [Tooltip("InkMark 스케일")]
        [SerializeField] private Vector3 inkMarkScale = Vector3.one;

        [Header("VFX 렌더링 설정")]
        [Tooltip("Start VFX Sorting Order (Player보다 우선 표시)")]
        [SerializeField] private int startVFXSortingOrder = 10;

        [Tooltip("End VFX Sorting Order (Player보다 우선 표시)")]
        [SerializeField] private int endVFXSortingOrder = 10;

        [Tooltip("VFX Z값 미세 보정 (Player보다 약간 앞으로, Z-fighting 방지)")]
        [SerializeField] private float vfxZOffset = 0.1f;

        [Header("End VFX 타이밍")]
        [Tooltip("Start VFX 생성 후 End VFX를 생성할 최소 지연 시간 (초)")]
        [SerializeField] private float endVFXMinDelay = 0.5f;

        [Header("VFX 재생 속도")]
        [Tooltip("의태 시간에 비례한 VFX 재생 속도 배수 (1 = 기본 속도)")]
        [SerializeField] private float vfxSpeedMultiplier = 1f;

        [Header("기존 효과 시스템 참조 (Member C가 작성한 컴포넌트 할당)")]
        [Tooltip("의태 시작/종료 시 사운드를 재생하는 컴포넌트")]
        [SerializeField] private MonoBehaviour soundEffect;

        private Transform _playerTransform;
        private GameObject _activeStartVFX;
        private Transform _startVFXTarget; // Start VFX가 따라다닐 타겟 (Player 또는 타겟 오브젝트)
        private readonly List<GameObject> _activeEndVFXs = new List<GameObject>();
        private float _startVFXSpawnTime; // Start VFX 생성 시간 기록
        private bool _wasPerfect; // Perfect 상태였는지 기록 (Perfect 해제 시 End VFX 생성용)

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
            CamouflageEvents.OnStateChanged += HandleStateChanged;

            Debug.Log("[EventBridge] Events subscribed.");
            Debug.Log($"[EventBridge] camouflageStartVFX assigned: {camouflageStartVFX != null}");
            Debug.Log($"[EventBridge] camouflageEndVFX assigned: {camouflageEndVFX != null}");
            Debug.Log($"[EventBridge] inkMarkVFXs count: {inkMarkVFXs?.Length ?? 0}");
        }

        private void OnDisable()
        {
            // 의태 이벤트 구독 해제 (메모리 누수 방지)
            CamouflageEvents.OnCamouflageStart -= HandleCamouflageStart;
            CamouflageEvents.OnCamouflageComplete -= HandleCamouflageComplete;
            CamouflageEvents.OnCamouflageEnd -= HandleCamouflageEnd;
            CamouflageEvents.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            // Start VFX: 타겟 오브젝트 위치를 따라다니되 Z값 보정
            if (_activeStartVFX != null && _startVFXTarget != null)
            {
                Vector3 pos = _startVFXTarget.position;
                pos.z += vfxZOffset;
                _activeStartVFX.transform.position = pos;
            }

            // End VFX: Player 위치를 따라다니되 Z값 보정
            for (int i = _activeEndVFXs.Count - 1; i >= 0; i--)
            {
                if (_activeEndVFXs[i] == null)
                {
                    _activeEndVFXs.RemoveAt(i);
                    continue;
                }
                Vector3 pos = _playerTransform.position;
                pos.z += vfxZOffset;
                _activeEndVFXs[i].transform.position = pos;
            }
        }

        /// <summary>
        /// 의태 상태 변화 처리 (Perfect 상태 도달 시 Start VFX 삭제)
        /// </summary>
        private void HandleStateChanged(CamouflageState state)
        {
            Debug.Log($"[EventBridge] OnStateChanged: {state}");

            // Perfect 상태 도달 시 Start VFX 삭제
            if (state == CamouflageState.Perfect)
            {
                if (_activeStartVFX != null)
                {
                    Debug.Log("[EventBridge] State=Perfect → Destroying Start VFX");
                    Destroy(_activeStartVFX);
                    _activeStartVFX = null;
                }
                _wasPerfect = true; // Perfect 상태였음 기록
            }
        }

        /// <summary>
        /// 의태 시작 시 호출 (None → Attached)
        /// </summary>
        private void HandleCamouflageStart(GameObject target)
        {
            Debug.Log($"[EventBridge] OnCamouflageStart called. target={target?.name ?? "null"}");
            Debug.Log($"[EventBridge] _activeStartVFX before: {_activeStartVFX != null}");
            Debug.Log($"[EventBridge] camouflageStartVFX prefab: {camouflageStartVFX != null}");

            // 기존 Start VFX가 있으면 삭제
            if (_activeStartVFX != null)
            {
                Debug.Log("[EventBridge] Destroying existing Start VFX");
                Destroy(_activeStartVFX);
            }

            // Start VFX 생성 (Player 위치에 고정)
            if (camouflageStartVFX != null)
            {
                Vector3 spawnPos = _playerTransform.position;
                spawnPos.z += vfxZOffset;

                Debug.Log($"[EventBridge] Instantiating Start VFX at {spawnPos}, scale={camouflageStartScale}");

                GameObject vfx = Instantiate(camouflageStartVFX, spawnPos, Quaternion.identity);
                vfx.transform.localScale = camouflageStartScale;

                // Sorting Order 설정 (Player보다 우선 표시)
                SpriteRenderer sr = vfx.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingOrder = startVFXSortingOrder;
                    Debug.Log($"[EventBridge] Start VFX sortingOrder set to {startVFXSortingOrder}");
                }
                else
                {
                    Debug.LogWarning("[EventBridge] Start VFX has no SpriteRenderer!");
                }

                ApplyVFXSpeed(vfx);
                _activeStartVFX = vfx;
                _startVFXTarget = _playerTransform;
                _startVFXSpawnTime = Time.time; // Start VFX 생성 시간 기록

                // Start VFX는 Perfect 상태까지 유지해야 하므로 VFXSelfDestruct 제거
                VFXSelfDestruct startSelfDestruct = vfx.GetComponent<VFXSelfDestruct>();
                if (startSelfDestruct != null)
                {
                    Destroy(startSelfDestruct);
                    Debug.Log("[EventBridge] Removed VFXSelfDestruct from Start VFX");
                }

                Debug.Log($"[EventBridge] Start VFX created: {vfx.name}, _activeStartVFX set: {_activeStartVFX != null}");
            }
            else
            {
                Debug.LogError("[EventBridge] camouflageStartVFX is NULL! Cannot create Start VFX.");
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
            Debug.Log("[EventBridge] OnCamouflageComplete called.");

            // Perfect에서는 InkMark 생성 안 함 (의태 해제 시에만 생성)

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
            Debug.Log($"[EventBridge] OnCamouflageEnd called. target={target?.name ?? "null"}, _wasPerfect={_wasPerfect}");

            // Start VFX 정리
            if (_activeStartVFX != null)
            {
                Debug.Log("[EventBridge] OnCamouflageEnd → Destroying Start VFX");
                Destroy(_activeStartVFX);
                _activeStartVFX = null;
            }
            else if (!_wasPerfect)
            {
                // Start VFX가 이미 삭제됨 + Perfect 상태가 아님 = 이미 End VFX 생성 처리 완료
                Debug.Log("[EventBridge] Start VFX already destroyed, skipping End VFX");
                return;
            }

            // Perfect 상태였다면 Start VFX 재생 시간과 관계없이 End VFX 생성
            if (_wasPerfect)
            {
                Debug.Log("[EventBridge] Was Perfect → Spawning End VFX immediately");
                _wasPerfect = false;
                SpawnEndVFX();
                return;
            }

            // Start VFX가 충분히 재생되었는지 확인
            float elapsed = Time.time - _startVFXSpawnTime;
            if (elapsed < endVFXMinDelay)
            {
                // 충분히 재생되지 않았으면 End VFX 생성 안 함
                Debug.Log($"[EventBridge] Start VFX only played for {elapsed:F2}s (min: {endVFXMinDelay:F2}s), skipping End VFX");
                return;
            }

            SpawnEndVFX();
        }

        /// <summary>
        /// End VFX 생성 (공통 로직)
        /// </summary>
        private void SpawnEndVFX()
        {
            if (camouflageEndVFX == null)
            {
                Debug.LogError("[EventBridge] camouflageEndVFX is NULL! Cannot create End VFX.");
                return;
            }

            Vector3 spawnPos = _playerTransform.position;
            spawnPos.z += vfxZOffset;

            Debug.Log($"[EventBridge] Instantiating End VFX at {spawnPos}");

            GameObject vfx = Instantiate(camouflageEndVFX, spawnPos, Quaternion.identity);
            vfx.transform.localScale = camouflageEndScale;

            // Sorting Order 설정 (Player보다 우선 표시)
            SpriteRenderer sr = vfx.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = endVFXSortingOrder;
                Debug.Log($"[EventBridge] End VFX sortingOrder set to {endVFXSortingOrder}");
            }

            ApplyVFXSpeed(vfx);

            // VFXSelfDestruct에 콜백 등록: 애니메이션 완료 시 InkMark 소환
            VFXSelfDestruct selfDestruct = vfx.GetComponent<VFXSelfDestruct>();
            if (selfDestruct == null)
            {
                selfDestruct = vfx.AddComponent<VFXSelfDestruct>();
                Debug.Log("[EventBridge] Added VFXSelfDestruct to End VFX");
            }
            selfDestruct.OnAnimationComplete += () =>
            {
                Debug.Log("[EventBridge] End VFX animation complete → Spawning InkMark");
                SpawnInkMark();
                _activeEndVFXs.Remove(vfx);
            };

            _activeEndVFXs.Add(vfx);
            Debug.Log($"[EventBridge] End VFX created: {vfx.name}");

            if (soundEffect != null)
            {
                soundEffect.SendMessage("PlayDetachSound", SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// InkMark 랜덤 선택 후 바닥에 생성
        /// </summary>
        private void SpawnInkMark()
        {
            if (inkMarkVFXs == null || inkMarkVFXs.Length == 0)
            {
                Debug.LogWarning("[EventBridge] inkMarkVFXs is empty! Cannot spawn InkMark.");
                return;
            }

            GameObject selectedMark = inkMarkVFXs[Random.Range(0, inkMarkVFXs.Length)];

            Vector3 spawnPos = _playerTransform.position;
            spawnPos.y += inkMarkYOffset;
            spawnPos.y += inkMarkYMicroOffset; // Z-fighting 방지 미세 보정

            // X축 -90°로 명시적 회전 (바닥에 눕힘)
            Quaternion spawnRotation = Quaternion.Euler(-90f, 0f, 0f);

            GameObject inkMark = Instantiate(selectedMark, spawnPos, spawnRotation);
            inkMark.transform.localScale = inkMarkScale;

            // Sorting Order 설정 (바닥보다 위에 표시)
            SpriteRenderer sr = inkMark.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = inkMarkSortingOrder;
            }

            Debug.Log($"[EventBridge] InkMark spawned: {selectedMark.name} at {spawnPos}");
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
