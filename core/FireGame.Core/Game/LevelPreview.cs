using System;
using System.Collections.Generic;
using System.Globalization;
using FireGame.Core.Data;
using FireGame.Core.Grid;
using FireGame.Core.Sim;

namespace FireGame.Core.Game
{
    /// <summary>
    /// 상점 카드에 "게임 말로" 보여 줄 레벨 효과.
    /// "위력 140% → 160%"는 현장에서 무엇이 달라지는지 알려 주지 않는다.
    /// 대신 "기름불 한 칸: 3발 → 2발"처럼 손으로 느끼는 차이를 실제 시뮬레이션으로 재서 보여 준다.
    /// </summary>
    public static class LevelPreview
    {
        private const int WarmupTicks = 50;
        private const int MaxShots = 99;

        /// <summary>약제별로 주로 상대하는 불.</summary>
        public static FireClass MainClass(EquipmentDef def)
        {
            switch (def.Agent.Type)
            {
                case AgentType.CO2: return FireClass.C;
                case AgentType.Foam: return FireClass.B;
                default: return FireClass.A;
            }
        }

        public static string FireName(FireClass fireClass)
        {
            switch (fireClass)
            {
                case FireClass.B: return "기름불";
                case FireClass.C: return "전기불";
                default: return "나무불";
            }
        }

        private static MaterialId MaterialOf(FireClass fireClass)
        {
            switch (fireClass)
            {
                case FireClass.B: return MaterialId.Oil;
                case FireClass.C: return MaterialId.Electric;
                default: return MaterialId.Wood;
            }
        }

        /// <summary>
        /// 화재 규모가 intensity인 그 등급 불길(5x5가 다 타는 중)의 한가운데 한 칸을 이 장비로 끄는 데 드는 발 수.
        /// 발 사이에는 장비 연사 간격만큼 불이 다시 달아오른다. 99발로도 못 끄면 99.
        /// 나무는 연료를 넉넉히 줘서 스스로 타 버리기 전의 "한창 타는" 상태를 잰다.
        /// </summary>
        public static int ShotsToPutOut(EquipmentDef def, FireClass fireClass, float intensity)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (Suppression.EffectivenessOf(def.Agent.Type, fireClass) <= 0f) return MaxShots;

            // 빽빽한 불길 한가운데. 한 줄로 타는 불은 Lv2~4면 전부 한 발이라 레벨 차이가 안 보인다.
            // 한가운데는 양동이 Lv1~3으로는 안 꺼진다(설계상 가장자리부터 방화선으로 끊어야 한다).
            var grid = new FireGrid(5, 5);
            for (int i = 0; i < grid.Count; i++)
            {
                grid.Cells[i].Material = (byte)MaterialOf(fireClass);
                grid.Cells[i].Fuel = 1000f;
                grid.Cells[i].State = CellState.Burning;
            }

            var sim = new FireSim(grid) { Intensity = intensity };
            for (int t = 0; t < WarmupTicks; t++) sim.Tick();

            int ticksBetweenShots = Math.Max(1, (int)Math.Ceiling(def.CooldownSeconds / SimConfig.TickDelta));
            for (int shot = 1; shot <= MaxShots; shot++)
            {
                if (Suppression.Apply(grid, 2, 2, def.Agent) == SuppressionOutcome.Extinguished) return shot;
                for (int t = 0; t < ticksBetweenShots; t++) sim.Tick();
            }

            return MaxShots;
        }

        /// <summary>
        /// 그 등급 불이 있는 현장 중 지금 열린 가장 센 현장(화재 규모가 같으면 뒤 현장). 열린 곳이 없으면 그 등급이 있는 첫 현장.
        /// "지금 내가 상대하는 불" 기준으로 보여 줘야 숫자가 와닿는다.
        /// </summary>
        public static StageDef ReferenceStage(SaveData save, FireClass fireClass)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            StageDef best = null;
            StageDef first = null;
            foreach (MissionDef mission in Campaign.Missions)
            {
                if (!HasFire(mission.Stage, fireClass)) continue;
                if (first == null) first = mission.Stage;
                if (!save.IsMissionUnlocked(mission)) continue;
                if (best == null || mission.Stage.FireIntensity >= best.FireIntensity) best = mission.Stage;
            }

            return best ?? first ?? Campaign.Missions[0].Stage;
        }

        private static bool HasFire(StageDef stage, FireClass fireClass)
        {
            FireGrid grid = MapLoader.Parse(stage.Map).Grid;
            for (int i = 0; i < grid.Count; i++)
            {
                if (Materials.Of(grid.Cells[i].Material).Class == fireClass) return true;
            }
            return false;
        }

        // 상점 카드 폭이 좁아 현장 이름을 짧게 쓴다. 인덱스 = 스테이지 id.
        private static readonly string[] ShortPlaces = { "주택가", "상가", "주유소", "창고", "공장", "항구" };

        private static string PlaceName(StageDef stage)
        {
            return stage.Id >= 0 && stage.Id < ShortPlaces.Length ? ShortPlaces[stage.Id] : stage.Name;
        }

        /// <summary>뿌리는 칸 수: 부채꼴은 3갈래 × 사거리, 직선은 사거리 + 끝 퍼짐.</summary>
        public static int CellsCovered(EquipmentDef def)
        {
            switch (def.Pattern)
            {
                case AimPattern.Cone: return 3 * Math.Max(1, def.Range);
                case AimPattern.Line: return def.Range + (2 * def.EndSpread);
                default: return 1;
            }
        }

        /// <summary>
        /// 다음 레벨에서 좋아지는 것들. 특성이 바뀌면 그 줄이 맨 앞이다.
        /// 최대 레벨이면 지금 값만, 잠겨 있으면 해금 안내와 Lv1 값.
        /// </summary>
        public static List<string> NextLevelLines(SaveData save, UpgradeTrack track)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (track == null) throw new ArgumentNullException(nameof(track));

            int level = save.LevelOf(track.Id);
            bool maxed = level >= track.MaxLevel;
            int next = maxed ? level : level + 1;
            var lines = new List<string>();

            switch (track.Kind)
            {
                case UpgradeKind.Suit:
                    lines.Add(GearStats.SuitName(next));
                    lines.Add("불 속에서 버티는 시간\n" + Change(Seconds(SecondsInFire(level)), Seconds(SecondsInFire(next)), maxed));
                    return lines;

                case UpgradeKind.Boots:
                    lines.Add("초당 " + Change(Speed(level), Speed(next), maxed) + "칸 달린다");
                    return lines;
            }

            EquipmentDef def = EquipmentCatalog.ById(track.Id);
            FireClass fireClass = MainClass(def);
            StageDef stage = ReferenceStage(save, fireClass);
            string fire = FireName(fireClass) + " 한가운데(" + PlaceName(stage) + ")";

            if (level == 0)
            {
                lines.Add("해금하면 현장에 들고 간다");
                lines.Add(fire + "\n" + Shots(ShotsToPutOut(def.AtLevel(1), fireClass, stage.FireIntensity)));
                return lines;
            }

            EquipmentDef now = def.AtLevel(level);
            EquipmentDef then = def.AtLevel(next);

            if (!maxed && EquipmentDef.Milestones(next) > EquipmentDef.Milestones(level))
            {
                string milestone = MilestoneText(def, next);
                if (milestone != null) lines.Add("특성! " + milestone);
            }

            lines.Add(fire + "\n" + Change(Shots(ShotsToPutOut(now, fireClass, stage.FireIntensity)), Shots(ShotsToPutOut(then, fireClass, stage.FireIntensity)), maxed));

            if (def.Resource == ResourceKind.Charges) lines.Add("횟수 " + Change(now.MaxCharges.ToString(CultureInfo.InvariantCulture), then.MaxCharges.ToString(CultureInfo.InvariantCulture), maxed));
            else if (def.Pattern == AimPattern.Line && then.Range != now.Range) lines.Add("사거리 " + Change(now.Range.ToString(CultureInfo.InvariantCulture), then.Range.ToString(CultureInfo.InvariantCulture), maxed) + "칸");
            else if (then.CooldownSeconds != now.CooldownSeconds) lines.Add("연사 " + Change(Seconds(now.CooldownSeconds), Seconds(then.CooldownSeconds), maxed));

            return lines;
        }

        /// <summary>다음 특성: "Lv10 · 앞 12칸에 뿜기". 더 없으면 null.</summary>
        public static string NextMilestone(UpgradeTrack track, int level)
        {
            if (track == null || track.Kind != UpgradeKind.Equipment) return null;

            EquipmentDef def = EquipmentCatalog.ById(track.Id);
            int target = (EquipmentDef.Milestones(level) + 1) * EquipmentDef.MilestoneEvery;
            if (target > track.MaxLevel) return null;

            string text = MilestoneText(def, target);
            return text == null ? null : "Lv" + target + " · " + text;
        }

        /// <summary>그 레벨에서 받는 특성 설명. 그 레벨에 특성이 없으면 null.</summary>
        private static string MilestoneText(EquipmentDef def, int level)
        {
            if (level % EquipmentDef.MilestoneEvery != 0) return null;
            EquipmentDef at = def.AtLevel(level);

            if (def.Pattern == AimPattern.Cone)
            {
                return "앞 " + CellsCovered(at) + "칸에 " + (def.Agent.Type == AgentType.Water ? "끼얹기" : "뿜기");
            }

            if (def.Pattern == AimPattern.Line)
            {
                int milestones = EquipmentDef.Milestones(level);
                if (milestones == 1) return "물줄기 끝이 T자로 퍼진다";
                if (milestones == 2) return "연사 " + Seconds(at.CooldownSeconds);
            }

            return null;
        }

        /// <summary>불 속에서 버티는 시간(초) = 최대 체력 ÷ (불 속 초당 피해 × 방화복 배율).</summary>
        public static float SecondsInFire(int suitLevel)
        {
            return GameConfig.PlayerMaxHp / (GameConfig.FireDamageInCell * GearStats.DamageMultiplier(suitLevel));
        }

        private static string Change(string now, string next, bool maxed)
        {
            return maxed || now == next ? now : now + " → " + next;
        }

        private static string Shots(int shots)
        {
            return shots >= MaxShots ? "못 끈다" : shots + "발";
        }

        private static string Seconds(float seconds)
        {
            return seconds.ToString("0.0", CultureInfo.InvariantCulture) + "초";
        }

        private static string Speed(int bootsLevel)
        {
            return (GameConfig.PlayerSpeed * GearStats.SpeedMultiplier(bootsLevel)).ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
