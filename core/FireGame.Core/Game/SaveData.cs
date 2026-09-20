using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FireGame.Core.Data;

namespace FireGame.Core.Game
{
    /// <summary>
    /// 진행 상황. 돈과 해금한 장비, 클리어한 스테이지 수를 담는다.
    ///
    /// 직렬화는 의존성 없는 줄 단위 텍스트로 한다.
    /// Unity의 JsonUtility는 Unity 밖에서 못 쓰고, System.Text.Json은
    /// netstandard2.1 기본 제공이 아니라 둘 다 코어를 오염시킨다.
    /// </summary>
    public sealed class SaveData
    {
        private const string FormatVersion = "v1";

        public int Money;
        public readonly List<int> Unlocked = new List<int>();

        /// <summary>클리어한 스테이지 수. 다음 스테이지 해금 판정에 쓴다.</summary>
        public int ClearedStages;

        public static SaveData NewGame()
        {
            var save = new SaveData();

            IReadOnlyList<int> starting = EquipmentCatalog.StartingEquipment;
            for (int i = 0; i < starting.Count; i++)
            {
                save.Unlocked.Add(starting[i]);
            }

            return save;
        }

        public bool Owns(int equipmentId)
        {
            return Unlocked.Contains(equipmentId);
        }

        /// <summary>스테이지를 깼다고 기록한다. 이미 더 진행했다면 되돌리지 않는다.</summary>
        public void RecordClear(int stageId)
        {
            int cleared = stageId + 1;
            if (cleared > ClearedStages) ClearedStages = cleared;
        }

        public bool IsStageUnlocked(StageDef stage)
        {
            return stage != null && ClearedStages >= stage.RequiredClears;
        }

        public string Serialize()
        {
            var builder = new StringBuilder();
            builder.Append(FormatVersion).Append('\n');
            builder.Append("money=").Append(Money.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("cleared=").Append(ClearedStages.ToString(CultureInfo.InvariantCulture)).Append('\n');

            builder.Append("unlocked=");
            for (int i = 0; i < Unlocked.Count; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(Unlocked[i].ToString(CultureInfo.InvariantCulture));
            }
            builder.Append('\n');

            return builder.ToString();
        }

        /// <summary>
        /// 저장 문자열을 복원한다. 모바일에서는 저장 중 앱이 죽어 파일이
        /// 잘릴 수 있으므로, 깨진 데이터에 예외를 던지는 대신 실패를 반환한다.
        /// </summary>
        public static bool TryDeserialize(string text, out SaveData save)
        {
            save = null;
            if (string.IsNullOrEmpty(text)) return false;

            string[] lines = text.Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != FormatVersion) return false;

            var parsed = new SaveData();
            bool sawMoney = false;

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;

                int separator = line.IndexOf('=');
                if (separator <= 0) return false;

                string key = line.Substring(0, separator);
                string value = line.Substring(separator + 1);

                switch (key)
                {
                    case "money":
                        if (!TryParseInt(value, out parsed.Money)) return false;
                        sawMoney = true;
                        break;

                    case "cleared":
                        if (!TryParseInt(value, out parsed.ClearedStages)) return false;
                        break;

                    case "unlocked":
                        if (!TryParseIdList(value, parsed.Unlocked)) return false;
                        break;

                    default:
                        // 모르는 키는 무시한다. 나중에 항목이 늘어도 구버전 세이브가 살아남는다.
                        break;
                }
            }

            if (!sawMoney) return false;

            save = parsed;
            return true;
        }

        private static bool TryParseInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseIdList(string text, List<int> target)
        {
            target.Clear();
            if (text.Length == 0) return true;

            string[] parts = text.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryParseInt(parts[i], out int id)) return false;
                if (!target.Contains(id)) target.Add(id);
            }

            return true;
        }
    }

    /// <summary>장비 구매 결과.</summary>
    public enum PurchaseResult : byte
    {
        Success = 0,
        AlreadyOwned = 1,
        NotEnoughMoney = 2,
        UnknownEquipment = 3,
    }

    /// <summary>장비 상점.</summary>
    public static class Shop
    {
        public static bool CanAfford(SaveData save, EquipmentDef def)
        {
            return save != null && def != null && save.Money >= def.Price;
        }

        public static PurchaseResult Buy(SaveData save, int equipmentId)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            EquipmentDef def = EquipmentCatalog.ById(equipmentId);
            if (def == null) return PurchaseResult.UnknownEquipment;

            if (save.Owns(equipmentId)) return PurchaseResult.AlreadyOwned;
            if (save.Money < def.Price) return PurchaseResult.NotEnoughMoney;

            save.Money -= def.Price;
            save.Unlocked.Add(equipmentId);
            return PurchaseResult.Success;
        }
    }
}
