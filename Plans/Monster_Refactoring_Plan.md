# 몬스터 대폭 감소 및 개선 계획

> **버전:** v1.3
> **작성일:** 2026-04-24
> **문서 상태:** 계획단계
> **참조:** `Plans/EnemyAI_기획서.md`, `Plans/Phase2_BossGimmicks_Design.md`, `Plans/Phase3_SensorArchitecture.md`

---

## 1. 최종 몬스터 구성

| 구분 | 구성 | 비고 |
|:----:|------|------|
| **보스** 🐟 | **가자미** / **곰치** / **전기뱀장어** / **상어** | 아귀 완전 제거 |
| **정예** ⚔️ | **청새치**(로직개선) / **바다거북**(계속사용) | 2종 |
| **일반** 🐚 | **게**(NormalEnemyController) / **성게**(SeaUrchin) | 2종 |

---

## 2. 작업 항목

### Phase A: 아귀(LureBaitGimmick) 완전 제거 🔥

#### A-1: 소스 파일 삭제
- [ ] `Assets/Core/Enemy/Boss/Gimmicks/LureBaitGimmick.cs` — 삭제
- [ ] `Assets/Core/Enemy/Boss/Gimmicks/LureBaitGimmick.cs.meta` — 삭제

#### A-2: `GimmickType` enum 수정
- 파일: `Boss/Gimmicks/IEnemyGimmick.cs`
- 내용: `LureBait` enum 값 제거

#### A-3: `BossEnemyController.cs` 정리
| 항목 | 내용 |
|------|------|
| `ConnectGimmickCallbacks()` switch문 | `case LureBaitGimmick` 블록 제거 |
| `ConnectLureBaitCallbacks()` | 메서드 전체 제거 |
| `InitializeGimmick()` | Ambush 전용 특수 처리 외에 Lure 관련 제거 |

#### A-4: ScriptableObject 에셋 정리
- [ ] `Assets/ScriptableObjects/Gimmicks/Gimmick_LureBait_Angler.asset` (존재 시) — 삭제
- [ ] `.meta` 파일 — 삭제

---

### Phase B: 청새치 로직 개선 🎯 (유일한 코드 변경)

#### B-1: 2단계 돌진 시스템
```
[순찰] ──Player 감지──▶ [조준 0.5s] ──▶ [돌진]
                              ↑              │
                              │     ┌─성공→쿨타임
                              │     ├─충돌→스턴→재정비
                              │     └─회피→선회→재돌진
```

#### B-2: 스태미나 기반 3종 패턴
| 패턴 | 스태미나 | 설명 |
|:----:|:--------:|------|
| 기본 돌진 | 20 | 단일 직선, 항상 가능 |
| 연속 돌진 | 25+25 | 2연타, 50% 이상 필요 |
| 광역 돌격 | 80 | 넓은 판정, 풀스태미나 필요 |

- 회복: 초당 10 (최대 100)

#### B-3: 시각적 피드백
조준 경고선 / 돌진 Trail / 충돌 파티클 / 스태미나 게이지

---

### Phase C: 바다거북 — 계속 사용 (변경 없음)

- `SeaTurtleBehavior.cs` 유지, 코드 변경 없음
- 문서에서 정예 항목에 포함

---

### Phase D: 일반 몬스터 — 게 + 성게 운영

- `NormalEnemyController.cs` — 게 컨트롤러로 유지
- `SeaUrchinController.cs` + `SeaUrchinPool.cs` — 성게 유지
- 불가사리/발광해면/가오리조각 — 기획에서 제외

---

### Phase E: 문서 업데이트

#### E-1: `Plans/EnemyAI_기획서.md`
| 항목 | 수정 |
|------|------|
| 2-1.1 보스 매핑 | 4종 (아귀 제거, 상어 Ch.4) |
| 2-1.2 일반 매핑 | 게 + 성게 |
| 2-1.3 정예 매핑 | 청새치 + 바다거북 |
| 3-1.4 아귀 기획 | 섹션 제거 |
| 3-1.5 상어 | Ch.5 → Ch.4 |
| 7. 파일 구조 | 변경 불필요 |

#### E-2: `Plans/Phase2_BossGimmicks_Design.md`
| 항목 | 수정 |
|------|------|
| 1.1 GimmickType 목록 | LureBait 제거 |
| 2.4 아귀 기믹 | 섹션 전체 제거 |
| 3.3 챕터별 설정값 | 아귀 행 제거 |
| 6. TODO | 아귀 관련 항목 제거 |

#### E-3: `Plans/Phase3_SensorArchitecture.md`
- Phase 3.3: 정예 = 청새치 + 바다거북

---

### Phase F: 테스트 및 검증

- [ ] 컴파일 — LureBait 제거 + 청새치 개선 에러 없음
- [ ] 보스 4종 — 가자미/곰치/전기뱀장어/상어 정상
- [ ] 청새치 — 2단계 돌진 + 스태미나 정상
- [ ] 바다거북 — 기존 동작 유지
- [ ] 게 + 성게 — 정상 동작

---

## 3. 삭제/수정 파일 목록

### 삭제
| 파일 | 사유 |
|------|------|
| `Boss/Gimmicks/LureBaitGimmick.cs` | 아귀 보스 제거 |
| (존재 시) `Gimmick_LureBait_Angler.asset` | 아귀 기믹 에셋 |

### 수정
| 파일 | 내용 |
|------|------|
| `Boss/Gimmicks/IEnemyGimmick.cs` | GimmickType enum LureBait 제거 |
| `Boss/BossEnemyController.cs` | Lure 관련 switch/callback/메서드 정리 |
| `Elite/Behaviors/SwordfishBehavior.cs` | **로직 개선** |
| `Plans/EnemyAI_기획서.md` | 몬스터 매핑 + 아귀 기획 제거 |
| `Plans/Phase2_BossGimmicks_Design.md` | 아귀 기믹 섹션 제거 |
| `Plans/Phase3_SensorArchitecture.md` | 정예 항목 수정 |

---

## 4. Git Commit 계획

| # | 메시지 |
|:-:|--------|
| 1 | `Remove: LureBaitGimmick (Anglerfish) - source, enum, references` |
| 2 | `Enhance: Swordfish behavior - 2-phase charge + stamina system` |
| 3 | `Docs: Update monster lineup - 4 bosses, 2 elites, 2 normals` |

---

*이 문서는 몬스터 대폭 감소 및 개선을 위한 계획서입니다.*
*Phase A부터 순차적으로 진행합니다.*
