#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace HideAndInk.Editor
{
    /// <summary>
    /// 의태용 오브젝트(Camouflageable) 레이어 자동 설정
    /// 메뉴: Tools > Setup > Camouflageable Layer
    /// </summary>
    public static class CamouflageableSetupEditor
    {
        private const string LAYER_NAME = "Camouflageable";

        [MenuItem("Tools/Setup/Camouflageable Layer")]
        public static void SetupCamouflageableLayer()
        {
            // 1. 레이어 생성
            int camoLayer = GetOrCreateLayer(LAYER_NAME);
            if (camoLayer < 0)
            {
                Debug.LogError("[CamouflageableSetup] 레이어 생성 실패. 유효한 빈 슬롯이 없습니다. (최대 32개)");
                return;
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");

            if (playerLayer < 0)
            {
                Debug.LogError("[CamouflageableSetup] 'Player' 레이어를 찾을 수 없습니다.");
                return;
            }

            // 2. 씬 내 모든 Camouflageable 태그 오브젝트에 레이어 할당
            var targets = GameObject.FindGameObjectsWithTag("Camouflageable");
            int assignedCount = 0;
            foreach (var obj in targets)
            {
                if (obj.layer != camoLayer)
                {
                    Undo.RecordObject(obj, "Assign Camouflageable Layer");
                    obj.layer = camoLayer;
                    assignedCount++;
                }
            }

            // 3. 충돌 매트릭스 설정
            // Player → Camouflageable: 충돌 안 함 (통과 가능)
            Physics.IgnoreLayerCollision(playerLayer, camoLayer, true);
            Physics2D.IgnoreLayerCollision(playerLayer, camoLayer, true);

            // Enemy → Camouflageable: 충돌 허용 (보스몹 공격/파괴용)
            if (enemyLayer >= 0)
            {
                Physics.IgnoreLayerCollision(enemyLayer, camoLayer, false);
                Physics2D.IgnoreLayerCollision(enemyLayer, camoLayer, false);
            }

            Debug.Log($"[CamouflageableSetup] 완료! " +
                      $"레이어 '{LAYER_NAME}'({camoLayer}) 생성/확인, " +
                      $"{assignedCount}/{targets.Length}개 오브젝트에 레이어 할당, " +
                      $"Player↔Camouflageable 충돌 비활성화, " +
                      $"Enemy↔Camouflageable 충돌 활성화");
        }

        /// <summary>
        /// 태그 매니저에서 지정 이름의 레이어를 찾거나 새로 생성합니다.
        /// </summary>
        private static int GetOrCreateLayer(string layerName)
        {
            // 이미 존재하는지 확인
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            // TagManager.asset 직접 수정
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            // 빈 슬롯(8번 이후) 찾기
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty layerSp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerSp.stringValue))
                {
                    layerSp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    return i;
                }
            }

            return -1; // 빈 슬롯 없음
        }
    }
}
#endif
