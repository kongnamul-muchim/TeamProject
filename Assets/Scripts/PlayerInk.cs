using HideAndInk.Core.Audio;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using UnityEngine;

/// <summary>
/// 두두의 잉크 수치와 대시 기능을 담당한다.
/// SpaceBar = 대시 (Ink가 최대치일 때만 발동)
/// 대시 중 Ink는 0까지 감소, 자연 회복 시작
/// </summary>
public class PlayerInk : MonoBehaviour
{
    /// <summary>
    /// 씬의 유일한 PlayerInk 인스턴스
    /// </summary>
    public static PlayerInk Instance { get; private set; }

    [Header("Ink")]
    [Tooltip("현재 잉크량")]
    [SerializeField] private float currentInk = 0f;
    [Tooltip("최대 잉크량")]
    [SerializeField] private float maxInk = 100f;
    [Tooltip("자연 회복으로 0→최대까지 차오르는 데 걸리는 시간(초)")]
    [SerializeField] private float passiveRechargeDuration = 10f;

    [Header("Dash")]
    [Tooltip("대시 지속 시간 (초)")]
    [SerializeField] private float dashDuration = 1.5f;
    [Tooltip("대시 속도 추가 보정 (기본 이동 속도에 더해짐)")]
    [SerializeField] private float dashSpeedBoost = 10f;
    [Tooltip("대시 시작 시 재생할 연막 파티클")]
    [SerializeField] private ParticleSystem smokeEffect;

    [Header("State")]
    [Tooltip("위협 상태 여부")]
    [SerializeField] private bool isUnderThreat = false;
    [Tooltip("보스 모방 사용 중 여부")]
    [SerializeField] private bool isUsingBossMimic = false;
    [Tooltip("접촉 충전 중 여부")]
    [SerializeField] private bool isContactCharging = false;

    private ISfxService _sfxService;
    private bool _isDashing;
    private float _dashTimer;
    private float _dashInputBlockedUntil;   // 대시 입력 차단 (대화 종료 후 잔여 입력 방지)
    private ParticleSystem _runtimeSmokeEffect;

    public float CurrentInk => currentInk;
    public float MaxInk => maxInk;
    public bool IsUnderThreat => isUnderThreat;
    public bool IsUsingBossMimic => isUsingBossMimic;
    public bool IsContactCharging => isContactCharging;
    public bool IsDashing => _isDashing;
    public float DashDuration => dashDuration;
    public float DashSpeedBoost => dashSpeedBoost;

    public System.Action<float, float> OnInkChanged;
    public System.Action<bool> OnThreatChanged;
    public System.Action<float, float> OnDashStarted;   // (duration, speedBoost)
    public System.Action OnDashEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        currentInk = Mathf.Clamp(currentInk, 0f, maxInk);

        // SFX 서비스 해결
        if (GameManager.Container != null && GameManager.Container.IsRegistered<ISfxService>())
        {
            _sfxService = GameManager.Container.Resolve<ISfxService>();
        }
    }

    private void Update()
    {
        HandleDashInput();
        UpdateDash();
        UpdatePassiveRecharge();
    }

    public bool NeedsInk()
    {
        return currentInk < maxInk;
    }

    public bool CanReceiveContactCharge()
    {
        return !isUnderThreat && !_isDashing && !isUsingBossMimic;
    }

    public void AddInk(float amount)
    {
        SetInkDirect(currentInk + amount);
    }

    public void SetThreat(bool threat)
    {
        if (isUnderThreat == threat)
            return;

        isUnderThreat = threat;
        OnThreatChanged?.Invoke(isUnderThreat);
    }

    public void SetContactCharging(bool charging)
    {
        isContactCharging = charging;
    }

    public void StopBossMimic()
    {
        isUsingBossMimic = false;
    }

    /// <summary>
    /// 대시 시작 (Ink가 최대치일 때만 발동)
    /// </summary>
    public bool TryStartDash()
    {
        if (_isDashing)
            return false;

        if (currentInk < maxInk)
            return false;

        _isDashing = true;
        _dashTimer = dashDuration;

        // 연막 파티클 재생 (프리팹이면 Instantiate 후 재생)
        ParticleSystem effect = GetOrCreateSmokeEffect();
        if (effect != null)
        {
            // 파티클이 플레이어 뒤로 나오도록 SortingLayer 설정
            var psRenderer = effect.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.sortingLayerName = "Player";
                int playerOrder = 0;
                var playerSr = GetComponent<SpriteRenderer>();
                if (playerSr != null)
                    playerOrder = playerSr.sortingOrder;
                psRenderer.sortingOrder = playerOrder - 1;
            }

            effect.gameObject.SetActive(true);
            effect.Clear(true);
            effect.Play(true);
            effect.Emit(20);
        }

        // 대시(먹물) 효과음 재생
        _sfxService?.Play(SfxId.InkShoot);

        OnDashStarted?.Invoke(dashDuration, dashSpeedBoost);
        return true;
    }

    /// <summary>
    /// smokeEffect 참조가 프리팹이면 Instantiate하여 런타임 인스턴스를 반환한다.
    /// 씬에 배치된 인스턴스면 그대로 반환한다.
    /// </summary>
    private ParticleSystem GetOrCreateSmokeEffect(bool createIfMissing = true)
    {
        if (smokeEffect == null)
            return null;

        // 이미 생성된 런타임 인스턴스가 있으면 반환
        if (_runtimeSmokeEffect != null)
            return _runtimeSmokeEffect;

        // 씬에 배치된 인스턴스면 그대로 사용
        if (smokeEffect.gameObject.scene.IsValid())
        {
            _runtimeSmokeEffect = smokeEffect;
            return _runtimeSmokeEffect;
        }

        // 프리팹이면 Instantiate 후 사용
        if (createIfMissing)
        {
            _runtimeSmokeEffect = Instantiate(smokeEffect, transform);
            _runtimeSmokeEffect.gameObject.SetActive(false);
            return _runtimeSmokeEffect;
        }

        return null;
    }

    /// <summary>
    /// 대화 종료 후 잔여 Space 입력으로 회피가 발동되는 것을 방지
    /// DialogueUIAdapter.OnDialogueEnd()에서 호출됨
    /// </summary>
    public void BlockDashInputTemporarily(float duration = 0.3f)
    {
        _dashInputBlockedUntil = Time.realtimeSinceStartup + duration;
    }

    private void HandleDashInput()
    {
        // 게임 일시정지(대화/메뉴) 중에는 대시 입력 무시
        if (Mathf.Approximately(Time.timeScale, 0f))
            return;

        // 대화 종료 직후 잔여 Space 입력 차단
        if (Time.realtimeSinceStartup < _dashInputBlockedUntil)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryStartDash();
        }
    }

    private void UpdateDash()
    {
        if (!_isDashing)
            return;

        _dashTimer -= Time.deltaTime;

        // Ink를 100→0 으로 선형 감소
        float inkDrainRate = maxInk / Mathf.Max(0.01f, dashDuration);
        SetInkDirect(currentInk - inkDrainRate * Time.deltaTime);

        if (_dashTimer <= 0f)
        {
            _isDashing = false;
            SetInkDirect(0f);

            ParticleSystem effect = GetOrCreateSmokeEffect(createIfMissing: false);
            if (effect != null)
            {
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            OnDashEnded?.Invoke();
        }
    }

    private void UpdatePassiveRecharge()
    {
        if (_isDashing || isUsingBossMimic || isContactCharging)
            return;

        // 자연 회복: 0→maxInk까지 passiveRechargeDuration 초 동안 선형 회복
        float rechargeRate = maxInk / Mathf.Max(0.01f, passiveRechargeDuration);
        SetInkDirect(currentInk + rechargeRate * Time.deltaTime);
    }

    private void SetInkDirect(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, maxInk);
        if (Mathf.Approximately(clamped, currentInk))
            return;

        currentInk = clamped;
        OnInkChanged?.Invoke(currentInk, maxInk);
    }
}
