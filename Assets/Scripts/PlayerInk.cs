using UnityEngine;

/// <summary>
/// 두두의 잉크 수치와 연막 사용만 담당한다.
/// </summary>
public class PlayerInk : MonoBehaviour
{
    [Header("Ink")]
    [SerializeField] private float currentInk = 0f;
    [SerializeField] private float maxInk = 100f;
    [SerializeField] private float passiveRechargeAmount = 10f;
    [SerializeField] private float passiveRechargeInterval = 10f;

    [Header("State")]
    [SerializeField] private bool isUnderThreat = false;
    [SerializeField] private bool isUsingSmoke = false;
    [SerializeField] private bool isUsingBossMimic = false;
    [SerializeField] private bool isContactCharging = false;

    [Header("Smoke")]
    [SerializeField] private KeyCode smokeKey = KeyCode.Space;
    [SerializeField] private float smokeDuration = 2f;
    [SerializeField] private float smokeReadyTolerance = 0.5f;
    [SerializeField] private ParticleSystem smokeEffect;

    private float _passiveRechargeTimer;
    private float _smokeTimer;

    public float CurrentInk => currentInk;
    public float MaxInk => maxInk;
    public bool IsUnderThreat => isUnderThreat;
    public bool IsUsingSmoke => isUsingSmoke;
    public bool IsUsingBossMimic => isUsingBossMimic;
    public bool IsContactCharging => isContactCharging;

    public System.Action<float, float> OnInkChanged;
    public System.Action<bool> OnThreatChanged;
    public System.Action OnSmokeStarted;
    public System.Action OnSmokeStopped;

    private void Awake()
    {
        currentInk = Mathf.Clamp(currentInk, 0f, maxInk);
        _passiveRechargeTimer = passiveRechargeInterval;
    }

    private void Update()
    {
        HandleSmokeInput();
        UpdateSmoke();
        UpdatePassiveRecharge();
    }

    public bool NeedsInk()
    {
        return currentInk < maxInk;
    }

    public bool CanReceiveContactCharge()
    {
        return NeedsInk() && !isUnderThreat && !isUsingSmoke && !isUsingBossMimic;
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

    public bool TryUseSmoke()
    {
        if (currentInk < maxInk - smokeReadyTolerance || isUsingSmoke)
            return false;

        isUsingSmoke = true;
        _smokeTimer = smokeDuration;
        SetInkDirect(0f);

        if (smokeEffect != null)
        {
            smokeEffect.gameObject.SetActive(true);
            smokeEffect.Clear(true);
            smokeEffect.Play(true);
            smokeEffect.Emit(20);
        }

        OnSmokeStarted?.Invoke();
        return true;
    }

    public void StopBossMimic()
    {
        isUsingBossMimic = false;
    }

    private void HandleSmokeInput()
    {
        if (Input.GetKeyDown(smokeKey))
        {
            TryUseSmoke();
        }
    }

    private void UpdateSmoke()
    {
        if (!isUsingSmoke)
            return;

        _smokeTimer -= Time.deltaTime;
        if (_smokeTimer > 0f)
            return;

        isUsingSmoke = false;

        if (smokeEffect != null)
        {
            smokeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        OnSmokeStopped?.Invoke();
    }

    private void UpdatePassiveRecharge()
    {
        if (isUsingSmoke || isUsingBossMimic || isContactCharging)
            return;

        _passiveRechargeTimer -= Time.deltaTime;
        if (_passiveRechargeTimer > 0f)
            return;

        SetInkDirect(currentInk + passiveRechargeAmount);
        _passiveRechargeTimer = passiveRechargeInterval;
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
