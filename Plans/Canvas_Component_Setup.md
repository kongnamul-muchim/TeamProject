# Canvas_Ingame 컴포넌트 연결 가이드

> 기준: art/micho 브랜치의 Canvas_Ingame.prefab
> Unity에서 `Assets/Prefabs/UI/Canvas_Ingame.prefab` 열고 작업

---

## 1. Canvas_Ingame (루트) — PauseHandler 추가

### Inspector 경로
```
Canvas_Ingame (선택)
  → Add Component → PauseHandler
```

### 할당할 필드
| 필드 | 값 |
|------|-----|
| `Pause Popup` | `Popup_Pause` (하위 오브젝트) 끌어다 놓기 |

---

## 2. HP_Container — PlayerHPUI (이미 있음, 확인만)

### Inspector 경로
```
Canvas_Ingame > Panel_PlayerInfo > HP_Container (선택)
```

### 자동 할당 확인
| 필드 | 값 | 상태 |
|------|-----|------|
| `Player Lives` | 비워둠 (런타임 Instance 자동찾음) | ✅ |
| `Heart Images` | Size=3, 각각 하트 이미지 연결됨 | ✅ |
| `Blink Duration` | 1 | ✅ |
| `Blink Frequency` | 10 | ✅ |

---

## 3. InkGauge_Frame — InkGaugeUI 추가

### Inspector 경로
```
Canvas_Ingame > Panel_PlayerInfo > InkGauge_Frame (선택)
  → Add Component → InkGaugeUI
```

### 할당할 필드
| 필드 | 값 |
|------|-----|
| `Player Ink` | 비워둠 (런타임 Instance 자동찾음) |
| `Ink Fill Image` | 자식 `InkGauge_Mask > InkGauge_Fill` 끌어다 놓기 |

---

## 4. Popup_Pause — SettingsPopup 추가

### Inspector 경로
```
Canvas_Ingame > Popup_Pause (선택)
  → Add Component → SettingsPopup
```

### 할당할 필드
| 필드 | 값 |
|------|-----|
| `Title Scene Name` | `"Title"` (문자열 입력) |
| `Fade In Obj Prefab` | 비워둠 (기존처럼 FadeInObj 없이 동작) |
| `Use Scene Transition` | 체크 ✅ |

---

## 5. GameObject (SuspicionMeterUI) — SuspicionMeterUI

### Inspector 경로
```
Canvas_Ingame > GameObject (선택)
```

### 현재 상태 확인
| 필드 | 값 | 상태 |
|------|-----|------|
| `Boss Suspicion System` | `None` | ✅ (비워둠) |
| `Suspicion Fill Image` | `UI_SuspicionVinette` | ✅ (이미 연결됨) |
| `Suspicion Text` | `Text (Legacy)` | ✅ (art/micho 추가) |

---

## 6. 버튼 onClick 다시 연결

### Btn_Pause
```
Canvas_Ingame > Btn_Pause (선택)
  → Button 컴포넌트의 OnClick()
  → + 버튼 클릭
  → 오브젝트: Canvas_Ingame (PauseHandler 있는 곳)
  → 함수: PauseHandler → TogglePause()
```

### Btn_Close (일시정지 창 닫기)
```
Canvas_Ingame > Popup_Pause > Btn_Close (선택)
  → Button 컴포넌트의 OnClick()
  → + 버튼 클릭
  → 오브젝트: Canvas_Ingame (PauseHandler 있는 곳)
  → 함수: PauseHandler → ResumeGame()
```

### Btn_GoTitle (팝업 내 타이틀 이동)
```
Canvas_Ingame > Popup_Pause > Btn_GoTitle (선택)
  → Button 컴포넌트의 OnClick()
  → + 버튼 클릭
  → 오브젝트: Popup_Pause (SettingsPopup 있는 곳)
  → 함수: SettingsPopup → OnGoTitleClicked()
```

### BGM_Slider
```
Canvas_Ingame > Popup_Pause > BGM_Slider (선택)
  → Slider 컴포넌트의 OnValueChanged()
  → + 버튼 클릭
  → 오브젝트: Popup_Pause (SettingsPopup 있는 곳)
  → 함수: SettingsPopup → SetBgmVolume(float)
```

### FX_Slider
```
Canvas_Ingame > Popup_Pause > FX_Slider (선택)
  → Slider 컴포넌트의 OnValueChanged()
  → + 버튼 클릭
  → 오브젝트: Popup_Pause (SettingsPopup 있는 곳)
  → 함수: SettingsPopup → SetSfxVolume(float)
```

### BGMToggle
```
Canvas_Ingame > Popup_Pause > BGMToggle (선택)
  → Toggle 컴포넌트의 OnValueChanged()
  → + 버튼 클릭
  → 오브젝트: Popup_Pause (SettingsPopup 있는 곳)
  → 함수: SettingsPopup → SetBgmMute(bool)
```

### FXToggle
```
Canvas_Ingame > Popup_Pause > FXToggle (선택)
  → Toggle 컴포넌트의 OnValueChanged()
  → + 버튼 클릭
  → 오브젝트: Popup_Pause (SettingsPopup 있는 곳)
  → 함수: SettingsPopup → SetSfxMute(bool)
```

---

## 7. GameOverUI (우리가 새로 만든 거)

### Inspector 경로
```
Canvas_Ingame (선택)
  → GameOverUI 컴포넌트 (이미 있음)
```

### 자동 할당 확인
| 필드 | 값 | 상태 |
|------|-----|------|
| `Game Over Panel` | `Popup_GameOver` (자동찾음) | ✅ |
| `Log Text` | `Text_GameOverLog` (자동찾음) | ✅ |
| `Continue Button` | `Panel > Btn_Continue` (자동찾음) | ✅ |
| `Restart Button` | `Panel > Btn_Restart` (자동찾음) | ✅ |
| `Title Button` | `Panel > Btn_GoTitle` (자동찾음) | ✅ |

---

## 전체 계층 구조 (완성 후)

```
Canvas_Ingame (루트)
├── [Components: CanvasScaler, GraphicRaycaster, PauseHandler, GameOverUI]
│
├── GameObject (SuspicionMeterUI)
├── Panel_PlayerInfo
│   ├── ico_Ink
│   ├── InkGauge_Frame [InkGaugeUI]
│   │   └── InkGauge_Mask > InkGauge_Fill
│   ├── Text_hp
│   └── HP_Container [PlayerHPUI]
│       ├── ico_Heart_1
│       ├── ico_Heart_2
│       └── ico_Heart_3
│
├── Btn_Pause [Button → PauseHandler.TogglePause()]
├── UI_SuspicionVinette
├── Popup_GameOver [게임오버 화면]
│   ├── Spr_GameOver
│   ├── Text_GameOverLog
│   └── Panel
│       ├── Btn_Continue
│       ├── Btn_Restart
│       └── Btn_GoTitle
│
└── Popup_Pause [SettingsPopup]
    ├── backgroundDimmer
    ├── Panel
    ├── Btn_Close [Button → PauseHandler.ResumeGame()]
    ├── Btn_GoTitle [Button → SettingsPopup.OnGoTitleClicked()]
    ├── PopupTitle ("일시 정지")
    ├── BGMToggle [Toggle → SettingsPopup.SetBgmMute()]
    ├── FXToggle [Toggle → SettingsPopup.SetSfxMute()]
    ├── BGM_Slider [Slider → SettingsPopup.SetBgmVolume()]
    └── FX_Slider [Slider → SettingsPopup.SetSfxVolume()]
```
