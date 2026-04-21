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
        [Tooltip("시야 감지 반경 (미터). 이 거리 내의 대상만 감지")]
        [SerializeField] private float viewRadius = 5f;
        [Tooltip("시야 각도 (도). 부채꼴의 너비")]
        [SerializeField] private float viewAngle = 60f;
        [Tooltip("시야 패턴 유형: Patrol(순찰), Observe(관찰), Guard(경계)")]
        [SerializeField] private VisionPatternType patternType = VisionPatternType.Patrol;

        [Header("레이어 설정")]
        [Tooltip("감지 대상 레이어 (Player 등 감지할 오브젝트가 속한 레이어)")]
        [SerializeField] private LayerMask targetLayer = -1;
        [Tooltip("장애물 레이어 (시야를 가리는 오브젝트가 속한 레이어)")]
        [SerializeField] private LayerMask obstacleLayer = -1;

        [Header("시야 방향 (기본값: 전방)")]
        [Tooltip("시야 방향 기준 Transform. 지정하면 해당 오브젝트의 forward 방향 사용")]
        [SerializeField] private Transform viewDirectionRef;
        [Tooltip("viewDirectionRef가 없을 때 사용할 커스텀 시야 방향")]
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
            Vector3 dir = customViewDirection.normalized;
            // zero vector 방지
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.forward;
            }
            return dir;
        }

        /// <summary>
        /// 시야 방향에 수직인 '위' 참조 벡터 계산
        /// 어떤 방향이든 안정적인 부채꼴 생성을 위해 사용
        /// </summary>
        public Vector3 GetViewUpReference()
        {
            Vector3 dir = GetViewDirection().normalized;
            float absX = Mathf.Abs(Vector3.Dot(dir, Vector3.right));
            float absY = Mathf.Abs(Vector3.Dot(dir, Vector3.up));
            float absZ = Mathf.Abs(Vector3.Dot(dir, Vector3.forward));

            if (absY < absX && absY < absZ)
                return Vector3.up;
            else if (absX < absZ)
                return Vector3.right;
            else
                return Vector3.forward;
        }

        /// <summary>
        /// 시야 부채꼴의 가장자리 방향 계산 (3D)
        /// </summary>
        public Vector3 GetConeEdgeDirection(float azimuthAngle)
        {
            Vector3 viewDir = GetViewDirection().normalized;
            Vector3 upRef = GetViewUpReference();

            // viewDirection에 수직인 기준 벡터
            Vector3 refPerp = Vector3.Cross(viewDir, upRef).normalized;
            if (refPerp.sqrMagnitude < 0.001f)
            {
                upRef = GetViewUpReference();
                refPerp = Vector3.Cross(viewDir, upRef).normalized;
            }

            // 기준 벡터를 viewDirection 축으로 azimuthAngle만큼 회전
            Vector3 rotatedPerp = Quaternion.AngleAxis(azimuthAngle, viewDir) * refPerp;

            // viewDirection에서 rotatedPerp 방향으로 halfAngle만큼 기울이기
            float halfAngleRad = (viewAngle / 2f) * Mathf.Deg2Rad;
            Vector3 edgeDir = Mathf.Cos(halfAngleRad) * viewDir + Mathf.Sin(halfAngleRad) * rotatedPerp;

            return edgeDir.normalized;
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

            // 레이어 마스크 내의 모든 Collider 가져오기
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
            float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

            return angle <= (viewAngle / 2f);
        }

        /// <summary>
        /// 대상까지의 시야가 가려졌는지 확인 (Raycast로 장애물 체크)
        /// </summary>
        public bool IsBlockedByObstacle(Vector3 targetPosition)
        {
            Vector3 directionToTarget = (targetPosition - Origin).normalized;
            float distanceToTarget = Vector3.Distance(Origin, targetPosition);

            // 시작점을 약간 앞에서 시작 (자기 Collider Hit 방지)
            Vector3 rayStart = Origin + directionToTarget * 0.1f;
            float rayDistance = distanceToTarget - 0.1f;

            // Raycast로 장애물 감지
            if (Physics.Raycast(rayStart, directionToTarget, out RaycastHit hit, rayDistance, obstacleLayer))
            {
                // 히트한 게 자기 자신이 아니고, 목표보다 가깝다면 장애물 있음
                if (hit.collider.gameObject != gameObject && hit.distance < distanceToTarget - 0.1f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 디버그 시야 표시 (VisionConeRenderer와 동일한 방식으로)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector3 origin = Origin;
            Vector3 viewDir = GetViewDirection().normalized;
            float halfAngle = viewAngle / 2f;
            int debugSegments = 16;

            // 시야 중심 방향 표시 (빨강)
            Gizmos.color = Color.red;
            Gizmos.DrawRay(origin, viewDir * viewRadius);

            // 부채꼴 테두리 표시 (주황)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);

            Vector3 prevPoint = origin;
            for (int i = 0; i <= debugSegments; i++)
            {
                float azimuthAngle = -halfAngle + (viewAngle / debugSegments) * i;
                Vector3 edgeDir = GetConeEdgeDirection(azimuthAngle);
                Vector3 point = origin + edgeDir * viewRadius;

                Gizmos.DrawLine(origin, point);
                if (i > 0)
                {
                    Gizmos.DrawLine(prevPoint, point);
                }
                prevPoint = point;
            }
        }
    }
}
