# 곰치 (Moray Eel) 로직

- **Patrol**: 랜덤 맵 배회, Player 추격 안 함. 의심도 자동 상승 (8f/s + zone + 이동).
- **의심도 100% → Chase**: `Animator.SetBool("IsChase", true)`.
- **Chase**: `MorayChargeDirector`가 Prepare(네모) → N회 돌진 실행. 돌진 횟수는 점진 증가 (1→2→3→...→5).
- **돌진 완료 → Patrol**: 의심도 리셋, `Animator.SetBool("IsChase", false)`. Patrol 복귀.
- **네모 Y**: `indicatorFloorY` (인스펙터 설정, Ground 표면 맞춤). 곰치는 자기 Y 유지.
