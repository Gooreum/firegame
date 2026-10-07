using System;
using System.Collections.Generic;
using System.Text;

namespace FireGame.Prototypes.Logic
{
    /// <summary>소방관 한 명: 시작 장비가 다르다. 모두 물대포는 든다(오른손이 할 일이 있어야 한다).</summary>
    public sealed class Firefighter
    {
        public string Id;
        public string Name;

        /// <summary>소방서 화면에 보이는 한 줄 소개.</summary>
        public string Story;
        public UpgradeId[] Start;

        /// <summary>해금에 드는 별. 0이면 처음부터.</summary>
        public int Cost;
    }

    /// <summary>소방관 명단. 비용은 통장의 별로 치른다.</summary>
    public static class Roster
    {
        public static readonly Firefighter[] All =
        {
            new Firefighter { Id = "rookie", Name = "신입 소방관", Story = "물대포 하나로 시작한다", Start = new[] { UpgradeId.Hose }, Cost = 0 },
            new Firefighter { Id = "rescue", Name = "스프링클러 기사", Story = "회전 스프링클러를 달고 출동한다", Start = new[] { UpgradeId.Hose, UpgradeId.Sprinkler }, Cost = 3 },
            new Firefighter { Id = "pilot", Name = "사슬 방수병", Story = "물 사슬을 쥐고 시작한다", Start = new[] { UpgradeId.Hose, UpgradeId.Chain }, Cost = 5 },
            new Firefighter { Id = "pump", Name = "펌프 기사", Story = "고압 펌프를 달고 시작한다", Start = new[] { UpgradeId.Hose, UpgradeId.Tank }, Cost = 5 },
            new Firefighter { Id = "veteran", Name = "베테랑", Story = "방화복을 입고 물 사슬을 쥔다", Start = new[] { UpgradeId.Hose, UpgradeId.Chain, UpgradeId.Suit }, Cost = 8 },
        };

        public const string Default = "rookie";

        /// <summary>id의 소방관. 없으면 신입.</summary>
        public static Firefighter Get(string id)
        {
            foreach (Firefighter f in All) if (f.Id == id) return f;
            return All[0];
        }

        public static bool Exists(string id)
        {
            foreach (Firefighter f in All) if (f.Id == id) return true;
            return false;
        }
    }

    /// <summary>판 사이에 남는 것: 모은 별(통장), 스테이지별 최고 별, 해금한 소방관, 고른 소방관.</summary>
    public sealed class FireStation
    {
        /// <summary>쓸 수 있는 별. 판을 이기면 받은 별이 더해지고, 해금에 쓴다.</summary>
        public int Stars;

        /// <summary>스테이지별 최고 별(1부터).</summary>
        public readonly int[] Best = new int[SurvivorStages.Count + 1];
        public readonly HashSet<string> Unlocked = new HashSet<string> { Roster.Default };
        public string Selected = Roster.Default;

        public Firefighter Current
        {
            get { return Roster.Get(Selected); }
        }

        /// <summary>이 스테이지의 시작 장비: 고른 소방관의 장비(모든 스테이지가 같은 아이템 풀이라 스테이지와 상관없다).</summary>
        public IReadOnlyList<UpgradeId> StartFor(StageRules stage)
        {
            return new List<UpgradeId>(Current.Start);
        }

        /// <summary>판 결과를 적는다: 받은 별을 통장에 더하고 최고 기록을 갱신한다. 돌려주는 값은 이번에 번 별.</summary>
        public int RecordResult(int stage, int stars)
        {
            if (stars <= 0) return 0;
            Stars += stars;
            if (stage >= 1 && stage < Best.Length && stars > Best[stage]) Best[stage] = stars;
            return stars;
        }

        public bool IsUnlocked(string id)
        {
            return Unlocked.Contains(id);
        }

        public bool CanUnlock(string id)
        {
            return Roster.Exists(id) && !IsUnlocked(id) && Stars >= Roster.Get(id).Cost;
        }

        /// <summary>별을 써서 해금하고 바로 고른다.</summary>
        public bool Unlock(string id)
        {
            if (!CanUnlock(id)) return false;
            Stars -= Roster.Get(id).Cost;
            Unlocked.Add(id);
            Selected = id;
            return true;
        }

        public bool Select(string id)
        {
            if (!IsUnlocked(id)) return false;
            Selected = id;
            return true;
        }

        /// <summary>"stars=5;best=2,1,0;unlocked=rookie,rescue;selected=rescue"</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("stars=").Append(Stars);
            sb.Append(";best=");
            for (int i = 1; i < Best.Length; i++)
            {
                if (i > 1) sb.Append(',');
                sb.Append(Best[i]);
            }
            sb.Append(";unlocked=");
            bool first = true;
            foreach (Firefighter f in Roster.All)
            {
                if (!Unlocked.Contains(f.Id)) continue;
                if (!first) sb.Append(',');
                sb.Append(f.Id);
                first = false;
            }
            sb.Append(";selected=").Append(Selected);
            return sb.ToString();
        }

        /// <summary>저장 글을 읽는다. 비거나 깨진 글은 새 소방서다. 모르는 소방관 id는 버린다.</summary>
        public static FireStation Parse(string text)
        {
            var station = new FireStation();
            if (string.IsNullOrEmpty(text)) return station;
            foreach (string part in text.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string key = part.Substring(0, eq);
                string value = part.Substring(eq + 1);
                switch (key)
                {
                    case "stars":
                        if (int.TryParse(value, out int stars) && stars >= 0) station.Stars = stars;
                        break;
                    case "best":
                    {
                        string[] cells = value.Split(',');
                        for (int i = 0; i < cells.Length && i + 1 < station.Best.Length; i++)
                        {
                            if (int.TryParse(cells[i], out int best) && best >= 0) station.Best[i + 1] = best;
                        }
                        break;
                    }
                    case "unlocked":
                        foreach (string id in value.Split(','))
                        {
                            if (Roster.Exists(id)) station.Unlocked.Add(id);
                        }
                        break;
                    case "selected":
                        if (Roster.Exists(value)) station.Selected = value;
                        break;
                }
            }
            if (!station.Unlocked.Contains(station.Selected)) station.Selected = Roster.Default;
            return station;
        }
    }
}
