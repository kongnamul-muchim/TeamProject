using System;
using UnityEngine;

/// <summary>
/// 시나리오/스토리 관련 열거형 및 데이터 구조
/// Scenario.md 의 내용을 기반으로 게임에서 사용할 데이터 모델
/// </summary>

#region Enums

/// <summary> 스토리 섹션 구분 </summary>
public enum StorySection
{
    Prologue,
    Tutorial,
    BossSwordfish,
    BossFlounder,
    BossMoray,
    BossGreatWhite,
    Epilogue
}

/// <summary> 튜토리얼 힌트 종류 </summary>
public enum TutorialType
{
    Camouflage,
    Escape,
    InkLow,
    InkSupply
}

/// <summary> 보스 종류 </summary>
public enum BossType
{
    Swordfish,
    Flounder,
    Moray,
    GreatWhite
}

#endregion

#region Data Model

/// <summary>
/// 한 줄의 대사 데이터
/// </summary>
[Serializable]
public struct DialogueLine
{
    public string speaker;   // 화자 이름
    public string text;      // 대사 내용
    public string sceneId;   // 컷신-순번 (예: "1-1"), 없으면 빈 문자열

    public DialogueLine(string speaker, string text, string sceneId = "")
    {
        this.speaker = speaker;
        this.text = text;
        this.sceneId = sceneId;
    }
}

/// <summary>
/// Scenario.md 기반 기본값 데이터 제공
/// StoryDatabaseSO 에셋 생성 시 PopulateDefaults() 로 초기값 채우기용
/// 게임 실행 중에는 이 클래스를 직접 사용하지 않음
/// </summary>
public static class StoryDatabase
{
    /// <summary>
    /// StoryDatabaseSO 에 기본 대사 데이터를 채워 넣음
    /// </summary>
    public static void PopulateDefaults(StoryDatabaseSO so)
    {
        // ─── 프롤로그 ───────────────────────────────────────────────
        so.prologue = new DialogueLine[]
        {
            new DialogueLine("네레이션", "종이 바다에서 쭉 살아온 문어 두두. 경쟁자와 포식자의 수가 들어 보금자리가 황폐해졌습니다.", "1-1"),
            new DialogueLine("두두", "사방이 온통 구겨진 종이 바위 뿐이야... 이제 여기서 먹을 걸 찾는 건 불가능해.", "1-2"),
            new DialogueLine("두두", "먼 바다 깊은 곳엔 아직 깨끗한 보금자리가 남아있을까? 무섭지만... 가봐야겠어.", "1-3"),
            new DialogueLine("두두", "응? 넌 뭐야? 문어처럼 생겼는데... 나랑은 좀 다르네. 조금 이상해. 이름이 뭐야?", "2-1"),
            new DialogueLine("치치", "치직.. 칙.", "2-2"),
            new DialogueLine("두두", "치치? 이름이 치치야? 너도 문어 맞지? 여기는 황폐해. 너도 여길 떠나는게 좋아.", "2-3"),
            new DialogueLine("치치", "(생태계 조사를 위해 문어 개체 관찰 중...) 치칙.", "3-1"),
            new DialogueLine("두두", "어? 왜 따라다니는거야? 곤란한데... 어, 야! 저기 포식자가 오잖아! 얼른 숨어!", "3-2"),
            new DialogueLine("치치", "(포식자의 행동 패턴 관찰 중...) ? 치지직.", "3-3"),
            new DialogueLine("큰 물고기", "(깨물깨물...) ...? 퉤.", "3-4"),
            new DialogueLine("두두", "와... 너 정말 튼튼하다. 큰 물고기가 공격했는데도 멀쩡해. 오히려 저 녀석이 흥미를 잃고 떠나버렸네!", "3-5"),
            new DialogueLine("치치", "...", "3-6"),
            new DialogueLine("두두", "내가 하는 말 듣고 있는거 맞니? 음... 대화 통하고 있는거 맞지?", "3-7"),
            new DialogueLine("치치", "(관찰 대상의 잉크 부족 현상 파악. 잉크를 보급 진행.) 치익.", "3-8"),
            new DialogueLine("두두", "어? 나한테 먹물을 나눠주는 거야? 고마워! 뭔가 이상하지만 착한 친구네.", "3-9"),
            new DialogueLine("두두", "치치! 너랑 같이 간다면 안전한 보금자리를 발견할 수 있을 것 같아! 같이 가자!", "4-1"),
            new DialogueLine("네레이션", "이렇게 두두와 치치의 종이 바다를 가로지르는 여정이 시작되었습니다.", "4-1"),
        };

        // ─── 튜토리얼 힌트 ─────────────────────────────────────────
        so.tutorialHints = new StoryDatabaseSO.TutorialHintEntry[]
        {
            new StoryDatabaseSO.TutorialHintEntry
            { type = TutorialType.Camouflage, line = new DialogueLine("치치", "[시스템] 주변 사물과 색상을 동화시키세요. 포식자의 의심을 피할 수 있습니다. (C키: 의태)") },
            new StoryDatabaseSO.TutorialHintEntry
            { type = TutorialType.Escape, line = new DialogueLine("두두", "위험해! 먹물을 내뿜어서 빨리 빠져나가야겠어! (Space: 고속 도주 - 무적 상태)") },
            new StoryDatabaseSO.TutorialHintEntry
            { type = TutorialType.InkLow, line = new DialogueLine("두두", "먹물이 다 떨어졌어... 움직이기가 너무 힘들어... 치치, 도와줘!") },
            new StoryDatabaseSO.TutorialHintEntry
            { type = TutorialType.InkSupply, line = new DialogueLine("치치", "[보급] 잉크 게이지를 재충전합니다. (X키: 치치 호출)") },
        };

        // ─── 보스 정보 ──────────────────────────────────────────────
        so.bossDialogues = new StoryDatabaseSO.BossDialogueEntry[]
        {
            new StoryDatabaseSO.BossDialogueEntry
            { type = BossType.Swordfish, line = new DialogueLine("두두", "저 뾰족한 코에 찔리면 무사하지 못할 거야. 내가 불안해할수록 더 빨리 돌진해오겠지? 여차하면 바위에 부딪히게 유인해보자!") },
            new StoryDatabaseSO.BossDialogueEntry
            { type = BossType.Flounder, line = new DialogueLine("두두", "모래바닥이 미세하게 떨리고 있어... 혹시 박스 가자미가 주변에 있는걸까? 녀석의 근처에 가면 튀어나와서 덮칠 거야. 저 모래구덩이에 빠지면 발이 묶이니 조심해야 해!") },
            new StoryDatabaseSO.BossDialogueEntry
            { type = BossType.Moray, line = new DialogueLine("두두", "저 녀석, 내 주변을 맴돌며 기회를 엿보고 있어. 가만히 있으면 의심만 살 뿐이야. 차라리 돌진을 유도하고 그 틈에 도망치자!") },
            new StoryDatabaseSO.BossDialogueEntry
            { type = BossType.GreatWhite, line = new DialogueLine("두두", "백상아리야... 숨을 곳을 무작위로 부수고 있어! 은신처가 사라지기 전에 지나가야 해. 들키는 순간 끝장이야!") },
        };

        // ─── 에필로그 ───────────────────────────────────────────────
        so.epilogue = new DialogueLine[]
        {
            new DialogueLine("두두", "여기가 내가 꿈꾸던 이상적인 보금자리야! 따뜻한 모래, 맑은 물... 이곳이 새로운 집이야.", "1-1"),
            new DialogueLine("두두", "치치... 네가 없었다면 여기까지 오지 못했을 거야. 정말 고마워...", "1-2"),
            new DialogueLine("네레이션", "시간이 흐르고, 새로운 보금자리에서 두두는 자신의 가족을 꾸리게 되었습니다.", "2-1"),
            new DialogueLine("두두", "...치치. 이 모든 모험이 끝났는데도 함께 해주는구나. 고마워. 근데, 몸이 너무 무겁다... 너무 졸려. 이제 긴 잠을 자야겠어. ...알들을 잘 부탁해...", "2-2"),
            new DialogueLine("치치", "치직...(개체 '두두'의 데이터 보존 중... 생명 반응: 정지)", "2-3"),
            new DialogueLine("네레이션", "두두의 여정은 여기서 멈췄지만, 그 기록은 끝나지 않았습니다.", "3-1"),
            new DialogueLine("치치", "(생명 반응 탐지... 다수의 어린 개체 움직임 확인.) ...치직.", "3-2"),
            new DialogueLine("아기 문어들", "당신은 누구에요? 치치라고요?", "3-3"),
            new DialogueLine("치치", "(관찰 데이터 시청 모드 활성화) ...치!", "4-1"),
            new DialogueLine("아기 문어들", "와아! 이 모습은 우리 엄마에요?", "4-2"),
            new DialogueLine("아기 문어들", "먹물은 이렇게 쓰는거구나... 아, 위험할 때는 이렇게 도망치는거야! ...우리를 위해서 알려주는거에요?", "4-3"),
            new DialogueLine("치치", "...(고개를 끄덕인다.)", "4-4"),
            new DialogueLine("네레이션", "종이바다 깊은 곳에서, 작은 종이문어 두두의 이야기는 후대를 이어 계속해서 전해지게 되었습니다.", "4-5"),
        };

        Debug.Log("[StoryDatabase] 기본 대사 데이터를 StoryDatabaseSO에 채웠습니다.");
    }
}

#endregion
