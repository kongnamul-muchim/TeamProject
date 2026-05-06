using System;

namespace HideAndInk.Core.Events
{
    /// <summary>
    /// 스토리/대화 관련 전역 정적 이벤트
    /// StoryManager → DialogueUIAdapter 등 구독자에게 알림
    /// </summary>
    public static class StoryEvents
    {
        /// <summary> 대화가 시작될 때 발생 (매개변수: speaker, text) </summary>
        public static event Action<string, string> OnDialogueStart;

        /// <summary> 한 줄의 대사가 변경될 때 발생 (매개변수: speaker, text) </summary>
        public static event Action<string, string> OnDialogueLineChanged;

        /// <summary> 현재 대사가 타자기 효과로 전부 출력되었을 때 발생 </summary>
        public static event Action OnLineFullyRevealed;

        /// <summary> 대화가 종료될 때 발생 </summary>
        public static event Action OnDialogueEnd;

        /// <summary> 스토리 섹션이 시작될 때 발생 (매개변수: StorySection) </summary>
        public static event Action<StorySection> OnSectionStarted;

        /// <summary> 스토리 섹션이 완전히 종료되었을 때 발생 (매개변수: StorySection) </summary>
        public static event Action<StorySection> OnSectionCompleted;

        // ─── 내부 호출 메서드 ────────────────────────────────────

        internal static void InvokeDialogueStart(string speaker, string text)
            => OnDialogueStart?.Invoke(speaker, text);

        internal static void InvokeDialogueLineChanged(string speaker, string text)
            => OnDialogueLineChanged?.Invoke(speaker, text);

        internal static void InvokeLineFullyRevealed()
            => OnLineFullyRevealed?.Invoke();

        internal static void InvokeDialogueEnd()
            => OnDialogueEnd?.Invoke();

        internal static void InvokeSectionStarted(StorySection section)
            => OnSectionStarted?.Invoke(section);

        internal static void InvokeSectionCompleted(StorySection section)
            => OnSectionCompleted?.Invoke(section);
    }
}
