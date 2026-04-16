# 프로젝트 설계도

> 마지막 업데이트: 2026-04-16

---

## 📁 폴더 구조

```
TeamProject/
├── Assets/                          # Unity 에셋
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
| Agents.md | AI 작업 규칙参照先 | 수동 |
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
