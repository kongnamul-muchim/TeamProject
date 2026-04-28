# 보스 기믹 설계 문서 v2

> **버전:** v2.0
> **작성일:** 2026-04-24
> **문서 상태:** 설계중
> **변경:** 존(Zone) 기반 4보스 체계로 개편. 전기뱀장어 제거, 청새치 Elite→Boss 승격

---

## 1. 존(Zone)별 보스 구성

```
Zone 1 (얕은 바다/산호초)  →  청새치 (Swordfish)  — 빠른 직선형 돌진
Zone 2 (해초 밀림/모래톱)   →  가자미 (Flounder)    — 모래 매복 후 기습
Zone 3 (침몰선 유적 지대)    →  곰치 (Moray Eel)     — 위장 감지 + 집요한 추격
Zone 4 (심해 절벽)           →  백상아리 (Great White) — 초고속 돌진 + 엄폐물 파괴
```

### 1.1 변경 이력

| 구분 | 이전 | 변경 | 사유 |
|------|------|------|------|
| Elite | 청새치 | → **Boss 승격** (Zone 1) | Elite보다 Boss 컨셉에 적합 |
| Boss | 전기뱀장어 | → **제거** | Zone 4 백상아리로 대체 |
| Boss | 상어(DashCharge) | → **백상아리** (Zone 4) | 최종 보스 컨셉 강화 |

---

## 2. 아키텍처

### 2.1 기믹 시스템 구조 (기존 유지)

```
IEnemyGimmick (ScriptableObject 인터페이스)
 ├── OnActivate / OnDeactivate
 ├── OnPatrolEnter / OnPatrolUpdate / OnPatrolExit
 ├── OnChaseEnter / OnChaseUpdate / OnChaseExit
 └── OnSearchEnter / OnSearchUpdate / OnSearchExit

BossEnemyController
 ├── gimmickAsset (ScriptableObject) — 우선
 ├── customGimmick (MonoBehaviour) — fallback
 └── ConnectGimmickCallbacks() — 기믹별 콜백 바인딩
```

### 2.2 파일 구조

```
Assets/Core/Enemy/Boss/Gimmicks/
 ├── IEnemyGimmick.cs                  # 공통 인터페이스 (기존)
 ├── AmbushGimmick.cs                  # 가자미 — 매복 (기존)
 ├── RelentlessChaseGimmick.cs         # 곰치 — 집요한 추격 (기존)
 ├── DashChargeGimmick.cs              # 백상아리 — 초고속 돌진 (기존)
 └── SwordfishGimmick.cs               # [신규] 청새치 — 직선 돌진

Assets/ScriptableObjects/Gimmicks/
 ├── Gimmick_Ambush_Flounder.asset      # [신규]
 ├── Gimmick_Relentless_Moray.asset     # 기존
 ├── Gimmick_DashCharge_Shark.asset     # [신규]
 └── Gimmick_Swordfish.asset           # [신규]
```

---

## 3. Zone 1 — 청새치 (Swordfish)

| 항목 | 내용 |
|------|------|
| **구분** | **Boss** (기존 Elite→승격) |
| **파일** | `SwordfishGimmick.cs` (신규) |
| **타입** | `GimmickType.Swordfish` |
| **핵심** | 빠른 속도의 직선형 돌진, Aim→Charge 2단계 |

### 동작 흐름

```
[Patrol] 중간 속도로 순찰
    ↓ Player 시야 감지
[Chase] 조준(Aim) 0.5초 → 돌진(Charge) 1초
    ↓ 돌진 중 경로상 산호/바위 충돌
[Stun] 0.5초 스턴 → 일반 Chase로 전환
    ↓ Player 놓침
[Search] 수색 → 미발견 시 Patrol 복귀
```

### 설정값 (기본값)

| 파라미터 | 값 | 설명 |
|----------|-----|------|
| `chargeSpeed` | 12 | 돌진 속도 |
| `aimDuration` | 0.5 | 조준 경고 시간 (Player 회피 기회) |
| `chargeDuration` | 1.0 | 돌진 지속 시간 |
| `stunDuration` | 0.5 | 충돌 스턴 시간 |

### 오브젝트 요소 (추후 구현)

| 요소 | 역할 |
|------|------|
| 산호 파편 | 충돌 유도용 장애물 |
| 단단한 바위 | 돌진 막는 벽 |

### 필요 프리팹

| 프리팹 | 용도 | 필수 |
|--------|------|------|
| 없음 | 코드만으로 동작 | ❌ |

---

## 4. Zone 2 — 가자미 (Flounder)

| 항목 | 내용 |
|------|------|
| **파일** | `AmbushGimmick.cs` (기존) |
| **타입** | `GimmickType.Ambush` |
| **핵심** | 모래 속 은신 후 기습 근접 공격 |

### 동작 흐름

```
[Patrol] Player 주변 매복 위치로 이동 → 대기 (시야 숨김)
    ↓ Player 근접
[Chase] 모래에서 튀어나와 1회 돌진
    ↓ 돌진 종료 or 실패
[Search] 매복 지점 복귀 → 재매복
```

### 보스 인스펙터 설정 (권장)

| 필드 | 값 |
|------|-----|
| Patrol Speed | 1.5 |
| Chase Speed | 5 |
| Search Speed | 2 |
| View Radius | 4 |
| View Angle | 45° |

### 오브젝트 요소 (추후 구현)

| 요소 | 역할 |
|------|------|
| 해초 | 매복 지점 가림 |
| 모래사장 | 매복 흔적 파악 기믹 |

### 필요 프리팹

| 프리팹 | 용도 | 필수 |
|--------|------|------|
| 없음 | 코드만으로 동작 | ❌ |

---

## 5. Zone 3 — 곰치 (Moray Eel)

| 항목 | 내용 |
|------|------|
| **파일** | `RelentlessChaseGimmick.cs` (기존) |
| **타입** | `GimmickType.RelentlessChase` |
| **핵심** | 의태 감지 시 좁은 구역 집중 순찰 + Chase 지속 |

### 동작 흐름

```
[Patrol] 의태 위치 기억 → 좁은 구역 집중 순찰 (반경 8m)
    ↓ Player 발견
[Chase] 의심도 하락률 30%로 감소 (오랜 추적 유지)
    ↓ Player 놓침
[Search] 수색 반경 1.25배 확대 → 의태 위치 중심 수색
```

### 보스 인스펙터 설정 (권장)

| 필드 | 값 |
|------|-----|
| Patrol Speed | 1.5 |
| Chase Speed | 6 |
| Search Speed | 3 |
| View Radius | 6 |
| View Angle | 90° |

### 오브젝트 요소 (추후 구현)

| 요소 | 역할 |
|------|------|
| 파이프 | 틈새 도주용 |
| 녹슨 닻 | 장애물 |
| 거목 | 은신처 |

### 필요 프리팹

| 프리팹 | 용도 | 필수 |
|--------|------|------|
| 없음 | 코드만으로 동작 | ❌ |

---

## 6. Zone 4 — 백상아리 (Great White Shark)

| 항목 | 내용 |
|------|------|
| **파일** | `DashChargeGimmick.cs` (기존) |
| **타입** | `GimmickType.DashCharge` |
| **핵심** | 초고속 직선 돌진 + 경로상 엄폐물 파괴 |

### 동작 흐름

```
[Patrol] 절벽 구간 빠르게 순찰
    ↓ Player 발견
[Chase] Player 위치 기록 → 직선 돌진 (속도 15)
    ↓ 돌진 중 엄폐물 충돌 시 파괴
[Stun] 2초 정지 → Patrol 복귀
```

### 보스 인스펙터 설정 (권장)

| 필드 | 값 |
|------|-----|
| Patrol Speed | 3 |
| Chase Speed | 5 |
| Search Speed | 2 |
| View Radius | 7 |
| View Angle | 120° |

### 오브젝트 요소 (추후 구현)

| 요소 | 역할 |
|------|------|
| 금속판 | 파괴 가능 엄폐물 |
| 거대 산호 | 파괴 가능 장애물 |

### 필요 프리팹

| 프리팹 | 용도 | 필수 |
|--------|------|------|
| 없음 | 코드만으로 동작 | ❌ |

---

## 7. 씬 설정 참고

### 모든 보스 공통

```
Boss 프리팹
 ├── BossEnemyController
 │   ├── Gimmick Asset → [해당 기믹 .asset 드래그]
 │   └── 상태별 속도 설정
 ├── ConeVisionSensor
 │   ├── Target Layer: Player
 │   └── Obstacle Layer: Obstacle
 ├── BossSuspicionSystem
 └── SpriteRenderer (보스 이미지)
```

### 기믹 에셋 생성

Unity 메뉴: `우클릭 → Create → Enemy Gimmicks → [기믹 선택]`

| 보스 | 메뉴 | 파일명 |
|------|------|--------|
| 가자미 | Ambush Gimmick | `Gimmick_Ambush_Flounder.asset` |
| 곰치 | Relentless Chase Gimmick | `Gimmick_Relentless_Moray.asset` (이미 있음) |
| 백상아리 | Dash Charge Gimmick | `Gimmick_DashCharge_Shark.asset` |
| 청새치 | Swordfish Gimmick | `Gimmick_Swordfish.asset` |

---

## 8. TODO

- [ ] `ElectricZoneGimmick` 연결 제거 (전기뱀장어 → 백상아리)
- [ ] `GimmickType` enum에 `Swordfish` 추가
- [ ] `SwordfishGimmick.cs` 생성 (Elite→Boss 전환)
- [ ] `BossEnemyController.ConnectGimmickCallbacks()`에 청새치 케이스 추가
- [ ] 기존 `SwordfishBehavior.cs` (Elite) 정리
- [ ] 가자미/백상아리/청새치 `.asset` 생성
- [ ] Zone별 보스별 Scene 배치

---

*이 문서는 Zone 기반 4보스 체계의 설계 문서입니다.*
*오브젝트 요소(산호, 바위 등)는 Phase 2에서 구현 예정입니다.*
