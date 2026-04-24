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

        // 의심도 설정 (단일 거리 기반)
        private readonly Vector2 _suspicionRadius;
        private readonly float _suspicionRate;
        private readonly float _curveExponent;

        // 이벤트
        public event Action<float, float> OnSuspicionIncrease;

        public AmbushSuspicionModule(AmbushGimmick gimmick)
        {
            _gimmick = gimmick;
            _suspicionRadius = gimmick.SuspicionRadius;
            _suspicionRate = gimmick.SuspicionRate;
            _curveExponent = gimmick.SuspicionCurveExponent;
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
        /// 의심도 계산 및 상승 (단일 거리 기반 커브)
        /// 거리에 따라 지수 함수로 의심도 상승률 조정 (중심에 가까울수록 급격히 상승)
        /// </summary>
        private void CalculateAndRaiseSuspicion(float deltaTime, Vector3 bossPos, Vector3 playerPos)
        {
            Vector3 delta = playerPos - bossPos;
            float absX = Mathf.Abs(delta.x);
            float absZ = Mathf.Abs(delta.z);

            // 0 나누기 방어
            float radiusX = Mathf.Max(_suspicionRadius.x, Mathf.Epsilon);
            float radiusZ = Mathf.Max(_suspicionRadius.y, Mathf.Epsilon);

            // 타원형 정규화 거리 계산: sqrt((x/rx)² + (z/rz)²)
            float normalizedDist = Mathf.Sqrt(Mathf.Pow(absX / radiusX, 2) + Mathf.Pow(absZ / radiusZ, 2));

            // 최대 거리 밖이면 무시
            if (normalizedDist > 1.0f) return;

            // 거리 계수: 중심(1.0) → 경계(0.0)
            float distanceFactor = 1f - normalizedDist;

            // 지수 커브 적용: 중심에 가까울수록 급격히 상승
            // curveExponent=2: 2차 곡선, curveExponent=3: 3차 곡선
            float curveFactor = Mathf.Pow(distanceFactor, _curveExponent);

            // 최종 의심도 상승률
            float weightedRate = _suspicionRate * curveFactor;

            OnSuspicionIncrease?.Invoke(weightedRate, deltaTime);
        }
    }
}
