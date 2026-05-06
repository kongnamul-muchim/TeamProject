using System.IO;
using UnityEngine;

namespace HideAndInk.Scripts.Save
{
    /// <summary>
    /// JSON 파일 기반 저장/불러오기 매니저.
    /// 저장 경로: Application.persistentDataPath + "/save.json"
    /// </summary>
    public static class SaveManager
    {
        private const string FileName = "save.json";

        /// <summary>이어하기 시 사용할 Zone 인덱스 (씬 간 전달용)</summary>
        public static int PendingZoneIndex { get; set; } = -1;

        /// <summary>이어하기 시 복원할 Player 위치 (씬 간 전달용)</summary>
        public static Vector3 PendingPlayerPosition { get; set; } = Vector3.zero;

        /// <summary>이어하기 시 복원할 치치 위치 (씬 간 전달용)</summary>
        public static Vector3 PendingSquidPosition { get; set; } = Vector3.zero;

        /// <summary>이어하기 모드인지 여부</summary>
        public static bool IsContinueMode => PendingZoneIndex >= 0;

        private static string SavePath
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, "Saves");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return Path.Combine(dir, FileName);
            }
        }

        // =====================================================
        // 저장 / 불러오기 / 삭제
        // =====================================================

        /// <summary>
        /// 게임 데이터를 JSON 파일로 저장합니다.
        /// </summary>
        public static void Save(SaveData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[SaveManager] 저장할 데이터가 null입니다.");
                return;
            }

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(SavePath, json);
                Debug.Log($"[SaveManager] 저장 완료: Zone_{data.lastZoneIndex} ({SavePath})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveManager] 저장 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 특정 Zone 인덱스로 간편 저장합니다 (위치 기본값).
        /// ZoneSaveHandler에서 직접 호출 대신 사용 중인 경우 대비.
        /// </summary>
        public static void Save(int zoneIndex, int waypoint = 0)
        {
            Save(new SaveData(zoneIndex, Vector3.zero, Vector3.zero, waypoint));
        }

        /// <summary>
        /// 저장된 데이터를 불러옵니다.
        /// </summary>
        /// <returns>저장 데이터, 없으면 null</returns>
        public static SaveData Load()
        {
            if (!HasSaveData())
            {
                Debug.Log("[SaveManager] 저장된 데이터가 없습니다.");
                return null;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                Debug.Log($"[SaveManager] 불러오기 완료: Zone_{data.lastZoneIndex}");
                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveManager] 불러오기 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 저장 파일이 존재하는지 확인합니다.
        /// </summary>
        public static bool HasSaveData()
        {
            return File.Exists(SavePath);
        }

        /// <summary>
        /// 저장 파일을 삭제합니다.
        /// </summary>
        public static void DeleteSave()
        {
            if (HasSaveData())
            {
                File.Delete(SavePath);
                Debug.Log("[SaveManager] 저장 데이터 삭제 완료");
            }
        }

        // =====================================================
        // 씬 간 전달 (Continue Zone)
        // =====================================================

        /// <summary>
        /// 이어하기 모드를 설정합니다. (TitleController.OnContinueClicked에서 호출)
        /// </summary>
        public static void SetContinueZone(int zoneIndex)
        {
            PendingZoneIndex = zoneIndex;
        }

        /// <summary>
        /// 이어하기 모드를 설정합니다 (위치 포함).
        /// </summary>
        public static void SetContinueZone(int zoneIndex, Vector3 playerPos, Vector3 squidPos)
        {
            PendingZoneIndex = zoneIndex;
            PendingPlayerPosition = playerPos;
            PendingSquidPosition = squidPos;
        }

        /// <summary>
        /// 이어하기 모드를 초기화합니다.
        /// </summary>
        public static void ClearContinueZone()
        {
            PendingZoneIndex = -1;
            PendingPlayerPosition = Vector3.zero;
            PendingSquidPosition = Vector3.zero;
        }
    }
}
