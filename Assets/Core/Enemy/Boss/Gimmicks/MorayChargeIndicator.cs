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
        [Tooltip("인디케이터 너비")]
        [SerializeField] private float indicatorWidth = 2.5f;
        [Tooltip("인디케이터 높이 (박스 두께)")]
        [SerializeField] private float indicatorHeight = 0.5f;
        [Tooltip("Player가 구역 밖일 때 색상 (연붉은색)")]
        [SerializeField] private Color safeColor = new Color(1f, 0.3f, 0.3f, 0.5f);
        [Tooltip("Player가 구역 안일 때 색상 (붉은색)")]
        [SerializeField] private Color dangerColor = new Color(1f, 0f, 0f, 0.85f);
        [Tooltip("임박 색상 (곧 돌진)")]
        [SerializeField] private Color imminentColor = new Color(1f, 0f, 0f, 1f);
        [Tooltip("오브젝트 풀 크기")]
        [SerializeField] private int poolSize = 5;

        // Director가 값을 적용할 수 있도록 public setter
        public float Width { set => indicatorWidth = value; }
        public float Height { set => indicatorHeight = value; }
        public Color SafeColor { set => safeColor = value; }
        public Color DangerColor { set => dangerColor = value; }
        public Color ImminentColor { set => imminentColor = value; }

        private enum IndicatorState
        {
            Safe,
            Danger,
            Imminent
        }

        private struct ChargePath
        {
            public Vector3 start;
            public Vector3 end;
            public GameObject gameObject;
            public MeshRenderer renderer;
            public MeshFilter filter;
            public bool isActive;
            public IndicatorState state;
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
                _baseMaterial.color = safeColor;
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

            // 풀용 기본 메쉬 (Spawn 시 UpdateMeshGeometryBox로 재설정)
            Mesh mesh = new Mesh();
            mesh.name = "ChargeBox_Pooled";

            // 8개 버텍스 (Box 메쉬), Spawn 시점에 실제 크기로 업데이트
            Vector3[] vertices = new Vector3[8];
            for (int vi = 0; vi < 8; vi++) vertices[vi] = Vector3.zero;
            mesh.vertices = vertices;
            mesh.triangles = new int[36] { 0,0,0,0,0,0, 0,0,0,0,0,0, 0,0,0,0,0,0, 0,0,0,0,0,0, 0,0,0,0,0,0, 0,0,0,0,0,0 };
            mesh.colors = new Color[8] { safeColor, safeColor, safeColor, safeColor, safeColor, safeColor, safeColor, safeColor };
            mesh.RecalculateNormals();
            mf.mesh = mesh;

            mr.material = _baseMaterial != null ? Instantiate(_baseMaterial) : null;
            if (mr.material != null) mr.material.color = safeColor;
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

            // ★ 오브젝트 위치 = charge 중심 (transform이 원점에서 멀어도 culling 안 되도록)
            Vector3 center = (start + end) * 0.5f;
            go.transform.position = center;

            MeshFilter mf = go.GetComponent<MeshFilter>();
            MeshRenderer mr = go.GetComponent<MeshRenderer>();

            Mesh mesh = mf.mesh;
            if (mesh == null)
            {
                mesh = new Mesh();
                mf.mesh = mesh;
            }

            // Local 좌표로 mesh 작성 (world - center) — 입체 Box
            UpdateMeshGeometryBox(mesh, start - center, end - center, indicatorHeight);

            if (mr.material != null) mr.material.color = safeColor;

            ChargePath path = new ChargePath
            {
                start = start,
                end = end,
                gameObject = go,
                renderer = mr,
                filter = mf,
                isActive = true,
                state = IndicatorState.Safe
            };

            _paths.Add(path);
            return _paths.Count - 1;
        }

        /// <summary>
        /// 입체 Box 메쉬 생성 (8버텍스, 12삼각형)
        /// 바닥면이 지면에 붙고, 윗면이 indicatorHeight만큼 솟음
        /// </summary>
        private void UpdateMeshGeometryBox(Mesh mesh, Vector3 localStart, Vector3 localEnd, float boxHeight)
        {
            Vector3 dir = (localEnd - localStart).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.right;
            Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
            if (perp.sqrMagnitude < 0.001f) perp = Vector3.forward;
            float halfW = indicatorWidth * 0.5f;

            // 8개 버텍스 (아래4 + 위4)
            Vector3[] vertices = new Vector3[8];
            // Bottom
            vertices[0] = localStart + perp * halfW;               // 0: 우측-시작
            vertices[1] = localStart - perp * halfW;               // 1: 좌측-시작
            vertices[2] = localEnd - perp * halfW;                 // 2: 좌측-끝
            vertices[3] = localEnd + perp * halfW;                 // 3: 우측-끝
            // Top
            vertices[4] = localStart + perp * halfW + Vector3.up * boxHeight;
            vertices[5] = localStart - perp * halfW + Vector3.up * boxHeight;
            vertices[6] = localEnd - perp * halfW + Vector3.up * boxHeight;
            vertices[7] = localEnd + perp * halfW + Vector3.up * boxHeight;
            mesh.vertices = vertices;

            // 12개 삼각형 (36 indices)
            mesh.triangles = new int[36]
            {
                // Bottom (0,1,2,3) — 뒷면이지만 Cull Off이므로 표시됨
                0, 3, 2,  0, 2, 1,
                // Top (4,5,6,7)
                4, 5, 6,  4, 6, 7,
                // Start face (0,1,5,4)
                0, 4, 5,  0, 5, 1,
                // End face (3,7,6,2)
                3, 2, 6,  3, 6, 7,
                // Left face (1,2,6,5)
                1, 5, 6,  1, 6, 2,
                // Right face (0,3,7,4)
                0, 7, 3,  0, 4, 7
            };

            // 현재 state에 맞는 색상 사용 (Pool 생성 시에는 safeColor)
            Color c = safeColor;
            Color[] colors = new Color[8] { c, c, c, c, c, c, c, c };
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

            // 오브젝트 위치 = charge 중심
            Vector3 center = (start + end) * 0.5f;
            if (path.gameObject != null)
                path.gameObject.transform.position = center;

            Mesh mesh = path.filter.mesh;
            if (mesh == null)
            {
                mesh = new Mesh();
                path.filter.mesh = mesh;
            }

            UpdateMeshGeometryBox(mesh, start - center, end - center, indicatorHeight);
            _paths[index] = path;
        }

        /// <summary>
        /// Player가 인디케이터 구역 안에 있는지 설정
        /// Safe ↔ Danger 색상 전환 (Imminent 상태는 오버라이드 하지 않음)
        /// </summary>
        public void SetPlayerInZone(int chargeIndex, bool inZone)
        {
            if (chargeIndex < 0 || chargeIndex >= _paths.Count) return;
            if (!_paths[chargeIndex].isActive) return;

            // Imminent 상태면 Player 감지 무시
            if (_paths[chargeIndex].state == IndicatorState.Imminent) return;

            var path = _paths[chargeIndex];
            path.state = inZone ? IndicatorState.Danger : IndicatorState.Safe;
            Color targetColor = inZone ? dangerColor : safeColor;

            if (path.renderer != null && path.renderer.material != null)
                path.renderer.material.color = targetColor;
            if (path.filter != null && path.filter.mesh != null)
            {
                Color[] colors = new Color[8] { targetColor, targetColor, targetColor, targetColor, targetColor, targetColor, targetColor, targetColor };
                path.filter.mesh.colors = colors;
            }

            _paths[chargeIndex] = path;
        }

        /// <summary>
        /// 임박 색상 설정 (곧 돌진할 네모를 진하게)
        /// Safe/Danger 상태를 Imminent로 오버라이드
        /// </summary>
        public void SetImminent(int chargeIndex)
        {
            for (int i = 0; i < _paths.Count; i++)
            {
                if (!_paths[i].isActive) continue;

                var path = _paths[i];
                if (i == chargeIndex)
                {
                    path.state = IndicatorState.Imminent;
                    _paths[i] = path;

                    if (path.renderer != null && path.renderer.material != null)
                        path.renderer.material.color = imminentColor;
                    if (path.filter != null && path.filter.mesh != null)
                    {
                        Color[] colors = new Color[8] { imminentColor, imminentColor, imminentColor, imminentColor, imminentColor, imminentColor, imminentColor, imminentColor };
                        path.filter.mesh.colors = colors;
                    }
                }
                else
                {
                    // 나머지는 Safe/Danger 유지 (Player 감지 결과 반영)
                    Color targetColor = path.state == IndicatorState.Danger ? dangerColor : safeColor;
                    if (path.renderer != null && path.renderer.material != null)
                        path.renderer.material.color = targetColor;
                    if (path.filter != null && path.filter.mesh != null)
                    {
                        Color[] colors = new Color[8] { targetColor, targetColor, targetColor, targetColor, targetColor, targetColor, targetColor, targetColor };
                        path.filter.mesh.colors = colors;
                    }
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
