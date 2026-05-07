using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스토리 대사 데이터를 저장하는 ScriptableObject
/// Inspector에서 직접 편집 가능, 언제든 값 변경 가능
/// </summary>
[CreateAssetMenu(menuName = "Story/Story Database", fileName = "StoryDatabase")]
public class StoryDatabaseSO : ScriptableObject
{
    // ─── 직렬화 가능한 엔트리 타입 ──────────────────────────────

    [System.Serializable]
    public class TutorialHintEntry
    {
        public TutorialType type;
        public DialogueLine[] lines;
    }

    [System.Serializable]
    public class BossDialogueEntry
    {
        public BossType type;
        public DialogueLine line;
    }

    // ─── 데이터 필드 ────────────────────────────────────────────

    [Header("프롤로그")]
    public DialogueLine[] prologue;

    [Header("튜토리얼 힌트")]
    public TutorialHintEntry[] tutorialHints;

    [Header("보스 조우 대사")]
    public BossDialogueEntry[] bossDialogues;

    [Header("에필로그")]
    public DialogueLine[] epilogue;

    // ─── 조회 메서드 ────────────────────────────────────────────

    /// <summary> 프롤로그 대사 배열 반환 </summary>
    public DialogueLine[] GetPrologue() => prologue ?? new DialogueLine[0];

    /// <summary> 에필로그 대사 배열 반환 </summary>
    public DialogueLine[] GetEpilogue() => epilogue ?? new DialogueLine[0];

    /// <summary> 특정 튜토리얼 힌트 대사 배열 조회 </summary>
    public DialogueLine[] GetTutorialHint(TutorialType type)
    {
        if (tutorialHints != null)
        {
            for (int i = 0; i < tutorialHints.Length; i++)
            {
                if (tutorialHints[i].type == type)
                    return tutorialHints[i].lines ?? new DialogueLine[0];
            }
        }
        Debug.LogWarning($"[StoryDatabaseSO] TutorialHint '{type}' not found. Check Inspector.");
        return new DialogueLine[] { new DialogueLine("?", "") };
    }

    /// <summary> 특정 보스 조우 대사 조회 </summary>
    public DialogueLine GetBossDialogue(BossType type)
    {
        if (bossDialogues != null)
        {
            for (int i = 0; i < bossDialogues.Length; i++)
            {
                if (bossDialogues[i].type == type)
                    return bossDialogues[i].line;
            }
        }
        Debug.LogWarning($"[StoryDatabaseSO] BossDialogue '{type}' not found. Check Inspector.");
        return new DialogueLine("?", "");
    }
}
