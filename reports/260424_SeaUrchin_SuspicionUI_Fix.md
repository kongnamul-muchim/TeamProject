# 버그 수정 보고서: 성게 생성 + 보스 의심도 UI

> 작성일: 2026-04-24
> 커밋: `8f41a9f`

---

## 1. 작업 개요

| 항목 | 내용 |
|------|------|
| **문제 1** | 조류 발동 시 성게(Sea Urchin)가 생성되지 않음 |
| **문제 2** | 보스 의심도 UI가 인스펙터에서 1개의 보스만 수동 연결 가능 |
| **해결 방안** | 성게 소환 좌표 버그 수정 + `SuspicionUIManager` 싱글톤 도입으로 자동 연결 |

---

## 2. 문제점 분석

### 2-1. 성게 생성 안 됨

**원인 A: `ViewportToWorldPoint` Z값 = 0**
```csharp
// 수정 전 (SeaUrchinController.cs)
Vector3 viewportPos = new Vector3(viewportX, 0.5f, 0f);
Vector3 worldPos = mainCamera.ViewportToWorldPoint(viewportPos);
```
- Z=0은 치에라의 **near clip plane** 위치를 의미
- Perspective 치에라: 성게가 치에라 바로 앞/뒤에 붙어서 사실상 보이지 않거나 즉시 사라짐
- Orthographic 치에라: Z=0이면 월드 Z=0에 생성되어 다른 오브젝트와 겹칠 수 있음

**원인 B: 풀에서 꺼낼 때 부모(로컬) 좌표계에 갇힘**
```csharp
// 수정 전 (SeaUrchinPool.cs)
var pooledUrchin = _pool.Dequeue();
pooledUrchin.SetupForTide(direction, tideForce);  // worldPos 대입
pooledUrchin.gameObject.SetActive(true);
// → SetParent(null)이 없어서 _parent(TideManager)의 로컬 좌표계에 남아있음
```

### 2-2. 보스 의심도 UI 수동 연결

**기존 구조:**
```
SuspicionMeterUI ──[SerializeField]──→ BossSuspicionSystem (1개만)
```
- 씬에 보스를 배치할 때마다 인스펙터에서 수동으로 `BossSuspicionSystem`을 할당해야 함
- 프리팹 재사용 시 매번 설정 필요

---

## 3. 수정 내용

### 3-1. 성게 생성 버그 수정

**파일: `Assets/Core/Enemy/Normal/SeaUrchinController.cs`**
```csharp
// 수정 후: 치에라와 성게가 같은 Z 평면에 있도록 거리 계산
float distanceToCameraPlane = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
Vector3 viewportPos = new Vector3(viewportX, 0.5f, distanceToCameraPlane);
Vector3 worldPos = mainCamera.ViewportToWorldPoint(viewportPos);
```

**파일: `Assets/Core/Enemy/Normal/SeaUrchinPool.cs`**
```csharp
// 수정 후: 풀에서 꺼낼 때 월드 좌표계로 이동
var pooledUrchin = _pool.Dequeue();
pooledUrchin.transform.SetParent(null);  // ← 추가
pooledUrchin.SetupForTide(direction, tideForce);
pooledUrchin.gameObject.SetActive(true);
```

### 3-2. 보스 의심도 UI 자동 연결 (방안 C)

**신규 파일: `Assets/Core/Perception/SuspicionUIManager.cs`**
- `Singleton<T>` 상속 싱글톤
- `Register(BossSuspicionSystem)` / `Unregister(BossSuspicionSystem)` 제공
- 보스 의심도 이벤트를 UI에 중계

**파일: `Assets/Core/Perception/BossSuspicionSystem.cs`**
```csharp
private void Awake()
{
    SuspicionUIManager.Instance?.Register(this);
}

private void OnDestroy()
{
    SuspicionUIManager.Instance?.Unregister(this);
    // ... 기존 정리 코드
}
```

**파일: `Assets/Core/Perception/SuspicionMeterUI.cs`**
- 레거시 호환: `bossSuspicionSystem` 직접 할당 시 우선 사용
- 자동 연결: `bossSuspicionSystem`이 null이면 `SuspicionUIManager.Instance` 이벤트 구독

---

## 4. 변경 파일 목록

| 파일 | 변경 유형 | 설명 |
|------|-----------|------|
| `Assets/Core/Enemy/Normal/SeaUrchinController.cs` | 수정 | `ViewportToWorldPoint` Z값 버그 수정 |
| `Assets/Core/Enemy/Normal/SeaUrchinPool.cs` | 수정 | `SpawnFromTide`에서 `SetParent(null)` 추가 |
| `Assets/Core/Perception/SuspicionUIManager.cs` | **신규** | 보스 의심도 UI 중계 싱글톤 |
| `Assets/Core/Perception/BossSuspicionSystem.cs` | 수정 | `Awake`/`OnDestroy`에서 자동 등록/해제 |
| `Assets/Core/Perception/SuspicionMeterUI.cs` | 수정 | `SuspicionUIManager` 자동 연결 지원 |

---

## 5. 테스트 방법

### 성게 생성 테스트
1. Unity 에디터에서 `Test.unity` 씬 실행
2. `TideManager`의 `tideInterval`을 짧게 설정(예: 2초)
3. 플레이 모드에서 조류 발동 시 성게가 치에라 밖에서 생성되어 굟는지 확인
4. Console에 `[TideManager] Tide started: ...` 로그 출력 확인

### 보스 의심도 UI 테스트
1. `SuspicionMeterUI`의 `bossSuspicionSystem` 필드를 **null**로 설정
2. 플레이 모드에서 보스가 Player를 감지하면 UI 게이지가 자동으로 상승하는지 확인
3. Console에 `[SuspicionUIManager] Registered: ...` 로그 출력 확인

---

## 6. 주의사항

- `SuspicionMeterUI`는 기존 인스펙터 연결을 **레거시 호환**으로 유지합니다.
  - `bossSuspicionSystem`에 값이 있으면 직접 참조를 우선 사용
  - null이면 `SuspicionUIManager` 자동 연결
- `SuspicionUIManager`는 `DontDestroyOnLoad`이므로 씬 전환 시에도 유지됩니다.
- 성게 프리팹(`Uni.prefab`)에 `Collider` + `Rigidbody`가 정상적으로 붙어있는지 별도 확인이 필요합니다.
  - `OnTriggerEnter`(Player 둔부)와 `OnCollisionEnter`(성게 간 충돌)가 동시에 존재하므로, Collider 설정에 따라 한쪽만 작동할 수 있습니다.

---

*보고서 작성 완료*
