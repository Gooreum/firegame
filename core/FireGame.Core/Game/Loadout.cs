using System;
using System.Collections.Generic;
using FireGame.Core.Data;

namespace FireGame.Core.Game
{
    /// <summary>
    /// 현장에 들고 가는 것 전부: 레벨이 반영된 장비, 방화복 레벨, 소방화 레벨.
    /// </summary>
    public sealed class Loadout
    {
        /// <summary>레벨이 반영된 장비 정의. 슬롯에 앞에서부터 들어간다.</summary>
        public readonly IReadOnlyList<EquipmentDef> Equipment;

        public readonly int SuitLevel;
        public readonly int BootsLevel;

        public Loadout(IReadOnlyList<EquipmentDef> equipment, int suitLevel, int bootsLevel)
        {
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            SuitLevel = suitLevel;
            BootsLevel = bootsLevel;
        }

        /// <summary>장비 id 목록을 전부 Lv1로, 방화복·소방화 없이. 테스트와 예전 호출용.</summary>
        public static Loadout FromIds(IReadOnlyList<int> ids)
        {
            var equipment = new List<EquipmentDef>();
            IReadOnlyList<int> source = ids ?? EquipmentCatalog.StartingEquipment;

            for (int i = 0; i < source.Count; i++)
            {
                EquipmentDef def = EquipmentCatalog.ById(source[i]);
                if (def != null) equipment.Add(def);
            }

            return new Loadout(equipment, 0, 0);
        }

        /// <summary>세이브의 레벨대로 꾸린다.</summary>
        public static Loadout From(SaveData save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            var equipment = new List<EquipmentDef>();
            foreach (int id in save.OwnedEquipment())
            {
                equipment.Add(EquipmentCatalog.ById(id).AtLevel(save.LevelOf(id)));
            }

            return new Loadout(equipment, save.LevelOf(GearId.Suit), save.LevelOf(GearId.Boots));
        }
    }
}
