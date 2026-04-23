using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class XRayMaterialDualizer : MonoBehaviour
{
    [Header("X-Ray Settings")]
    [Tooltip("장애물 뒤에서만 그려질 2번째 외곽선 전용 머티리얼 (Mat_Outline)을 넣어주세요.")]
    public Material outlineMaterial;

    void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();

        // 외곽선 머티리얼이 정상적으로 할당되어 있고, 
        // 외부 스크립트에 의해 이미 배열이 생성되지 않은(기본 상태인) 경우에만 실행
        if (outlineMaterial != null && sr.materials.Length == 1)
        {
            // 유니티 2D 스프라이트 렌더러의 다중 머티리얼 봉인을 스크립트 단에서 해제합니다.
            // sr.material 은 인스턴스화된 첫 번째 머티리얼을 보존합니다.
            sr.materials = new Material[] { sr.material, outlineMaterial };
        }
    }
}
