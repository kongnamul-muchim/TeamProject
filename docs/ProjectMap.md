# ProjectMap - AI Navigation Guide

> 마지막 업데이트: 2026-04-16
> 이 파일은 프로젝트 파일 위치를 한눈에 파악するための 길찾기 지도です.

---

## 📁 프로젝트 구조

```
TeamProject/
├── Assets/
│   ├── Scripts/          # C# 스크립트 (유니티 비헤이비어)
│   ├── Core/             # 핵심 시스템 (인터페이스, 매니저, 퍼셉션)
│   ├── QuickOutline/     # 서드파티 아웃라인 효과
│   ├── Scenes/           # 유니티 씬 파일
│   ├── _Recovery/        # 복구용 백업
│   └── TutorialInfo/     # 기본 튜토리얼
├── Logs/                 # 플레이 로그 (자동 생성)
├── docs/                 # 문서 (TaskList, ProjectMap)
├── plans/                # 기획 문서
├── reports/              # 작업 완료 보고서
└── [설정 파일들]
```

---

## 🎮 핵심 시스템 (Core)

### 매니저 (Managers)
| 파일 | 경로 | 설명 |
|------|------|------|
| GameManager | `Assets/Core/Managers/GameManager.cs` | DI 컨테이너, 서비스 등록 |
| GameStateMachine | `Assets/Core/Managers/GameStateMachine.cs` | 게임 상태 관리 ( singleton) |

### 의존성 주입 (DI)
| 파일 | 경로 | 설명 |
|------|------|------|
| DIContainer | `Assets/Core/Managers/DIContainer.cs` | 서비스_locator 패턴 |
| IDIContainer | `Assets/Core/Interfaces/IDIContainer.cs` | DI 컨테이너 인터페이스 |

---

## 👁️ 퍼셉션 시스템 (Perception)

### 의심 미터 (Suspicion)
| 파일 | 경로 | 설명 |
|------|------|------|
| SuspicionMeter | `Assets/Core/Perception/SuspicionMeter.cs` | 의심도 게이지 (MonoBehaviour) |
| SuspicionMeterUI | `Assets/Core/Perception/SuspicionMeterUI.cs` | UI 표시 |
| ISuspicionMeter | `Assets/Core/Interfaces/ISuspicionMeter.cs` | 의심 미터 인터페이스 |
| VisionBasedSuspicionManager | `Assets/Core/Perception/VisionBasedSuspicionManager.cs` | 시야 기반 의심 증가 |
| SuspicionToGameStateLink | `Assets/Core/Perception/SuspicionToGameStateLink.cs` | 의심 → 게임 상태 연동 |

### 시야 감지 (Vision)
| 파일 | 경로 | 설명 |
|------|------|------|
| ConeVisionSensor | `Assets/Core/Perception/ConeVisionSensor.cs` | 원뿔형 시야 감지 |
| IVisionSensor | `Assets/Core/Interfaces/IVisionSensor.cs` | 시야 센서 인터페이스 |

### 위장 (Camouflage)
| 파일 | 경로 | 설명 |
|------|------|------|
| CamouflageAdapter | `Assets/Scripts/Player/CamouflageAdapter.cs` | 위장 어댑터 |
| CamouflageStateMachine | `Assets/Core/Perception/CamouflageStateMachine.cs` | 위장 상태 머신 |
| CamouflageDetector | `Assets/Core/Perception/CamouflageDetector.cs` | 위장 감지 |
| CamouflageToSuspicionLink | `Assets/Core/Perception/CamouflageToSuspicionLink.cs` | 위장 → 의심 연동 |
| ICamouflageStateMachine | `Assets/Core/Interfaces/ICamouflageStateMachine.cs` | 위장 상태 머신 인터페이스 |
| ICamouflageDetector | `Assets/Core/Interfaces/ICamouflageDetector.cs` | 위장 감지 인터페이스 |

### 시각 효과 (Visual)
| 파일 | 경로 | 설명 |
|------|------|------|
| BillboardSprite | `Assets/Core/Perception/BillboardSprite.cs` | 카메라 향하는 스프라이트 |
| MaterialCloner | `Assets/Core/Perception/MaterialCloner.cs` | 메테리얼 복제 (인스턴싱) |
| IMaterialCloner | `Assets/Core/Interfaces/IMaterialCloner.cs` | 메테리얼 클론너 인터페이스 |

---

## 🏃 플레이어 (Player)

| 파일 | 경로 | 설명 |
|------|------|------|
| PlayerMovement | `Assets/Core/Player/PlayerMovement.cs` | 플레이어 이동 |
| PlayerMovementAdapter | `Assets/Scripts/Player/PlayerMovementAdapter.cs` | 이동 어댑터 |
| IPlayerMovement | `Assets/Core/Interfaces/IPlayerMovement.cs` | 이동 인터페이스 |

---

## 🛠️ 유틸리티 (Utilities)

| 파일 | 경로 | 설명 |
|------|------|------|
| LogModule | `Assets/Scripts/LogModule.cs` | 로그 캡처 (태그별 .md 저장, singleton) |

---

## 📄 씬 (Scenes)

| 파일 | 경로 | 설명 |
|------|------|------|
| Test | `Assets/Scenes/Test.unity` | 메인 테스트 씬 |
| 0 (Recovery) | `Assets/_Recovery/0.unity` | 복구용 백업 |

---

## 📚 문서 (Documentation)

| 파일 | 경로 | 설명 |
|------|------|------|
| Spec | `Spec.md` | 프로젝트 설계도 |
| Agents | `Agents.md` | AI 작업 규칙 |
| ProjectMap | `docs/ProjectMap.md` | AI용 파일 길찾기 지도 (이 파일) |
| TaskList | `docs/TaskList_*.md` | 작업 목록 |
| Plans | `plans/*.md` | 기획 문서 |
| Milestone | `plans/milestone_ai_assisted.md` | 마일스톤 및 역할 분배 |
| Reports | `reports/*.md` | 완료 보고서 |

---

## 📊 게임 상태 (Game State)

```
[Title] → [Playing] → [GameOver]
              ↑
         [Suspicion Too High] → [Caught]
```

- **GameStateMachine**: 상태 전환 관리
- **SuspicionMeter**: 의심도 0~100 (100 도달 시 GameOver)
- **CamouflageStateMachine**: 정상 ↔ 위장 상태 전환

---

## 🔗 시스템 연동도

```
시야 감지 (ConeVisionSensor)
    ↓
의심 증가 (VisionBasedSuspicionManager)
    ↓
의심 미터 (SuspicionMeter) ←→ UI 표시 (SuspicionMeterUI)
    ↓
임계점 초과 시 (SuspicionToGameStateLink) → GameStateMachine → GameOver

위장 감지 (CamouflageDetector)
    ↓
상태 전환 (CamouflageStateMachine)
    ↓
의심 감소 (CamouflageToSuspicionLink)
```

---

*이 파일은 AI가 프로젝트 파일을 빠르게 찾기 위한 참고용 지도입니다.*
*파일 위치 찾을 때 이 파일만 읽으면 됩니다.*
