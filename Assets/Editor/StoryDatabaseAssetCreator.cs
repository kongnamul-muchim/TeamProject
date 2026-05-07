using UnityEngine;
using UnityEditor;

/// <summary>
/// StoryDatabaseSO 에셋 생성 + 기본 데이터 채우기 Editor 유틸리티
/// 사용법: 메뉴 > Assets > Create > Story Database (from Scenario.md)
/// </summary>
public static class StoryDatabaseAssetCreator
{
    private const string DefaultPath = "Assets/ScriptableObjects/StoryData";

    [MenuItem("Assets/Create/Story Database (from Scenario.md)", priority = 100)]
    private static void CreateStoryDatabaseAsset()
    {
        // 폴더 없으면 생성
        if (!AssetDatabase.IsValidFolder(DefaultPath))
        {
            System.IO.Directory.CreateDirectory(DefaultPath);
            AssetDatabase.Refresh();
        }

        // 기존 에셋 체크
        string assetPath = $"{DefaultPath}/StoryDatabase.asset";
        var existing = AssetDatabase.LoadAssetAtPath<StoryDatabaseSO>(assetPath);
        if (existing != null)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "Already exists",
                $"StoryDatabase.asset already exists at:\n{assetPath}\n\nOverwrite with default data?",
                "Yes", "Cancel"
            );

            if (!overwrite) return;

            // 기존 데이터 덮어쓰기
            StoryDatabase.PopulateDefaults(existing);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[StoryDatabaseAssetCreator] Updated existing asset: {assetPath}");
            return;
        }

        // 새 에셋 생성
        var so = ScriptableObject.CreateInstance<StoryDatabaseSO>();
        StoryDatabase.PopulateDefaults(so);

        AssetDatabase.CreateAsset(so, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 선택 상태로 표시
        Selection.activeObject = so;

        Debug.Log($"[StoryDatabaseAssetCreator] Created new asset: {assetPath}");
    }
}
