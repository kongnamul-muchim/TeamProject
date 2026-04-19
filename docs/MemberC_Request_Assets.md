# Member C 요청서: 의태 효과 에셋 전달

> **대상:** Member C (이펙트/사운드 담당)
> **담당:** Member A (통합 및 코드 작업)

---

## 📦 전달받아야 할 파일 (총 6개)

Member C는 **코딩 없이** 아래 에셋 파일만 준비해서 Member A에게 전달하면 됩니다.
Member A가 이를 받아서 게임 내 의태 시스템에 자동으로 연결합니다.

### 1. 파티클 프리팹 (3개)
의태 상태 변화 시 터질 파티클 효과입니다. Unity의 `.prefab` 파일로 전달해주세요.

| 파일명 (권장) | 효과 설명 | 재생 위치 |
|---------------|-----------|-----------|
| `Particle_Attach.prefab` | **의태 시작**: 잉크가 뿜어져 나오며 달라붙는 효과 | 타겟 오브젝트 위치 |
| `Particle_Perfect.prefab` | **완벽 의태**: 색상이 완전히 일치하며 안정화되는 효과 | 플레이어 위치 |
| `Particle_Detach.prefab` | **의태 해제**: 잉크가 벗겨지며 원래 모습으로 돌아오는 효과 | 플레이어 위치 |

### 2. 사운드 파일 (3개)
의태 상태 변화 시 재생될 효과음입니다. `.wav` 또는 `.mp3` 파일로 전달해주세요.

| 파일명 (권장) | 효과 설명 |
|---------------|-----------|
| `SFX_Attach.wav` | **의태 시작**: "슈웅~" (부착음) |
| `SFX_Perfect.wav` | **완벽 의태**: "띵!" (성공/안정화음) |
| `SFX_Detach.wav` | **의태 해제**: "빠빅!" (분리음) |

---

## 📂 전달 형식

아래와 같이 폴더를 만들어서 zip으로 압축하거나, Unity 패키지 형태로 던져주세요.

```
MemberC_Effects/
├── Particles/
│   ├── Particle_Attach.prefab
│   ├── Particle_Perfect.prefab
│   └── Particle_Detach.prefab
└── Sounds/
    ├── SFX_Attach.wav
    ├── SFX_Perfect.wav
    └── SFX_Detach.wav
```

---

## ⚠️ 체크리스트

- [ ] 파티클은 **Looping**이 꺼져 있는지 확인 (한 번 재생 후 사라져야 함)
- [ ] 파티클의 **Start Lifetime**이 적절한지 확인 (너무 길거나 짧지 않게)
- [ ] 사운드 파일의 볼륨이 적절한지 확인 (너무 크거나 작지 않게)
- [ ] 파일명에 **공백**이 없는지 확인 (예: `Particle Attach.prefab` ❌ → `Particle_Attach.prefab` ✅)

---

## 💡 참고

- Member A가 받은 파일을 Unity 프로젝트에 import하고, 코드 (`CamouflageEventBridge`)에서 자동으로 호출하도록 처리합니다.
- 추가적인 효과 (예: 셰이더 번짐 등)가 필요하면 Member A와 상의하세요. (현재는 파티클/사운드만 적용 예정)

---

> **문의:** Member A (코어 프로그래머)
> **작성일:** 2026-04-17
