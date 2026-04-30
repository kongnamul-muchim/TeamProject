using System;

namespace HideAndInk.Scripts.Save
{
    /// <summary>
    /// 저장 데이터 구조.
    /// 마지막으로 진행한 Zone 번호와 Waypoint를 저장합니다.
    /// JSON 직렬화를 위해 [Serializable] 적용.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>마지막으로 활성화된 Zone 번호</summary>
        public int lastZoneIndex;

        /// <summary>Zone 내 전환지점 인덱스 (추후 확장용)</summary>
        public int lastWaypoint;

        /// <summary>저장 시간 (UTC)</summary>
        public string saveTime;

        public SaveData()
        {
            lastZoneIndex = 0;
            lastWaypoint = 0;
            saveTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public SaveData(int zoneIndex, int waypoint = 0)
        {
            lastZoneIndex = zoneIndex;
            lastWaypoint = waypoint;
            saveTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
