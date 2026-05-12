using System;
using UnityEngine;

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

        /// <summary> 에필로그의 마지막 대사에서 입력을 받았을 때 (UI가 꺼지기 직전) 발생 </summary>
        public static event Action OnEpilogueWillEnd;

        /// <summary> 대화가 ESC로 스킵될 때 발생 </summary>
        public static event Action OnStorySkipped;

        /// <summary> 페이드 연출 등 중요 연출 중 대사 스킵 입력을 막기 위한 플래그 </summary>
        public static bool IsInputBlocked { get; set; } = false;

        /// <summary> 스토리 섹션이 시작될 때 발생 (매개변수: StorySection) </summary>
        public static event Action<StorySection> OnSectionStarted;

        /// <summary> 스토리 섹션이 완전히 종료되었을 때 발생 (매개변수: StorySection) </summary>
        public static event Action<StorySection> OnSectionCompleted;

        /// <summary> 컷씬 배경 이미지가 변경될 때 발생 (null = 이미지 제거) </summary>
        public static event Action<Sprite> OnCutsceneBackgroundChanged;

        // ─── 내부 호출 메서드 ────────────────────────────────────

        internal static void InvokeStorySkipped()
            => OnStorySkipped?.Invoke();

        internal static void InvokeDialogueStart(string speaker, string text)
            => OnDialogueStart?.Invoke(speaker, text);

        internal static void InvokeDialogueLineChanged(string speaker, string text)
            => OnDialogueLineChanged?.Invoke(speaker, text);

        internal static void InvokeLineFullyRevealed()
            => OnLineFullyRevealed?.Invoke();

        internal static void InvokeDialogueEnd()
            => OnDialogueEnd?.Invoke();

        public static void InvokeEpilogueWillEnd()
            => OnEpilogueWillEnd?.Invoke();

        internal static void InvokeSectionStarted(StorySection section)
            => OnSectionStarted?.Invoke(section);

        internal static void InvokeSectionCompleted(StorySection section)
            => OnSectionCompleted?.Invoke(section);

        internal static void InvokeCutsceneBackgroundChanged(Sprite sprite)
            => OnCutsceneBackgroundChanged?.Invoke(sprite);
    }
}
