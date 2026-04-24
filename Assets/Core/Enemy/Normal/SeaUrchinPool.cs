using System.Collections.Generic;
using UnityEngine;
using HideAndInk.Core.Environment;

namespace HideAndInk.Core.Enemy.Normal
{
    /// <summary>
    /// 성게 오브젝트 풀 관리
    /// 조류 발동 시 카메라 밖에서 성게 소환
    /// </summary>
    public class SeaUrchinPool : MonoBehaviour
    {
        private GameObject _prefab;
        private int _poolSize;
        private Queue<SeaUrchinController> _pool;
        private Transform _parent;

        /// <summary>
        /// 풀 초기화
        /// </summary>
        public void Initialize(GameObject prefab, int poolSize, Transform parent)
        {
            _prefab = prefab;
            _poolSize = poolSize;
            _parent = parent;
            _pool = new Queue<SeaUrchinController>(poolSize);

            // 풀 미리 생성
            for (int i = 0; i < poolSize; i++)
            {
                var urchin = CreateUrchin();
                urchin.gameObject.SetActive(false);
                _pool.Enqueue(urchin);
            }
        }

        /// <summary>
        /// 조류 방향에서 성게 소환
        /// </summary>
        public void SpawnFromTide(TideDirection direction, float tideForce)
        {
            if (_pool.Count == 0)
            {
                // 풀이 비었으면 확장
                var urchin = CreateUrchin();
                urchin.transform.SetParent(null);
                urchin.SetupForTide(direction, tideForce);
                urchin.gameObject.SetActive(true);
                return;
            }

            var pooledUrchin = _pool.Dequeue();
            pooledUrchin.transform.SetParent(null);
            pooledUrchin.SetupForTide(direction, tideForce);
            pooledUrchin.gameObject.SetActive(true);
        }

        /// <summary>
        /// 성게를 풀로 반환
        /// </summary>
        public void Return(SeaUrchinController urchin)
        {
            urchin.ResetState();
            urchin.gameObject.SetActive(false);
            urchin.transform.SetParent(_parent);
            _pool.Enqueue(urchin);
        }

        /// <summary>
        /// 새 성게 생성
        /// </summary>
        private SeaUrchinController CreateUrchin()
        {
            var obj = Instantiate(_prefab, _parent);
            var controller = obj.GetComponent<SeaUrchinController>();

            if (controller == null)
            {
                controller = obj.AddComponent<SeaUrchinController>();
            }

            controller.SetPool(this);
            return controller;
        }
    }
}
