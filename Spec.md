# 프로젝트 설계도

> 마지막 업데이트: 2026-04-29

---

## 📁 폴더 구조

```
TeamProject/
├── Assets/                          # Unity 에셋
│   ├── Core/                        # DI 컨테이너 및 핵심 시스템
│   │   ├── Interfaces/              # 인터페이스 정의
│   │   │   └── IDIContainer.cs     # DI 컨테이너 인터페이스
│   │   ├── Managers/                # 시스템 관리자
│   │   │   ├── DIContainer.cs      # DI 컨테이너 구현체 (순수 C#)
│   │   │   └── GameManager.cs      # MonoBehaviour, 컨테이너 초기화
│   │   └── (기타 서비스)            # 각종 서비스 인터페이스/구현체
│   ├── Scripts/                     # C# 스크립트
│   │   └── LogModule.cs            # 로그 모듈 (태그별 .md 저장)
│   ├── TutorialInfo/               # 튜토리얼 (기본 제공)
│   └── ...
├── Logs/                           # 플레이 로그 (자동 생성)
│   └── {년-월-일}/                 # 날짜별 폴더
│       ├── INFO.md                 # [INFO] 태그 로그
│       ├── WARN.md                 # [WARN] 태그 로그
│       ├── ERROR.md                # [ERROR] 태그 로그
│       ├── DEBUG.md                # [DEBUG] 태그 로그
│       └── FATAL.md                # [FATAL] 태그 로그
├── Library/                        # Unity 라이브러리 (자동 관리)
├── Temp/                           # 임시 파일 (자동 관리)
├── UserSettings/                   # 에디터 설정
└── .vscode/                        # VSCode 설정
```

---

## 📝 문서 목록

| 파일명 | 용도 | 생성 주기 |
|--------|------|-----------|
| ProjectMap.md | AI용 파일 길찾기 지도 | 수동 |
| Agents.md | AI 작업 규칙 | 수동 |
| TaskList_*.md | 작업 목록 | 수동 |
| milestone_*.md | 마일스톤 기록 | 수동 |
| Logs/{날짜}/ | 플레이 로그 | Unity 실행 시 자동 |

---

## 🔧 스크립트 규칙

### LogModule.cs
- **위치**: `Assets/Scripts/LogModule.cs`
- **종류**: Singleton, DontDestroyOnLoad
- **기능**:
  - Unity 로그 자동 캡처 (`Application.logMessageReceived`)
  - 태그별 .md 파일 분리 저장
  - 동일 날짜 재실행 시 기존 로그 초기화

### 로그 저장 규칙
```
Logs/{년-월-일}/INFO.md
Logs/{년-월-일}/WARN.md
Logs/{년-월-일}/ERROR.md
```

| Unity 로그 타입 | 변환 태그 |
|----------------|----------|
| Debug.Log | INFO |
| Debug.LogWarning | WARN |
| Debug.LogError | ERROR |
| Debug.LogException | ERROR |
| Assert | DEBUG |

---

## 🏗️ DI 컨테이너 아키텍처

### Core 폴더 구조
```
Assets/Core/
├── Interfaces/
│   └── IDIContainer.cs       ← DI 컨테이너 인터페이스
├── Managers/
│   ├── DIContainer.cs        ← DI 컨테이너 구현체 (순수 C#, MonoBehaviour 아님)
│   └── GameManager.cs        ← MonoBehaviour, 컨테이너 생성 및 관리
└── (서비스 인터페이스 및 구현체)
```

### 계층 구조
```
순수 C# 계층                    Unity 계층
─────────────                   ────────────
IDIContainer (인터페이스)        
DIContainer (구현체)     ←──    GameManager (MonoBehaviour)
  - Register<T>()                     └─ Awake() → new DIContainer()
  - Resolve<T>()                      └─ Container.Resolve<T>() 접근
  - CreateScope()                     └─ Adapter들이 Container로 서비스 주입
```

### 특징
- **컨테이너 본체**는 순수 C# 클래스 — `UnityEngine` 의존성 없음
- **GameManager**만 MonoBehaviour — 씬에서 `Awake()` 시 컨테이너 초기화
- 생성자 주입 방식 지원 (`DIContainer`가 생성자 파라미터 자동 해결)
- MonoBehaviour는 생성자 주입이 안 되므로 Adapter 패턴으로 `GameManager.Container` 직접 접근
- 서비스 생명주기: `Transient` / `Scoped` / `Singleton`

### 상세 사용법
→ **Agents.md의 [🏗️ DI 컨테이너 아키텍처] 섹션 참조**

---

## 🚫 금지 사항

- **unity-cli**는 이 프로젝트에서 **사용 금지**
- Unity Editor를 CLI로 제어하는 모든 명령은 허용되지 않음

---

## 📌 작업 흐름

```
[사용자 요청]
    ↓
[Agents.md 읽기] → 작업 규칙 확인
    ↓
[설계도.md 읽기] → 파일 구조 확인
    ↓
[작업 분석 및 계획]
    ↓
[사용자 승인 후 실행]
    ↓
[작업 완료] → Git Commit → 문서화
```

---

## 📊 상태 정의

| 상태 | 설명 |
|------|------|
| 계획단계 | 작업 시작 전, 계획 수립 |
| 진행단계 | 작업 실행 중 |
| 완료단계 | 작업 완료, Commit 완료 |
| 오류단계 | 문제 발생, 해결 필요 |

---

*이 문서는 프로젝트 구조를 한눈에 파악하기 위한 참고용 문서입니다.*
