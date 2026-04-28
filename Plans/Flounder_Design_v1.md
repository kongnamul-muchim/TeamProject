# 가자미 (Flounder) Boss 기믹 설계서 v1

> **버전:** v1.0
> **작성일:** 2026-04-27
> **상태:** 설계 완료 (구현 전)
> **파일:** `AmbushGimmick.cs` + `SandPit.cs` (신규)

---

## 1. 개요

| 항목 | 내용 |
|------|------|
| **Zone** | Zone 2 (해초 밀림/모래톱) |
| **파일** | `AmbushGimmick.cs` (ScriptableObject, 기존) |
| **핵심 컨셉** | **모래 속 매복 → 이동 자국(구덩이) → 기습 돌진** |
| **핵심 돌파 요소** | 가자미의 이동 경로를 읽고, 구덩이를 피하면서 접근 |

> 첫 번째 보스(청새치)가 "직선 예측"을 가르친다면,  
> 가자미는 **"흔적을 읽고 위치를 추적"** 하는 법을 가르침.

---

## 2. 동작 흐름

```
[Patrol] Player 주변 매복 위치로 이동 (빙글빙글 ⭕ 말고, 포인트 이동 ●)
   │       이동한 자리마다 SandPit(구덩이) 생성
   │
   ├─ Player가 SandPit 위를 밟음
   │     ▼
   │  의심도 상승 (발자국/진동 감지 컨셉)
   │
   ├─ Player 감지 (시야 or 거리)
   │     ▼
   │  [Chase: PreDelay] 0.3초 기습 준비 (모래 폭발 이펙트)
   │     ▼
   │  [Chase: Dash] Player 방향으로 1회 고속 돌진
   │     ▼
   │  [Chase: End] 속도 복원 → 일반 추적
   │
   ├─ Player 놓침
   │     ├─ 의심도 Safe → [Patrol] 재매복
   │     └─ 의심도 높음 → [Search] 매복 지점 복귀 → 재매복
   │
   └─ (Player 의태 중) 감지 자체가 안 됨
```

---

## 3. 핵심 시스템: SandPit (구덩이 프리팹)

### 3.1 동작 방식

```
AmbushGimmick (매복 위치 변경)
    │
    └─ OnSpawnPit?.Invoke(이전 위치)
          │
          ▼
BossEnemyController
    │
    └─ Instantiate(SandPitPrefab, 이전 위치)
          │
          ▼
SandPit (MonoBehaviour, 자체 완결)
    ├─ Trigger Collider (범위 1~2m)
    ├─ 일정 시간(8~10초) 후 자동 소멸
    ├─ Player 충돌 시 → 의심도 증가 (진동 감지 컨셉)
    └─ 시각 효과 (모래 구덩이 메쉬 / 알파 소멸)
```

### 3.2 왜 Prefab인가

| 이유 | 설명 |
|------|------|
| **책임 분리** | Gimmick은 위치만 알리고, Prefab이 자기 할 일을 스스로 함 |
| **ScriptableObject 한계 회피** | 위치 리스트/타이머 관리가 필요 없음 |
| **재사용성** | 다른 맵이나 보스에서도 동일 Prefab 사용 가능 |
| **시각 효과 자유** | Prefab 안에서 Mesh/파티클/알파 소멸 등 마음대로 |

---

## 4. 수정 파일 목록

| 파일 | 변경 | 내용 |
|------|:----:|------|
| `AmbushGimmick.cs` | 수정 | `OnSpawnPit` 콜백 1줄 추가 + 호출 1줄 |
| `BossEnemyController.cs` | 수정 | 프리팹 Instantiate 연결 3~5줄 |
| `SandPit.cs` | **신규** | Trigger + Timer + 의심도 연동 |
| SandPit Prefab | **신규** | Collider + Mesh + Material |

### 변경량 (예상)

```
AmbushGimmick.cs     : +3줄 (콜백 선언 + 호출)
BossEnemyController  : +5줄 (Instantiate 연결)
SandPit.cs           : ~30줄 (MonoBehaviour)
SandPit.prefab       : 신규 (Circle Mesh + Trigger)
```

---

## 5. 설정값 (전부 Inspector)

### AmbushGimmick (ScriptableObject)

| 파라미터 | 기본값 | 설명 |
|----------|:------:|------|
| `ambushDistance` | 6 | Player 유지 거리 |
| `ambushMinDistance` | 3 | 최소 접근 거리 (이하 시 반대 방향) |
| `dashSpeed` | 8 | 기습 돌진 속도 |
| `dashDuration` | 1.5 | 돌진 지속 시간 |
| `dashPreDelay` | 0.3 | 돌진 전 딜레이 (모래 폭발) |
| `suspicionRadius` | (10, 10) | 의심도 범위 |
| `suspicionDropThreshold` | 20 | 이하 시 Patrol 직행 |

### SandPit (MonoBehaviour)

| 파라미터 | 기본값 | 설명 |
|----------|:------:|------|
| `lifetime` | 8 | 구덩이 유지 시간 (초) |
| `suspicionAmount` | 15 | 밟을 시 의심도 증가량 |
| `triggerRadius` | 1.5 | 감지 범위 반경 |

---

## 6. 기존 코드 대비 차이점

| 항목 | 현재 (v0) | 변경 (v1) |
|------|----------|----------|
| Patrol 이동 | Player 주변 빙글빙글 | **포인트 이동 + 구덩이 생성** |
| 의심도 | 장소 무관, 시야 기반 | **구덩이 밟으면 추가 상승** |
| Player 전략 | 그냥 멀어지면 됨 | **구덩이 피해서 이동**해야 함 |
| 시각 효과 | 없음 | SandPit 메쉬 + 알파 소멸 |
| 코드 복잡도 | 낮음 | 거의 동일 (Prefab에 책임 위임) |

---

## 7. 향후 확장 (추후 고려)

| 요소 | 설명 |
|------|------|
| 모래 폭발 이펙트 | Dash 전 PreDelay 시 모래 파티클 |
| 구덩이 종류 | 큰 구덩이(천천히 소멸) / 작은 구덩이(빨리 소멸) |
| 의태 시너지 | 의태 중 구덩이 밟아도 의심도 상승 차단 |
| 구덩이 밟은 Player 이동 속도 감소 | 슬로우 디버프 (추후 고려) |

---

*이 문서는 가자미(Flounder) 보스의 구덩이(Pit) 기반 매복 시스템 설계입니다.*
*핵심 돌파 요소: 이동 경로 추적 → 구덩이 회피 → 위치 예측*
