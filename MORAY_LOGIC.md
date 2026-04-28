# 곰치 (Moray Eel) 로직

- **Patrol**: Player 근처 랜덤 위치로 서성임 (반경 8m, 2~5초마다 새 목표). Player 직접 추격은 안 함.
- **의심도 자동 상승**: 8f/s + zone 보너스 + Player 이동 시 추가 (12f/s).
- **의심도 100% → Chase**: `Animator.SetBool("IsChase", true)`. Director가 Prepare(네모) → N회 돌진 실행.
- **돌진 횟수**: 점진 증가 (1→2→3→...→5). `_morayChaseEntryCount`로 Controller가 직접 관리.
- **돌진 완료 → Patrol**: 의심도 리셋, `Animator.SetBool("IsChase", false)`. Patrol 복귀.
- **네모 Y**: `indicatorFloorY` (인스펙터 설정, Ground 표면 맞춤). 곰치는 자기 Y 유지.
