using System.Collections.Generic;
using FireGame.Core.Game;
using FireGame.Core.Sim;

namespace FireGame.Core.Data
{
    /// <summary>장비 id. 세이브 데이터에 숫자로 남으므로 값을 바꾸면 안 된다.</summary>
    public static class EquipmentId
    {
        public const int Bucket = 0;
        public const int Extinguisher = 1;
        public const int Hose = 2;
        public const int FoamExtinguisher = 3;
    }

    /// <summary>
    /// 장비 정의 테이블.
    ///
    /// 진압력은 <b>군집 화재의 열 재생 속도</b>를 기준으로 잡았다.
    /// 이웃이 함께 타면 한 칸의 열이 초당 목재 1.85 / 전기 1.11 / 유류 3.70 씩 되살아난다.
    /// 이보다 느린 장비는 아무리 쏴도 진압이 되지 않는다.
    ///
    /// 장비별 해당 등급 초당 진압력:
    ///   양동이 → 목재 1.60  : 1칸 폭 목조 벽(재생 1.50)은 감당하지만
    ///                          빽빽한 군집(재생 1.85)에는 밀린다 — 능력의 경계가 뚜렷하다
    ///   CO2   → 전기 2.40  : 제압 가능(재생 1.11)
    ///   호스   → 목재 6.00 x 5칸 : 어디서나 쏜다. 대형 화재의 답이다
    ///                          (예전엔 소화전 8칸 안에서만 나가서, 불 난 곳에 닿는 자리가 거의 없었다)
    ///   폼    → 유류 4.80  : 제압 가능(재생 3.70)
    /// </summary>
    public static class EquipmentCatalog
    {
        public static readonly EquipmentDef Bucket = new EquipmentDef(
            id: EquipmentId.Bucket,
            name: "양동이",
            agent: new Agent(AgentType.Water, 1.6f, 0.5f, 0.5f),
            // 한 칸에 찔끔 붓는 대신 앞 3칸에 확 끼얹는다.
            pattern: AimPattern.Cone,
            range: 1,
            resource: ResourceKind.Cooldown,
            cooldownSeconds: 1.0f,
            maxCharges: 0,
            // 한 번에 확 끼얹는 만큼 많이 든다. 탱크 160으로 13번.
            waterCost: 12f);

        public static readonly EquipmentDef Extinguisher = new EquipmentDef(
            id: EquipmentId.Extinguisher,
            name: "CO2 소화기",
            agent: new Agent(AgentType.CO2, 1.0f, 0f, 2.5f),
            // 앞 두 줄까지 뿜어 한 번에 최대 6칸을 덮는다.
            pattern: AimPattern.Cone,
            range: 2,
            resource: ResourceKind.Charges,
            cooldownSeconds: 0.5f,
            maxCharges: 12,
            // CO2는 기체다. 물이 바닥나도 전기 화재는 끌 수 있어야
            // "물 떨어져서 아무것도 못 한다"가 되지 않는다.
            waterCost: 0f);

        public static readonly EquipmentDef Hose = new EquipmentDef(
            id: EquipmentId.Hose,
            name: "소방 호스",
            agent: new Agent(AgentType.Water, 1.2f, 0.35f, 0.5f),
            pattern: AimPattern.Line,
            range: 5,
            resource: ResourceKind.Cooldown,
            cooldownSeconds: 0.2f,
            maxCharges: 0,
            // 0.2초마다 나가므로 초당 20. 가득 찬 탱크로 8초 연속 방수다.
            waterCost: 4f);

        public static readonly EquipmentDef FoamExtinguisher = new EquipmentDef(
            id: EquipmentId.FoamExtinguisher,
            name: "폼 소화기",
            agent: new Agent(AgentType.Foam, 2.0f, 0.6f, 3.0f),
            pattern: AimPattern.Cone,
            range: 2,
            resource: ResourceKind.Charges,
            cooldownSeconds: 0.5f,
            // 유류 풀은 스스로 꺼지지 않아 진압해야 할 셀 수가 많다.
            // 8회로는 풀 하나도 못 덮는다.
            maxCharges: 30,
            endSpread: 0,
            // 충전 30회를 다 쓰기 전에 물이 바닥나지 않게 낮게 잡았다.
            waterCost: 3f);

        public static readonly EquipmentDef[] All =
        {
            Bucket,
            Extinguisher,
            Hose,
            FoamExtinguisher,
        };

        public static EquipmentDef ById(int id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id) return All[i];
            }
            return null;
        }

        /// <summary>게임 시작 시 이미 가지고 있는 장비.</summary>
        public static IReadOnlyList<int> StartingEquipment
        {
            get { return new[] { EquipmentId.Bucket }; }
        }
    }
}
