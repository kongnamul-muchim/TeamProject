# Hide and Ink - Member A 1주차 Task List

> **버전:** v1.1
> **작성일:** 2026-04-14
> **최종 업데이트:** 2026-04-16
> **담당자:** Member A (코어 프로그래머)
> **목표:** 문어 이동 + 완벽 의태 시스템 구현

---

## 1. 프로젝트 환경 설정 (Day 1)

### 1.1 유니티 버전 통일
- [x] 팀원 전원 Unity 6000.3.9f1 버전 통일 확인
- [x] Git 브랜치 전략 수립 (main / develop / feature/*)

### 1.2 프로젝트 구조 설계
```
Assets/
├── Core/                    # 핵심 시스템 (인터페이스 + 구현)
│   ├── Interfaces/          # DI용 인터페이스
│   ├── Player/              # 플레이어 이동, 의태
│   ├── Perception/          # 시야 감지, 의심도
│   └── Managers/            # GameManager, PoolManager 등
├── Player/                  # 유니티 어댑터 스크립트
├── QuickOutline/            # Occlusion 표시 Asset
├── Resources/              # 스프라이트 리소스
├── Shader/                 # ShaderGraph 파일
└── Scenes/                 # 유니티 씬
```

### 1.3 DI 컨테이너 설계
- [x] DI 컨테이너 클래스 작성 (DIContainer.cs)
- [x] 서비스 등록 메서드 작성 (RegisterCoreServices - 미완성)
- [x] 의존성 해결 메서드 작성 (Resolve)

### 1.4 SOLID / DI 준수 체크리스트
- [x] 모든 시스템은 인터페이스를 통해 통신
- [x] 단일 책임 원칙 준수
- [x] 개방-폐쇄 원칙 준수
- [x] 리스코프 치환 원칙 준수
- [x] 의존성 역전 원칙 준수

---

## 2. 플레이어 이동 시스템 (Day 1-2) ✅ 완료

### 2.1 이동 시스템 설계
- [x] `IPlayerMovement` 인터페이스 작성
- [x] `PlayerMovement` 클래스 작성 (8방향 + 가감속)
- [x] 이동 방향 벡터 계산 (MoveDirection enum: Down/Right/Up/Left)
- [x] 속도, 가속도, 마찰력 파라미터

### 2.2 유니티 변환
- [x] `PlayerMovementAdapter` 유니티 스크립트 작성
- [x] Unity 기본 InputSystem 연동 (Keyboard 입력)
- [x] Rigidbody + Transform 조작으로 이동 적용

### 2.3 테스트
- [x] 8방향 이동 정상 작동 확인
- [x] 가감속 자연스러움 확인
- [x] 경계 영역 충돌 확인 (wallLayer 설정)

---

## 3. 의태 시스템 (Day 2-3) ✅ 완료

### 3.1 의태 시스템 설계

#### 3.1.1 오브젝트 탐지
- [x] `ICamouflageDetector` 인터페이스
- [x] `CamouflageDetector` 클래스 (반경 내 오브젝트 탐지)
- [x] OverlapSphere 활용

#### 3.1.2 의태 상태 관리
- [x] `ICamouflageStateMachine` 인터페이스
- [x] `CamouflageStateMachine` 클래스
  - 상태: `None` → `Attached` → `Locked` → `Approaching` → `Partial` → `Perfect`
  - C 키 토글 방식 (누르고 있으면 진행, 떼면 취소/유지)
  - 2초 가만히 유지 시 `Perfect`로 전환

#### 3.1.3 색상 복사 시스템 (OriginalRate 기반)
- [x] `IMaterialCloner` 인터페이스
- [x] `MaterialCloner` 클래스 (ShaderGraph OriginalRate 활용)
  - OriginalRate로 시각적 색상 조절 (1=원본, 0=타겟 색)

#### 3.1.4 Billboard 스프라이트
- [x] `BillboardSprite` 클래스

### 3.2 의태 규칙 구현
| 규칙 | 구현 여부 |
|------|----------|
| 반경 내 오브젝트 접근 시 자동 변신 | ✅ C 키 입력 시 |
| 이동하면 즉시 해제 | ✅ |
| 2초 가만히 → 완벽 의태 인정 | ✅ |
| 한 번에 하나의 오브젝트만 의태 | ✅ |

---

## 4. Occlusion 시스템 ✅ 완료

### 4.1 Quick Outline Asset 적용
- [x] 플레이어가 장애물에 가려질 때 외각선으로 위치 표시
- [x] Occlusion 모드: 가려진 부분만 외각선 표시

### 4.2 스프라이트 방향 변경
- [x] 이동 방향에 따라 스프라이트 자동 교체 (Down/Left/Right/Up)
- [x] Resources.Load 방식으로 파일명 자동 로드

---

## 5. 완료 체크리스트

| 항목 | 상태 |
|------|------|
| 문어 8방향 이동 + 가감속 | ✅ 완료 |
| 반경 내 오브젝트 탐지 (CamouflageDetector) | ✅ 완료 |
| 오브젝트 색 복사 (OriginalRate 기반 ShaderGraph) | ✅ 완료 |
| Billboard 스프라이트 | ✅ 완료 |
| 이동 시 즉시 의태 해제 | ✅ 완료 |
| 2초 가만히 시 완벽 의태 | ✅ 완료 |
| 플레이어 가려질 때 외각선 표시 (Quick Outline) | ✅ 완료 |
| 스프라이트 방향별 변경 | ✅ 완료 |
| DI 컨테이너 작동 확인 | ✅ 완료 |
| 빌드 가능 상태 | ✅ 완료 |

---

> **문서 작성자:** Member A
> **최종 업데이트:** 2026-04-16
