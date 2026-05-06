# DialogueUI 타자기 효과 — 사용 및 설정 가이드

> **마지막 업데이트**: 2026-05-06
> **관련 스크립트**: `Assets/Scripts/UI/DialogueUIAdapter.cs`

---

## 📖 개요

`DialogueUIAdapter`는 NPC 대화를 **타자기 효과**(한 글자씩 순차 출력)로 표시합니다.
본 문서는 타자기 효과의 동작 원리와 최적화 방법, 설정 값을 다룹니다.

---

## ⚙️ 인스펙터 설정

| 필드 | 타입 | 기본값 | 설명 |
|------|------|:------:|------|
| `DialogeUI Root` | GameObject | — | DialogeUI 최상위 오브젝트 |
| `Text Dialoge` | TextMeshProUGUI | — | 대사 텍스트 |
| `Text Speaker` | TextMeshProUGUI | — | 화자 이름 텍스트 |
| `End Dialogue Sign` | GameObject | — | "이어 하기" 표시 |
| `Char Delay` | float | **0.04** | 글자당 표시 지연 시간 (초) |
| `Punctuation Delay` | float | **0.15** | `.` `?` `!` `,` 의 지연 시간 (초) |

### 타자기 속도 조절 (charDelay)

| 값 | 느낌 | 사용 예 |
|:--:|:----:|---------|
| 0.02 | ⚡ 빠름 | 경쾌한 대화, 긴급 상황 |
| 0.03~0.04 | ✅ 보통 | 일반 대화 (권장) |
| 0.05~0.06 | 🐢 약간 느림 | 설명, 차분한 분위기 |
| 0.08~0.1 | 🐌 느림 | 무거운 장면, 긴장감 |

> **팁**: 인스펙터에서 **Play Mode 중에도 실시간 조정 가능**하므로, 씬을 재생하면서 직접 값을 바꿔보고 적절한 속도를 찾으세요.

---

## 🎯 동작 방식

### OnDialogueLineChanged → 타자기 시작

```
 StoryEvents.OnDialogueLineChanged
         ↓
   1. 텍스트 1회 할당 (text = _currentFullText)
   2. maxVisibleCharacters = 0
   3. TypewriterRoutine() 시작
         ↓
   for 루프: 0 → length-1
         ↓
   maxVisibleCharacters = i + 1
         ↓
   WaitForSeconds(딜레이)
         ↓
   루프 종료 → EndDialogueSign 표시
```

### 입력 처리 흐름

| 입력 상황 | 동작 |
|-----------|------|
| 타자기 중 + 클릭/Space | `maxVisibleCharacters = 전체 길이` → 즉시 완료 |
| 타자기 완료 + 클릭/Space | `_storyManager.NextLine()` → 다음 대사 |

---

## 💡 핵심 최적화 — maxVisibleCharacters

### ❌ 기존 방식 (문제점)

```csharp
// 매 프레임마다 문자열 새로 할당 + TMP 레이아웃 재계산
for (int i = 0; i < length; i++)
{
    textDialoge.text += fullText[i];  // ← GC 할당 폭발
}
```

### ✅ 최적화 방식 (적용 완료)

```csharp
// 전체 텍스트 1회만 할당, visible count만 제어
textDialoge.text = _currentFullText;          // 1회만 할당
textDialoge.maxVisibleCharacters = i + 1;     // count만 변경
```

### 성능 비교

| 항목 | Before (text += c) | After (maxVisibleCharacters) |
|------|:------------------:|:---------------------------:|
| 문자열 할당 횟수 | **N**번 (글자 수만큼) | **1**번 |
| TMP 레이아웃 재계산 | N번 | **1**번 |
| 타자기 스킵 시 할당 | 문자열 다시 생성 | visibleCount만 변경 |
| 100글자 기준 GC 부하 | ~5KB | ~0.1KB |

---

## 🛠️ 확장 가이드

### 문자별 개별 딜레이 추가

```csharp
private float GetCharDelay(char c)
{
    return c switch
    {
        '.' or '?' or '!' or ',' => punctuationDelay,
        ' '  => 0.02f,          // 공백 빠르게
        '…' or '—' => 0.2f,     // 줄임표/대시 길게
        '\n' => 0.0f,           // 개행 딜레이 없음
        _ => charDelay           // 기본값
    };
}
```

### 상황별 속도 변경

```csharp
[SerializeField] private float excitedDelay = 0.02f;  // 흥분
[SerializeField] private float calmDelay = 0.07f;     // 차분

private float _currentDelayOverride = -1f;

public void SetTypewriterSpeed(float delay)
{
    _currentDelayOverride = delay;
}

// 호출 예시
// dialogueUI.SetTypewriterSpeed(0.02f);  // 빠르게
// dialogueUI.SetTypewriterSpeed(0.07f);  // 느리게
```

---

## ⚠️ 주의사항

1. **SDF 폰트 아틀라스 설정**
   - `Atlas Population Mode`: 반드시 **Static**
   - `Padding`: **10~15** 권장
   - `Atlas Resolution`: **4096×4096**
   - 자세한 설정: `docs/` 폰트 관련 문서 참고

2. **한글 처리**
   - 완성형 한글(U+AC00~U+D7AF) 기준, 한 음절 = 1char
   - 타자기 루프에서 정상 동작 확인 완료

3. **maxVisibleCharacters 주의점**
   - `text`를 설정한 **후**에 `maxVisibleCharacters`를 조절할 것
   - 설정 순서가 바뀌면 의도치 않은 출력 발생 가능

---

## 📂 파일 위치

```
Assets/
├── Scripts/
│   └── UI/
│       └── DialogueUIAdapter.cs    ← 타자기 구현체
└── TextMesh Pro/
    ├── Fonts/
    │   └── BMKkubulim.otf          ← 원본 폰트
    └── BMKkubulim SDF.asset        ← TMP SDF 폰트 에셋
docs/
└── DialogueUI_Typewriter_Guide.md  ← 본 문서
```

---

*이 문서는 별거 아니니까 기억하지 마.*
