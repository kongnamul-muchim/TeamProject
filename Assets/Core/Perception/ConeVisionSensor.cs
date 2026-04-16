using UnityEngine;
using System.Collections.Generic;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 부채꼴 시야 감지 시스템 구현체
    /// </summary>
    public sealed class ConeVisionSensor : MonoBehaviour, IVisionSensor
    {
        [Header("시야 설정")]
        [SerializeField] private float viewRadius = 5f;
        [SerializeField] private float viewAngle = 60f;
        [SerializeField] private VisionPatternType patternType = VisionPatternType.Patrol;

        [Header("레이어 설정")]
        [SerializeField] private LayerMask targetLayer = -1;          // 감지 대상 레이어
        [SerializeField] private LayerMask obstacleLayer = -1;        // 장애물 레이어

        [Header("시야 방향 (기본값: 전방)")]
        [SerializeField] private Transform viewDirectionRef;          // 시야 방향 기준 (없으면 자신 전방)
        [SerializeField] private Vector3 customViewDirection = Vector3.forward;

        // 캐싱
        private Vector3 _cachedOrigin;
        private float _cachedViewRadius;
        private float _cachedViewAngle;

        /// <summary>
        /// 시야 감지 원점
        /// </summary>
        public Vector3 Origin => transform.position;

        /// <summary>
        /// 시야 반경
        /// </summary>
        public float ViewRadius => viewRadius;

        /// <summary>
        /// 시야 각도 (도 단위)
        /// </summary>
        public float ViewAngle => viewAngle;

        /// <summary>
        /// 시야 패턴 유형
        /// </summary>
        public VisionPatternType PatternType => patternType;

        private void OnValidate()
        {
            // 패턴별 기본값 설정
            switch (patternType)
            {
                case VisionPatternType.Patrol:
                    if (viewAngle <= 0) viewAngle = 45f;
                    if (viewRadius <= 0) viewRadius = 4f;
                    break;
                case VisionPatternType.Observe:
                    if (viewAngle <= 0) viewAngle = 90f;
                    if (viewRadius <= 0) viewRadius = 6f;
                    break;
                case VisionPatternType.Guard:
                    if (viewAngle <= 0) viewAngle = 120f;
                    if (viewRadius <= 0) viewRadius = 5f;
                    break;
            }
        }

        /// <summary>
        /// 시야 방향 가져오기
        /// </summary>
        public Vector3 GetViewDirection()
        {
            if (viewDirectionRef != null)
            {
                return viewDirectionRef.forward;
            }
            return customViewDirection.normalized;
        }

        /// <summary>
        /// 특정 대상이 시야 내에 있는지 확인
        /// </summary>
        public bool CanSee(GameObject target)
        {
            if (target == null) return false;

            Vector3 targetPosition = target.transform.position;
            Vector3 directionToTarget = targetPosition - Origin;
            float distanceToTarget = directionToTarget.magnitude;

            // 거리 체크
            if (distanceToTarget > viewRadius)
            {
                return false;
            }

            // 각도 체크
            if (!IsWithinViewAngle(targetPosition))
            {
                return false;
            }

            // 장애물 체크
            if (IsBlockedByObstacle(targetPosition))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 시야 내 모든 감지 가능한 대상 가져오기
        /// </summary>
        public List<GameObject> GetAllVisibleTargets()
        {
            var visibleTargets = new List<GameObject>();

            // 레이어 마스크范围内的 모든 Collider 가져오기
            Collider[] colliders = Physics.OverlapSphere(Origin, viewRadius, targetLayer);

            foreach (var collider in colliders)
            {
                // 자기 자신 제외
                if (collider.gameObject == gameObject) continue;

                if (CanSee(collider.gameObject))
                {
                    visibleTargets.Add(collider.gameObject);
                }
            }

            return visibleTargets;
        }

        /// <summary>
        /// 대상이 시야 각도 내에 있는지 확인
        /// </summary>
        public bool IsWithinViewAngle(Vector3 targetPosition)
        {
            Vector3 directionToTarget = (targetPosition - Origin).normalized;
            Vector3 viewDirection = GetViewDirection();

            // 내적 계산으로 각도 구하기
            float dot = Vector3.Dot(viewDirection, directionToTarget);
            float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;

            return angle <= (viewAngle / 2f);
        }

        /// <summary>
        /// 대상까지의 시야가 가려졌는지 확인 (Raycast로 장애물 체크)
        /// </summary>
        public bool IsBlockedByObstacle(Vector3 targetPosition)
        {
            Vector3 directionToTarget = (targetPosition - Origin).normalized;
            float distanceToTarget = Vector3.Distance(Origin, targetPosition);

            // Raycast로 장애물 감지
            if (Physics.Raycast(Origin, directionToTarget, out RaycastHit hit, distanceToTarget, obstacleLayer))
            {
                // 히트 지점이 목표보다 가까우면 장애물 있음
                return hit.distance < distanceToTarget - 0.1f;
            }

            return false;
        }

        /// <summary>
        /// 디버그 시야 표시
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector3 viewDir = GetViewDirection();
            Vector3 leftDir = Quaternion.Euler(0, -viewAngle / 2f, 0) * viewDir;
            Vector3 rightDir = Quaternion.Euler(0, viewAngle / 2f, 0) * viewDir;

            // 시야 범위 표시
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawFrustum(Origin, viewAngle, viewRadius, 0f, 1f);

            // 시야 방향 표시
            Gizmos.color = Color.red;
            Gizmos.DrawRay(Origin, viewDir * viewRadius);

            // 왼쪽/오른쪽 경계선
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(Origin, leftDir * viewRadius);
            Gizmos.DrawRay(Origin, rightDir * viewRadius);
        }
    }
}
