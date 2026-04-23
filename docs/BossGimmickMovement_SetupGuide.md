# 보스 기믹 이동 제어권 - 설정 및 테스트 가이드

> **버전:** v1.0
> **작성일:** 2026-04-22
> **문서 상태:** 작성완료

---

## 1. 개요

보스 몬스터가 **기믹 기반으로 이동**하도록 변경되었습니다.

### 변경 전 vs 변경 후

| 구분 | 변경 전 | 변경 후 |
|------|---------|---------|
| **Patrol 이동** | X축 전용 (좌/우만) | 기믹 기반 X-Z 평면 (Z축 제한) |
| **Search 이동** | X축 전용 (좌/우만) | 기믹 기반 X-Z 평면 (Z축 제한) |
| **Chase 이동** | Player 추적 (X-Z) | Player 추적 (X-Z) - 변경 없음 |

### Z축 제한 규칙

| 상태 | X축 | Z축 |
|------|-----|-----|
| **Patrol** | 자유롭게 이동 | 미세 이동만 (기믹별 ±0~2m) |
| **Search** | 수색 반경 내 이동 | 미세 이동만 (기믹별 ±0~2m) |
| **Chase** | Player 추적 (제한 없음) | Player 추적 (제한 없음) |

---

## 2. Unity 인스펙터 설정

### 2.1 보스 프리팹 설정

```
BossEnemy (Prefab)
├── BossEnemyController (컴포넌트)
│   ├── 기본 설정
│   │   ├── Move Speed: 3
│   │   ├── Acceleration: 8
│   │   ├── Friction: 0.9
│   │   └── Max Speed: 5
│   ├── 시야 설정
│   │   ├── Enemy Forward: (ConeVisionSensor 참조)
│   │   └── View Rotation Speed: 5
│   ├── Ground 제한 설정
│   │   ├── Ground Layer: Ground
│   │   ├── Ground Check Distance: 0.5
│   │   ├── Ground Check Radius: 0.3
│   │   ├── Ground Scan Distance: 50
│   │   └── Ground Scan Step: 1
│   ├── 보스 설정
│   │   ├── Vision Sensor: [ConeVisionSensor 드래그]
│   │   └── Suspicion Meter: (전역 SuspicionManager 사용)
│   ├── 상태별 속도
│   │   ├── Patrol Speed: 2
│   │   ├── Chase Speed: 5
│   │   └── Search Speed: 3
│   ├── 탐색 설정
│   │   ├── Search Distance: 3
│   │   └── Search Duration: 5
│   └── 기믹 설정 ⭐ 중요
│       ├── Gimmick Asset: [ScriptableObject 기믹 에셋 드래그]
│       └── Custom Gimmick: (선택사항, fallback용)
```

### 2.2 ScriptableObject 기믹 에셋 생성 방법

1. Unity 에디터 Project 창에서 **우클릭**
2. **Create** → **Enemy Gimmicks** → 원하는 기믹 선택
   - `Ambush Gimmick` (Ch.1 가자미)
   - `Relentless Chase Gimmick` (Ch.2 곰치)
   - `Electric Zone Gimmick` (Ch.3 전기뱀장어)
   - `Lure Bait Gimmick` (Ch.4 아귀)
   - `Dash Charge Gimmick` (Ch.5 백상아리)
3. 생성된 `.asset` 파일을 `Assets/ScriptableObjects/Gimmicks/`에 저장
4. 인스펙터에서 설정값 조절 (아래 표 참조)
5. `BossEnemyController`의 **Gimmick Asset** 필드에 드래그 앤 드롭

---

## 3. 챕터별 보스 설정값

### 3.1 Ch.1 가자미 (AmbushGimmick)

| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| `Ambush Duration` | 3 | 매복 대기 시간 (초) |
| `Ambush Move Radius Min` | 5 | Player 기준 최소 이동 거리 |
| `Ambush Move Radius Max` | 12 | Player 기준 최대 이동 거리 |
| `Lock Z Axis` | ✅ true | Z축 고정 여부 (매복 시 Z 고정) |
| `Far Suspicion Radius` | 10 | 원거리 의심도 범위 |
| `Near Suspicion Radius` | 3 | 근접 의심도 범위 |
| `Far Suspicion Rate` | 10 | 원거리 의심도 상승률 |
| `Near Suspicion Rate` | 40 | 근접 의심도 상승률 |
| `Suspicion Drop Threshold` | 20 | 추적 취소 의심도 기준 |
| `Dash Speed` | 8 | 기습 돌진 속도 |
| `Dash Duration` | 1.5 | 돌진 지속 시간 |

**이동 패턴**: 매복 위치로 이동 → 대기 → Player 발견 시 돌진 → 추적

### 3.2 Ch.2 곰치 (RelentlessChaseGimmick)

| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| `Suspicion Decay Multiplier` | 0.3 | 의심도 하락 배율 (30%) |
| `Search Radius Multiplier` | 1.25 | 수색 반경 배율 |
| `Patrol Area Radius` | 8 | 집중 순찰 반경 (m) |

**이동 패턴**: 집중 순찰 영역 내 X-Z 이동 (Z ±2m 제한)

### 3.3 Ch.3 전기뱀장어 (ElectricZoneGimmick)

| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| `Zone Duration` | 5 | 감전 구역 지속 시간 (초) |
| `Install Cooldown` | 8 | 설치 쿨타임 (초) |
| `Install Probability` | 0.3 | 설치 확률 (30%) |
| `Check Interval` | 1 | 설치 시도 체크 간격 (초) |
| `Electric Zone Prefab` | (할당 필요) | 감전 구역 프리팹 |

**이동 패턴**: X-Z 평면 순찰 (Z ±1m 제한) + 구역 설치

### 3.4 Ch.4 아귀 (LureBaitGimmick)

| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| `Bait Count` | 5 | 동시 미끼 개수 |
| `Bait Lifetime` | 10 | 미끼 유지 시간 (초) |
| `Chase Transition Delay` | 2 | Chase 전환 지연 시간 (초) |
| `Bait Trigger Radius` | 2 | 미끼 감지 반경 (m) |
| `Bait Prefab` | (할당 필요) | 발광 미끼 프리팹 |

**이동 패턴**: 미끼 배치 위치 순회 (Z ±2m 제한)

### 3.5 Ch.5 백상아리 (DashChargeGimmick)

| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| `Dash Speed` | 15 | 돌진 속도 |
| `Dash Cooldown` | 2 | 돌진 후 정지 시간 (초) |
| `Charge Width` | 2 | 돌진 판정 너비 (m) |
| `Max Dash Distance` | 20 | 최대 돌진 거리 (m) |

**이동 패턴**: 절벽 구간 X축 순찰 (Z 고정) → Player 발견 시 직선 돌진

---

## 4. 테스트 진행 방법

### 4.1 사전 준비

1. **Player 오브젝트**에 `Tag: Player` 설정 확인
2. **Ground 오브젝트**에 `Layer: Ground` 설정 확인
3. **ConeVisionSensor** 컴포넌트가 보스에 부착되어 있는지 확인
4. **SuspicionManager**가 Scene에 존재하는지 확인

### 4.2 테스트 단계

#### 단계 1: 보스 배치 및 기믹 할당

1. Scene에 `BossEnemy` 프리팹 배치
2. `BossEnemyController` 컴포넌트에서 **Gimmick Asset** 필드에 기믹 에셋 드래그
3. Play 모드 진입

#### 단계 2: Patrol 이동 테스트

1. 보스가 **Patrol 상태**에서 이동하는지 확인
2. **Z축 이동 제한** 확인:
   - 가자미/백상아리: Z축 고정 (변화 없음)
   - 곰치/아귀: Z축 ±2m 이내 이동
   - 전기뱀장어: Z축 ±1m 이내 이동
3. **Ground 경계**를 벗어나지 않는지 확인

#### 단계 3: Chase 이동 테스트

1. Player가 보스 시야에 들어오도록 이동
2. 보스가 **Chase 상태**로 전환되는지 확인
3. 보스가 **Player를 추적**하는지 확인 (X-Z 평면, 제한 없음)
4. Player가 시야에서 벗어나면 **Search 상태**로 전환되는지 확인

#### 단계 4: Search 이동 테스트

1. 보스가 **Search 상태**에서 이동하는지 확인
2. Z축 이동 제한이 Patrol과 동일하게 적용되는지 확인
3. 탐색 시간 초과 후 **Patrol 상태로 복귀**하는지 확인

#### 단계 5: 기믹별 특수 동작 테스트

| 챕터 | 테스트 항목 |
|------|------------|
| **Ch.1 가자미** | 매복 위치 이동 → 대기 → Player 발견 시 돌진 |
| **Ch.2 곰치** | 집중 순찰 영역 내 이동 → Chase 시 의심도 하락 감소 |
| **Ch.3 전기뱀장어** | 이동 중 감전 구역 설치 → Player 접촉 시 감전 |
| **Ch.4 아귀** | 미끼 배치 → 미끼 순회 이동 → Player 접근 시 Chase |
| **Ch.5 백상아리** | 절벽 순찰 → Player 발견 시 직선 돌진 → 엄폐물 파괴 |

### 4.3 디버깅 팁

#### Console 로그 확인

Play 모드에서 다음 로그가 출력되는지 확인:

```
[BossEnemyController] ScriptableObject gimmick loaded: Ambush
[BossEnemyController] Ground bounds scanned: X(-25.0 ~ 25.0), Z(-2.0 ~ 2.0)
```

#### Gizmos 확인

Scene 뷰에서 다음을 확인:

- **Ground 경계**: 보스가 Ground 범위 내에서만 이동
- **시야 범위**: ConeVisionSensor의 시야 원뿔 표시
- **미끼/구역**: 아귀 미끼, 전기뱀장어 구역 시각적 표시

#### 이동 경로 확인

1. Scene 뷰에서 보스 선택
2. `Gizmos` 활성화
3. 보스 이동 경로가 **Z축 제한**을 따르는지 확인

---

## 5. 문제 해결

### 5.1 보스가 움직이지 않음

| 원인 | 해결 방법 |
|------|----------|
| `Gimmick Asset` 미할당 | 인스펙터에서 기믹 에셋 드래그 |
| `Ground Layer` 미설정 | Ground 오브젝트에 Layer 할당 |
| `Vision Sensor` 미연결 | ConeVisionSensor 컴포넌트 참조 연결 |
| Player Tag 오류 | Player 오브젝트에 `Tag: Player` 설정 |

### 5.2 보스가 X축으로만 이동

| 원인 | 해결 방법 |
|------|----------|
| 기믹의 `HasMovementOverride`가 false | 기믹 코드 확인 (모든 기믹이 `true` 반환) |
| `PatrolBehavior`에 기믹 미주입 | `BossEnemyController.InitializeBehaviors()` 확인 |
| `InitializeGimmick()` 순서 오류 | `Start()`에서 기믹 → Behavior 순서 확인 |

### 5.3 Z축 이동이 너무 큼

| 원인 | 해결 방법 |
|------|----------|
| 기믹별 Z축 제한값 확인 | 각 기믹의 `GetPatrolTarget()`에서 Z 제한 확인 |
| GroundBounds Z 범위 확인 | `ScanGroundBounds()` 결과 확인 |

### 5.4 Chase 상태에서 Player 추적 안 함

| 원인 | 해결 방법 |
|------|----------|
| `ChaseBehavior`에 PlayerTransform 미연결 | `BossEnemyController.InitializeBehaviors()` 확인 |
| Player가 시야 밖 | ConeVisionSensor 설정 확인 (View Radius, View Angle) |
| SuspicionManager 미설정 | Scene에 SuspicionManager 존재 확인 |

---

## 6. 변경된 파일 목록

| 파일 | 변경 내용 |
|------|----------|
| `IEnemyGimmick.cs` | `HasMovementOverride`, `GetPatrolTarget()`, `GetSearchTarget()` 추가 |
| `PatrolBehavior.cs` | 기믹 주입 + 이동 제어권 처리 |
| `SearchBehavior.cs` | 기믹 주입 + 이동 제어권 처리 |
| `BossEnemyController.cs` | Behavior 생성 시 기믹 전달 + 초기화 순서 수정 |
| `AmbushGimmick.cs` | 매복 이동 로직 구현 (Z 고정) |
| `RelentlessChaseGimmick.cs` | 집중 순찰 이동 구현 (Z ±2m) |
| `ElectricZoneGimmick.cs` | X-Z 순찰 이동 구현 (Z ±1m) |
| `LureBaitGimmick.cs` | 미끼 순회 이동 구현 (Z ±2m) |
| `DashChargeGimmick.cs` | 절벽 순찰 이동 구현 (Z 고정) |

---

## 7. 참고 사항

- **일반 몬스터**: 기존 `PatrolBehavior` (X축 이동) 유지 → 영향 없음
- **정예 몬스터**: 제자리 고정 (별도 처리 예정)
- **ChaseBehavior**: Player 추적 로직 변경 없음 (X-Z 평면 제한 없음)
- **ScriptableObject**: 보스마다 개별 에셋 사용 → 상태 공유 문제 없음

---

*이 문서는 보스 기믹 이동 제어권 설정 및 테스트를 위한 가이드입니다.*
*문제 발생 시 이 문서를 참조하세요.*
