using System.Collections.Generic;
using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 탐지 시스템 구현체
    /// 반경 내 오브젝트 탐지 (OverlapSphere 사용)
    /// </summary>
    public sealed class CamouflageDetector : ICamouflageDetector
    {
        private const string CAMOUFLAGEABLE_TAG = "Camouflageable";
        private readonly float _detectionRadius;
        private LayerMask _layerMask;

        public float DetectionRadius => _detectionRadius;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="detectionRadius">탐지 반경 (기본값: 1.0f)</param>
        /// <param name="layerMask">탐지할 레이어 (기본값: Everything)</param>
        public CamouflageDetector(float detectionRadius = 1.0f)
        {
            _detectionRadius = detectionRadius;
            _layerMask = -1; // Everything
        }

        /// <summary>
        /// 반경 내 의태 가능 오브젝트들 탐지
        /// </summary>
        public List<GameObject> DetectCandidates(Vector3 position)
        {
            var results = new List<GameObject>();
            Collider[] colliders = Physics.OverlapSphere(position, _detectionRadius, _layerMask);

            foreach (var collider in colliders)
            {
                // 자기 자신 제외
                if (collider.gameObject.CompareTag("Player")) continue;

                // 의태 가능한 오브젝트인지 확인 (Tag 또는 Component로 구분)
                if (IsCamouflageable(collider.gameObject))
                {
                    results.Add(collider.gameObject);
                }
            }

            return results;
        }

        /// <summary>
        /// 가장 가까운 의태 가능 오브젝트 탐지
        /// </summary>
        public GameObject FindNearestCandidate(Vector3 position)
        {
            GameObject nearest = null;
            float nearestDistance = float.MaxValue;

            var candidates = DetectCandidates(position);

            foreach (var candidate in candidates)
            {
                float distance = Vector3.Distance(position, candidate.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 레이어 마스크 설정
        /// </summary>
        public void SetLayerMask(LayerMask layerMask)
        {
            _layerMask = layerMask;
        }

        /// <summary>
        /// 의태 가능한 오브젝트인지 확인
        /// </summary>
        private bool IsCamouflageable(GameObject obj)
        {
            return obj.CompareTag(CAMOUFLAGEABLE_TAG);
        }
    }
}