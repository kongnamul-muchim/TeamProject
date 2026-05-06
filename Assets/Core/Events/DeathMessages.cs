using System.Collections.Generic;
using UnityEngine;

namespace HideAndInk.Core.Events
{
    /// <summary>
    /// 사망 원인별 멘트 데이터베이스
    /// 각 DeathCause에 1~2개의 한국어 사망 멘트를 제공
    /// </summary>
    public static class DeathMessages
    {
        private static readonly Dictionary<DeathCause, string[]> Messages = new()
        {
            // ===== 일반 적 =====
            [DeathCause.CrabAttack] = new[]
            {
                "게의 집게발에 끌려들어갔습니다...",
                "hard-shelled creature에게 잡아먹혔습니다...",
            },
            [DeathCause.EliteAttack] = new[]
            {
                "정예 몬스터의 강력한 공격에 쓰러졌습니다...",
                "상대를 얕봤나 봅니다...",
            },

            // ===== 보스 =====
            [DeathCause.GajamiDash] = new[]
            {
                "가자미의 기습에 당했습니다...",
                "모래 속에 숨은 적을 보지 못했습니다...",
            },
            [DeathCause.MorayCharge] = new[]
            {
                "곰치의 돌진에 휩쓸렸습니다...",
                "끝없는 추격, 결국 잡히고 말았습니다...",
            },
            [DeathCause.SwordfishCharge] = new[]
            {
                "청새치의 일격에 꿰뚫렸습니다...",
                "눈앞에서 번개一样的 돌진을 피하지 못했습니다...",
            },
            [DeathCause.GreatWhiteCharge] = new[]
            {
                "백상아리의 돌진에 산산조각났습니다...",
                "거대한 포식자 앞에서는 도망칠 수 없었습니다...",
            },
            [DeathCause.CamouflageObstacleDestroyed] = new[]
            {
                "금속과 함께 하나의 식사가 되어버렸습니다...",
                "의태한 오브젝트가 파괴되면서 함께 사라졌습니다...",
            },
            [DeathCause.BossCollision] = new[]
            {
                "보스의 공격에 쓰러졌습니다...",
                "강력한 적의 일격을 견디지 못했습니다...",
            },

            // ===== 환경 =====
            [DeathCause.Drowning] = new[]
            {
                "깊은 바닷속으로 가라앉았습니다...",
                "맵 밖으로 떨어져 버렸습니다...",
            },
            [DeathCause.Poisoned] = new[]
            {
                "독에 중독되었습니다...",
                "독성 해류에 휩쓸렸습니다...",
            },

            // ===== 시스템 =====
            [DeathCause.Debug] = new[]
            {
                "[디버그 모드] 개발자에게 잡혔습니다...",
            },
            [DeathCause.Unknown] = new[]
            {
                "알 수 없는 이유로 사망했습니다...",
                "무언가 잘못되었습니다...",
            },
        };

        /// <summary>
        /// 사망 원인에 해당하는 랜덤 멘트 반환
        /// </summary>
        public static string GetRandom(DeathCause cause)
        {
            if (Messages.TryGetValue(cause, out var messages) && messages.Length > 0)
            {
                return messages[Random.Range(0, messages.Length)];
            }
            return "사망했습니다...";
        }

        /// <summary>
        /// 사망 원인에 해당하는 첫 번째 멘트 반환 (일관된 테스트용)
        /// </summary>
        public static string GetFirst(DeathCause cause)
        {
            if (Messages.TryGetValue(cause, out var messages) && messages.Length > 0)
            {
                return messages[0];
            }
            return "사망했습니다...";
        }

        /// <summary>
        /// 특정 사망 원인의 멘트 개수
        /// </summary>
        public static int GetMessageCount(DeathCause cause)
        {
            return Messages.TryGetValue(cause, out var messages) ? messages.Length : 1;
        }
    }
}
