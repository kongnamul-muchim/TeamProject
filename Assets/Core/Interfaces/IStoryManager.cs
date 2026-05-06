using System;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 스토리 매니저 인터페이스
    /// Scenario.md 기반 대사 출력 및 스토리 흐름 제어
    /// </summary>
    public interface IStoryManager
    {
        /// <summary> 현재 대화가 재생 중인지 여부 </summary>
        bool IsDialoguePlaying { get; }

        /// <summary> 현재 재생 중인 섹션 (없으면 null) </summary>
        StorySection? CurrentSection { get; }

        /// <summary> 현재 대사 인덱스 </summary>
        int CurrentLineIndex { get; }

        /// <summary> 전체 대사 줄 수 </summary>
        int TotalLineCount { get; }

        // ─── 재생 메서드 ────────────────────────────────────────

        /// <summary> 프롤로그 재생 </summary>
        void PlayPrologue();

        /// <summary> 에필로그 재생 </summary>
        void PlayEpilogue();

        /// <summary> 특정 튜토리얼 힌트 표시 </summary>
        void ShowTutorialHint(TutorialType type);

        /// <summary> 특정 보스 조우 대사 표시 </summary>
        void ShowBossDialogue(BossType type);

        // ─── 제어 메서드 ────────────────────────────────────────

        /// <summary> 다음 대사로 진행 (또는 현재 대사 전체 표시 중이면 전체 공개) </summary>
        void NextLine();

        /// <summary> 현재 대화 세션 강제 종료 </summary>
        void StopStory();
    }
}
