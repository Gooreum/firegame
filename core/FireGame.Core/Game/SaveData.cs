using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FireGame.Core.Data;

namespace FireGame.Core.Game
{
    /// <summary>
    /// 진행 상황. 돈과 해금한 장비, 현장별 최고 별점을 담는다.
    ///
    /// 직렬화는 의존성 없는 줄 단위 텍스트로 한다.
    /// Unity의 JsonUtility는 Unity 밖에서 못 쓰고, System.Text.Json은
    /// netstandard2.1 기본 제공이 아니라 둘 다 코어를 오염시킨다.
    /// </summary>
    public sealed class SaveData
    {
        private const string FormatVersion = "v2";
        private const string LegacyFormatVersion = "v1";

        public int Money;
        public readonly List<int> Unlocked = new List<int>();

        /// <summary>현장 id → 최고 별점. 기록이 없으면 0.</summary>
        private readonly Dictionary<int, int> _bestStars = new Dictionary<int, int>();

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

        public int StarsFor(int missionId)
        {
            return _bestStars.TryGetValue(missionId, out int stars) ? stars : 0;
        }

        /// <summary>
        /// 현장 결과를 기록한다. 더 못한 기록으로 덮어쓰지 않는다 —
        /// 연습 삼아 다시 하다 실패해도 이미 딴 별을 잃으면 안 된다.
        /// </summary>
        public void RecordResult(int missionId, int stars)
        {
            if (stars < 0) stars = 0;
            if (stars > StarRating.MaxStars) stars = StarRating.MaxStars;

            if (stars > StarsFor(missionId)) _bestStars[missionId] = stars;
        }

        /// <summary>선행 현장에서 별을 하나라도 땄으면(=클리어했으면) 열린다.</summary>
        public bool IsMissionUnlocked(MissionDef mission)
        {
            if (mission == null) return false;
            if (mission.RequiresMission < 0) return true;

            return StarsFor(mission.RequiresMission) > 0;
        }

        public int TotalStars
        {
            get
            {
                int total = 0;
                foreach (int stars in _bestStars.Values) total += stars;
                return total;
            }
        }

        public string Serialize()
        {
            var builder = new StringBuilder();
            builder.Append(FormatVersion).Append('\n');
            builder.Append("money=").Append(Money.ToString(CultureInfo.InvariantCulture)).Append('\n');

            builder.Append("unlocked=");
            for (int i = 0; i < Unlocked.Count; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(Unlocked[i].ToString(CultureInfo.InvariantCulture));
            }
            builder.Append('\n');

            builder.Append("stars=");
            var ids = new List<int>(_bestStars.Keys);
            ids.Sort();
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(ids[i].ToString(CultureInfo.InvariantCulture))
                       .Append(':')
                       .Append(_bestStars[ids[i]].ToString(CultureInfo.InvariantCulture));
            }
            builder.Append('\n');

            return builder.ToString();
        }

        /// <summary>
        /// 저장 문자열을 복원한다. 모바일에서는 저장 중 앱이 죽어 파일이
        /// 잘릴 수 있으므로, 깨진 데이터에 예외를 던지는 대신 실패를 반환한다.
        ///
        /// v1(클리어 수 하나만 저장하던 형식)도 읽는다. 앞에서부터 N개 현장을
        /// 별 1개로 옮겨, 업데이트 후에도 열어 둔 현장이 다시 잠기지 않게 한다.
        /// </summary>
        public static bool TryDeserialize(string text, out SaveData save)
        {
            save = null;
            if (string.IsNullOrEmpty(text)) return false;

            string[] lines = text.Split('\n');
            string version = lines[0].Trim();
            bool legacy = version == LegacyFormatVersion;
            if (!legacy && version != FormatVersion) return false;

            var parsed = new SaveData();
            bool sawMoney = false;
            int legacyCleared = 0;

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

                    case "unlocked":
                        if (!TryParseIdList(value, parsed.Unlocked)) return false;
                        break;

                    case "stars":
                        if (!TryParseStars(value, parsed)) return false;
                        break;

                    case "cleared":
                        if (legacy && !TryParseInt(value, out legacyCleared)) return false;
                        break;

                    default:
                        // 모르는 키는 무시한다. 나중에 항목이 늘어도 구버전 세이브가 살아남는다.
                        break;
                }
            }

            if (!sawMoney) return false;

            for (int id = 0; id < legacyCleared; id++)
            {
                parsed.RecordResult(id, 1);
            }

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

        private static bool TryParseStars(string text, SaveData target)
        {
            if (text.Length == 0) return true;

            string[] entries = text.Split(',');
            for (int i = 0; i < entries.Length; i++)
            {
                string[] pair = entries[i].Split(':');
                if (pair.Length != 2) return false;
                if (!TryParseInt(pair[0], out int missionId)) return false;
                if (!TryParseInt(pair[1], out int stars)) return false;

                target.RecordResult(missionId, stars);
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
