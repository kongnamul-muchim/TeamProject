using UnityEngine;

namespace HideAndInk.Gameplay
{
    /// <summary>
    /// 씬 로드 시 Camouflageable 태그 오브젝트들의 레이어를 자동 할당하고
    /// 충돌 매트릭스를 설정합니다.
    /// </summary>
    public sealed class CamouflageableAutoAssign : MonoBehaviour
    {
        private const string LAYER_NAME = "Camouflageable";
        private static bool _initialized = false;

        private void Awake()
        {
            if (_initialized) return;
            _initialized = true;

            int camoLayer = LayerMask.NameToLayer(LAYER_NAME);
            if (camoLayer < 0)
            {
                Debug.LogWarning($"[CamouflageableAutoAssign] '{LAYER_NAME}' 레이어가 존재하지 않습니다. " +
                                 "Tools > Setup > Camouflageable Layer 메뉴를 먼저 실행해주세요.");
                return;
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");

            // 레이어 할당
            var targets = GameObject.FindGameObjectsWithTag("Camouflageable");
            int assignedCount = 0;
            foreach (var obj in targets)
            {
                if (obj.layer != camoLayer)
                {
                    obj.layer = camoLayer;
                    assignedCount++;
                }
            }

            // 충돌 매트릭스
            if (playerLayer >= 0)
            {
                Physics.IgnoreLayerCollision(playerLayer, camoLayer, true);
                Physics2D.IgnoreLayerCollision(playerLayer, camoLayer, true);
            }

            if (enemyLayer >= 0)
            {
                Physics.IgnoreLayerCollision(enemyLayer, camoLayer, false);
                Physics2D.IgnoreLayerCollision(enemyLayer, camoLayer, false);
            }

            if (assignedCount > 0)
            {
                Debug.Log($"[CamouflageableAutoAssign] {assignedCount}개 오브젝트 레이어 자동 할당 완료.");
            }
        }
    }
}
