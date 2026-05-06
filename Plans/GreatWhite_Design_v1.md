# 백상아리 (Great White Shark) Boss 기믹 설계서 v1

> **버전:** v1.0
> **작성일:** 2026-04-28
> **상태:** 설계 완료 (구현 전)
> **파일:** `DashChargeGimmick.cs` (전면 개편)
> **참조:** `SwordfishGimmick.cs`, `RelentlessChaseGimmick.cs`, `AmbushGimmick.cs`

---

## 1. 개요

| 항목 | 내용 |
|------|------|
| **Zone** | Zone 4 (심해 절벽) |
| **구분** | **최종 보스** |
| **파일** | `DashChargeGimmick.cs` (전면 개편) |
| **타입** | `GimmickType.DashCharge` |
| **핵심 컨셉** | **숨거나 말거나 — 결국 부숴버린다** |
| **핵심 돌파 요소** | 의태 타이밍 관리 + 오브젝트 위치 예측 + 의심도 게이지 컨트롤 |

> 청새치가 "직선 예측"을, 가자미가 "흔적 추적"을, 곰치가 "경고 회피"를 가르쳤다면,
> 백상아리는 **"의심도를 통제하고 환경을 예측하는 법"** 을 가르침.
> 절대 Chase에 들어가선 안 된다.

---

## 2. 핵심 룰

```
1. 백상아리는 Player 주변을 서성인다 → 의심도가 서서히 자동 상승
2. 의심도가 높을수록 상승 속도도 빨라짐 (눈덩이 효과)
3. 의태 중: 의심도가 상승하지 않고 하락한다
   → BUT 백상아리가 Player 근처 무작위 오브젝트에 돌진해서 부순다
   → Player가 붙은 오브젝트면 Player 데미지
4. 의심도 100% = Chase 폭주
   → Player에게 무한 연속 돌진 (죽을 때까지 Chase 해제 안 됨)
   → 경로상 모든 오브젝트 파괴 (방어/기절 불가)
   → Chase에 절대 들어가선 안 되는 구조
5. 파괴된 오브젝트는 재생성 안 됨 → 맵이 점점 황폐화
```

### 2.1 Player 전략 가이드

```
1. 의심도가 낮을 때 미리미리 의태로 관리하라
   → 높을수록 가속되므로 낮을 때 확실히 내려야 함

2. 의태는 항상 위험을 수반한다
   → 의심도는 내려가지만 내가 붙은 오브젝트가 털릴 수도 있음

3. 오브젝트 위치를 기억하라
   → 파괴된 오브젝트는 다시 못 씀

4. 의심도 80% 이상이면 위험 신호
   → 반드시 의태로 낮출 것
   → 100% = 즉사급
```

---

## 3. 동작 흐름

```
[Patrol] 백상아리, Player 주변 서성임 (시야 무시)
   │       의심도 자동 상승 (단계별 가속)
   │       (낮을수록 느림 → 높을수록 빠름)
   │
   ├─ [Player 의태 ON] ──────────────────────────────────┐
   │     │                                                 │
   │     ├─ 의심도 상승 정지 → 하락 시작                    │
   │     │                                                 │
   │     └─ Patrol 중이면 돌진 모드 진입                     │
   │           └─ [Player 근처 오브젝트 Lock-On] 0.5s       │
   │                 └─ Player 주변 무작위 오브젝트 선정     │
   │                       └─ [Charge] 오브젝트 방향 돌진    │
   │                             ├─ 충돌 시 오브젝트 파괴    │
   │                             └─ Player 붙어있음 → 데미지 │
   │                                   └─ [Stun 없음] 재정비 │
   │                                  → Patrol 복귀          │
   │                                                         │
   ├─ [Player NOT 의태] ──────────────────────────────────┐
   │     의심도 가속 상승                                    │
   │     100% 도달                                          │
   │     ↓                                                  │
   └─ [Chase: 폭주 모드] ────────────────────────────────┐
          Player에게 무한 연속 돌진
          ├─ 속도 18~25 (의심도 100% 고정)
          ├─ 경로상 모든 오브젝트 파괴
          ├─ 방어/기절/벽 막힘 없음 (그냥 뚫고 감)
          ├─ Player 놓쳐도 Chase 유지 (죽을 때까지)
          └─ [Player 사망] → 의심도 리셋 → Patrol 복귀
```

---

## 4. 상태별 상세

### 4.0 Player 회피 수단: 먹물 분사

Player가 의태 중인 오브젝트가 샤크의 타겟이 되었을 때,
**먹물 분사(SPACE, 별도 시스템)** 를 사용하여 즉시 의태 해제 + 회피할 수 있다.

- 먹물 분사 상세: `Plans/Hide_and_Ink_Ability_System.md` 참조
- 기존 `PlayerInk.cs` 시스템 재활용
- 본 문서에서는 "먹물로 도주 가능"만 명시하며, 구현 상세는 별도 진행

---

### 4.1 Patrol — 의심도 단계별 가속 상승

| 항목 | 내용 |
|:----:|------|
| **행동** | Player 주변을 자유롭게 배회 (3D 공간, XZ 평면 이동) — Player를 놀리듯 선회 |
| **시야** | **불필요** — 시야 기반 감지 안 함 |
| **의심도 상승** | 현재 의심도 단계에 따라 상승률 가속 (아래 표) |
| **의태 중 의심도** | 상승 정지 → 10%/s 속도로 하락 |
| **시작 의심도** | Zone 진입 시 0% |

#### 의심도 단계별 상승률

| 의심도 범위 | 단계 | 상승률 | 설명 |
|:----------:|:----:|:-----:|:------|
| 0~30% | 안정 | +3%/s | 초기, 느리게 상승 |
| 30~60% | 주의 | +8%/s | 중간, 눈여겨봐야 할 시점 |
| 60~90% | 위험 | +15%/s | 빠름, 반드시 의태 필요 |
| 90~100% | **치명** | +25%/s | 매우 빠름, 즉시 의태하지 않으면 Chase |

> 의심도가 높을수록 백상아리의 흥분도도 함께 올라 공격성이 증가함을 표현.
> Player는 **낮은 단계에서 미리 의태로 의심도를 관리**해야 함.

### 4.2 의태 — 돌진 트리거

| 항목 | 내용 |
|:----:|------|
| **의태 시작** | Player가 오브젝트에 달라붙음 |
| **의심도 변화** | 상승 정지 → 10%/s 속도로 하락 |
| **샤크 반응** | Patrol 중이면 즉시 돌진 모드 진입 |

#### 의태 타겟팅 로직

```
[Player 의태 ON]
    │
    ├─ 0.5s 후 Player 주변 무작위 오브젝트 선정
    │     ├─ Player 위치 기준 반경 내 오브젝트 중 랜덤
    │     ├─ 아직 파괴되지 않은 오브젝트만
    │     └─ Player가 붙은 오브젝트도 포함될 수 있음 (운빨)
    │
    ├─ 선정된 오브젝트 머리 위에 붉은 공격 표식 표시 (0.5~1s)
    │     → Player가 떨어져서 도망칠 시간을 줌
    │
    └─ Charge 실행 → 오브젝트 파괴
```

**의태 관련 중요 룰:**

| 조건 | 결과 |
|:----:|:----:|
| 오브젝트 없음 (모두 파괴됨) | 의태 불가 → Chase 직행 |
| 의태 중 오브젝트 파괴됨 | Player 데미지 + 강제 이탈 |
| 타겟이 Player 오브젝트가 아님 | 안전, BUT 의심도 하락 중단 후 재상승 |

### 4.3 Chase — 폭주 모드 (데스라인)

| 항목 | 내용 |
|:----:|------|
| **진입 조건** | 의심도 100% 도달 |
| **행동** | Player에게 무한 연속 돌진 |
| **종료 조건** | **Player 사망만** (Chase 해제 안 됨) |
| **속도** | 18~25 (고정, 의심도 100% 기준) |
| **돌진 사이 간격** | 약 1~2초 (잠시 멈췄다가 재돌진) |
| **오브젝트 파괴** | 경로상 전부 파괴 (방어 수단 없음) |
| **Stun** | **없음** — 벽도 뚫고 감 |
| **Player 사망 후** | 의심도 0% 리셋 → Patrol 복귀 |

#### Chase 시 인디케이터

```
[Chase 중]
    │
    ├─ 돌진 직전: 얇은 붉은색 직선 (Lock-On 선)
    │     → 0.3~0.5s 유지 후 돌진
    │     → 청새치 인디케이터 재활용 (더 얇고 긴 스타일로 차별화)
    │
    └─ 돌진 중: 인디케이터 소멸
```

### 4.4 오브젝트 파괴 시스템

| 항목 | 내용 |
|:----:|------|
| **파괴 대상** | Layermask "CamouflageTarget" (기존 DashCharge 재활용) |
| **파괴 효과** | `SetActive(false)` (기존 로직 재활용) |
| **재생성** | ❌ 파괴된 오브젝트는 재생성 안 됨 |
| **의미** | 게임 진행에 따라 숨을 곳이 점점 사라짐 → 심리적 압박 |

---

## 5. 의심도 시스템

### 5.1 의심도 증감

| 상황 | 변화율 | 설명 |
|:----:|:-----:|:------|
| 일반 (0~30%) | +3%/s | 초기 느린 상승 — 낮을 때 관리해야 하는 이유 |
| 일반 (30~60%) | +8%/s | 중간 속도 |
| 일반 (60~90%) | +15%/s | 빠름 — 의태 필요 |
| 일반 (90~100%) | **+25%/s** | 매우 빠름 — 즉시 조치 필요 |
| 의태 중 | **-10%/s** | 일정하게 하락 |
| Chase 중 | **100% 고정** | 내려가지 않음 (죽을 때까지) |

### 5.2 의심도 임계값

| 값 | 상태 | 설명 |
|:--:|:----:|:------|
| 0~30% | 안정 | 느린 상승, 의태할 여유 있음 |
| 30~60% | 주의 | 상승 가속 시작, 의태 고려 |
| 60~90% | **위험** | 빠른 상승, 반드시 의태 필요 |
| 90~100% | **치명** | 즉시 의태하지 않으면 Chase 직행 |
| 100% | **Chase 진입** | 폭주 모드, 돌이킬 수 없음 |

---

## 6. 기존 코드 재활용 상세

| 소스 파일 | 재활용 내용 |
|-----------|-----------|
| **`RelentlessChaseGimmick.cs`** | 자동 의심도 상승 로직 (곰치의 Patrol 의심도 관리 재활용) |
| **`DashChargeGimmick.cs`** | 오브젝트(CamouflageTarget) 파괴, X축 순찰 (Z 고정), 돌진 이동 |
| **`SwordfishGimmick.cs`** | Aim→Charge 구조, 인디케이터(LineRenderer), 의심도 스케일링 함수, Wall 충돌 감지 |
| **`BossEnemyController.cs`** | `ConnectGimmickCallbacks()` 패턴, 상태 전환, Gimmick 인터페이스 연결 |
| **`IGimmickPlayerAware`** | Player 위치/의태/시야 정보 수신 |
| **`IGimmickViewDirection`** | 돌진 방향 시선 고정, 인디케이터 표시 |
| **`IGimmickCombatCycle`** | Chase 중 Search 전환 차단 |

### 6.1 DashChargeGimmick.cs 변경 계획

| 영역 | 변경 | 설명 |
|:----:|:----:|:------|
| 의심도 | **전면 개편** | 자동 상승 (곰치 스타일) + 의태 시 하락 |
| Patrol | **수정** | Player 근처 서성임 (거리 기반) |
| Chase | **전면 개편** | 무한 연속 돌진, Stun 없음, 벽 관통 |
| 의태 연동 | **신규** | 의태 → 랜덤 오브젝트 타겟팅 + 돌진 |
| 인디케이터 | **수정** | 청새치 인디케이터 재활용 + 차별화 |
| 오브젝트 파괴 | **유지** | 기존 CamouflageTarget 파괴 로직 |

---

## 7. 설정값 (전부 Inspector)

### DashChargeGimmick (ScriptableObject)

#### 의심도 파라미터

| 파라미터 | 기본값 | 설명 | 재활용 |
|:---------|:------:|:------|:------:|
| `suspicionRateStage1` | 3 | 0~30% 단계 상승률 (%/s) | **신규** |
| `suspicionRateStage2` | 8 | 30~60% 단계 상승률 (%/s) | **신규** |
| `suspicionRateStage3` | 15 | 60~90% 단계 상승률 (%/s) | RelentlessChase |
| `suspicionRateStage4` | 25 | 90~100% 단계 상승률 (%/s) | **신규** |
| `suspicionDecreaseRateCamouflage` | 10 | 의태 중 초당 의심도 하락량 | **신규** |
| `postChaseSuspicion` | 0 | Chase 종료 후 의심도 | **신규** |

#### 돌진 파라미터

| 파라미터 | 기본값 | 설명 | 재활용 |
|:---------|:------:|:------|:------:|
| `patrolSpeed` | 2 | Patrol 이동 속도 | DashCharge |
| `chargeSpeedPatrol` | 15 | Patrol 중 돌진 속도 | DashCharge |
| `chargeSpeedChase` | 22 | Chase 중 돌진 속도 | **신규** |
| `chargeWidth` | 2.5 | 돌진 판정 너비 | DashCharge |
| `maxDashDistance` | 25 | 최대 돌진 거리 | DashCharge |
| `wallLayerName` | "Wall" | 벽 레이어 (관통용) | Swordfish |
| `chargeCooldownChase` | 1.5 | Chase 중 돌진 사이 간격 | **신규** |

#### 의태/타겟팅 파라미터

| 파라미터 | 기본값 | 설명 |
|:---------|:------:|:------|
| `camouflageLockDelay` | 0.5 | 의태 → 오브젝트 선정까지 딜레이 |
| `indicatorDuration` | 0.8 | 오브젝트 머리 위 공격 표식 지속 시간 |
| `targetSearchRadius` | 12 | Player 기준 오브젝트 탐색 반경 (m) |

---

## 8. 인터페이스 구현 계획

```csharp
// DashChargeGimmick.cs (전면 개편)

public sealed class DashChargeGimmick : ScriptableObject, IEnemyGimmick,
    IGimmickPlayerAware, IGimmickViewDirection, IGimmickCombatCycle
{
    // --- 의심도 ---
    private float _normalizedSuspicion;
    private bool _isCamouflaging;      // Player 의태 여부
    private Transform _playerTransform;

    // --- 의태 타겟팅 ---
    private Transform _targetObject;   // 돌진할 오브젝트

    // --- 돌진 ---
    private enum Phase { Idle, Locking, Charging, Cooldown }
    private Phase _currentPhase;
    private bool _isChaseMode;         // Chase 폭주 모드 플래그

    // --- 콜백 ---
    public System.Action<float> OnSpeedOverride;
    public System.Action<Vector3> OnDashStarted;
    public System.Action<GameObject> OnObstacleHit;
    public System.Action<Vector3> OnMoveTo;
    public System.Action OnMovementStop;
}
```

---

## 9. 수정 파일 목록

| 파일 | 변경 | 내용 |
|:----:|:----:|:------|
| `DashChargeGimmick.cs` | **전면 재작성** | Patrol 자동 의심도 + 의태 연동 타겟팅 + Chase 폭주 모드 |
| `BossEnemyController.cs` | 수정 | 의태 연동 콜백 추가, 의심도 시스템 연동 |
| `DashChargeGimmick.asset` | 수정 | 신규 파라미터 인스펙터 반영 |
| `IEnemyGimmick.cs` | (변경 없음) | 현 인터페이스로 충분 |

### 변경량 (예상)

```
DashChargeGimmick.cs    : ~500줄 (전면 재작성)
BossEnemyController.cs  : +20~30줄 (콜백 + 의태 연동)
```

---

## 10. 기존 대비 차이점

| 항목 | 기존 DashCharge | 변경 (v1) |
|:----:|:--------------:|:---------:|
| 의심도 | 일반 의심도 시스템 | **자동 상승 + 의태 시 하락** |
| Patrol | X축 순찰 | **Player 근처 서성임** |
| Chase | 1회 돌진 → Patrol 복귀 | **무한 연속 돌진 (사망까지)** |
| Stun | 2초 정지 | **없음** |
| 벽 충돌 | 멈춤 | **관통 (파괴는 안 함)** |
| 의태 연동 | 없음 | **의태 = 오브젝트 타겟팅 트리거** |
| 오브젝트 파괴 | Chase 중만 | **Patrol 중 의태 시에도 파괴** |
| 인디케이터 | 없음 | **붉은 Lock-On 선 + 오브젝트 마커** |
| Player 전략 | 그냥 피하기 | **의심도 관리 + 오브젝트 위치 예측** |

---

## 11. 구현 시 주의사항

| 항목 | 설명 |
|:----:|:------|
| **Chase 해제 방지** | Chase 진입 시 `IGimmickCombatCycle.IsInCombatCycle` = true 유지 → 상태 전환 차단 |
| **의태 타겟 필터** | 이미 파괴된 오브젝트는 타겟 풀에서 제외, Player 근처 오브젝트만 대상 |
| **Player 사망 후 리셋** | Chase 종료 시 의심도 0%, Patrol 정상 복귀 |
| **오브젝트 파괴 동기화** | CamouflageTarget 레이어 통일 (기존 유지) |
| **인디케이터 시각화** | Patrol 돌진: 오브젝트 머리 위 붉은 공격 표식 / Chase 돌진: 직선 Lock-On 선 |

---

*이 문서는 백상아리(Great White Shark) 최종 보스의 기믹 설계입니다.*
*핵심 돌파 요소: 의심도 타이밍 관리 → 오브젝트 위치 예측 → 의태로 Chase 방지*
*기존 `DashChargeGimmick.cs`를 전면 개편하여 구현합니다.*
