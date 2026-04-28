using System.Collections.Generic;
using UnityEngine;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// 곰치 돌진 경로를 표시하는 붉은 네모(Mesh 평면) 관리자
    /// MeshRenderer 기반으로 카메라 각도에서도 잘 보임
    /// 순차 생성 → 순차 소멸 → 임박 색상
    /// </summary>
    public class MorayChargeIndicator : MonoBehaviour
    {
        [Header("네모 설정")]
        [SerializeField] private float indicatorWidth = 2.5f;
        [SerializeField] private Color activeColor = new Color(1f, 0.2f, 0.2f, 0.6f);
        [SerializeField] private Color imminentColor = new Color(1f, 0f, 0f, 0.9f);
        [SerializeField] private float heightOffset = 0.2f; // 바닥보다 약간 위

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
        }

        /// <summary>
        /// 돌진 경로 네모 생성 (Mesh 평면)
        /// </summary>
        public int SpawnIndicator(Vector3 start, Vector3 end)
        {
            GameObject go = new GameObject($"ChargePath_{_paths.Count}");
            // ★ 중요: World Space에 직접 생성 (곰치에 붙어서 이동하지 않도록)
            go.transform.SetParent(null);
            go.layer = gameObject.layer;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            // 평면 메쉬 생성 (4각형, 2개 삼각형)
            Mesh mesh = new Mesh();
            mesh.name = $"ChargeQuad_{_paths.Count}";

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

            int[] triangles = new int[6] { 0, 1, 2, 0, 2, 3 };
            mesh.triangles = triangles;

            Color[] colors = new Color[4]
            {
                activeColor, activeColor, activeColor, activeColor
            };
            mesh.colors = colors;

            mesh.RecalculateNormals();

            mf.mesh = mesh;
            mr.material = _baseMaterial != null ? Instantiate(_baseMaterial) : null;
            if (mr.material != null) mr.material.color = activeColor;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.sortingLayerName = "Default";
            mr.sortingOrder = -1;

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

        /// <summary>
        /// 지정한 인덱스의 네모 소멸
        /// </summary>
        public void DespawnIndicator(int index)
        {
            if (index < 0 || index >= _paths.Count) return;
            if (!_paths[index].isActive) return;

            ChargePath path = _paths[index];
            if (path.gameObject != null)
            {
                Destroy(path.gameObject);
            }
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
            mesh.RecalculateNormals();
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
        /// 모든 네모 초기화
        /// </summary>
        public void ClearAll()
        {
            for (int i = 0; i < _paths.Count; i++)
            {
                if (_paths[i].gameObject != null)
                {
                    Destroy(_paths[i].gameObject);
                }
            }
            _paths.Clear();
        }

        private void OnDestroy()
        {
            ClearAll();
            if (_baseMaterial != null)
            {
                Destroy(_baseMaterial);
            }
        }
    }
}
