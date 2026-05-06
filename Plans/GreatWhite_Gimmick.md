# 백상아리 (Great White Shark) — 기믹 설명서

> **최종 보스 — Zone 4**
> **파일:** `DashChargeGimmick.cs` (전면 개편)

---

## 🎯 핵심 룰

```
1. 백상아리는 Player 주변을 서성인다 → 의심도 자동 상승 (단계별 가속)
2. 의태 중: 의심도 하락 BUT Player 근처 무작위 오브젝트에 돌진
3. Player가 붙은 오브젝트면 → 데미지
4. 의심도 100% = Chase 폭주 (죽을 때까지 해제 안 됨)
5. 파괴된 오브젝트는 재생성 ❌
```

---

## 🔄 상태 흐름

```
[Patrol] Player 주변 서성임
   │    의심도 단계별 가속 상승 (낮음→느림 / 높음→빠름)
   │
   ├─ [Player 의태 ON] ─────────────────┐
   │     ├─ 의심도 하락 (-10%/s)         │
   │     └─ Player 근처 오브젝트 돌진    │
   │           └─ Player 있음 → 데미지   │
   │              없음 → 그냥 파괴       │
   │                                     │
   └─ [의심도 100%] ────────────────────┐
         └─ [Chase 폭주]
              ├─ 무한 연속 돌진
              ├─ 경로상 모든 오브젝트 파괴
              ├─ 스턴/방어/벽막힘 없음
              └─ Player 사망 시에만 종료
```

---

## 📊 의심도 단계

| 의심도 | 상승률 | 의미 |
|:-----:|:-----:|:----|
| 0~30% | +3%/s | 안정 — 여유 있음 |
| 30~60% | +8%/s | 주의 — 관리 필요 |
| 60~90% | +15%/s | 위험 — 의태 필수 |
| 90~100% | +25%/s | 치명 — 즉시 의태 |
| 의태 중 | **-10%/s** | 하락 (일정) |
| Chase 중 | **100% 고정** | 내려가지 않음 |

---

## ⚔️ 패턴 상세

### Patrol — Player 주변 배회
- Player주변 자유롭게 선회 (XZ 평면 이동), 시야 불필요
- 의심도가 낮을수록 느리게, 높을수록 빠르게 상승

### 의태 — 오브젝트 타겟팅
1. Player가 오브젝트에 붙음 → 의심도 하락 시작
2. 0.5s 후 Player 근처 무작위 오브젝트 선정
3. 해당 오브젝트 머리 위에 **붉은 공격 표식** 표시 (0.5~1s)
4. 돌진 → 오브젝트 파괴
5. Player가 붙어있으면 데미지

### Chase — 폭주 모드
- 의심도 100% 도달 시 발동
- Player에게 무한 연속 돌진 (사망 시까지 해제 ❌)
- 경로상 모든 오브젝트 파괴, 스턴 없음, 벽 관통
- 돌진 직전 얇은 붉은 Lock-On 선 표시 (청새치 인디케이터 재활용)

---

## 🛠️ 기존 코드 재활용

| 출처 | 사용처 |
|------|--------|
| `RelentlessChaseGimmick` | 자동 의심도 상승 로직 |
| `DashChargeGimmick` | 오브젝트 파괴, 순찰, 돌진 기본 구조 |
| `SwordfishGimmick` | Aim→Charge 구조, 인디케이터, Wall 충돌 |
| `BossEnemyController` | 콜백 연결 패턴, 상태 전환 |
| `IGimmick*` 인터페이스들 | Player 정보 수신, 시선 고정, 전투 사이클 차단 |

---

## ⚙️ Inspector 설정값

### 의심도
| 파라미터 | 기본값 |
|:---------|:------:|
| `suspicionRateStage1` (0~30%) | 3 |
| `suspicionRateStage2` (30~60%) | 8 |
| `suspicionRateStage3` (60~90%) | 15 |
| `suspicionRateStage4` (90~100%) | 25 |
| `suspicionDecreaseRateCamouflage` | 10 |
| `postChaseSuspicion` | 0 |

### 돌진
| 파라미터 | 기본값 |
|:---------|:------:|
| `patrolSpeed` | 2 |
| `chargeSpeedPatrol` | 15 |
| `chargeSpeedChase` | 22 |
| `chargeWidth` | 2.5 |
| `maxDashDistance` | 25 |
| `chargeCooldownChase` | 1.5 |

### 의태/타겟팅
| 파라미터 | 기본값 |
|:---------|:------:|
| `camouflageLockDelay` | 0.5s |
| `indicatorDuration` | 0.8s |
| `targetSearchRadius` | 12m |

---

## 📝 수정 파일

| 파일 | 작업 |
|:----|:----|
| `DashChargeGimmick.cs` | 전면 재작성 (~500줄) |
| `BossEnemyController.cs` | 콜백 추가 (+20~30줄) |
| `DashChargeGimmick.asset` | 신규 파라미터 반영 |

---

*핵심 돌파 요소: 의심도 단계별 관리 → 의태 타이밍 → 오브젝트 위치 예측*
