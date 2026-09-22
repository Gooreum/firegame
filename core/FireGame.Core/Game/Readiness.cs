using System;
using System.Collections.Generic;

namespace FireGame.Core.Game
{
    /// <summary>필요 장비 하나가 얼마나 모자란가.</summary>
    public readonly struct Shortfall
    {
        public readonly int TrackId;
        public readonly int CurrentLevel;
        public readonly int RequiredLevel;

        /// <summary>지금 레벨에서 필요 레벨까지 올리는 값의 합.</summary>
        public readonly int Cost;

        public Shortfall(int trackId, int currentLevel, int requiredLevel, int cost)
        {
            TrackId = trackId;
            CurrentLevel = currentLevel;
            RequiredLevel = requiredLevel;
            Cost = cost;
        }
    }

    /// <summary>
    /// "이 신고를 풀 준비가 됐나"와 "다음에 뭘 사야 하나".
    /// 브리핑·지도·결과·상점이 모두 이것만 보고 그려서, 화면마다 말이 달라지지 않게 한다.
    /// </summary>
    public static class Readiness
    {
        /// <summary>모자란 필요 장비. 다 갖췄으면 빈 목록.</summary>
        public static List<Shortfall> Missing(SaveData save, MissionDef mission)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            var missing = new List<Shortfall>();
            if (mission == null) return missing;

            foreach (Requirement requirement in mission.Requirements)
            {
                int current = save.LevelOf(requirement.TrackId);
                if (current >= requirement.Level) continue;

                UpgradeTrack track = UpgradeCatalog.ById(requirement.TrackId);
                int cost = 0;
                for (int level = current + 1; level <= requirement.Level; level++)
                {
                    int step = track != null ? track.CostToReach(level) : -1;
                    if (step > 0) cost += step;
                }

                missing.Add(new Shortfall(requirement.TrackId, current, requirement.Level, cost));
            }

            return missing;
        }

        public static bool IsReady(SaveData save, MissionDef mission)
        {
            return Missing(save, mission).Count == 0;
        }

        /// <summary>필요 장비를 전부 갖추는 데 드는 돈. 이미 갖췄으면 0.</summary>
        public static int CostToReady(SaveData save, MissionDef mission)
        {
            int total = 0;
            foreach (Shortfall shortfall in Missing(save, mission)) total += shortfall.Cost;
            return total;
        }

        /// <summary>신고가 들어온 현장 = 열려 있는데 아직 별이 없는 첫 현장. 다 깼으면 null.</summary>
        public static MissionDef NextCall(SaveData save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            foreach (MissionDef mission in Campaign.Missions)
            {
                if (save.IsMissionUnlocked(mission) && save.StarsFor(mission.Id) == 0) return mission;
            }
            return null;
        }

        /// <summary>지금 돈으로 한 레벨 올릴 수 있는 상점 항목 수.</summary>
        public static int AffordableUpgrades(SaveData save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            int count = 0;
            foreach (UpgradeTrack track in UpgradeCatalog.All)
            {
                int cost = Shop.NextCost(save, track.Id);
                if (cost >= 0 && save.Money >= cost) count++;
            }
            return count;
        }
    }
}
