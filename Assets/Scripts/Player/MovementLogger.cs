using System;
using System.IO;
using UnityEngine;

namespace HideAndInk.Player
{
    /// <summary>
    /// 이동 로그 기록 클래스 (파일 I/O 책임 분리)
    /// SRP 준수: 이 클래스는 오직 로그 기록만 담당
    /// </summary>
    public sealed class MovementLogger : IDisposable
    {
        private StreamWriter _logWriter;
        private bool _isInitialized;

        /// <summary>
        /// 로그 파일 초기화
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            string dateString = DateTime.Now.ToString("yyyy-MM-dd");
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string logFolder = Path.Combine(projectPath, "Logs", dateString);

            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }

            string filePath = Path.Combine(logFolder, "MOVE.md");
            _logWriter = new StreamWriter(filePath, false);
            _logWriter.WriteLine("# MOVE Log\n");
            _logWriter.WriteLine("---");
            _logWriter.AutoFlush = true;

            _isInitialized = true;
            Debug.Log($"[MovementLogger] Initialized: {filePath}");
        }

        /// <summary>
        /// 이동 좌표 로그 기록
        /// </summary>
        public void Log(string message)
        {
            if (_logWriter == null) return;

            string timeString = DateTime.Now.ToString("HH:mm:ss.fff");
            string logEntry = $"## {timeString}\n{message}";
            _logWriter.WriteLine(logEntry);
        }

        /// <summary>
        /// 리소스 정리
        /// </summary>
        public void Dispose()
        {
            if (_logWriter != null)
            {
                _logWriter.Close();
                _logWriter.Dispose();
                _logWriter = null;
            }
        }
    }
}
