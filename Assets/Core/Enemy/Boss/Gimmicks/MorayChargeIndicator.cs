using System.Collections.Generic;
using UnityEngine;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// 곰치 돌진 경로를 표시하는 붉은 네모(Mesh 평면) 관리자
    /// MeshRenderer 기반으로 카메라 각도에서도 잘 보임
    /// 순차 생성 → 순차 소멸 → 임박 색상
    /// 오브젝트 풀링 적용 (반복적인 Instantiate/Destroy 방지)
    /// </summary>
    public class MorayChargeIndicator : MonoBehaviour
    {
        [Header("네모 설정")]
        [SerializeField] private float indicatorWidth = 2.5f;
        [SerializeField] private Color activeColor = new Color(1f, 0.2f, 0.2f, 0.6f);
        [SerializeField] private Color imminentColor = new Color(1f, 0f, 0f, 0.9f);
        [SerializeField] private float heightOffset = 0.2f; // 바닥보다 약간 위
        [SerializeField] private int poolSize = 5; // 최대 동시 네모 수

        // Director가 값을 적용할 수 있도록 public setter (중복 설정 방지)
        public float Width { set => indicatorWidth = value; }
        public Color ActiveColor { set => activeColor = value; }
        public Color ImminentColor { set => imminentColor = value; }

        private struct ChargePath
        {
            public Vector3 start;
            public Vector3 end;
            public GameObject gameObject;
            public MeshRenderer renderer;
            public MeshFilter filter;
            public bool isActive;
        }

        private List<ChargePath> _paths = new List<ChargePath>();
        private Queue<GameObject> _pool = new Queue<GameObject>();
        private Material _baseMaterial;

        private void Awake()
        {
            Shader shader = Shader.Find("Custom/VertexColorUnlitTransparent");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            _baseMaterial = new Material(shader);
            if (_baseMaterial != null)
            {
                _baseMaterial.color = activeColor;
                _baseMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }

            // 미리 poolSize만큼 오브젝트 생성
            for (int i = 0; i < poolSize; i++)
            {
                CreatePoolObject();
            }
        }

        private GameObject CreatePoolObject()
        {
            GameObject go = new GameObject("ChargePath_Pooled");
            go.transform.SetParent(null);
            go.layer = gameObject.layer;
            go.SetActive(false);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            // 풀용 기본 메쉬 (빈 메쉬, Spawn 시 업데이트)
            Mesh mesh = new Mesh();
            mesh.name = "ChargeQuad_Pooled";

            // 최소 크기 4각형 (Spawn 시 재설정)
            Vector3[] vertices = new Vector3[4];
            vertices[0] = Vector3.zero;
            vertices[1] = Vector3.zero;
            vertices[2] = Vector3.zero;
            vertices[3] = Vector3.zero;
            mesh.vertices = vertices;
            mesh.triangles = new int[6] { 0, 1, 2, 0, 2, 3 };
            mesh.colors = new Color[4] { activeColor, activeColor, activeColor, activeColor };
            mesh.RecalculateNormals();
            mf.mesh = mesh;

            mr.material = _baseMaterial != null ? Instantiate(_baseMaterial) : null;
            if (mr.material != null) mr.material.color = activeColor;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.sortingLayerName = "Default";
            mr.sortingOrder = -1;

            _pool.Enqueue(go);
            return go;
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0)
            {
                GameObject go = _pool.Dequeue();
                go.SetActive(true);
                return go;
            }
            // 풀 부족 시 새로 생성 (확장)
            return CreatePoolObject();
        }

        private void ReturnToPool(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            _pool.Enqueue(go);
        }

        /// <summary>
        /// 돌진 경로 네모 생성 (Mesh 평면) — 풀에서 재사용
        /// </summary>
        public int SpawnIndicator(Vector3 start, Vector3 end)
        {
            GameObject go = GetFromPool();
            go.transform.SetParent(null);
            go.layer = gameObject.layer;

            MeshFilter mf = go.GetComponent<MeshFilter>();
            MeshRenderer mr = go.GetComponent<MeshRenderer>();

            Mesh mesh = mf.mesh;
            if (mesh == null)
            {
                mesh = new Mesh();
                mf.mesh = mesh;
            }

            UpdateMeshGeometry(mesh, start, end);

            if (mr.material != null) mr.material.color = activeColor;

            ChargePath path = new ChargePath
            {
                start = start,
                end = end,
                gameObject = go,
                renderer = mr,
                filter = mf,
                isActive = true
            };

            _paths.Add(path);
            return _paths.Count - 1;
        }

        private void UpdateMeshGeometry(Mesh mesh, Vector3 start, Vector3 end)
        {
            Vector3 dir = (end - start).normalized;
            Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
            float halfW = indicatorWidth * 0.5f;
            float h = heightOffset;

            Vector3[] vertices = new Vector3[4];
            vertices[0] = start + perp * halfW + Vector3.up * h;
            vertices[1] = start - perp * halfW + Vector3.up * h;
            vertices[2] = end - perp * halfW + Vector3.up * h;
            vertices[3] = end + perp * halfW + Vector3.up * h;
            mesh.vertices = vertices;

            mesh.triangles = new int[6] { 0, 1, 2, 0, 2, 3 };

            Color[] colors = new Color[4]
            {
                activeColor, activeColor, activeColor, activeColor
            };
            mesh.colors = colors;

            mesh.RecalculateNormals();
        }

        /// <summary>
        /// 지정한 인덱스의 네모 소멸 (풀로 반환)
        /// </summary>
        public void DespawnIndicator(int index)
        {
            if (index < 0 || index >= _paths.Count) return;
            if (!_paths[index].isActive) return;

            ChargePath path = _paths[index];
            ReturnToPool(path.gameObject);
            path.isActive = false;
            _paths[index] = path;
        }

        /// <summary>
        /// 지정한 인덱스의 네모 위치 업데이트 (동적 카메라)
        /// </summary>
        public void UpdateIndicator(int index, Vector3 start, Vector3 end)
        {
            if (index < 0 || index >= _paths.Count) return;
            if (!_paths[index].isActive) return;
            if (_paths[index].filter == null) return;

            var path = _paths[index];
            path.start = start;
            path.end = end;

            Mesh mesh = path.filter.mesh;
            if (mesh == null)
            {
                mesh = new Mesh();
                path.filter.mesh = mesh;
            }

            UpdateMeshGeometry(mesh, start, end);
            _paths[index] = path;
        }

        /// <summary>
        /// 임박 색상 설정 (곧 돌진할 네모를 진하게)
        /// </summary>
        public void SetImminent(int chargeIndex)
        {
            for (int i = 0; i < _paths.Count; i++)
            {
                if (!_paths[i].isActive) continue;

                Color targetColor = (i == chargeIndex) ? imminentColor : activeColor;
                if (_paths[i].renderer != null && _paths[i].renderer.material != null)
                {
                    _paths[i].renderer.material.color = targetColor;
                }
                if (_paths[i].filter != null && _paths[i].filter.mesh != null)
                {
                    Color[] colors = new Color[4] { targetColor, targetColor, targetColor, targetColor };
                    _paths[i].filter.mesh.colors = colors;
                }
            }
        }

        /// <summary>
        /// 모든 네모 초기화 (전부 풀로 반환)
        /// </summary>
        public void ClearAll()
        {
            for (int i = 0; i < _paths.Count; i++)
            {
                if (_paths[i].gameObject != null)
                    ReturnToPool(_paths[i].gameObject);
            }
            _paths.Clear();
        }

        private void OnDestroy()
        {
            // 풀 포함 모든 오브젝트 정리
            foreach (var go in _pool)
            {
                if (go != null) Destroy(go);
            }
            _pool.Clear();

            for (int i = 0; i < _paths.Count; i++)
            {
                if (_paths[i].gameObject != null)
                    Destroy(_paths[i].gameObject);
            }
            _paths.Clear();

            if (_baseMaterial != null)
            {
                Destroy(_baseMaterial);
            }
        }
    }
}
