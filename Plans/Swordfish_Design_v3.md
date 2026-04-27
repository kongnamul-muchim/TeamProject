# 청새치 (Swordfish) Boss 기믹 설계서 v3

> **버전:** v3.0
> **작성일:** 2026-04-27
> **상태:** 설계 완료 (구현 준비)
> **기준:** 사용자 요구사항 직접 반영

---

## 1. 개요

| 항목 | 내용 |
|------|------|
| **구분** | **Boss** (Zone 1 — 4보스 중 첫번째) |
| **파일** | `SwordfishGimmick.cs` (ScriptableObject + IEnemyGimmick) |
| **핵심** | 조준(Aim) → 돌진(Charge) → 스턴(Stun) 3단어 사이클 |
| **핵심 돌파 요소** | 의태(의심도 관리) + Wall 유도(Stun) + 시야 이탈 |

---

## 2. 동작 흐름

```
[Patrol] X축 순찰 (patrolSpeed)
   │
   ├─ Player 시야 감지 or 근접 거리 감지
   ▼
[Chase: Idle phase]
   │  Player 방향으로 시선 회전 (아직 돌진 안 함)
   │
   ├─ Player 시야에 잡힘
   │     ▼
   │  [Aim]  이동 정지, Player 방향 조준, 인디케이터 표시
   │     │   aimDuration 후
   │     ▼
   │  [Charge]  예측 위치로 고속 돌진
   │     │
   │     ├─ Wall Layer 충돌 or Bounds 이탈
   │     │     ▼
   │     │  [Stun] stunDuration 초 정지
   │     │     ▼
   │     │  → Idle phase (Player 재탐색)
   │     │
   │     └─ 타이머 만료 (노히트)
   │           ▼
   │        → Idle phase (Player 재탐색)
   │
   ├─ Player 시야에서 벗어남
   │     │
   │     ├─ 의심도 Safe → [Patrol] 복귀
   │     └─ 의심도 높음 → [Search] 수색
   │
   └─ (Player 의태 중) 애초에 감지 안 됨
```

---

## 3. 의심도 기반 난이도 스케일링

의심도가 높을수록 돌진이 빡세짐 (0% ~ 100% 선형 보간):

| 항목 | 의심도 0% | 의심도 100% | 식 |
|------|:--------:|:--------:|-----|
| `predictionFactor` | 0.3 | 0.9 | `lerp(0.3, 0.9, suspicion)` |
| `aimDuration` | 0.8s | 0.2s | `lerp(0.8, 0.2, suspicion)` |
| `chargeSpeed` | 10 | 15 | `lerp(10, 15, suspicion)` |

---

## 4. 충돌 및 경계 처리

| 조건 | 결과 |
|------|------|
| 예측 위치가 GroundBounds 밖 | → `_hasHitWall = true` → Stun |
| 돌진 경로에 Wall Layer 오브젝트 | → `_hasHitWall = true` → Stun |
| chargeDuration 타이머 만료 | → Stun 없이 Idle 복귀 |
| GroundBounds 이탈 방지 | GetPatrolTarget + ClampToBounds |

Wall Layer 이름: **"Wall"**

---

## 5. 시각 효과

- **Aim 중**: 붉은 실선 사각형 인디케이터 (LineRenderer)
- **그 외**: 인디케이터 숨김
- Trail / 파티클 / 애니메이션: 없음

---

## 6. IEnemyGimmick 인터페이스 확장

```csharp
void SetPlayerTransform(Transform playerTransform);
void SetSuspicionLevel(float normalizedSuspicion);
bool IsInCombatCycle { get; }
bool IsCharging { get; }
bool OverridesViewDirection { get; }
Vector3 GetViewDirectionVector();
bool ShowChargeIndicator { get; }
bool ShouldSkipSearchOnLostPlayer(float normalizedSuspicion);
```

---

## 7. 설정값 (전부 Inspector)

### SwordfishGimmick (ScriptableObject)

| 파라미터 | 기본값 | 설명 |
|----------|:------:|------|
| `chargeSpeed` | 12 | 돌진 속도 (의심도 보간) |
| `aimDuration` | 0.5 | 조준 시간 (의심도 보간) |
| `chargeDuration` | 1.0 | 돌진 지속 시간 |
| `stunDuration` | 0.5 | 스턴 지속 시간 |
| `predictionFactor` | 0.7 | 예측 계수 (의심도 보간) |
| `minPredictionFactor` | 0.3 | 의심도 0%일 때 예측 계수 |
| `maxPredictionFactor` | 0.9 | 의심도 100%일 때 예측 계수 |
| `minAimDuration` | 0.8 | 의심도 0%일 때 조준 시간 |
| `maxAimDuration` | 0.2 | 의심도 100%일 때 조준 시간 |
| `minChargeSpeed` | 10 | 의심도 0%일 때 돌진 속도 |
| `maxChargeSpeed` | 15 | 의심도 100%일 때 돌진 속도 |

### BossEnemyController

| 파라미터 | 기본값 | 설명 |
|----------|:------:|------|
| `chargeIndicatorLength` | 12 | 인디케이터 길이 |
| `chargeIndicatorWidth` | 1.5 | 인디케이터 폭 |
| `knockbackForce` | 12 | 넉백 힘 |

---

## 8. 수정 파일 목록

| 파일 | 변경 내용 |
|------|----------|
| `IEnemyGimmick.cs` | 새 멤버 8개 추가 |
| `AmbushGimmick.cs` | 새 멤버 최소 구현 |
| `RelentlessChaseGimmick.cs` | 새 멤버 최소 구현 |
| `DashChargeGimmick.cs` | 새 멤버 최소 구현 |
| `SwordfishGimmick.cs` | **전면 수정** |
| `BossEnemyController.cs` | 분기 정리 + 로직 개선 |

---

*이 문서는 사용자 요구사항을 직접 반영한 청새치 기믹 최종 설계입니다.*
*핵심 돌파 요소: 의태(의심도 관리) → Wall 유도(Stun) → 시야 이탈(회피)*
