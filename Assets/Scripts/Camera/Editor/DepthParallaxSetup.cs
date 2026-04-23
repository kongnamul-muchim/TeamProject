using UnityEngine;
using UnityEditor;
using HideAndInk.ParallaxSystem;

namespace HideAndInk.Editor
{
    /// <summary>
    /// Y축 깊이 패럴랙스 시스템 원클릭 설정 에디터.
    /// 
    /// 메뉴: Tools > Depth Parallax > Setup Scene
    /// 
    /// 실행 시:
    /// 1. 씬의 기존 ParallaxLayer를 분석하여 depthRatio를 자동 추정
    /// 2. 각 레이어에 DepthParallaxLayer를 부착하고 설정
    /// 3. DepthParallaxController를 생성하고 레이어를 연결
    /// 4. Player에 SortingOrderUpdater를 부착
    /// </summary>
    public static class DepthParallaxSetup
    {
        private const string MENU_PATH = "Tools/Depth Parallax/";

        // ── 프리셋 ──

        /// <summary>레이어 이름 키워드와 기본 depthRatio 매핑</summary>
        private static readonly (string keyword, float depthRatio, float yDepth, float scaleBoost)[] LayerPresets =
        {
            ("background", 0.05f, 0.1f, 0.03f),
            ("distant",    0.1f,  0.15f, 0.05f),
            ("far",        0.15f, 0.15f, 0.05f),
            ("midground",  0.4f,  0.25f, 0.1f),
            ("mid",        0.4f,  0.25f, 0.1f),
            ("nearground", 0.6f,  0.35f, 0.15f),
            ("foreground", 0.8f,  0.45f, 0.2f),
            ("front",      0.8f,  0.45f, 0.2f),
        };

        // ── 메뉴 항목 ──

        [MenuItem(MENU_PATH + "Setup Scene", false, 0)]
        public static void SetupScene()
        {
            Undo.SetCurrentGroupName("Depth Parallax Setup");
            int undoGroup = Undo.GetCurrentGroup();

            // 1. 캐릭터 찾기
            var player = FindPlayer();
            if (player == null)
            {
                Debug.LogWarning("[DepthParallax] Player를 찾을 수 없습니다. 수동으로 Target을 설정해주세요.");
            }

            // 2. DepthParallaxController 생성
            var controller = CreateController(player);

            // 3. 기존 ParallaxLayer 분석 → DepthParallaxLayer 부착
            SetupLayers(controller);

            // 4. Player에 SortingOrderUpdater 부착
            if (player != null)
                SetupSortingOrder(player);

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[DepthParallax] 설정 완료! Inspector에서 값을 미세 조정하세요.");
            Selection.activeGameObject = controller.gameObject;
        }

        [MenuItem(MENU_PATH + "Add DepthParallaxLayer to Selection", false, 1)]
        public static void AddLayerToSelection()
        {
            int count = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (go.GetComponent<DepthParallaxLayer>() != null) continue;

                Undo.AddComponent<DepthParallaxLayer>(go);
                var layer = go.GetComponent<DepthParallaxLayer>();
                var preset = EstimatePreset(go.name);
                ApplyPresetToLayer(layer, preset);

                count++;
            }

            Debug.Log($"[DepthParallax] {count}개 오브젝트에 DepthParallaxLayer 추가 완료");
        }

        [MenuItem(MENU_PATH + "Add SortingOrderUpdater to Selection", false, 2)]
        public static void AddSortingToSelection()
        {
            int count = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (go.GetComponent<SpriteRenderer>() == null)
                {
                    Debug.LogWarning($"[DepthParallax] {go.name}: SpriteRenderer가 없습니다. 스킵합니다.");
                    continue;
                }

                if (go.GetComponent<SortingOrderUpdater>() != null) continue;

                var updater = Undo.AddComponent<SortingOrderUpdater>(go);
                SerializedObject so = new SerializedObject(updater);

                var precisionProp = so.FindProperty("precision");
                if (precisionProp != null)
                {
                    precisionProp.intValue = 10;
                    so.ApplyModifiedProperties();
                }

                count++;
            }

            Debug.Log($"[DepthParallax] {count}개 오브젝트에 SortingOrderUpdater 추가 완료");
        }

        [MenuItem(MENU_PATH + "Remove All DepthParallax Components", false, 100)]
        public static void RemoveAll()
        {
            Undo.SetCurrentGroupName("Remove Depth Parallax");
            int undoGroup = Undo.GetCurrentGroup();

            // Controller 제거
            var controller = Object.FindObjectOfType<DepthParallaxController>();
            if (controller != null)
                Undo.DestroyObjectImmediate(controller.gameObject);

            // Layer 제거
            foreach (var layer in Object.FindObjectsOfType<DepthParallaxLayer>())
                Undo.DestroyObjectImmediate(layer);

            // SortingOrderUpdater 제거
            foreach (var updater in Object.FindObjectsOfType<SortingOrderUpdater>())
                Undo.DestroyObjectImmediate(updater);

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[DepthParallax] 모든 컴포넌트 제거 완료");
        }

        // ── 내부 메서드 ──

        private static Transform FindPlayer()
        {
            // 1. "Player" 태그
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) return playerObj.transform;

            // 2. 이름으로 검색
            var byName = GameObject.Find("Player");
            if (byName != null) return byName.transform;

            // 3. PlayerMovement 포함 오브젝트
            foreach (var pm in Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (pm.GetType().Name.Contains("PlayerMovement"))
                    return pm.transform;
            }

            return null;
        }

        private static DepthParallaxController CreateController(Transform player)
        {
            // 기존 Controller가 있으면 재사용
            var existing = Object.FindObjectOfType<DepthParallaxController>();
            if (existing != null)
            {
                Debug.Log("[DepthParallax] 기존 DepthParallaxController를 사용합니다.");
                return existing;
            }

            var go = new GameObject("DepthParallaxController");
            Undo.RegisterCreatedObjectUndo(go, "Create DepthParallaxController");

            var controller = go.AddComponent<DepthParallaxController>();

            // Target 설정
            if (player != null)
            {
                SerializedObject so = new SerializedObject(controller);
                var targetProp = so.FindProperty("target");
                if (targetProp != null)
                {
                    targetProp.objectReferenceValue = player;
                    so.ApplyModifiedProperties();
                }
            }

            return controller;
        }

        private static void SetupLayers(DepthParallaxController controller)
        {
            var existingLayers = Object.FindObjectsOfType<ParallaxLayer>();

            foreach (var parallaxLayer in existingLayers)
            {
                var go = parallaxLayer.gameObject;

                // 이미 DepthParallaxLayer가 있으면 스킵
                if (go.GetComponent<DepthParallaxLayer>() != null) continue;

                // DepthParallaxLayer 부착
                var depthLayer = Undo.AddComponent<DepthParallaxLayer>(go);

                // 기존 ParallaxLayer의 rate를 기반으로 depthRatio 추정
                var preset = EstimatePreset(go.name, parallaxLayer.rate);
                ApplyPresetToLayer(depthLayer, preset);

                Debug.Log($"[DepthParallax] {go.name}: depthRatio={preset.depthRatio:F2}, " +
                          $"yDepth={preset.yDepth:F2}, scaleBoost={preset.scaleBoost:F2}");
            }

            // Controller에 레이어 자동 연결 (Discovery Mode = Scene이면 자동)
            Debug.Log($"[DepthParallax] {existingLayers.Length}개 레이어 설정 완료 " +
                      "(Controller의 Discovery Mode = Scene이면 런타임에 자동 수집)");
        }

        private static void SetupSortingOrder(Transform player)
        {
            var go = player.gameObject;

            // 이미 있으면 스킵
            if (go.GetComponent<SortingOrderUpdater>() != null)
            {
                Debug.Log($"[DepthParallax] {go.name}에 이미 SortingOrderUpdater가 있습니다.");
                return;
            }

            // SpriteRenderer가 없으면 자식에서 찾기
            var target = go;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = go.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                    target = sr.gameObject;
            }

            if (sr == null)
            {
                Debug.LogWarning($"[DepthParallax] {go.name}: SpriteRenderer가 없어 SortingOrderUpdater를 부착할 수 없습니다.");
                return;
            }

            var updater = Undo.AddComponent<SortingOrderUpdater>(target);
            SerializedObject so = new SerializedObject(updater);

            var trackingProp = so.FindProperty("trackingTarget");
            var precisionProp = so.FindProperty("precision");
            var yOffsetProp = so.FindProperty("yOffset");

            if (trackingProp != null) trackingProp.objectReferenceValue = player;
            if (precisionProp != null) precisionProp.intValue = 10;
            if (yOffsetProp != null) yOffsetProp.floatValue = 0.5f;

            so.ApplyModifiedProperties();

            Debug.Log($"[DepthParallax] {target.name}에 SortingOrderUpdater 부착 완료");
        }

        // ── 프리셋 추정 ──

        private static (float depthRatio, float yDepth, float scaleBoost) EstimatePreset(
            string objectName, float existingRate = -1f)
        {
            string nameLower = objectName.ToLower();

            // 이름 기반 매칭
            foreach (var preset in LayerPresets)
            {
                if (nameLower.Contains(preset.keyword))
                    return (preset.depthRatio, preset.yDepth, preset.scaleBoost);
            }

            // 기존 ParallaxLayer.rate 기반 추정
            if (existingRate >= 0f)
            {
                // rate가 낮을수록 원경, 높을수록 전경
                // rate를 그대로 depthRatio로 사용하되 범위 보정
                float depthRatio = Mathf.Clamp(existingRate, 0.05f, 1f);
                float yDepth = 0.1f + depthRatio * 0.4f;
                float scaleBoost = 0.03f + depthRatio * 0.17f;
                return (depthRatio, yDepth, scaleBoost);
            }

            // 기본값 (중경)
            return (0.5f, 0.25f, 0.1f);
        }

        private static void ApplyPresetToLayer(
            DepthParallaxLayer layer,
            (float depthRatio, float yDepth, float scaleBoost) preset)
        {
            SerializedObject so = new SerializedObject(layer);

            var depthRatioProp = so.FindProperty("depthRatio");
            var yDepthProp = so.FindProperty("yDepthFactor");
            var scaleBoostProp = so.FindProperty("scaleBoost");

            if (depthRatioProp != null) depthRatioProp.floatValue = preset.depthRatio;
            if (yDepthProp != null) yDepthProp.floatValue = preset.yDepth;
            if (scaleBoostProp != null) scaleBoostProp.floatValue = preset.scaleBoost;

            so.ApplyModifiedProperties();
        }
    }
}