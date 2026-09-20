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
    /// 진압력은 <see cref="SimConfig.SelfHeatFactor"/> 기준 유지 열량과 맞물려 설계했다.
    /// 단독 연소 시 유지 열량은 목재 0.625 / 유류 1.25 / 전기 0.375 이므로,
    /// "맞는 장비는 한두 방, 틀린 장비는 여러 방 또는 역효과"가 성립한다.
    /// </summary>
    public static class EquipmentCatalog
    {
        public static readonly EquipmentDef Bucket = new EquipmentDef(
            id: EquipmentId.Bucket,
            name: "BUCKET",
            agent: new Agent(AgentType.Water, 0.7f, 0.5f),
            pattern: AimPattern.Single,
            range: 1,
            resource: ResourceKind.Cooldown,
            cooldownSeconds: 1.0f,
            maxCharges: 0,
            requiresHydrant: false,
            hydrantRadius: 0f,
            price: 0);

        public static readonly EquipmentDef Extinguisher = new EquipmentDef(
            id: EquipmentId.Extinguisher,
            name: "CO2 EXT",
            agent: new Agent(AgentType.CO2, 0.5f, 0f),
            pattern: AimPattern.Cone,
            range: 1,
            resource: ResourceKind.Charges,
            cooldownSeconds: 0.5f,
            maxCharges: 12,
            requiresHydrant: false,
            hydrantRadius: 0f,
            price: 500);

        public static readonly EquipmentDef Hose = new EquipmentDef(
            id: EquipmentId.Hose,
            name: "HOSE",
            agent: new Agent(AgentType.Water, 0.5f, 0.35f),
            pattern: AimPattern.Line,
            range: 5,
            resource: ResourceKind.Cooldown,
            cooldownSeconds: 0.2f,
            maxCharges: 0,
            requiresHydrant: true,
            hydrantRadius: 8f,
            price: 3000);

        public static readonly EquipmentDef FoamExtinguisher = new EquipmentDef(
            id: EquipmentId.FoamExtinguisher,
            name: "FOAM",
            agent: new Agent(AgentType.Foam, 0.8f, 0.6f),
            pattern: AimPattern.Cone,
            range: 1,
            resource: ResourceKind.Charges,
            cooldownSeconds: 0.5f,
            maxCharges: 8,
            requiresHydrant: false,
            hydrantRadius: 0f,
            price: 10000);

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
