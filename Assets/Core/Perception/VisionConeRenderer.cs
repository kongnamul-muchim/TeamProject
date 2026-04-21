using UnityEngine;
using HideAndInk.Core.Perception;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 적의 시야가 바닥에 닿는 영역을 붉은 타원으로 표시
    /// 자식 객체를 바닥 높이에 배치, XZ 평면에서 부채꼴 계산
    /// </summary>
    [RequireComponent(typeof(ConeVisionSensor))]
    public sealed class VisionConeRenderer : MonoBehaviour
    {
        [Header("시야 센서 참조")]
        [Tooltip("시야 데이터를 가져올 ConeVisionSensor 컴포넌트")]
        [SerializeField] private ConeVisionSensor visionSensor;

        [Header("색상 설정")]
        [Tooltip("시야 영역의 색상 (RGB=색상, A=기본 투명도)")]
        [SerializeField] private Color coneColor = new Color(1f, 0f, 0f, 0.4f);

        [Header("렌더링 설정")]
        [Tooltip("부채꼴의 세그먼트 수 (높을수록 부드러움, 8~64)")]
        [SerializeField, Range(8, 64)] private int segmentCount = 32;
        [Tooltip("바닥으로 인식할 레이어 (이 레이어의 오브젝트 위에만 시야 표시)")]
        [SerializeField] private LayerMask groundLayer = -1;
        [Tooltip("바닥과의 Z-fighting 방지를 위한 Y축 오프셋")]
        [SerializeField] private float meshYOffset = 0.05f;

        [Header("디버깅")]
        [Tooltip("초기화 시 로그 출력 여부")]
        [SerializeField] private bool debugLogging = false;

        // 자식 렌더러
        private GameObject _renderObject;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _coneMesh;
        private Material _coneMaterial;
        private ConeVisionSensor _cachedSensor;
        private bool _isInitialized = false;
        private int _frameCount = 0;
        private const int LOG_INTERVAL = 120;

        private void Awake()
        {
            TryInitialize();
        }

        private void TryInitialize()
        {
            if (_isInitialized) return;

            if (visionSensor == null)
                visionSensor = GetComponent<ConeVisionSensor>();
            if (visionSensor == null) return;

            _cachedSensor = visionSensor;

            // 자식 GameObject
            _renderObject = new GameObject("VisionConeFloor");
            _renderObject.transform.SetParent(transform);
            _renderObject.transform.localPosition = Vector3.zero;
            _renderObject.transform.localRotation = Quaternion.identity;

            _meshFilter = _renderObject.AddComponent<MeshFilter>();
            _meshRenderer = _renderObject.AddComponent<MeshRenderer>();

            if (_meshFilter == null || _meshRenderer == null) return;

            _coneMesh = new Mesh();
            _coneMesh.name = "VisionConeFloorMesh";
            _meshFilter.sharedMesh = _coneMesh;

            // 커스텀 Vertex Color Unlit Shader 사용 (URP 호환 + 정점 색상 지원)
            Shader shader = Shader.Find("Custom/VertexColorUnlitTransparent");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                _coneMaterial = new Material(shader);
                _coneMaterial.color = coneColor;
                _coneMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _meshRenderer.material = _coneMaterial;
                _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _meshRenderer.sortingLayerName = "Default";
                _meshRenderer.sortingOrder = -5;
            }

            _isInitialized = true;

            if (debugLogging)
                Debug.Log($"[VisionConeRenderer] Initialized: shader={shader?.name}", this);
        }

        private void Update()
        {
            _frameCount++;

            if (!_isInitialized)
            {
                TryInitialize();
                if (!_isInitialized) return;
            }

            if (_cachedSensor == null || _coneMesh == null) return;

            BuildFloorMesh();
        }

        /// <summary>
        /// 바닥에 투영된 부채꼴 메쉬 생성
        /// 1. 아래로 Raycast 한 번으로 바닥 높이 찾음
        /// 2. 자식 객체를 바닥 높이에 배치
        /// 3. XZ 평면에서 시야 방향 기준으로 각도 계산하여 부채꼴 생성
        /// </summary>
        private void BuildFloorMesh()
        {
            Vector3 origin = _cachedSensor.Origin;
            float viewRadius = _cachedSensor.ViewRadius;
            float viewAngle = _cachedSensor.ViewAngle;
            Vector3 viewDir = _cachedSensor.GetViewDirection();

            if (viewDir.sqrMagnitude < 0.001f) return;
            viewDir = viewDir.normalized;

            // 1. 아래로 Raycast 한 번으로 바닥 높이 찾음
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit floorHit, viewRadius * 3f, groundLayer)) return;
            float floorY = floorHit.point.y;

            // 2. 자식 객체를 바닥 높이에 배치 (XZ는 origin과 동일)
            _renderObject.transform.position = new Vector3(origin.x, floorY + meshYOffset, origin.z);

            // 3. XZ 평면에서 시야 방향 투영
            Vector3 viewXZ = new Vector3(viewDir.x, 0f, viewDir.z).normalized;
            if (viewXZ.sqrMagnitude < 0.001f) viewXZ = Vector3.forward;

            // 4. 부채꼴 점 계산 (로컬 좌표, Y=0)
            Vector3[] floorPoints = new Vector3[segmentCount + 1];
            floorPoints[0] = Vector3.zero; // 중심 (로컬 원점)

            float halfAngle = viewAngle / 2f;
            for (int i = 0; i < segmentCount; i++)
            {
                float azimuth = -halfAngle + (viewAngle / segmentCount) * i;
                Vector3 dir = Quaternion.AngleAxis(azimuth, Vector3.up) * viewXZ;
                floorPoints[i + 1] = dir * viewRadius;
            }

            BuildMesh(floorPoints);
        }

        private void BuildMesh(Vector3[] points)
        {
            int count = points.Length;
            Vector3[] vertices = new Vector3[count];
            Color[] colors = new Color[count];

            // 그라데이션: 중심 alpha 0.8 → 가장자리 alpha 0.3
            float centerAlpha = 0.8f;
            float edgeAlpha = 0.3f;
            float viewRadius = _cachedSensor != null ? _cachedSensor.ViewRadius : 5f;

            for (int i = 0; i < count; i++)
            {
                // points는 이미 _renderObject 기준 로컬 좌표 (Y=0)
                vertices[i] = points[i];

                if (i == 0)
                {
                    // 중심점: 가장 진하게
                    colors[i] = new Color(coneColor.r, coneColor.g, coneColor.b, centerAlpha);
                }
                else
                {
                    // 가장자리: 중심에서 거리 비율로 alpha 보간
                    float dist = points[i].magnitude;
                    float t = Mathf.Clamp01(dist / viewRadius);
                    float alpha = Mathf.Lerp(centerAlpha, edgeAlpha, t);
                    colors[i] = new Color(coneColor.r, coneColor.g, coneColor.b, alpha);
                }
            }

            int triCount = (count - 1) * 3;
            int[] triangles = new int[triCount];
            int idx = 0;
            for (int i = 1; i < count - 1; i++)
            {
                triangles[idx++] = 0;
                triangles[idx++] = i;
                triangles[idx++] = i + 1;
            }

            _coneMesh.Clear();
            _coneMesh.vertices = vertices;
            _coneMesh.colors = colors;
            _coneMesh.triangles = triangles;
            _coneMesh.RecalculateNormals();
        }

        private void OnDrawGizmosSelected()
        {
            if (visionSensor == null)
                visionSensor = GetComponent<ConeVisionSensor>();
            if (visionSensor == null) return;

            Vector3 origin = visionSensor.Origin;
            float viewRadius = visionSensor.ViewRadius;
            float viewAngle = visionSensor.ViewAngle;
            Vector3 viewDir = visionSensor.GetViewDirection().normalized;

            // 바닥 높이 찾음
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit floorHit, viewRadius * 3f, groundLayer))
            {
                Vector3 floorCenter = new Vector3(origin.x, floorHit.point.y + 0.05f, origin.z);

                // XZ 평면에서 시야 방향 투영
                Vector3 viewXZ = new Vector3(viewDir.x, 0f, viewDir.z).normalized;
                if (viewXZ.sqrMagnitude < 0.001f) viewXZ = Vector3.forward;

                // 부채꼴 테두리 (주황)
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
                float halfAngle = viewAngle / 2f;
                int segs = 16;
                Vector3 prev = floorCenter;

                for (int i = 0; i <= segs; i++)
                {
                    float azimuth = -halfAngle + (viewAngle / segs) * i;
                    Vector3 dir = Quaternion.AngleAxis(azimuth, Vector3.up) * viewXZ;
                    Vector3 pt = floorCenter + dir * viewRadius;
                    Gizmos.DrawLine(floorCenter, pt);
                    if (i > 0) Gizmos.DrawLine(prev, pt);
                    prev = pt;
                }
            }
        }

        private void OnDestroy()
        {
            if (_coneMaterial != null) DestroyImmediate(_coneMaterial);
            if (_coneMesh != null) DestroyImmediate(_coneMesh);
            if (_renderObject != null) DestroyImmediate(_renderObject);
        }
    }
}
