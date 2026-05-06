using UnityEngine;
using System;
using System.Collections.Generic;
using HideAndInk.Core.Audio;
using HideAndInk.Core.Events;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
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

        [Header("SFX")]
        [Tooltip("의태 시작/종료 시 사운드 재생 (DI: ISfxService)")]
        [SerializeField] private bool useSfxService = true;

        // DI로 주입받은 SFX 서비스
        private ISfxService _sfxService;
        private IEventBus _eventBus;

        private Transform _playerTransform;
        private GameObject _activeStartVFX;
        private Transform _startVFXTarget; // Start VFX가 따라다닐 타겟 (Player 또는 타겟 오브젝트)
        private readonly List<GameObject> _activeEndVFXs = new List<GameObject>();
        private float _startVFXSpawnTime; // Start VFX 생성 시간 기록
        private bool _wasPerfect; // Perfect 상태였는지 기록 (Perfect 해제 시 End VFX 생성용)
        private bool _endVFXSpawnedForCurrentCycle; // 의태 사이클당 End VFX 생성 여부 (중복 방지)

        private void Awake()
        {
            _playerTransform = transform;

            // DI 컨테이너에서 서비스 해결
            if (GameManager.Container != null)
            {
                if (useSfxService && GameManager.Container.IsRegistered<ISfxService>())
                {
                    _sfxService = GameManager.Container.Resolve<ISfxService>();
                }

                if (GameManager.Container.IsRegistered<IEventBus>())
                {
                    _eventBus = GameManager.Container.Resolve<IEventBus>();
                }
            }
        }

        private void OnEnable()
        {
            // VFX 프리팹이 할당되지 않은 인스턴스는 이벤트 구독 안 함 (더미 방지)
            if (camouflageStartVFX == null && camouflageEndVFX == null)
            {
                enabled = false;
                return;
            }

            if (_eventBus == null) return;

            // 의태 이벤트 구독 (EventBus 통해)
            _eventBus.Subscribe<CamouflageStartEvent>(OnCamouflageStartEvent);
            _eventBus.Subscribe<CamouflageCompleteEvent>(OnCamouflageCompleteEvent);
            _eventBus.Subscribe<CamouflageEndEvent>(OnCamouflageEndEvent);
            _eventBus.Subscribe<CamouflageStateChangedEvent>(OnCamouflageStateChangedEvent);
        }

        private void OnDisable()
        {
            if (_eventBus == null) return;

            _eventBus.Unsubscribe<CamouflageStartEvent>(OnCamouflageStartEvent);
            _eventBus.Unsubscribe<CamouflageCompleteEvent>(OnCamouflageCompleteEvent);
            _eventBus.Unsubscribe<CamouflageEndEvent>(OnCamouflageEndEvent);
            _eventBus.Unsubscribe<CamouflageStateChangedEvent>(OnCamouflageStateChangedEvent);
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
        private void OnCamouflageStateChangedEvent(CamouflageStateChangedEvent e)
        {
            if (e.State == CamouflageState.Perfect)
            {
                if (_activeStartVFX != null)
                {
                    Destroy(_activeStartVFX);
                    _activeStartVFX = null;
                }
                _wasPerfect = true;
            }
        }

        /// <summary>
        /// 의태 시작 시 호출 (None → Attached)
        /// </summary>
        private void OnCamouflageStartEvent(CamouflageStartEvent e)
        {
            // 의태 사이클 시작 시 플래그 리셋
            _endVFXSpawnedForCurrentCycle = false;
            _wasPerfect = false;

            // 기존 Start VFX가 있으면 삭제
            if (_activeStartVFX != null)
            {
                Destroy(_activeStartVFX);
            }

            // Start VFX 생성 (Player 위치에 고정)
            if (camouflageStartVFX != null)
            {
                Vector3 spawnPos = _playerTransform.position;
                spawnPos.z += vfxZOffset;

                GameObject vfx = Instantiate(camouflageStartVFX, spawnPos, Quaternion.identity);
                vfx.transform.localScale = camouflageStartScale;

                // Sorting Order 설정 (Player보다 우선 표시)
                SpriteRenderer sr = vfx.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingOrder = startVFXSortingOrder;
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
                }
            }

            // 사운드 효과 재생
            _sfxService?.Play(SfxId.CamouflageAttach);
        }

        /// <summary>
        /// 완벽 의태 달성 시 호출 (Perfect 도달)
        /// </summary>
        private void OnCamouflageCompleteEvent(CamouflageCompleteEvent e)
        {
            _sfxService?.Play(SfxId.CamouflagePerfect);
        }

        /// <summary>
        /// 의태 해제 시 호출 (→ None)
        /// </summary>
        private void OnCamouflageEndEvent(CamouflageEndEvent e)
        {
            // 의태 사이클당 End VFX 한 번만 생성 (중복 방지)
            if (_endVFXSpawnedForCurrentCycle)
            {
                return;
            }
            _endVFXSpawnedForCurrentCycle = true;

            // Start VFX 정리
            if (_activeStartVFX != null)
            {
                Destroy(_activeStartVFX);
                _activeStartVFX = null;
            }

            // Perfect 상태였다면 Start VFX 재생 시간과 관계없이 End VFX 생성
            if (_wasPerfect)
            {
                SpawnEndVFX();
                _wasPerfect = false;
                return;
            }

            // Start VFX가 충분히 재생되었는지 확인 (권장 사항, 차단 아님)
            float elapsed = Time.time - _startVFXSpawnTime;
            if (elapsed < endVFXMinDelay)
            {
                Debug.LogWarning($"[EventBridge] End VFX spawned before min delay ({elapsed:F2}s < {endVFXMinDelay:F2}s)");
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

            GameObject vfx = Instantiate(camouflageEndVFX, spawnPos, Quaternion.identity);

            if (vfx == null)
            {
                Debug.LogError("[EventBridge] End VFX Instantiate returned NULL! Prefab may be missing or destroyed.");
                return;
            }

            Debug.Log($"[EventBridge] End VFX spawned: {vfx.name} (instanceID: {vfx.GetInstanceID()})");

            vfx.transform.localScale = camouflageEndScale;

            // Animator 명시적 초기화 (Instantiate 후 캐시된 상태 방지)
            Animator animator = vfx.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Rebind();
                animator.Play(0, 0, 0f);
                animator.Update(0f);
            }

            // Sorting Order 설정 (Player보다 우선 표시)
            SpriteRenderer sr = vfx.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = endVFXSortingOrder;
            }

            ApplyVFXSpeed(vfx);

            // VFXSelfDestruct에 콜백 등록: 애니메이션 완료 시 InkMark 소환
            VFXSelfDestruct selfDestruct = vfx.GetComponent<VFXSelfDestruct>();
            if (selfDestruct == null)
            {
                selfDestruct = vfx.AddComponent<VFXSelfDestruct>();
            }
            selfDestruct.OnAnimationComplete += () =>
            {
                SpawnInkMark();
                _activeEndVFXs.Remove(vfx);
            };

            _activeEndVFXs.Add(vfx);

            _sfxService?.Play(SfxId.CamouflageDetach);
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

            GameObject selectedMark = inkMarkVFXs[UnityEngine.Random.Range(0, inkMarkVFXs.Length)];

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
