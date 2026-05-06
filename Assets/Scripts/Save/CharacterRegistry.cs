using UnityEngine;

namespace HideAndInk.Scripts.Save
{
    /// <summary>
    /// Player/치치 Transform을 Find/태그 없이 참조할 수 있도록 등록하는 스태틱 레지스트리.
    /// 
    /// 등록 방식:
    /// - Player 오브젝트의 Awake()에서 CharacterRegistry.RegisterPlayer(this.transform) 호출
    /// - 치치 오브젝트도 동일한 방식으로 등록
    /// 
    /// 사용 방식:
    /// - ZoneSaveHandler / ContinueZoneHandler 에서 CharacterRegistry.Player 로 참조
    /// - Find/태그/이름 문자열 하드코딩 제로
    /// 
    /// 왜 이렇게 하는가?
    /// - GameObject.Find("Squid") → 이름 바뀌면 조용히 깨짐
    /// - GameObject.FindGameObjectWithTag("Player") → 태그 중복/변경 위험
    /// - Inspector 수동 할당 → ZoneChanger 5개에 각각 할당해야 함 (번거로움)
    /// - Registry = 자기등록 방식 → Player 프리팹이 한 번만 등록하면 끝
    /// </summary>
    public static class CharacterRegistry
    {
        /// <summary>Player Transform (없으면 null)</summary>
        public static Transform Player { get; private set; }

        /// <summary>치치 Transform (없으면 null)</summary>
        public static Transform Squid { get; private set; }

        /// <summary>
        /// Player가 자신의 Transform을 등록합니다.
        /// (PlayerMovementAdapter.Awake() 등에서 호출)
        /// </summary>
        public static void RegisterPlayer(Transform playerTransform)
        {
            Player = playerTransform;
            Debug.Log($"[CharacterRegistry] Player 등록: {playerTransform.name} at {playerTransform.position}");
        }

        /// <summary>
        /// 치치가 자신의 Transform을 등록합니다.
        /// </summary>
        public static void RegisterSquid(Transform squidTransform)
        {
            Squid = squidTransform;
            Debug.Log($"[CharacterRegistry] 치치 등록: {squidTransform.name} at {squidTransform.position}");
        }

        /// <summary>
        /// 씬 전환 시 등록을 초기화합니다.
        /// (ContinueZoneHandler.HandleNewGame 등에서 호출)
        /// </summary>
        public static void Clear()
        {
            Player = null;
            Squid = null;
        }
    }
}