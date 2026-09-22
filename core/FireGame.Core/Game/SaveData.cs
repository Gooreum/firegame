using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FireGame.Core.Data;

namespace FireGame.Core.Game
{
    /// <summary>
    /// 진행 상황. 돈, 상점 항목별 레벨(장비·방화복·소방화), 현장별 최고 별점을 담는다.
    ///
    /// 직렬화는 의존성 없는 줄 단위 텍스트로 한다.
    /// Unity의 JsonUtility는 Unity 밖에서 못 쓰고, System.Text.Json은
    /// netstandard2.1 기본 제공이 아니라 둘 다 코어를 오염시킨다.
    /// </summary>
    public sealed class SaveData
    {
        private const string FormatVersion = "v3";
        private const string V2 = "v2";
        private const string V1 = "v1";

        public int Money;

        /// <summary>상점 항목 id → 레벨. 없으면 그 항목의 시작 레벨.</summary>
        private readonly Dictionary<int, int> _levels = new Dictionary<int, int>();

        /// <summary>현장 id → 최고 별점. 기록이 없으면 0.</summary>
        private readonly Dictionary<int, int> _bestStars = new Dictionary<int, int>();

        // 현장별 최단 클리어 시간(0.1초 단위). 장비를 올릴수록 빨라지는 걸 결과 화면이 보여 준다.
        private readonly Dictionary<int, int> _bestTimes = new Dictionary<int, int>();

        public static SaveData NewGame()
        {
            // 시작 장비(양동이 Lv1)는 트랙의 StartLevel로 표현되므로 따로 넣을 것이 없다.
            return new SaveData();
        }

        public int LevelOf(int trackId)
        {
            if (_levels.TryGetValue(trackId, out int level)) return level;

            UpgradeTrack track = UpgradeCatalog.ById(trackId);
            return track != null ? track.StartLevel : 0;
        }

        /// <summary>레벨을 정한다. 0..최대 레벨로 자르고, 모르는 항목은 무시한다.</summary>
        public void SetLevel(int trackId, int level)
        {
            UpgradeTrack track = UpgradeCatalog.ById(trackId);
            if (track == null) return;

            if (level < 0) level = 0;
            if (level > track.MaxLevel) level = track.MaxLevel;
            _levels[trackId] = level;
        }

        public bool Owns(int equipmentId)
        {
            return LevelOf(equipmentId) > 0;
        }

        /// <summary>보유한(Lv1 이상) 장비 id. 상점 순서대로.</summary>
        public List<int> OwnedEquipment()
        {
            var owned = new List<int>();
            foreach (EquipmentDef def in EquipmentCatalog.All)
            {
                if (Owns(def.Id)) owned.Add(def.Id);
            }
            return owned;
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

        /// <summary>그 현장의 최단 클리어 시간(0.1초 단위). 기록이 없으면 -1.</summary>
        public int BestTimeFor(int missionId)
        {
            return _bestTimes.TryGetValue(missionId, out int time) ? time : -1;
        }

        /// <summary>클리어 시간을 기록한다. 지금 기록보다 빠를 때만 바꾸고 true.</summary>
        public bool RecordTime(int missionId, float seconds)
        {
            int time = (int)Math.Round(seconds * 10f);
            if (time < 0) return false;

            int best = BestTimeFor(missionId);
            if (best >= 0 && time >= best) return false;

            _bestTimes[missionId] = time;
            return true;
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

            AppendPairs(builder, "levels", _levels);
            AppendPairs(builder, "stars", _bestStars);
            if (_bestTimes.Count > 0) AppendPairs(builder, "best", _bestTimes);

            return builder.ToString();
        }

        /// <summary>"key=id:value,id:value" 한 줄. id 순으로 적어 같은 상태면 같은 문자열이 된다.</summary>
        private static void AppendPairs(StringBuilder builder, string key, Dictionary<int, int> pairs)
        {
            builder.Append(key).Append('=');
            var ids = new List<int>(pairs.Keys);
            ids.Sort();
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(ids[i].ToString(CultureInfo.InvariantCulture))
                       .Append(':')
                       .Append(pairs[ids[i]].ToString(CultureInfo.InvariantCulture));
            }
            builder.Append('\n');
        }

        /// <summary>
        /// 저장 문자열을 복원한다. 모바일에서는 저장 중 앱이 죽어 파일이
        /// 잘릴 수 있으므로, 깨진 데이터에 예외를 던지는 대신 실패를 반환한다.
        ///
        /// 예전 형식도 읽는다. v1/v2의 unlocked(산 장비 목록)는 각 장비 Lv1로,
        /// v1의 cleared=N은 앞에서부터 N개 현장 별 1개로 옮긴다.
        /// 업데이트 후에도 산 장비와 열어 둔 현장이 사라지지 않게 하려는 것이다.
        /// </summary>
        public static bool TryDeserialize(string text, out SaveData save)
        {
            save = null;
            if (string.IsNullOrEmpty(text)) return false;

            string[] lines = text.Split('\n');
            string version = lines[0].Trim();
            bool legacy = version == V1;
            if (!legacy && version != V2 && version != FormatVersion) return false;

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
                        if (!TryParseUnlocked(value, parsed)) return false;
                        break;

                    case "levels":
                        if (!TryParseLevels(value, parsed)) return false;
                        break;

                    case "stars":
                        if (!TryParseStars(value, parsed)) return false;
                        break;

                    case "best":
                        if (!TryParsePairs(value, (id, time) => { if (time >= 0) parsed._bestTimes[id] = time; })) return false;
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

        /// <summary>v1/v2의 산 장비 목록. 산 장비는 Lv1로 옮긴다.</summary>
        private static bool TryParseUnlocked(string text, SaveData target)
        {
            if (text.Length == 0) return true;

            string[] parts = text.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryParseInt(parts[i], out int id)) return false;
                if (target.LevelOf(id) < 1) target.SetLevel(id, 1);
            }

            return true;
        }

        private static bool TryParseLevels(string text, SaveData target)
        {
            return TryParsePairs(text, (id, level) => target.SetLevel(id, level));
        }

        private static bool TryParseStars(string text, SaveData target)
        {
            return TryParsePairs(text, target.RecordResult);
        }

        /// <summary>"id:value,id:value"를 읽는다. 한 항목이라도 깨져 있으면 실패.</summary>
        private static bool TryParsePairs(string text, Action<int, int> apply)
        {
            if (text.Length == 0) return true;

            string[] entries = text.Split(',');
            for (int i = 0; i < entries.Length; i++)
            {
                string[] pair = entries[i].Split(':');
                if (pair.Length != 2) return false;
                if (!TryParseInt(pair[0], out int id)) return false;
                if (!TryParseInt(pair[1], out int value)) return false;

                apply(id, value);
            }

            return true;
        }
    }

    /// <summary>상점 레벨업 결과.</summary>
    public enum PurchaseResult : byte
    {
        Success = 0,

        /// <summary>이미 최대 레벨이다.</summary>
        MaxLevel = 1,

        NotEnoughMoney = 2,
        UnknownEquipment = 3,
    }

    /// <summary>
    /// 소방서 상점. 모든 항목을 한 칸씩 올린다: Lv0 → Lv1이 "해금", 그 뒤는 "레벨업".
    /// </summary>
    public static class Shop
    {
        /// <summary>다음 레벨 값. 최대 레벨이거나 모르는 항목이면 -1.</summary>
        public static int NextCost(SaveData save, int trackId)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            UpgradeTrack track = UpgradeCatalog.ById(trackId);
            if (track == null) return -1;

            return track.CostToReach(save.LevelOf(trackId) + 1);
        }

        public static PurchaseResult Upgrade(SaveData save, int trackId)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            if (UpgradeCatalog.ById(trackId) == null) return PurchaseResult.UnknownEquipment;

            int cost = NextCost(save, trackId);
            if (cost < 0) return PurchaseResult.MaxLevel;
            if (save.Money < cost) return PurchaseResult.NotEnoughMoney;

            save.Money -= cost;
            save.SetLevel(trackId, save.LevelOf(trackId) + 1);
            return PurchaseResult.Success;
        }
    }
}
