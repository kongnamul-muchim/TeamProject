using System;
using UnityEngine;

namespace HideAndInk.Scripts.Save
{
    /// <summary>
    /// 저장 데이터 구조.
    /// 마지막 Zone 번호 + Player/치치 위치를 저장합니다.
    /// JSON 직렬화를 위해 [Serializable] 적용.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>마지막으로 활성화된 Zone 번호</summary>
        public int lastZoneIndex;

        /// <summary>Zone 내 전환지점 인덱스 (추후 확장용)</summary>
        public int lastWaypoint;

        /// <summary>Player 위치 X</summary>
        public float playerPosX;
        /// <summary>Player 위치 Y</summary>
        public float playerPosY;
        /// <summary>Player 위치 Z</summary>
        public float playerPosZ;

        /// <summary>치치 위치 X</summary>
        public float squidPosX;
        /// <summary>치치 위치 Y</summary>
        public float squidPosY;
        /// <summary>치치 위치 Z</summary>
        public float squidPosZ;

        /// <summary>저장 시간 (UTC)</summary>
        public string saveTime;

        /// <summary>Player 위치를 읽어온 GameObject 이름 (디버깅용)</summary>
        public string playerSourceName;

        /// <summary>치치 위치를 읽어온 GameObject 이름 (디버깅용)</summary>
        public string squidSourceName;

        public SaveData()
        {
            lastZoneIndex = 0;
            lastWaypoint = 0;
            saveTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public SaveData(int zoneIndex, Vector3 playerPos, Vector3 squidPos, int waypoint = 0)
        {
            lastZoneIndex = zoneIndex;
            lastWaypoint = waypoint;

            playerPosX = playerPos.x;
            playerPosY = playerPos.y;
            playerPosZ = playerPos.z;

            squidPosX = squidPos.x;
            squidPosY = squidPos.y;
            squidPosZ = squidPos.z;

            saveTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            playerSourceName = "";
            squidSourceName = "";
        }

        /// <summary>저장된 Player 위치를 Vector3로 반환</summary>
        public Vector3 GetPlayerPosition() => new Vector3(playerPosX, playerPosY, playerPosZ);

        /// <summary>저장된 치치 위치를 Vector3로 반환</summary>
        public Vector3 GetSquidPosition() => new Vector3(squidPosX, squidPosY, squidPosZ);
    }
}
