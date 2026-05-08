using System;
using HideAndInk.Core.Events;
using HideAndInk.Core.Interfaces;
using UnityEngine;

namespace HideAndInk.Core.Managers
{
    /// <summary>
    /// 스토리 매니저 구현체
    /// 대사 흐름 제어, Time.timeScale 관리, StoryEvents 발행
    /// StoryDatabaseSO를 생성자 주입받아 데이터 참조
    /// </summary>
    public sealed class StoryManager : IStoryManager
    {
        // ─── 데이터베이스 ────────────────────────────────────────
        private readonly StoryDatabaseSO _database;

        // ─── 상태 필드 ──────────────────────────────────────────
        private DialogueLine[] _currentLines;
        private int _currentIndex;
        private StorySection? _currentSection;
        private bool _isPlaying;
        private float _previousTimeScale = 1f;

        // ─── 생성자 (DI를 통해 StoryDatabaseSO 주입) ─────────────
        public StoryManager(StoryDatabaseSO database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        // ─── IStoryManager 프로퍼티 ─────────────────────────────
        public bool IsDialoguePlaying => _isPlaying;
        public StorySection? CurrentSection => _currentSection;
        public int CurrentLineIndex => _currentIndex;
        public int TotalLineCount => _currentLines?.Length ?? 0;

        // ─── 프롤로그 ───────────────────────────────────────────
        public void PlayPrologue()
        {
            if (_isPlaying) return;
            BeginSection(StorySection.Prologue, _database.GetPrologue());
        }

        // ─── 에필로그 ───────────────────────────────────────────
        public void PlayEpilogue()
        {
            if (_isPlaying) return;
            BeginSection(StorySection.Epilogue, _database.GetEpilogue());
        }

        // ─── 튜토리얼 힌트 (여러 페이지 지원) ──────────────────────
        public void ShowTutorialHint(TutorialType type)
        {
            if (_isPlaying) return;
            var lines = _database.GetTutorialHint(type);
            if (lines.Length == 1)
            {
                BeginSingleLine(lines[0]);
            }
            else if (lines.Length > 1)
            {
                BeginSection(StorySection.Tutorial, lines);
            }
        }

        // ─── 보스 조우 대사 (여러 페이지 지원) ──────────────────────
        public void ShowBossDialogue(BossType type)
        {
            if (_isPlaying) return;
            var lines = _database.GetBossDialogue(type);
            if (lines.Length == 1)
            {
                BeginSingleLine(lines[0]);
            }
            else if (lines.Length > 1)
            {
                BeginSection(StorySection.Tutorial, lines);
            }
        }

        // ─── 다음 대사 진행 ─────────────────────────────────────
        public void NextLine()
        {
            if (!_isPlaying || _currentLines == null) return;

            _currentIndex++;

            if (_currentIndex >= _currentLines.Length)
            {
                // 모든 대사 출력 완료 → 섹션 종료
                EndDialogue();
            }
            else
            {
                // 다음 대사 출력
                ShowCurrentLine();
            }
        }

        // ─── 강제 종료 ──────────────────────────────────────────
        public void StopStory()
        {
            if (!_isPlaying) return;
            EndDialogue();
        }

        // ─── 내부 메서드 ────────────────────────────────────────

        /// <summary>
        /// 여러 줄로 구성된 섹션 시작
        /// </summary>
        private void BeginSection(StorySection section, DialogueLine[] lines)
        {
            _currentLines = lines;
            _currentIndex = 0;
            _currentSection = section;
            _isPlaying = true;

            // TimeScale 정지 (게임오브젝트 정지)
            PauseGameTime();

            // 섹션 시작 이벤트
            StoryEvents.InvokeSectionStarted(section);

            // 첫 줄 출력
            ShowCurrentLine();
        }

        /// <summary>
        /// 단일 대사 출력 (튜토리얼/보스)
        /// </summary>
        private void BeginSingleLine(DialogueLine line)
        {
            _currentLines = new DialogueLine[] { line };
            _currentIndex = 0;
            _currentSection = null;
            _isPlaying = true;

            PauseGameTime();

            ShowCurrentLine();
        }

        /// <summary>
        /// 현재 인덱스의 대사를 UI에 전달
        /// cutsceneBg가 null이 아니면 컷씬 배경 변경 이벤트도 함께 발행
        /// </summary>
        private void ShowCurrentLine()
        {
            if (_currentLines == null || _currentIndex >= _currentLines.Length) return;

            var line = _currentLines[_currentIndex];
            Debug.Log($"[StoryManager] ShowCurrentLine - speaker={line.speaker}, text={line.text.Substring(0, Mathf.Min(20, line.text.Length))}...");
            StoryEvents.InvokeDialogueLineChanged(line.speaker, line.text);

            // 컷씬 배경 이미지가 있으면 교체 (null이면 이전 이미지 유지)
            if (line.cutsceneBg != null)
            {
                StoryEvents.InvokeCutsceneBackgroundChanged(line.cutsceneBg);
            }
        }

        /// <summary>
        /// 대화 종료 처리
        /// </summary>
        private void EndDialogue()
        {
            var completedSection = _currentSection;

            _isPlaying = false;
            _currentLines = null;
            _currentIndex = 0;
            _currentSection = null;

            // 게임 시간 복원
            ResumeGameTime();

            // 컷씬 배경 이미지 제거
            StoryEvents.InvokeCutsceneBackgroundChanged(null);

            // 종료 이벤트
            StoryEvents.InvokeDialogueEnd();

            if (completedSection.HasValue)
            {
                StoryEvents.InvokeSectionCompleted(completedSection.Value);
            }
        }

        // ─── TimeScale 제어 ─────────────────────────────────────

        private void PauseGameTime()
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        private void ResumeGameTime()
        {
            Time.timeScale = _previousTimeScale;
        }
    }
}
