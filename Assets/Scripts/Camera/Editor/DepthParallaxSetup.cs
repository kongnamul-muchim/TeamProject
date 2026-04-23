using UnityEngine;
using UnityEditor;
using HideAndInk.ParallaxSystem;

namespace HideAndInk.Editor
{
    /// <summary>
    /// Y축 깊이 패럴랙스 시스템 원클릭 설정 에디터.
    /// 
    /// 메뉴: Tools > Depth Parallax > Setup Scene
    /// </summary>
    public static class DepthParallaxSetup
    {
        private const string MENU_PATH = "Tools/Depth Parallax/";

        // ── 프리셋 ──

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

            // 3. Player에 SortingOrderUpdater 부착
            if (player != null)
                SetupSortingOrder(player);

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[DepthParallax] 설정 완료! Inspector에서 값을 미세 조정하세요.");
            Selection.activeGameObject = controller.gameObject;
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

            // SortingOrderUpdater 제거
            foreach (var updater in Object.FindObjectsOfType<SortingOrderUpdater>())
                Undo.DestroyObjectImmediate(updater);

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[DepthParallax] 모든 컴포넌트 제거 완료");
        }

        // ── 내부 메서드 ──

        private static Transform FindPlayer()
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) return playerObj.transform;

            var byName = GameObject.Find("Player");
            if (byName != null) return byName.transform;

            foreach (var pm in Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (pm.GetType().Name.Contains("PlayerMovement"))
                    return pm.transform;
            }

            return null;
        }

        private static DepthParallaxController CreateController(Transform player)
        {
            var existing = Object.FindObjectOfType<DepthParallaxController>();
            if (existing != null)
            {
                Debug.Log("[DepthParallax] 기존 DepthParallaxController를 사용합니다.");
                return existing;
            }

            var go = new GameObject("DepthParallaxController");
            Undo.RegisterCreatedObjectUndo(go, "Create DepthParallaxController");

            var controller = go.AddComponent<DepthParallaxController>();

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

        private static void SetupSortingOrder(Transform player)
        {
            var go = player.gameObject;

            if (go.GetComponent<SortingOrderUpdater>() != null)
            {
                Debug.Log($"[DepthParallax] {go.name}에 이미 SortingOrderUpdater가 있습니다.");
                return;
            }

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
    }
}