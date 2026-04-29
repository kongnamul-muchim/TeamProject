# 곰치(Moray Eel) 코드 분석 리포트

> **버전:** v1.0
> **작성일:** 2026-04-28
> **대상 파일:**
> - `RelentlessChaseGimmick.cs`
> - `MorayChargeDirector.cs`
> - `MorayChargeIndicator.cs`
> - `BossEnemyController.cs` (곰치 관련 부분)

---

## 🔴 BUG — `_hasHitThisCharge`가 절대 리셋되지 않음

**위치**: `MorayChargeDirector.cs:344-370`
**파일**: `Assets/Core/Enemy/Boss/Gimmicks/MorayChargeDirector.cs`
**영향**: **치명적 — 첫 돌진 이후 모든 충돌 판정 무효**
**수정 난이도**: 1줄 추가

```csharp
private void CheckChargeHit()
{
    ...
    if (_hasHitThisCharge) return;  // ← 첫 히트 후 영구 차단!

    if (hits.Length > 0)
    {
        _hasHitThisCharge = true;   // ← true가 된 후 절대 false로 안 돌아감
    }
}

private bool _hasHitThisCharge;  // ← BeginPrepare / ExecuteCharge / ResetCharges 중 리셋하는 곳 없음
```

**재현**: Charge 2회차 이상 진입 시 2번째 돌진부터는 어떤 상황에서도 Player를 절대 맞출 수 없음.

**수정**: `ExecuteCharge(int index)` 첫 줄에 `_hasHitThisCharge = false;` 추가.

---

## 🔴 BUG — `Initialize()` 시점에 GroundBounds가 없어 돌진 경로가 왜곡됨

**위치**: `BossEnemyController.cs:159-166`
**파일**: `Assets/Core/Enemy/Boss/BossEnemyController.cs`
**영향**: 돌진 범위가 -2m ~ 2m로 극도로 좁아짐
**수정 난이도**: 중

```csharp
chargeDirector.Initialize(transform, _groundBounds);
```

`InitializeGimmick()`은 `Start()`에서 호출. 이 시점에 GroundBounds 스캔이 안 끝났으면 `_groundBounds = (0,0,0,0)`.
이후 `_isGroundBoundsScanned = true`가 되어도 Director는 재초기화되지 않음.

Director 내 경로 계산:
```csharp
float left = _groundBounds.MinX - screenEdgeOffset;  // 0 - 2 = -2
float right = _groundBounds.MaxX + screenEdgeOffset; // 0 + 2 = 2
```

**수정**: GroundBounds 스캔 완료 시 Director도 재초기화하거나, `Initialize()`를 늦게 호출.

---

## 🔴 DESIGN — Prepare Phase에 화면 밖 이동이 없음

**위치**: `MorayChargeDirector.cs:250-275` + `BossEnemyController.cs:873-882`
**영향**: 곰치가 사라졌다 나타나는 연출 안 됨. Prepare 중에도 Patrol 위치에 있음.
**수정 난이도**: 중

현재 흐름:
```
BeginPrepare()
  → Prepare (네모 생성)
  → ExecuteCharge(0)
    → TeleportTo(start)  ← 돌진 직전에야 순간이동
    → MoveTo(end)
```

기획 요구:
```
BeginPrepare()
  → TeleportTo(화면밖_진입점)  ← Prepare 시작하자마자 화면 밖으로
  → Prepare (네모 생성)
  → ExecuteCharge(0)
    → MoveTo(end)
```

**수정**: `BeginPrepare()` 내에서 Charge 0번의 시작 위치로 즉시 TeleportTo 호출.

---

## 🔴 FLOW — 돌진 중 Player 이탈/사망에도 Charge 시퀀스 계속됨

**위치**: `BossEnemyController.cs:476-479`
**영향**: Player가 Zone 밖으로 도망가도 Charge가 끝날 때까지 아무 일도 안 일어남
**수정 난이도**: 중

```csharp
else if (isRelentlessGimmick)
{
    // Moray: 기믹(OnChargeSequenceEnd)이 직접 Patrol 전환 제어
    // 여기서는 아무것도 안 함
}
```

Chase 중 Player가 Zone 이탈 / 사망 / 게임오버 되어도 아무 상태 전이 없음. Director가 Charge 시퀀스를 끝까지 실행한 후에야 `OnMorayChargesComplete` → Patrol 복귀.

**수정**: `MorayChargeDirector`에 강제 중단 메서드(`ForceInterrupt()`) 추가. 외부에서 호출 가능하게 함.

---

## 🟠 DESIGN — 돌진 방향이 좌/우 2방향 고정

**위치**: `MorayChargeDirector.cs:292-304`
**영향**: 패턴이 단조로워 플레이어가 쉽게 예측
**수정 난이도**: 상 (Camera frustum 계산 필요)

```csharp
int startDir = (_lastDirection == 0) ? 1 : 0;
int endDir = (_lastDirection == 0) ? 0 : 1;
```

- 상/하/대각선 방향 없음
- GroundBounds가 X축으로 길쭉하지 않으면 돌진 경로가 너무 짧거나 맵 밖으로 나감

**수정**: `Camera.main.ViewportToWorldPoint()` 기반 8방향 진입점 계산으로 변경.

---

## 🟠 FLOW — 최초 강제 Chase가 25m 거리 조건에 막힘

**위치**: `BossEnemyController.cs:170-182`
**영향**: Player가 멀리서 Zone에 진입하면 영원히 Chase가 발동하지 않음
**수정 난이도**: 중

```csharp
relentless.OnForceInitialChase = () =>
{
    ...
    float dist = Vector3.Distance(transform.position, _playerTransform.position);
    if (dist < 25f)  // ← 이 조건이 문제
    {
        _hasInitialMorayChaseTriggered = true;
        suspicionSystem?.ForceSetSuspicion(100f);
    }
};
```

- 25m 조건 실패 시 `_hasInitialMorayChaseTriggered`는 `false`로 남음
- `_hasTriggeredInitialChase`는 `true`가 되어 `OnForceInitialChase` 재호출 안 됨
- Patrol 중 의심도 상승도 `!_hasInitialMorayChaseTriggered`면 차단됨

**수정**: 25m 조건 제거. 또는 Zone 진입 Trigger(GroundBounds 기반)로 변경.

---

## 🟠 FLOW — 의심도 상승/하락 이중 계산

**위치**: `BossEnemyController.cs:532-538` + `BossSuspicionSystem.cs:213-222` + `BossEnemyController.cs:228-233`
**영향**: Patrol 중 순 상승률이 10%/s (의도: 15%/s)
**수정 난이도**: 중

Patrol 중 의심도 변화:
| 요인 | 방향 | 속도 |
|:----|:----:|:----:|
| Gimmick.OnPatrolUpdate → AddSuspicion(15, dt) | 상승 | +15%/s |
| BossSuspicionSystem.Update() 자체 하락 | 하락 | -5%/s × 1.0 = -5%/s |
| **순 상승률** | | **+10%/s** (의도: +15%/s) |

**수정**: RelentlessChaseGimmick 전용으로 `BossSuspicionSystem`의 자체 하락을 정지하거나, Gimmick의 상승률을 20%/s로 조정.

---

## 🟠 REDUNDANCY — Director와 Indicator가 같은 설정값 중복 보유

**위치**: `MorayChargeDirector.cs:18-22` + `MorayChargeIndicator.cs:13-17`
**영향**: Inspector에서 설정 변경 시 두 군데 모두 수정해야 함
**수정 난이도**: 하

`Director` 필드:
```csharp
[SerializeField] private float indicatorWidth = 2.5f;
[SerializeField] private Color activeColor = new Color(1f, 0.2f, 0.2f, 0.6f);
[SerializeField] private Color imminentColor = new Color(1f, 0f, 0f, 0.9f);
```

`Indicator` 필드:
```csharp
[SerializeField] private float indicatorWidth = 2.5f;
[SerializeField] private Color activeColor = new Color(1f, 0.2f, 0.2f, 0.6f);
[SerializeField] private Color imminentColor = new Color(1f, 0f, 0f, 0.9f);
```

**수정**: Director의 값 삭제하고 Indicator 참조로만 사용. 또는 Director가 Indicator의 값을 설정하는 `ApplySettings()` 추가.

---

## 🟠 STYLE — UpdatePreparing에서 `_currentChargeIndex` 역할 혼재

**위치**: `MorayChargeDirector.cs:172-193`
**영향**: 가독성 저하, 유지보수 리스크
**수정 난이도**: 하

Prepare 단계에서는 "생성된 개수 - 1"을 의미하다가, ExecuteCharge에서는 "현재 실행 중인 charge 인덱스"로 의미가 바뀜.

**수정**: Prepare 전용 카운터(`_preparedCount`)를 별도로 분리.

---

## 🟡 MINOR — 세부 잡음

| 항목 | 위치 | 내용 |
|:----|:----|:------|
| `CanBossDamagePlayer()` Moray false | BossEnemyController.cs:854 | Collision 데미지와 OverlapBox 데미지 이원화 — 의도된 거라도 혼란 |
| `IGimmickViewDirection` 미구현 | RelentlessChaseGimmick.cs:143-145 | 보스 스프라이트가 돌진 방향 반영 안 함 |
| `Awake()`에서 Player 탐색 | MorayChargeDirector.cs:75-78 | BossEnemyController와 중복. Player 늦게 생성 시 null |
| `IAware.SetCamouflageState` 빈 구현 | RelentlessChaseGimmick.cs:140 | 의태 상태를 받지만 아무 처리 안 함 |

---

## 📊 우선순위 정리

| 순위 | 문제 | 영향 | 난이도 | 예상 작업량 |
|:---:|:----|:----|:------:|:----------:|
| **1** | `_hasHitThisCharge` 리셋 누락 | 첫 돌진 이후 데미지 전무 | **1줄** | ~1분 |
| **2** | GroundBounds 초기화 불일치 | 돌진 범위 4m 고정 | **중** | ~10분 |
| **3** | 최초 강제 Chase 25m 조건 | Zone 진입해도 Chase 안 걸림 | **중** | ~15분 |
| **4** | Prepare Phase 화면 밖 이동 없음 | 연출/피드백 부족 | **중** | ~20분 |
| **5** | Charge 종료 후 복귀 위치 없음 | 곰치 위치 어긋남 | **하** | ~10분 |
| **6** | 의심도 상승/하락 이중 계산 | Patrol 상승률 10%(의도:15) | **중** | ~15분 |
| **7** | Director/Indicator 설정 중복 | 유지보수성 저하 | **하** | ~10분 |
| **8** | 좌/우 2방향 고정 | 패턴 단조로움 | **상** | 별도 작업 |

---

## 💡 구조 개선 방안 (문제 해결 후 적용)

> 아래 개선안은 버그 수정 및 기획 정합성 확보(**Step 1~2**)가 완료된 후,  
> 코드 품질 향상을 위해 **Step 3**에서 적용할 것을 권장합니다.

---

### 개선 1 — Prepare 단계를 Update() 상태머신 → 코루틴으로 변경

**현재**: `Update()`에서 매 프레임 `UpdatePreparing()` 호출, `_stateTimer`로 간격 제어

**문제점**:
- `UpdatePreparing()`에서 while문으로 한 프레임에 여러 네모를 생성할 수 있음 (interval이 짧으면 한 번에 다 생성됨)
- `_isPreparing / _isCharging / _isTransitioning` 3개 상태 플래그 필요
- `_stateTimer`로 타이밍 수동 관리

**개선**:
```csharp
public void BeginPrepare(int chargeCount)
{
    _totalCharges = chargeCount;
    _currentChargeIndex = 0;
    // ...경로 선계산...
    if (indicator != null) indicator.ClearAll();
    StartCoroutine(PrepareCoroutine());
}

private IEnumerator PrepareCoroutine()
{
    for (int i = 0; i < _totalCharges; i++)
    {
        if (indicator != null)
            indicator.SpawnIndicator(_chargeStarts[i], _chargeEnds[i]);
        yield return new WaitForSeconds(GetSpawnInterval(i));
    }
    ExecuteCharge(0);
}

private float GetSpawnInterval(int chargeIndex)
{
    // 회차별 간격 단축: max(0.5 - (chaseEntryCount-1) * 0.05, 0.2)
    float baseInterval = 0.5f;
    float step = 0.05f;
    float minInterval = 0.2f;
    return Mathf.Max(baseInterval - (_chaseEntryCount - 1) * step, minInterval);
}
```

**효과**:
- 상태 플래그 3개 → 코루틴 1개로 감소
- 간격 동적 계산 자연스럽게 적용 가능
- Prepare 중 강제 종료도 `StopCoroutine()`으로 간단 처리

---

### 개선 2 — Charge 단계도 코루틴으로 변경 (Transform 직접 보간)

**현재**:
```csharp
// BossEnemyController.OnMorayChargeExecute
em.TeleportTo(start);   // velocity = 0
em.MoveTo(end);         // 0 → chargeSpeed까지 가속 필요
```
- `TeleportTo()`가 `_velocity = Vector3.zero`로 리셋 → 돌진 초반 속도 느림
- EnemyMovement의 가속도/감속도 시스템의 영향을 받아 궤적이 일정하지 않음

**개선**:
```csharp
private IEnumerator ChargeCoroutine(int index)
{
    Vector3 start = _chargeStarts[index];
    Vector3 end = _chargeEnds[index];
    start.y = _bossTransform.position.y;
    end.y = start.y;
    
    // 임박 색상
    if (indicator != null) indicator.SetImminent(index);
    OnSpeedOverride?.Invoke(chargeSpeed);
    
    // 시작 위치로 순간이동
    _bossTransform.position = start;
    
    // 돌진 (Lerp 기반, 일정한 속도)
    float distance = Vector3.Distance(start, end);
    float duration = distance / chargeSpeed;
    float elapsed = 0f;
    bool hasHit = false;
    
    while (elapsed < duration)
    {
        float t = elapsed / duration;
        _bossTransform.position = Vector3.Lerp(start, end, t);
        
        // 충돌 체크 (매 프레임)
        if (!hasHit) hasHit = CheckChargeHit(index);
        
        elapsed += Time.deltaTime;
        yield return null;
    }
    
    _bossTransform.position = end;
    
    // 인디케이터 소멸
    if (indicator != null) indicator.DespawnIndicator(index);
    
    // 다음 Charge 실행
    int nextIdx = index + 1;
    if (nextIdx >= _totalCharges)
    {
        OnMovementStop?.Invoke();
        OnChargesComplete?.Invoke();
    }
    else
    {
        yield return new WaitForSeconds(chargeDelay);
        StartCoroutine(ChargeCoroutine(nextIdx));
    }
}
```

**효과**:
- 일정한 속도의 직선 돌진 보장
- EnemyMovement의 Patrol/Chase와 완전히 독립적
- 가속도/감속도 문제 해결
- 예측 가능한 돌진 시간

---

### 개선 3 — 전체 Prepare → Charge 시퀀스를 단일 코루틴으로 통합

위 개선 1 + 2를 합치면:

```csharp
public void BeginPrepare(int chargeCount)
{
    _totalCharges = chargeCount;
    _currentChargeIndex = 0;
    if (indicator != null) indicator.ClearAll();
    
    // 경로 선계산
    _chargeStarts = new Vector3[chargeCount];
    _chargeEnds = new Vector3[chargeCount];
    for (int i = 0; i < chargeCount; i++)
        CalculateChargePath(i);
    
    // 시퀀스 시작
    StopAllCoroutines();
    StartCoroutine(ChargeSequenceCoroutine());
}

private IEnumerator ChargeSequenceCoroutine()
{
    // === Prepare Phase ===
    for (int i = 0; i < _totalCharges; i++)
    {
        // 1. 시작 위치로 순간이동 (Prepare 시작 = 화면 밖으로)
        if (i == 0)
        {
            Vector3 preparePos = _chargeStarts[0];
            preparePos.y = _bossTransform.position.y;
            _bossTransform.position = preparePos;
        }
        
        // 2. 네모 생성
        if (indicator != null)
            indicator.SpawnIndicator(_chargeStarts[i], _chargeEnds[i]);
        
        yield return new WaitForSeconds(GetSpawnInterval(i));
    }
    
    // === Charge Phase (N회 연속) ===
    for (int i = 0; i < _totalCharges; i++)
    {
        // 돌진 실행 (Lerp)
        yield return StartCoroutine(ExecuteSingleCharge(i));
        
        // 돌진 사이 텀
        if (i < _totalCharges - 1)
            yield return new WaitForSeconds(chargeDelay);
    }
    
    // === 완료 ===
    OnMovementStop?.Invoke();
    OnChargesComplete?.Invoke();
}

private IEnumerator ExecuteSingleCharge(int index)
{
    Vector3 start = _chargeStarts[index];
    Vector3 end = _chargeEnds[index];
    start.y = _bossTransform.position.y;
    end.y = start.y;
    
    if (indicator != null) indicator.SetImminent(index);
    OnSpeedOverride?.Invoke(chargeSpeed);
    
    // 이미 Prepare에서 첫 위치로 이동했으면, 2회차부터는 마지막 위치에서 다음 시작점으로 순간이동
    if (index > 0)
        _bossTransform.position = start;
    
    float distance = Vector3.Distance(start, end);
    float duration = Mathf.Max(distance / chargeSpeed, 0.3f);
    float elapsed = 0f;
    bool hasHit = false;
    
    while (elapsed < duration)
    {
        _bossTransform.position = Vector3.Lerp(start, end, elapsed / duration);
        if (!hasHit) hasHit = CheckChargeHit(index);
        elapsed += Time.deltaTime;
        yield return null;
    }
    
    _bossTransform.position = end;
    if (indicator != null) indicator.DespawnIndicator(index);
}
```

**효과**:
- `_isPreparing / _isCharging / _isTransitioning` → **전부 제거**
- `_stateTimer` → **제거**
- `_currentChargeIndex` → 지역 변수로 변경 가능
- 코드 라인 수 약 150줄 → 80줄로 감소
- Prepare → Charge 사이클이 한 눈에 보임

---

### 개선 4 — `ForceInterrupt()` 추가 (예외 상황 대응)

**현재**: Player 이탈/사망 시 Charge 시퀀스를 강제 종료할 방법 없음

**개선**:
```csharp
public class MorayChargeDirector : MonoBehaviour
{
    private Coroutine _chargeSequence;
    
    public void BeginPrepare(int chargeCount)
    {
        // ...기존 로직...
        StopAllCoroutines();
        _chargeSequence = StartCoroutine(ChargeSequenceCoroutine());
    }
    
    public void ForceInterrupt()
    {
        StopAllCoroutines();
        _chargeSequence = null;
        
        if (indicator != null)
            indicator.ClearAll();
        
        OnMovementStop?.Invoke();
        OnChargesComplete?.Invoke();  // → Patrol 복귀 트리거
    }
    
    public void ResetCharges()
    {
        StopAllCoroutines();
        _chargeSequence = null;
        
        if (indicator != null)
            indicator.ClearAll();
    }
}
```

`BossEnemyController.CheckStateTransitions()`에서:
```csharp
else if (isRelentlessGimmick)
{
    // Player가 너무 멀어졌으면 강제 중단
    if (_playerTransform != null && 
        Vector3.Distance(transform.position, _playerTransform.position) > abandonDistance)
    {
        chargeDirector?.ForceInterrupt();
    }
}
```

---

### 개선 5 — `PlayerMovementAdapter.GetComponent()` 캐싱

**현재**: `MorayChargeDirector.Update()`에서 매 프레임 `GetComponent<PlayerMovementAdapter>()` 호출

**개선**:
```csharp
private PlayerMovementAdapter _cachedMovement;

private void Awake()
{
    GameObject playerObj = GameObject.FindWithTag("Player");
    if (playerObj != null)
    {
        _playerTransform = playerObj.transform;
        _cachedMovement = playerObj.GetComponent<PlayerMovementAdapter>();
    }
}

// Update()에서는 캐시된 값 사용
private void Update()
{
    // → 개선 3 적용 시 Update()가 단순해져서 이 부분이 사실상 필요 없을 수 있음
}
```

---

### 개선 6 — 의심도 시스템에 곰치 전용 자체 하락 정지 모드

**현재**: Gimmick이 `AddSuspicion()`으로 상승시키는 것과 BossSuspicionSystem의 자체 하락이 동시에 동작

**개선**:
```csharp
// BossSuspicionSystem.cs
private bool _autoDecayEnabled = true;

public void SetAutoDecayEnabled(bool enabled)
{
    _autoDecayEnabled = enabled;
}

private void Update()
{
    // 모듈 업데이트 (기존)
    if (_suspicionModule != null) { ... }
    
    // 발각 체크 (기존)
    ...
    
    // 의심도 하락 (조건부)
    if (_autoDecayEnabled && _currentValue > 0f)  // ← 조건 추가
    {
        float decreaseSpeed = _isCamouflaging ? camouflageDecreaseSpeed : normalDecreaseSpeed;
        decreaseSpeed *= _suspicionDecayMultiplier;
        _currentValue -= decreaseSpeed * Time.deltaTime;
        _currentValue = Mathf.Max(_currentValue, 0f);
    }
    
    // 레벨 체크 (기존)
    ...
}
```

`BossEnemyController.OnAIStateChanged()`에서:
```csharp
if (isRelentless)
{
    suspicionSystem.SetAutoDecayEnabled(false);  // Gimmick이 의심도 전담 제어
    suspicionSystem.SetVisionIncreaseSpeed(0f);
}
```

---

### 개선 7 — Indicator 오브젝트 풀링

**현재**: `SpawnIndicator()`에서 `new GameObject` + `AddComponent` 수행, `DespawnIndicator()`에서 `Destroy()`

**개선**:
```csharp
public class MorayChargeIndicator : MonoBehaviour
{
    private Queue<GameObject> _pool = new Queue<GameObject>();
    private const int POOL_SIZE = 5;
    
    private void Awake()
    {
        // 미리 5개 생성
        for (int i = 0; i < POOL_SIZE; i++)
        {
            GameObject go = CreateIndicatorObject();
            go.SetActive(false);
            _pool.Enqueue(go);
        }
    }
    
    public int SpawnIndicator(Vector3 start, Vector3 end)
    {
        GameObject go = _pool.Dequeue();
        go.SetActive(true);
        UpdateMesh(go, start, end);
        // ... ChargePath 저장 ...
        return _paths.Count - 1;
    }
    
    public void DespawnIndicator(int index)
    {
        // ... 게임오bject 비활성화 후 풀에 반환 ...
        _paths[index].gameObject.SetActive(false);
        _pool.Enqueue(_paths[index].gameObject);
    }
    
    public void ClearAll()
    {
        for (int i = 0; i < _paths.Count; i++)
        {
            if (_paths[i].gameObject != null)
            {
                _paths[i].gameObject.SetActive(false);
                _pool.Enqueue(_paths[i].gameObject);
            }
        }
        _paths.Clear();
    }
}
```

---

### 개선 8 — Director-Indicator 위임 관계 정리

**현재**: Director와 Indicator가 같은 설정값(indicatorWidth, activeColor, imminentColor) 중복 보유

**개선 (옵션 A — Director가 Indicator 설정)**:
```csharp
public void BeginPrepare(int chargeCount)
{
    if (indicator != null)
    {
        indicator.Width = indicatorWidth;
        indicator.ActiveColor = activeColor;
        indicator.ImminentColor = imminentColor;
    }
    // ...
}
```

**개선 (옵션 B — Indicator가 Director 참조, 권장)**:
```csharp
// Indicator에서 Director 참조
public class MorayChargeIndicator : MonoBehaviour
{
    [SerializeField] private MorayChargeDirector director;
    
    private float EffectiveWidth => director != null ? director.indicatorWidth : indicatorWidth;
    private Color EffectiveActiveColor => director != null ? director.activeColor : activeColor;
    private Color EffectiveImminentColor => director != null ? director.imminentColor : imminentColor;
    
    // SpawnIndicator 등에서 EffectiveWidth 사용
}
```

---

### 개선 9 — `_currentChargeIndex` Prepare/Charge 역할 분리

**현재**: Prepare 단계에서는 "생성된 개수-1", Charge 단계에서는 "실행 중인 인덱스"

**개선** (개선 3의 코루틴 적용 시 자연스럽게 해결됨):

코루틴 기반으로 변경하면 `_currentChargeIndex`가 필드가 아닌 **지역 변수**로 변경 가능:
```csharp
private IEnumerator ChargeSequenceCoroutine()
{
    for (int i = 0; i < _totalCharges; i++)  // ← i가 지역 변수
    {
        yield return StartCoroutine(ExecuteSingleCharge(i));
    }
}
```

---

### 개선 10 — 충돌 체크를 `_hasHitThisCharge` 단일 플래그에서 배열로 변경

**현재**: `_hasHitThisCharge` boolean 하나로 모든 charge의 히트 상태 관리 → 올바르게 리셋해도 charge 간 히트 이력이 공유됨

**개선**:
```csharp
private bool[] _chargeHitFlags;

public void BeginPrepare(int chargeCount)
{
    _chargeHitFlags = new bool[chargeCount];
    // ...
}

private bool CheckChargeHit(int index)
{
    if (index >= _chargeHitFlags.Length) return false;
    if (_chargeHitFlags[index]) return false;  // 해당 charge에서 이미 hit
    
    // ...OverlapBox 체크...
    if (hits.Length > 0)
    {
        _chargeHitFlags[index] = true;
        OnPlayerHit?.Invoke();
        return true;
    }
    return false;
}
```

---

## 🗺️ 전체 작업 로드맵

```
Step 1: BUG FIX (우선)
├── 1. _hasHitThisCharge 리셋         ← 1줄, 1분
├── 2. GroundBounds 초기화 시점        ← ~10분
└── 3. 최초 강제 Chase 25m 조건 제거   ← ~15분
↓ 게임 정상 동작 범위 확보

Step 2: DESIGN FIX (기획 정합성)
├── 4. Prepare Phase 화면 밖 이동     ← ~20분
├── 5. Charge 종료 후 복귀 위치       ← ~10분
├── 6. 의심도 상승/하락 이중 계산      ← ~15분
└── 7. Director/Indicator 중복 정리   ← ~10분
↓ 기획 의도대로 동작

Step 3: IMPROVEMENT (코드 품질)
├── 8. Prepare/Charge 코루틴화         ← ~1시간  (개선 1+2+3 통합)
├── 9. ForceInterrupt() 추가           ← ~20분  (개선 4)
├── 10. GetComponent 캐싱              ← ~5분   (개선 5)
├── 11. 의심도 자체 하락 정지 모드      ← ~15분  (개선 6)
├── 12. Indicator 오브젝트 풀링        ← ~30분  (개선 7)
└── 13. _chargeHitFlags 배열화         ← ~10분  (개선 10)
↓ 유지보수 용이, 성능 개선
```

---

*이 문서는 곰치(Moray Eel) 보스 코드의 문제점 분석 + 개선 방안입니다.*
*문제 수정(Step 1~2) 후 개선(Step 3) 순서로 진행하는 것을 권장합니다.*
