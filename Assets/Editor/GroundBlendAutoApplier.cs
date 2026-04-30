using UnityEngine;
using UnityEditor;
using HideAndInk.Core.Environment;

namespace HideAndInk.Editor
{
    /// <summary>
    /// Ground_01 ~ Ground_06 오브젝트에 GroundBlend 셰이더를 자동으로 적용하는 에디터 툴.
    /// MenuItem: HideAndInk > Apply GroundBlend to Grounds
    /// </summary>
    public class GroundBlendAutoApplier : EditorWindow
    {
        [MenuItem("HideAndInk/Apply GroundBlend to Grounds")]
        public static void ShowWindow()
        {
            GetWindow<GroundBlendAutoApplier>("GroundBlend Applier");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("GroundBlend 셰이더 자동 적용", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "씬 내 Ground_01 ~ Ground_06 오브젝트를 찾아 GroundBlend Material을 생성/할당하고, " +
                "인접 Ground 간 경계(BlendCenter)를 Renderer bounds 기준으로 자동 계산합니다.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("Apply GroundBlend", GUILayout.Height(50)))
            {
                ApplyGroundBlend();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "적용 후 Inspector에서 _ColorA / _ColorB를 미세 조정할 수 있습니다.",
                MessageType.Info);
        }

        private static void ApplyGroundBlend()
        {
            // ── 1. GroundBlend Material 생성/로드 ─────────────────
            string materialPath = "Assets/Materials/GroundBlend.mat";
            Material groundBlendMat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (groundBlendMat == null)
            {
                Shader groundBlendShader = Shader.Find("HideAndInk/GroundBlend");
                if (groundBlendShader == null)
                {
                    EditorUtility.DisplayDialog(
                        "에러",
                        "'HideAndInk/GroundBlend' 셰이더를 찾을 수 없습니다.\n" +
                        "셰이더 컴파일 에러가 없는지 확인해주세요.",
                        "확인");
                    return;
                }

                if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                {
                    AssetDatabase.CreateFolder("Assets", "Materials");
                }

                groundBlendMat = new Material(groundBlendShader);
                groundBlendMat.name = "GroundBlend";
                groundBlendMat.SetFloat("_BlendWidth", 3f);
                groundBlendMat.SetFloat("_BlendAxis", 0f);
                groundBlendMat.SetFloat("_NoiseAmount", 0.3f);
                groundBlendMat.SetFloat("_NoiseScale", 1f);

                AssetDatabase.CreateAsset(groundBlendMat, materialPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[GroundBlendAutoApplier] Material 생성: {materialPath}");
            }

            // ── 2. 원래 색상 백업 (Material 교체 전) ──────────────
            Color[] originalColors = new Color[7]; // index 1~6 사용

            for (int i = 1; i <= 6; i++)
            {
                GameObject groundObj = GameObject.Find($"Ground_{i:D2}");
                if (groundObj == null) continue;

                Renderer renderer = groundObj.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    originalColors[i] = renderer.sharedMaterial.GetColor("_BaseColor");
                    if (originalColors[i] == default(Color))
                        originalColors[i] = renderer.sharedMaterial.color;
                }
            }

            // ── 3. 각 Ground에 Material 할당 & PropertyBlock 설정 ──
            int appliedCount = 0;
            for (int i = 1; i <= 6; i++)
            {
                string objName = $"Ground_{i:D2}";
                GameObject groundObj = GameObject.Find(objName);
                if (groundObj == null)
                {
                    Debug.LogWarning($"[GroundBlendAutoApplier] '{objName}'를 씬에서 찾을 수 없습니다.");
                    continue;
                }

                Renderer renderer = groundObj.GetComponent<Renderer>();
                if (renderer == null)
                {
                    Debug.LogWarning($"[GroundBlendAutoApplier] '{objName}'에 Renderer가 없습니다.");
                    continue;
                }

                // Material 할당
                Undo.RecordObject(renderer, "Apply GroundBlend Material");
                renderer.sharedMaterial = groundBlendMat;

                // GroundTilePropertyBlock 추가/확인
                GroundTilePropertyBlock propertyBlock = groundObj.GetComponent<GroundTilePropertyBlock>();
                if (propertyBlock == null)
                {
                    Undo.RecordObject(groundObj, "Add GroundTilePropertyBlock");
                    propertyBlock = groundObj.AddComponent<GroundTilePropertyBlock>();
                }

                Undo.RecordObject(propertyBlock, "Setup GroundTilePropertyBlock");

                SerializedObject so = new SerializedObject(propertyBlock);
                so.FindProperty("_blendAxis").floatValue = 0f;  // X축 기준
                so.FindProperty("_blendWidth").floatValue = 3f;
                so.FindProperty("_noiseAmount").floatValue = 0.3f;
                so.FindProperty("_noiseScale").floatValue = 1f;

                if (originalColors[i] != default(Color))
                {
                    so.FindProperty("_baseColor").colorValue = originalColors[i];
                }

                so.ApplyModifiedProperties();

                appliedCount++;
                Debug.Log($"[GroundBlendAutoApplier] '{objName}'에 GroundBlend 적용 완료");
            }

            // ── 4. 인접 Ground 쌍의 BlendCenter 및 색상 교차 설정 ──
            CalculateBlendCenters(originalColors);

            EditorUtility.DisplayDialog(
                "완료",
                $"총 {appliedCount}개의 Ground에 GroundBlend가 적용되었습니다.\n\n" +
                "인스펙터에서 _ColorA, _ColorB를 조정하여 경계 색상을 맞춰주세요.",
                "확인");
        }

        private static void CalculateBlendCenters(Color[] originalColors)
        {
            for (int i = 1; i <= 5; i++)
            {
                GameObject current = GameObject.Find($"Ground_{i:D2}");
                GameObject next = GameObject.Find($"Ground_{i + 1:D2}");

                if (current == null || next == null) continue;

                Renderer currentRenderer = current.GetComponent<Renderer>();
                Renderer nextRenderer = next.GetComponent<Renderer>();

                if (currentRenderer == null || nextRenderer == null) continue;

                // 두 Ground 사이 경계의 중간 지점 계산
                float boundaryCenter = (currentRenderer.bounds.max.x + nextRenderer.bounds.min.x) * 0.5f;

                // 현재 Ground (왼쪽) 설정
                GroundTilePropertyBlock currentPB = current.GetComponent<GroundTilePropertyBlock>();
                if (currentPB != null)
                {
                    Undo.RecordObject(currentPB, "Set Blend Properties");
                    SerializedObject so = new SerializedObject(currentPB);
                    so.FindProperty("_blendCenter").floatValue = boundaryCenter;

                    if (originalColors[i] != default(Color))
                        so.FindProperty("_colorA").colorValue = originalColors[i];
                    if (originalColors[i + 1] != default(Color))
                        so.FindProperty("_colorB").colorValue = originalColors[i + 1];

                    so.ApplyModifiedProperties();
                    currentPB.ApplyProperties();
                }

                // 다음 Ground (오른쪽) 설정 — 동일한 경계를 공유
                GroundTilePropertyBlock nextPB = next.GetComponent<GroundTilePropertyBlock>();
                if (nextPB != null)
                {
                    Undo.RecordObject(nextPB, "Set Blend Properties");
                    SerializedObject so = new SerializedObject(nextPB);
                    so.FindProperty("_blendCenter").floatValue = boundaryCenter;

                    if (originalColors[i] != default(Color))
                        so.FindProperty("_colorA").colorValue = originalColors[i];
                    if (originalColors[i + 1] != default(Color))
                        so.FindProperty("_colorB").colorValue = originalColors[i + 1];

                    so.ApplyModifiedProperties();
                    nextPB.ApplyProperties();
                }

                Debug.Log(
                    $"[GroundBlendAutoApplier] Ground_{i:D2} ↔ Ground_{i + 1:D2} " +
                    $"경계: BlendCenter={boundaryCenter:F2}");
            }
        }
    }
}
