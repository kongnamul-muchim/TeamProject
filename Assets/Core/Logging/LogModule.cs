using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using HideAndInk.Core.Utilities;

namespace HideAndInk.Core.Logging
{
    /// <summary>
    /// 전역 Unity 로그 캡처 + 파일 기록 모듈
    /// Application.logMessageReceived를 가로채 태그별 .md 파일로 기록
    /// </summary>
    public class LogModule : Singleton<LogModule>
    {
        private string _logFolder;
        private Dictionary<string, StreamWriter> _writers = new Dictionary<string, StreamWriter>();
        private bool _isDisposed;

        private string[] _logTypeTags = { "INFO", "WARN", "ERROR", "FATAL", "DEBUG", "DEATH" };

        private void Start()
        {
            InitializeLogFolder();
        }

        private void InitializeLogFolder()
        {
            string dateString = DateTime.Now.ToString("yyyy-MM-dd");
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            _logFolder = Path.Combine(projectPath, "Logs", dateString);

            // 같은 날 재실행 시 기존 로그 초기화
            if (Directory.Exists(_logFolder))
            {
                ClearExistingLogs();
            }
            else
            {
                Directory.CreateDirectory(_logFolder);
            }

            // 각 태그별 StreamWriter 초기화
            foreach (string tag in _logTypeTags)
            {
                string filePath = Path.Combine(_logFolder, $"{tag}.md");
                _writers[tag] = new StreamWriter(filePath, false);
                
                // 마크다운 헤더 작성
                _writers[tag].WriteLine($"# {tag} Log\n");
                _writers[tag].WriteLine("---");
                _writers[tag].AutoFlush = true;
            }

            // Unity 로그 콜백 등록
            Application.logMessageReceived += OnLogReceived;
        }

        private void ClearExistingLogs()
        {
            DirectoryInfo dir = new DirectoryInfo(_logFolder);
            foreach (FileInfo file in dir.GetFiles("*.md"))
            {
                file.Delete();
            }
        }

        private void OnLogReceived(string logString, string stackTrace, LogType type)
        {
            string tag = GetTagFromLogType(type);
            if (string.IsNullOrEmpty(tag)) return;

            string timeString = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"## {timeString}\n[{tag}] {logString}";

            WriteLog(tag, logEntry);
        }

        private string GetTagFromLogType(LogType type)
        {
            switch (type)
            {
                case LogType.Log:
                    return "INFO";
                case LogType.Warning:
                    return "WARN";
                case LogType.Error:
                    return "ERROR";
                case LogType.Exception:
                    return "ERROR";
                case LogType.Assert:
                    return "DEBUG";
                default:
                    return "INFO";
            }
        }

        private void WriteLog(string tag, string logEntry)
        {
            if (_isDisposed) return;
            if (_writers.ContainsKey(tag) && _writers[tag] != null)
            {
                try
                {
                    _writers[tag].WriteLine(logEntry);
                }
                catch (ObjectDisposedException)
                {
                    // StreamWriter가 이미 닫힌 경우 무시
                }
            }
        }

        // Unity 로그 외에도 직접 로그를 남기고 싶을 때 사용
        public void Log(string message, string tag = "INFO")
        {
            string timeString = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"## {timeString}\n[{tag}] {message}";

            WriteLog(tag, logEntry);
        }

        /// <summary>
        /// StreamWriter 정리 (중복 코드 통합)
        /// </summary>
        private void DisposeWriters()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            foreach (var writer in _writers.Values)
            {
                if (writer != null)
                {
                    writer.Close();
                }
            }
        }

        protected override void OnDestroy()
        {
            Application.logMessageReceived -= OnLogReceived;
            DisposeWriters();
        }
    }
}
