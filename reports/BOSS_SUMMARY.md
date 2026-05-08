# 보스 3종 요약

## Gajami (가자미) — Ch1, AmbushGimmick
모래 속에 숨었다가 Player가 근접(1.5m)하면 튀어나와 Dash → Pit 생성 → 다시 숨는 사이클.
Pit을 밟으면 의심도 +30, 이동속도 감소.
PreDelay → Dash → Rest 3단계 CombatCycle로 패턴화.

## Moray Eel (곰치) — Ch2, RelentlessChaseGimmick  
Patrol 중 Player 주변(8m)을 서성이며 의심도 자동 상승.
발각(100%) 시 Camera Viewport 기준 붉은 네모(Indicator)를 깔고 좌→우 돌진.
돌진 횟수는 Chase 진입 횟수에 비례 (1→2→3→...→5). 완료 후 의심도 리셋, Patrol 복귀.

## Swordfish (청새치) — Ch3, SwordfishGimmick  
Idle(선회) → Aiming(붉은 사각형 Indicator) → Charge(돌진) → Stunned(머리박고 기절) 사이클.
의심도가 높을수록 조준시간 짧아지고 돌진속도 빨라짐.
Player 놓치면 의심도 Safe 시 Patrol 직행, 아니면 Search.
