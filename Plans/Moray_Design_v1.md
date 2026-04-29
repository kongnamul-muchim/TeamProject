# 곰치 (Moray Eel) Boss 기믹 설계서 v1

> **버전:** v1.0
> **작성일:** 2026-04-28
> **상태:** 설계 완료 (구현 전)
> **파일:** `RelentlessChaseGimmick.cs` (전면 개편) → 또는 신규 `MorayEelGimmick.cs`

---

## 1. 개요

| 항목 | 내용 |
|------|------|
| **Zone** | Zone 3 (침몰선 유적 지대) |
| **파일** | `RelentlessChaseGimmick.cs` (기존, 전면 개편) |
| **핵심 컨셉** | **화면 밖 은신 → 붉은 네모 경고 → 고속 돌진 → 반복** |
| **핵심 돌파 요소** | 붉은 네모(돌진 경로)를 읽고, 돌진 사이 텀에 이동 |

> 가자미가 "흔적을 읽고 위치를 추적"하는 법을 가르쳤다면,  
> 곰치는 **"경고를 읽고 다중 위협을 회피"** 하는 법을 가르침.

---

## 2. 동작 흐름

```
[Zone 3 진입]
   │
   ├─ [최초 1회] 무조건 의심도 100% → Chase 강제 실행
   │     └─ 돌진 1회 경험 후 Patrol (의심도 30)
   │
   └─ [이후 Patrol] 구역 내 체류 시 의심도 자동 상승
         └─ 매초 15% 상승 (의심도 30에서 시작)
         └─ 100% 도달 → Chase N회차 진입
         └─ N = Chase 진입 횟수 (1, 2, 3, ...)

[Chase N회차 진입]
   │
   ├─ 돌진 횟수 = N (최대 5)
   ├─ 네모 생성 간격 = max(0.5 - (N-1) * 0.05, 0.2)초
   │
   ▼
[Prepare Phase] 곰치 화면 밖으로 이동
   │          붉은 네모 돌진 횟수만큼 순차 생성
   │          (1회차: 0.5s 간격 1개, 3회차: 0.4s 간격 3개, ...)
   ▼
[Charge Phase] 곰치가 네모 경로 따라 화면 끝→끝 돌진
   │          돌진 방향 = 카메라 8방향 진입점 중 랜덤
   │          돌진 완료 시 해당 네모 소멸
   │          반대편 진입점 → 다음 네모 경로로 재돌진
   │          모든 네모 소멸 = Charge Phase 종료
   ▼
[Patrol 복귀] 의심도 30 고정 설정
   │          Player 추적 재개
   ▼
(구역 내 체류 → 의심도 재상승 → 다음 Chase 사이클)
```

---

## 3. 의심도 시스템

### 3.1 의심도 상승

| 상황 | 상승률 | 30→100% 소요 시간 | 비고 |
|:----:|:-----:|:-----------------:|:----:|
| **구역 내 비의태** | 15%/s | 약 4.7초 | 기본 |
| **구역 내 의태 중** | 15%/s | 약 4.7초 | **의태 무시** — 구역 자체가 위험 |
| **구역 이탈** | 하락 (5%/s) | 100→30 = 14초 | 완전 이탈 시 안전 |

> **의태가 소용없는 이유**: 곰치는 시력이 나쁘지만 후각/진동 감지로  
> 구역 내 생물 존재 자체를 감지함. 의태는 소리/냄새를 가리지 못함.

### 3.2 최초 1회 강제 발동

```
Zone 3 진입
   │
   └─ [BossEnemyController.OnPlayerEnterZone]
         └─ 의심도 = 100 즉시 설정
         └─ Chase 진입 (1회차, 돌진 1회)
         └─ 종료 후 의심도 30
```

### 3.3 Patrol 중 재상승

```
의심도 30 (고정)
   │
   ├─ Player가 구역(GroundBounds) 내에 있음
   │     └─ 매초 15%씩 상승
   │     └─ 약 4.7초 후 100% 도달
   │
   ├─ Player가 구역 밖으로 나감
   │     └─ 의심도 하락 (5%/s)
   │     └─ 완전히 나가면 0
   │
   └─ 100% 도달 → Chase N회차 진입
```

---

## 4. 돌진 시스템

### 4.1 돌진 횟수 에스컬레이션

| Chase 진입 횟수 | 돌진 횟수 | 네모 간격 | 예상 총 소요 시간 |
|:--------------:|:---------:|:---------:|:----------------:|
| 1회차 | 1회 | 0.50s | 0.5s (준비) + 0.5s (돌진) = 1.0s |
| 2회차 | 2회 | 0.45s | 0.9s + 1.0s = 1.9s |
| 3회차 | 3회 | 0.40s | 1.2s + 1.5s = 2.7s |
| 4회차 | 4회 | 0.35s | 1.4s + 2.0s = 3.4s |
| 5회차+ | 5회 | 0.30s | 1.5s + 2.5s = 4.0s |

### 4.2 돌진 방향 다양화

**진입점**: 카메라 frustum 경계 기준 8방향

```
       (상)
  (좌상)   (우상)
      \     /
(좌)---[화면]---(우)
      /     \
  (좌하)   (우하)
       (하)
```

**돌진 결정 알고리즘:**

```
1. 8방향 중 랜덤 시작점 선택 (화면 밖, Camera frustum + 2m)
2. 반대 방향 종료점 계산
3. GroundBounds 내인지 확인
   ├─ 시작점과 종료점 모두 GroundBounds 내부여야 함
   └─ 벗어나면 재선택 (최대 3회)
4. 경로 상 화면 안 구간만 네모 렌더링
5. 곰치가 시작점 → 종료점으로 고속 이동
```

**방향 조합 보장** (같은 방향 연속 방지):

```
1회차: [좌→우]
2회차: [상→하] [우→좌]          ← 방향 다양화 시작
3회차: [좌→우] [좌상→우하] [하→상]
4회차: [우→좌] [좌하→우상] [상→하] [좌→우]
```

### 4.3 돌진 시퀀스 예시 (3회차 Chase)

```
t=0.0s:  3개 네모 순차 생성 시작
         ┌─────────────────────────────┐
         │    ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓    │  ← 좌→우 (생성 완료)
         │         ▓▓▓▓▓▓▓▓▓▓▓▓▓       │  ← 좌상→우하 (생성 완료)
         │                              │  ← 아직 생성 중
         └─────────────────────────────┘

t=0.4s:  네모 #3 생성 완료
         ┌─────────────────────────────┐
         │    ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓    │
         │         ▓▓▓▓▓▓▓▓▓▓▓▓▓       │
         │    ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓    │  ← 하→상 (생성 완료)
         └─────────────────────────────┘

t=0.4s:  돌진 #1 시작! → 네모 #1 소멸
         ┌─────────────────────────────┐
         │                              │
         │         ▓▓▓▓▓▓▓▓▓▓▓▓▓       │
         │    ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓    │
         └─────────────────────────────┘

t=0.8s:  돌진 #2 시작! → 네모 #2 소멸
         ┌─────────────────────────────┐
         │                              │
         │                              │
         │    ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓    │
         └─────────────────────────────┘

t=1.2s:  돌진 #3 시작! → 네모 #3 소멸 → Charge 종료
```

### 4.4 네모 렌더링 방식

| 항목 | 내용 |
|:----:|------|
| **형태** | 바닥에 붙은 붉은 반투명 사각형 |
| **범위** | 화면 안 구간만 (시작점/종료점은 화면 밖이므로 렌더링 안 됨) |
| **너비** | 돌진 판정 범위와 동일 (약 2~3m) |
| **생성 간격** | Chase 회차별 단축 (0.5s → 0.3s) |
| **소멸 조건** | 해당 경로 돌진 완료 시 즉시 소멸 |
| **색상** | 생성 직후 연한 붉은색 → 돌진 임박 시 진한 붉은색 (펄스) |

---

## 5. 상태별 동작

### 5.1 Patrol

| 항목 | 내용 |
|:----:|------|
| **행동** | 구역 내 랜덤 순찰 (속도 느림, 약 1.5) |
| **시야** | ConeVisionSensor (60°, 반경 6m) |
| **의심도 상승** | 구역 내 자동 15%/s |
| **최초** | Zone 진입 시 강제 100% |
| **Chase 종료 후** | 의심도 30 고정 |

### 5.2 Chase — Prepare Phase

| 항목 | 내용 |
|:----:|------|
| **진입 조건** | 의심도 100% 도달 |
| **곰치 행동** | 즉시 화면 밖으로 이동 (SetActive(false) or 이동) |
| **네모 생성** | 돌진 횟수(N)만큼 순차 생성 |
| **생성 간격** | `max(0.5 - (N-1) * 0.05, 0.2)` |
| **Player 행동** | 네모 보고 돌진 경로 예측, 회피 준비 |

### 5.3 Chase — Charge Phase

| 항목 | 내용 |
|:----:|------|
| **곰치 위치** | 화면 밖 진입점 → 화면 끝→끝 고속 이동 |
| **돌진 속도** | `chargeSpeed` (18) |
| **돌진 방향** | 8방향 랜덤 (GroundBounds 내) |
| **Player 피격** | 체력 데미지 (의심도 아님) |
| **의태 효과** | **무시** — 구역 자체가 위험 |
| **반격** | **불가** — 도망만 가능 |

### 5.4 Chase 종료 → Patrol 전환

| 항목 | 내용 |
|:----:|------|
| **전환 조건** | 모든 N회 돌진 완료 |
| **의심도** | 30 고정 (즉시 설정) |
| **곰치 위치** | 화면 밖 → Patrol 시작 위치로 복귀 |
| **이동** | 구역 내 랜덤 순찰 재개 |
| **Player 상태** | 체력 데미지 누적 확인 |

---

## 6. 설정값 (전부 Inspector)

### RelentlessChaseGimmick (ScriptableObject)

| 파라미터 | 기본값 | 설명 |
|----------|:------:|------|
| `chargeSpeed` | 18 | 돌진 속도 |
| `chargeWidth` | 2.5 | 돌진 판정 너비 (네모 너비) |
| `baseSquareSpawnInterval` | 0.5 | 1회차 네모 생성 간격 (초) |
| `squareSpawnIntervalStep` | 0.05 | 회차당 간격 감소량 |
| `minSquareSpawnInterval` | 0.2 | 최소 네모 생성 간격 |
| `maxChargesPerCycle` | 5 | 최대 돌진 횟수 (상한) |
| `suspicionIncreaseRate` | 15 | 구역 내 초당 의심도 상승량 |
| `postChaseSuspicion` | 30 | 돌진 종료 후 고정 의심도 |
| `screenEdgeOffset` | 2 | 화면 경계에서 진입점까지 거리 |

### Inspector 조정 추천 범위

| 파라미터 | 최소 | 최대 | 기본 |
|----------|:---:|:---:|:----:|
| `chargeSpeed` | 12 | 25 | 18 |
| `suspicionIncreaseRate` | 5 | 25 | 15 |
| `baseSquareSpawnInterval` | 0.3 | 0.8 | 0.5 |

---

## 7. 기술적 고려사항

### 7.1 화면 밖 판정 + 진입점 계산

```csharp
// Camera frustum 기준 8방향 진입점 계산
Vector3[] GetScreenEdgePoints()
{
    Camera cam = Camera.main;
    float offset = gimmick.screenEdgeOffset;
    
    // 화면 4개 모서리 월드 좌표
    Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, cam.nearClipPlane));
    Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, cam.nearClipPlane));
    
    float left = bottomLeft.x - offset;
    float right = topRight.x + offset;
    float top = topRight.y + offset;
    float bottom = bottomLeft.y - offset;
    float centerX = (left + right) / 2f;
    float centerY = (top + bottom) / 2f;
    
    return new Vector3[]
    {
        new Vector3(left, centerY, 0),      // 좌
        new Vector3(right, centerY, 0),     // 우
        new Vector3(centerX, top, 0),       // 상
        new Vector3(centerX, bottom, 0),    // 하
        new Vector3(left, top, 0),          // 좌상
        new Vector3(right, top, 0),         // 우상
        new Vector3(left, bottom, 0),       // 좌하
        new Vector3(right, bottom, 0),      // 우하
    };
}

// GroundBounds 내 확인
bool IsWithinBounds(Vector3 point, GroundBounds bounds)
{
    return point.x >= bounds.MinX && point.x <= bounds.MaxX
        && point.z >= bounds.MinZ && point.z <= bounds.MaxZ;
}
```

### 7.2 네모 렌더링 (LineRenderer 권장)

```csharp
public class MorayChargeIndicator : MonoBehaviour
{
    [SerializeField] private Material indicatorMaterial;
    [SerializeField] private float indicatorWidth = 2.5f;
    [SerializeField] private Color warningColor = new Color(1, 0.2f, 0.2f, 0.6f);
    
    private List<LineRenderer> activeIndicators = new List<LineRenderer>();
    
    // 네모 생성
    public void SpawnIndicator(Vector3 start, Vector3 end, int index)
    {
        // LineRenderer 생성 및 설정
        // start→end 직선, width=indicatorWidth, color=warningColor
    }
    
    // 네모 소멸
    public void DespawnIndicator(int index) { }
    
    // 전체 초기화
    public void ClearAll() { }
}
```

### 7.3 돌진 충돌 판정

```csharp
// OverlapBox 기반 충돌 체크
// 매 프레임 현재 돌진 경로와 Player 충돌 확인

bool CheckChargeHit(Vector3 chargeStart, Vector3 chargeEnd, float width)
{
    Vector3 direction = (chargeEnd - chargeStart).normalized;
    float distance = Vector3.Distance(chargeStart, chargeEnd);
    Vector3 center = (chargeStart + chargeEnd) / 2f;
    
    Collider[] hits = Physics.OverlapBox(
        center, 
        new Vector3(width / 2f, 1f, distance / 2f),
        Quaternion.LookRotation(direction),
        playerLayerMask
    );
    
    return hits.Length > 0;
}
```

### 7.4 기존 코드와의 관계

| 항목 | 현재 (RelentlessChaseGimmick) | 변경 |
|:----:|:-----------------------------:|:----:|
| 의심도 하락 30% | 있었음 | **제거** |
| 순찰 반경 제한 | 5m | **유지** (Patrol에서 사용) |
| 수색 반경 1.5배 | 있었음 | **제거** (Search 없음) |
| Chase 행동 | 느린 의심 하락 + 추적 | **전면 개편** — 화면 밖 돌진 |
| Player 피격 | 없음 | **추가** — 체력 데미지 |

---

## 8. 수정 파일 목록

| 파일 | 변경 | 내용 |
|:----:|:----:|------|
| `RelentlessChaseGimmick.cs` | **전면 재작성** | 화면 밖 돌진 + 네모 + 에스컬레이션 |
| `BossEnemyController.cs` | 수정 | 의심도 30 고정 + 최초 강제 100% |
| `MorayChargeIndicator.cs` | **신규** | 붉은 네모 생성/소멸/애니메이션 |
| `PlayerHealth.cs` (가칭) | (추후) | 체력 시스템 (공통, 추후 구현) |
| `RelentlessChaseGimmick.asset` | 수정 | 신규 파라미터 Inspector 반영 |
| `EnemyMovement.cs` | (필요 시) | 화면 밖 순간 이동 함수 |

### 변경량 (예상)

```
RelentlessChaseGimmick.cs  : ~450줄 (전면 재작성)
BossEnemyController        : +10줄 (최초 강제 100% + 의심도 30 고정)
MorayChargeIndicator.cs    : ~100줄 (네모 관리)
EnemyMovement.cs           : +15줄 (화면 밖 순간 이동)
```

---

## 9. 구현 시 주의사항

| 항목 | 설명 |
|:----:|------|
| **GroundBounds 이탈 방지** | 8방향 진입점 선택 시 GroundBounds 체크 필수 |
| **화면 밖 위치 동기화** | 곰치가 화면 밖에 있는 동안 Patrol 이동 로직 정지 |
| **네모와 실제 돌진 경로 일치** | 네모 위치와 실제 OverlapBox 판정 위치 동일해야 함 |
| **Chase 횟수 저장** | `chaseEntryCount`는 `RelentlessChaseGimmick` 내부 저장 |
| **동시 네모 최대 개수** | 최대 5개 (maxChargesPerCycle) |
| **Player 체력 시스템** | 보스 외 공통 시스템이므로 인터페이스로 추상화 |

---

## 10. Player 전략 가이드

```
1. 네모가 생성되는 방향과 개수를 본다
   → 몇 번 돌진할지, 어디로 돌진할지 예측 가능

2. 돌진 사이 텀을 노린다
   → 이 짧은 시간에 안전 지대로 이동

3. 돌진 횟수(N)를 의식한다
   → 3회차 Chase부터는 경로 3개를 동시에 피해야 함
   → 5회차면 5개. 거의 전 화면이 위험.

4. 체력 관리가 중요
   → 한 대라도 맞으면 위험. 두 대 이상은 치명적.

5. 의태는 소용없다
   → 곰치는 시각이 아니라 구역 자체를 감지함
   → 잉크 아껴서 달리기나 하자

6. 빨리 통과하는 게 최선
   → 오래 있으면 있을수록 돌진 횟수는 늘어남
```

---

## 11. 기존 대비 차이점

| 항목 | 기존 Phase2 설계 | 변경 (v1) |
|:----:|:---------------:|:---------:|
| 핵심 위협 | 의심도 천천히 떨어짐 | **화면 밖 돌진 + 체력 데미지** |
| Chase 행동 | Player 추적 | **화면 끝→끝 고속 돌진** |
| 피격 효과 | 없음 | **체력 데미지** |
| Player 행동 | 의태 / 도망 | **네모 보고 회피** |
| 의심도 역할 | Chase 지속 시간 결정 | **Chase 진입 트리거 + 30 고정** |
| 에스컬레이션 | 없음 | **Chase 횟수 = 돌진 횟수** |
| 돌진 방향 | 고정 (좌→우) | **8방향 랜덤** |
| 네모 간격 | 고정 (0.5s) | **회차별 단축 (0.5→0.3)** |
| 시각 피드백 | 없음 | **붉은 네모 경고 시스템** |

---

*이 문서는 곰치(Moray Eel) 보스의 화면 밖 강습 기반 설계입니다.*
*핵심 돌파 요소: 경고(네모) 읽기 → 순차적 회피 → 텀에 이동*

*기존 `RelentlessChaseGimmick.cs`를 전면 개편하여 구현합니다.*
