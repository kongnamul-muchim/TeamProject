using UnityEngine;
using System;
using HideAndInk.Core.Enemy.Boss.Gimmicks;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 가자미(Ambush) 기믹 전용 의심도 계산 모듈
    /// 거리 기반 2단계 의심도 상승 (근접/원거리)
    /// </summary>
    public class AmbushSuspicionModule : ISuspicionModule
    {
        public string ModuleName => "AmbushSuspicion";

        private readonly AmbushGimmick _gimmick;

        // 의심도 설정 (가자미 기믹에서 가져옴)
        private readonly Vector2 _farSuspicionRadius;
        private readonly Vector2 _nearSuspicionRadius;
        private readonly float _farSuspicionRate;
        private readonly float _nearSuspicionRate;
        private readonly bool _lockZAxis;

        // 이벤트
        public event Action<float, float> OnSuspicionIncrease;

        public AmbushSuspicionModule(AmbushGimmick gimmick)
        {
            _gimmick = gimmick;
            _farSuspicionRadius = gimmick.FarSuspicionRadius;
            _nearSuspicionRadius = gimmick.NearSuspicionRadius;
            _farSuspicionRate = gimmick.FarSuspicionRate;
            _nearSuspicionRate = gimmick.NearSuspicionRate;
            _lockZAxis = gimmick.LockZAxis;
        }

        public void OnActivate()
        {
            // 활성화 시 초기화 작업 (필요시)
        }

        public void OnDeactivate()
        {
            OnSuspicionIncrease = null;
        }

        public void Update(float deltaTime, Vector3 bossPos, Vector3? playerPos)
        {
            if (!playerPos.HasValue) return;

            CalculateAndRaiseSuspicion(deltaTime, bossPos, playerPos.Value);
        }

        public float GetCurrentRate()
        {
            // 현재 상승률은 거리 기반이므로 고정값 반환 불가
            // 실제 계산은 Update에서 수행
            return 0f;
        }

        /// <summary>
        /// 의심도 계산 및 상승 (AmbushGimmick.UpdateSuspicion 로직 추출)
        /// </summary>
        private void CalculateAndRaiseSuspicion(float deltaTime, Vector3 bossPos, Vector3 playerPos)
        {
            Vector3 delta = playerPos - bossPos;
            float absX = Mathf.Abs(delta.x);
            float absZ = Mathf.Abs(delta.z);

            // 0 나누기 방어
            float nearX = Mathf.Max(_nearSuspicionRadius.x, Mathf.Epsilon);
            float farX = Mathf.Max(_farSuspicionRadius.x, Mathf.Epsilon);

            // 의심도 계산은 항상 X/Z 축 거리를 모두 고려 (lockZAxis는 이동 전용)
            float checkZ = absZ;
            float farZ = Mathf.Max(_farSuspicionRadius.y, Mathf.Epsilon);
            float nearZ = Mathf.Max(_nearSuspicionRadius.y, Mathf.Epsilon);

            // 근접 범위 체크 (직사각형)
            bool inNearZone = absX <= nearX && checkZ <= nearZ;
            // 원거리 범위 체크 (직사각형)
            bool inFarZone = absX <= farX && checkZ <= farZ;

            if (inNearZone)
            {
                // 거리 가중치: 중심 1.0 → 가장자리 0.3
                float xFactor = 1f - (absX / nearX);
                float zFactor = 1f - (checkZ / nearZ);
                float distanceFactor = Mathf.Min(xFactor, zFactor);
                float weightedRate = _nearSuspicionRate * Mathf.Lerp(0.3f, 1f, distanceFactor);

                OnSuspicionIncrease?.Invoke(weightedRate, deltaTime);
            }
            else if (inFarZone)
            {
                // 거리 가중치: 중심 1.0 → 가장자리 0.2
                float xFactor = 1f - (absX / farX);
                float zFactor = 1f - (checkZ / farZ);
                float distanceFactor = Mathf.Min(xFactor, zFactor);
                float weightedRate = _farSuspicionRate * Mathf.Lerp(0.2f, 1f, distanceFactor);

                OnSuspicionIncrease?.Invoke(weightedRate, deltaTime);
            }
            // 범위 밖이면 의심도 상승 없음 (자연 하락에 맡김)
        }
    }
}
