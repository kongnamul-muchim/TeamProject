# Phase 2: 보스 기믹 설계 문서

> **버전:** v1.0
> **작성일:** 2026-04-21
> **문서 상태:** 구현완료
> **참조:** `Plans/EnemyAI_기획서.md`

---

## 1. 아키텍처 개요

### 1.1 기믹 시스템 구조

```
IEnemyGimmick (인터페이스)
 ├── OnActivate / OnDeactivate
 ├── OnPatrolEnter / OnPatrolUpdate / OnPatrolExit
 ├── OnChaseEnter / OnChaseUpdate / OnChaseExit
 └── OnSearchEnter / OnSearchUpdate / OnSearchExit

ScriptableObject 기반 기믹 에셋
 ├── [CreateAssetMenu]로 우클릭 → Create → Enemy Gimmicks → 선택
 ├── 인스펙터에서 모든 설정값 직접 조절
 ├── BossEnemyController에 드래그 앤 드롭으로 할당
 └── 보스마다 다른 설정값으로 재사용 가능

BossEnemyController
 ├── gimmickAsset (ScriptableObject) → 우선 사용
 ├── customGimmick (MonoBehaviour) → fallback
 └── 콜백 연결 → 속도/시야/의심도/상태전환 연동
```

### 1.2 파일 구조

```
Assets/Core/Enemy/Boss/Gimmicks/
├── IEnemyGimmick.cs              # 기믹 공통 인터페이스
├── AmbushGimmick.cs              # Ch.1 가자미 (매복 → 기습)
├── RelentlessChaseGimmick.cs     # Ch.2 곰치 (집요한 추격)
├── ElectricZoneGimmick.cs        # Ch.3 전기뱀장어 (감전 구역)
├── LureBaitGimmick.cs            # Ch.4 아귀 (발광 미끼)
└── DashChargeGimmick.cs          # Ch.5 백상아리 (초고속 돌진)

Assets/ScriptableObjects/Gimmicks/ (생성 예정)
├── Gimmick_Ambush_Flounder.asset
├── Gimmick_Relentless_Moray.asset
├── Gimmick_ElectricZone_Eel.asset
├── Gimmick_LureBait_Angler.asset
└── Gimmick_DashCharge_Shark.asset
```

---

## 2. 보스별 기믹 상세

### 2.1 Ch.1 가자미 (Flounder) - 매복 기믹

| 항목 | 내용 |
|------|------|
| **파일** | `AmbushGimmick.cs` |
| **타입** | `GimmickType.Ambush` |
| **핵심** | 모래에 파묻혀 매복 → 기습 돌진 |

#### 동작 흐름
```
[Patrol] 매복 지점에서 대기 (시야 숨김)
    ↓ Player 감지
[Chase] 모래에서 튀어나와 고속 돌진 (1.5초)
    ↓ 돌진 종료
[Search] 매복 지점으로 복귀 → 다시 매복
```

#### 설정값 (기본값)
| 파라미터 | 값 | 설명 |
|----------|-----|------|
| `ambushDuration` | 3초 | 매복 대기 시간 |
| `dashSpeed` | 8 | 기습 돌진 속도 |
| `dashDuration` | 1.5초 | 돌진 지속 시간 |
| `returnSpeed` | 2 | 매복 지점 복귀 속도 |

#### 콜백
| 콜백 | 용도 |
|------|------|
| `OnSpeedOverride` | 돌진 시 속도 변경 |
| `OnVisibilityToggle` | 매복 중 시야 숨김/표시 |
| `OnDashCompleted` | 돌진 완료 알림 |

#### 필요 프리팹
| 프리팹 | 용도 | 필수 |
|--------|------|------|
| 없음 | 코드만으로 동작 | ❌ |

#### Scene 설정
| 오브젝트 | 설명 |
|----------|------|
| `AmbushPoint` (선택) | 매복 지점 마커 (시각적 표시용) |

---

### 2.2 Ch.2 곰치 (Moray Eel) - 집요한 추격 기믹

| 항목 | 내용 |
|------|------|
| **파일** | `RelentlessChaseGimmick.cs` |
| **타입** | `GimmickType.RelentlessChase` |
| **핵심** | 의태 감지 시 좁은 구역 집중 순찰 + Chase 지속 |

#### 동작 흐름
```
[Patrol] 의태 위치 기억 → 좁은 구역 집중 순찰 (반경 8m)
    ↓ Player 발견
[Chase] 의심도 하락률 30%로 감소 (오랜 추적 유지)
    ↓ Player 놓침
[Search] 수색 반경 1.25배 확대 → 의태 위치 중심 수색
```

#### 설정값 (기본값)
| 파라미터 | 값 | 설명 |
|----------|-----|------|
| `suspicionDecayMultiplier` | 0.3 | 의심도 하락 30% |
| `searchRadiusMultiplier` | 1.25 | 수색 반경 1.25배 |
| `patrolAreaRadius` | 8m | 집중 순찰 반경 |

#### 콜백
| 콜백 | 용도 |
|------|------|
| `OnSuspicionDecayRateOverride` | 의심도 하락률 변경 |
| `OnSearchRadiusOverride` | 수색 반경 변경 |
| `OnPatrolAreaOverride` | 순찰 영역 제한 (중심점, 반경) |

#### 필요 프리팹
| 프리팹 | 용도 | 필수 |
|--------|------|------|
| 없음 | 코드만으로 동작 | ❌ |

---

### 2.3 Ch.3 전기뱀장어 (Electric Eel) - 감전 구역 기믹

| 항목 | 내용 |
|------|------|
| **파일** | `ElectricZoneGimmick.cs` |
| **타입** | `GimmickType.ElectricZone` |
| **핵심** | 이동 중 일정 확률로 감전 구역 설치 |

#### 동작 흐름
```
[Patrol] 이동 중 30% 확률로 감전 구역 설치 → 쿨타임 8초
    ↓ Player 발견
[Chase] 설치 확률 2배 증가 (60%) → 더 자주 설치
    ↓ Player가 감전 구역 접촉
[함정] Player 3초간 행동 불가 → 구역 즉시 소멸
```

#### 설정값 (기본값)
| 파라미터 | 값 | 설명 |
|----------|-----|------|
| `zoneDuration` | 5초 | 감전 구역 지속 시간 |
| `installCooldown` | 8초 | 설치 쿨타임 |
| `installProbability` | 0.3 (30%) | 설치 확률 |
| `checkInterval` | 1초 | 설치 시도 체크 간격 |

#### 콜백
| 콜백 | 용도 |
|------|------|
| `OnZoneCreated` | 구역 생성 시 |
| `OnPlayerTrapped` | Player 감전 시 (3초 행동 불가) |

#### 필요 프리팹
| 프리팹 | 용도 | 필수 |
|--------|------|------|
| **ElectricZone** | 감전 구역 시각화 + 충돌 판정 | ✅ |

#### ElectricZone 프리팹 설계도
```
ElectricZone (Prefab)
├── Collider (3D 또는 2D)
│   ├── Trigger 활성화
│   └── 레이어: "Trap" (신규 레이어 권장)
├── Renderer (시각 효과)
│   ├── MeshRenderer (3D) 또는 SpriteRenderer (2D)
│   ├── 재질: 전격 이펙트 (파란색 발광)
│   └── 크기: 반경 3m 원형
├── ElectricZone (컴포넌트)
│   ├── Initialize(duration, onPlayerTrapped)
│   ├── OnTriggerEnter / OnTriggerEnter2D
│   └── Update() → 타이머 → 자동 소멸
└── VFX (선택)
    ├── ParticleSystem (전기 이펙트)
    └── AudioSource (찌릿 사운드)
```

#### 인스펙터 설정
```
BossEnemyController
├── Gimmick Type: ElectricZone
└── ElectricZoneGimmick
    └── Electric Zone Prefab: [ElectricZone 프리팹 드래그]
```

---

### 2.4 Ch.4 아귀 (Anglerfish) - 발광 미끼 기믹

| 항목 | 내용 |
|------|------|
| **파일** | `LureBaitGimmick.cs` |
| **타입** | `GimmickType.LureBait` |
| **핵심** | Patrol 중 맵 랜덤 위치에 미끼 5개 배치 |

#### 동작 흐름
```
[Patrol] Ground 범위 내 랜덤 위치 5곳에 미끼 배치
    ↓ 10초 경과
[갱신] 기존 미끼 모두 제거 → 새 미끼 5개 재생성
    ↓ Player가 미끼 접근 (반경 2m)
[감지] Player 위치 기록 → 2초 후 Chase 전환 (의태 시간 확보)
    ↓ Chase 진입
[추격] 미끼 모두 제거 → 빠른 속도로 Player 추적
```

#### 설정값 (기본값)
| 파라미터 | 값 | 설명 |
|----------|-----|------|
| `baitCount` | 5 | 동시 미끼 개수 |
| `baitLifetime` | 10초 | 미끼 유지 시간 |
| `chaseTransitionDelay` | 2초 | Chase 전환 지연 |
| `baitTriggerRadius` | 2m | 미끼 감지 반경 |

#### 콜백
| 콜백 | 용도 |
|------|------|
| `OnBaitCreated` | 미끼 생성 시 |
| `OnBaitDestroyed` | 미끼 제거 시 |
| `OnPlayerDetectedByBait` | Player 감지 시 (위치 전달) |
| `OnChaseTriggered` | Chase 전환 시 (지연 후 자동) |

#### 필요 프리팹
| 프리팹 | 용도 | 필수 |
|--------|------|------|
| **LureBait** | 발광 미끼 시각화 + 감지 판정 | ✅ |

#### LureBait 프리팹 설계도
```
LureBait (Prefab)
├── Collider (3D 또는 2D)
│   ├── Trigger 활성화
│   └── 레이어: "Lure" (신규 레이어 권장)
├── Renderer (시각 효과)
│   ├── MeshRenderer (3D) 또는 SpriteRenderer (2D)
│   ├── 재질: 발광 이펙트 (노란색/주황색 빛)
│   └── 크기: 반경 0.5m 구체
├── Light (선택)
│   ├── Point Light
│   ├── 색상: 따뜻한 노란색
│   └── 강도: 2~3
├── LureBait (컴포넌트)
│   ├── Initialize(triggerRadius, onTriggered)
│   ├── Update() → Player 거리 체크 → 감지 시 콜백
│   └── OnDrawGizmos() → 감지 반경 표시 (디버깅)
└── VFX (선택)
    ├── ParticleSystem (반짝이 이펙트)
    └── AudioSource (낮은 윙윙거림)
```

#### 인스펙터 설정
```
BossEnemyController
├── Gimmick Type: LureBait
└── LureBaitGimmick
    └── Bait Prefab: [LureBait 프리팹 드래그]
```

---

### 2.5 Ch.5 백상아리 (Great White Shark) - 초고속 돌진 기믹

| 항목 | 내용 |
|------|------|
| **파일** | `DashChargeGimmick.cs` |
| **타입** | `GimmickType.DashCharge` |
| **핵심** | Chase 전환 시 Player 위치 기록 → 직선 돌진 |

#### 동작 흐름
```
[Patrol] 절벽 구간 빠르게 순찰
    ↓ Player 발견 → Chase 전환
[Chase] Player 위치 기록 → 그 방향으로 직선 돌진 (속도 15)
    ↓ 돌진 중 경로상 엄폐물 충돌
[파괴] CamouflageTarget 비활성화 (SetActive(false))
    ↓ 최대 20m 돌진 또는 Player 위치 도달
[정지] 2초간 정지 → Patrol 복귀
```

#### 설정값 (기본값)
| 파라미터 | 값 | 설명 |
|----------|-----|------|
| `dashSpeed` | 15 | 돌진 속도 (매우 빠름) |
| `dashCooldown` | 2초 | 돌진 후 정지 시간 |
| `chargeWidth` | 2m | 돌진 판정 너비 (넓음) |
| `maxDashDistance` | 20m | 최대 돌진 거리 |

#### 콜백
| 콜백 | 용도 |
|------|------|
| `OnSpeedOverride` | 돌진 시 속도 변경 |
| `OnDashStarted` | 돌진 시작 시 (방향 전달) |
| `OnObstacleHit` | 엄폐물 충돌 시 (비활성화) |
| `OnDashCompleted` | 돌진 완료 → Patrol 복귀 |

#### 필요 프리팹
| 프리팹 | 용도 | 필수 |
|--------|------|------|
| 없음 | 코드만으로 동작 | ❌ |

#### 돌진 판정 방식
```
SphereCast (반지름: chargeWidth/2 = 1m)
├── 시작: Enemy 현재 위치
├── 방향: Player 위치 기준 직선
├── 매 프레임 체크
└── 충돌 시: CamouflageTarget 레이어 → SetActive(false)
```

---

## 3. 인스펙터 설정 가이드

### 3.1 보스 프리팹 공통 설정

```
BossEnemy (Prefab)
├── BossEnemyController (컴포넌트)
│   ├── 기본 설정
│   │   ├── Move Speed: 3
│   │   ├── Acceleration: 8
│   │   ├── Friction: 0.9
│   │   └── Max Speed: 5
│   ├── 시야 설정
│   │   ├── Enemy Forward: (사용 안 함 - Custom View Direction 사용)
│   │   └── View Rotation Speed: 5
│   ├── Ground 제한 설정
│   │   ├── Ground Layer: Ground
│   │   ├── Ground Check Distance: 0.5
│   │   ├── Ground Check Radius: 0.3
│   │   ├── Ground Scan Distance: 50
│   │   └── Ground Scan Step: 1
│   ├── 보스 설정
│   │   ├── Vision Sensor: [ConeVisionSensor 참조]
│   │   └── Suspicion Meter: [SuspicionMeter 참조]
│   ├── 상태별 속도
│   │   ├── Patrol Speed: 2
│   │   ├── Chase Speed: 5
│   │   └── Search Speed: 3
│   ├── 탐색 설정
│   │   ├── Search Distance: 3
│   │   └── Search Duration: 5
│   └── 기믹 설정
│       ├── Gimmick Asset: [ScriptableObject 기믹 에셋 드래그]
│       └── Custom Gimmick: (선택사항, fallback용)
├── ConeVisionSensor (컴포넌트)
│   ├── View Radius: 5
│   ├── View Angle: 60
│   ├── Pattern Type: Patrol
│   ├── Target Layer: Player
│   ├── Obstacle Layer: Obstacle
│   └── Custom View Direction: (90, 0, 0)
├── SuspicionMeter (컴포넌트)
│   └── (기존 설정 유지)
└── 자식 오브젝트
    └── Enemy_Forward (사용 안 함 - Custom View Direction으로 대체)
```

### 3.2 ScriptableObject 기믹 에셋 생성 방법

1. Unity 에디터에서 우클릭 → **Create** → **Enemy Gimmicks** → 원하는 기믹 선택
2. 생성된 `.asset` 파일을 `Assets/ScriptableObjects/Gimmicks/`에 저장
3. 인스펙터에서 설정값 조절
4. `BossEnemyController`의 **Gimmick Asset** 필드에 드래그 앤 드롭

### 3.3 챕터별 보스 설정값

| 설정 | Ch.1 가자미 | Ch.2 곰치 | Ch.3 전기뱀장어 | Ch.4 아귀 | Ch.5 백상아리 |
|------|-------------|-----------|-----------------|-----------|---------------|
| **Gimmick Type** | Ambush | RelentlessChase | ElectricZone | LureBait | DashCharge |
| **Patrol Speed** | 1.5 | 1.5 | 2.5 | 1 | 3 |
| **Chase Speed** | 5 | 6 | 5 | 7 | 5 |
| **Search Speed** | 2 | 3 | 3 | 3 | 2 |
| **View Radius** | 4 | 6 | 5 | 4 | 7 |
| **View Angle** | 45° | 90° | 60° | 45° | 120° |
| **View Direction** | (90,0,0) | (90,0,0) | (90,0,0) | (90,0,0) | (90,0,0) |
| **필요 프리팹** | 없음 | 없음 | ElectricZone | LureBait | 없음 |

---

## 4. 신규 레이어 권장

| 레이어명 | 용도 | 충돌 설정 |
|----------|------|-----------|
| **Trap** | 감전 구역 등 함정 | Player와 충돌 |
| **Lure** | 발광 미끼 | Player와 충돌 |
| **CamouflageTarget** | 엄폐물 (기존) | 백상아리 돌진에 파괴 |

---

## 5. 기믹 확장 가이드

### 5.1 새 기믹 추가 방법

1. `IEnemyGimmick` 인터페이스 구현한 `ScriptableObject` 클래스 생성
2. `[CreateAssetMenu]` 어트리뷰트 추가
3. `BossEnemyController.ConnectGimmickCallbacks()`에 콜백 연결 추가
4. Unity에서 우클릭 → Create → Enemy Gimmicks → 새 기믹 선택 → 에셋 생성

### 5.2 예시: 새 기믹 템플릿

```csharp
[CreateAssetMenu(menuName = "Enemy Gimmicks/My New Gimmick", fileName = "MyNewGimmick")]
public sealed class MyNewGimmick : ScriptableObject, IEnemyGimmick
{
    public GimmickType Type => GimmickType.새타입;

    [Header("설정")]
    [SerializeField] private float someValue = 1f;

    public void OnActivate(Transform bossTransform) { }
    public void OnDeactivate() { }
    public void OnPatrolEnter() { }
    public void OnPatrolUpdate(float deltaTime) { }
    public void OnPatrolExit() { }
    public void OnChaseEnter() { }
    public void OnChaseUpdate(float deltaTime) { }
    public void OnChaseExit() { }
    public void OnSearchEnter() { }
    public void OnSearchUpdate(float deltaTime) { }
    public void OnSearchExit() { }
}
```

---

## 6. TODO / 향후 작업

- [ ] ElectricZone 프리팹 생성 (3D/2D 결정 후)
- [ ] LureBait 프리팹 생성 (3D/2D 결정 후)
- [ ] SuspicionMeter에 `SetDecayRate()` 메서드 추가 (곰치 기믹 연동)
- [ ] PlayerController에 `SetStunned(duration)` 메서드 추가 (감전 연동)
- [ ] 기믹별 사운드 이펙트 연동
- [ ] 기믹별 VFX 이펙트 연동

---

*이 문서는 Phase 2 보스 기믹의 설계 문서입니다.*
*프리팹 생성 시 이 문서를 참조하세요.*
