using FireGame.Core.Data;

namespace FireGame.Core.Game
{
    /// <summary>
    /// 출동 지도 위의 현장 하나. 한 판(StageDef)에 이야기와 지도 위치를 입힌 것이다.
    /// 현장을 늘릴 때는 여기 데이터만 추가하면 된다.
    /// </summary>
    public sealed class MissionDef
    {
        public readonly int Id;
        public readonly string Title;
        public readonly string Location;
        public readonly StageDef Stage;

        /// <summary>출동 전 서장의 대사. 한 줄씩 넘겨 보여준다.</summary>
        public readonly string[] Briefing;

        /// <summary>진압에 성공한 뒤의 대사.</summary>
        public readonly string[] Debrief;

        /// <summary>지도 위 위치. 왼쪽 아래가 (0,0), 오른쪽 위가 (1,1).</summary>
        public readonly float MapX;

        public readonly float MapY;

        /// <summary>먼저 해결해야 하는 현장 id. -1이면 처음부터 열려 있다.</summary>
        public readonly int RequiresMission;

        public MissionDef(
            int id,
            string title,
            string location,
            StageDef stage,
            string[] briefing,
            string[] debrief,
            float mapX,
            float mapY,
            int requiresMission)
        {
            Id = id;
            Title = title;
            Location = location;
            Stage = stage;
            Briefing = briefing;
            Debrief = debrief;
            MapX = mapX;
            MapY = mapY;
            RequiresMission = requiresMission;
        }
    }

    /// <summary>캠페인 현장 목록. 지도에서 순서대로 열린다.</summary>
    public static class Campaign
    {
        public const string ChiefName = "김 서장";

        public static readonly MissionDef[] Missions =
        {
            new MissionDef(
                id: 0,
                title: "2층 주택 화재",
                location: "햇살동 주택가",
                stage: StageCatalog.Residential,
                briefing: new[]
                {
                    "신입, 첫 출동이다. 햇살동 주택에서 불이 났다는 신고가 들어왔어.",
                    "안에 주민 한 명이 남아 있다. 먼저 구해서 출구로 데려가.",
                    "양동이로 불꽃을 쫓아가면 못 이긴다. 불이 아직 안 닿은 벽을 먼저 적셔서 길을 끊어!",
                },
                debrief: new[]
                {
                    "잘했다! 방화선을 치는 감각이 좋군.",
                    "번 돈으로 소방서에서 장비를 갖춰 둬. 다음 현장은 만만치 않을 거다.",
                },
                mapX: 0.22f,
                mapY: 0.30f,
                requiresMission: -1),

            new MissionDef(
                id: 1,
                title: "상가 배전반 화재",
                location: "햇살동 상가",
                stage: StageCatalog.Shopping,
                briefing: new[]
                {
                    "상가 건물 배전반에서 불이 났다.",
                    "전기 화재에 물은 절대 안 된다. 아무리 부어도 꺼지지 않아.",
                    "CO2 소화기가 필요하다. 아직 없다면 상점에서 먼저 사 와!",
                },
                debrief: new[]
                {
                    "전기 화재를 제대로 잡았군. 맞는 장비가 전부다.",
                    "바람언덕 주유소 쪽이 불안하다는 제보가 있다. 대비해 둬.",
                },
                mapX: 0.50f,
                mapY: 0.62f,
                requiresMission: 0),

            new MissionDef(
                id: 2,
                title: "주유소 유류 화재",
                location: "바람언덕 주유소",
                stage: StageCatalog.GasStation,
                briefing: new[]
                {
                    "비상이다! 바람언덕 주유소에서 기름에 불이 붙었다.",
                    "기름불에 물을 뿌리면 오히려 번진다. 폼 소화기로 덮어야 해.",
                    "바람이 세다. 불이 너보다 빠르니 쫓지 말고 앞을 막아!",
                },
                debrief: new[]
                {
                    "해냈다! 이 도시 최고의 소방관이다.",
                    "오늘은 푹 쉬어라. 다음 신고가 올 때까지.",
                },
                mapX: 0.78f,
                mapY: 0.35f,
                requiresMission: 1),
        };

        public static MissionDef ById(int id)
        {
            for (int i = 0; i < Missions.Length; i++)
            {
                if (Missions[i].Id == id) return Missions[i];
            }
            return null;
        }
    }

    /// <summary>
    /// 결과를 별 0~3개로 평가한다. 보상 금액과 별개로
    /// "다시 해서 더 잘하고 싶게" 만드는 목표 역할을 한다.
    /// </summary>
    public static class StarRating
    {
        /// <summary>이 이상 건물을 지켜야 별 하나.</summary>
        public const float IntactForStar = 0.6f;

        /// <summary>젖은 칸이 이 이하여야 별 하나. 물을 아껴 쓴 보상이다.</summary>
        public const int MaxWetCellsForStar = 30;

        public const int MaxStars = 3;

        public static int For(in StageResult result)
        {
            if (!result.Won) return 0;

            int stars = 1;
            if (result.IntactRatio >= IntactForStar) stars++;
            if (result.WetCellCount <= MaxWetCellsForStar) stars++;
            return stars;
        }
    }
}
