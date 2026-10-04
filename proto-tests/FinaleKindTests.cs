using System;
using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>대화재 종류(맵 특색 패스): 공통 감독 위에 스테이지마다 다른 사건 하나 — 숲 불 전선, 공단 연쇄 폭발, 항구 유조선 좌초, 야시장 불꽃 폭주. 마을은 그대로.</summary>
    [Collection("Heavy")]
    public class FinaleKindTests
    {
        /// <summary>
        /// 적을 치우고 체력을 채우며 t초까지. 다른 소방관이 맡았다고 치고 집·나무 불은 틱마다 끈다(안 끄면 3:00 전에 동네를 잃는다).
        /// 랜드마크·대형 신고·드럼·가판대·배는 그대로 둔다(종류별 사건이 보는 것).
        /// </summary>
        private static void RunRaw(SurvivorSim sim, float t)
        {
            while (sim.Outcome == SOutcome.Playing && sim.Time < t)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                foreach (Structure s in sim.Structures)
                {
                    if (!s.Burning || s == sim.BigReport || s == sim.Landmark) continue;
                    if (s.Kind == StructureKind.Gas || s.Kind == StructureKind.Fireworks || s.Kind == StructureKind.Boat) continue;
                    s.Fire = 0f;
                }
                sim.Step(0f, 0f);
            }
        }

        private static SurvivorSim AtFinale(int stage)
        {
            var sim = new SurvivorSim(3, stage);
            sim.Reports = true;
            RunRaw(sim, SurvivorSim.FinaleAt + 0.1f);
            Assert.True(sim.Finale, "3:00이면 대화재");
            return sim;
        }

        [Fact]
        public void ForestFront_StartsAtTheTreeline_LaysFireRows_AndMovesTowardTheCamp()
        {
            SurvivorSim sim = AtFinale(2);
            Assert.Equal(FinaleKind.FireFront, sim.Stage.Finale);
            Assert.True(sim.FrontY.HasValue && Math.Abs(sim.FrontY.Value - SurvivorSim.FrontStart) < 0.2f, "전선 시작 " + sim.FrontY);
            int rows = 0;
            float until = sim.Time + 10f;
            while (sim.Time < until)
            {
                RunRaw(sim, sim.Time + SurvivorSim.Dt);
                if (sim.JustFront) rows++;
            }
            Assert.True(sim.FrontY.Value < 34.5f, "10초면 1.5칸 넘게 내려온다: " + sim.FrontY);
            Assert.True(rows >= 2, "줄 " + rows);
            Assert.Contains(sim.BurningGround, p => !p.Oil && Math.Abs(p.Pos.Y - sim.FrontY.Value) < 1.5f);
        }

        [Fact]
        public void ForestFront_SlowsDown_WhenTheLastRowIsDoused()
        {
            SurvivorSim sim = AtFinale(2);
            while (sim.FrontRowPuddles.Count == 0) RunRaw(sim, sim.Time + SurvivorSim.Dt);
            float full = sim.FrontSpeedNow;
            Assert.True(full > 0f);
            int half = sim.FrontRowPuddles.Count / 2;
            for (int i = 0; i < half; i++) sim.FrontRowPuddles[i].Out = true;
            float ratio = half / (float)sim.FrontRowPuddles.Count;
            Assert.Equal(full * (1f - (SurvivorSim.FrontHoldMax * ratio)), sim.FrontSpeedNow, 3);
            foreach (Puddle p in sim.FrontRowPuddles) p.Out = true;
            Assert.Equal(full * (1f - SurvivorSim.FrontHoldMax), sim.FrontSpeedNow, 3);
        }

        [Fact]
        public void FactoryChain_IgnitesTwoDrumsAtOnce_ThenOneEveryTwelveSeconds_AndWaterCancelsTheFuse()
        {
            SurvivorSim sim = AtFinale(3);
            Assert.Equal(FinaleKind.ChainBlast, sim.Stage.Finale);
            List<Structure> lit = sim.Structures.FindAll(s => s.Kind == StructureKind.Gas && s.Burning);
            Assert.True(lit.Count >= 2, "시작에 드럼 둘: " + lit.Count);
            Assert.True(lit[0].Fuse > SurvivorSim.GasFuse, "연쇄 점화 퓨즈는 길다: " + lit[0].Fuse);
            // 하나를 물방울로 끈다: 퓨즈가 풀리고 그 자리는 안 터진다.
            Structure drum = lit[0];
            // 물방울은 Life가 다하면 떨어진다(0.5초) → 1초 뒤 본다.
            sim.Shots.Add(new Shot { Kind = ShotKind.Drop, Pos = drum.Pos, Life = 0.5f, Damage = 40f, Radius = 0.6f });
            RunRaw(sim, sim.Time + 1f);
            Assert.False(drum.Burning);
            Assert.True(drum.Fuse < 0f);
            bool chained = false;
            float until = sim.Time + 13f;
            while (sim.Time < until)
            {
                RunRaw(sim, sim.Time + SurvivorSim.Dt);
                if (sim.JustChain != null) chained = true;
            }
            Assert.True(chained, "13초 안에 다음 드럼 점화");
            Assert.DoesNotContain(sim.GasBlasts, at => at.DistanceTo(drum.Pos) < 0.1f);
            Assert.False(drum.Collapsed, "끈 드럼은 안 터진다");
        }

        [Fact]
        public void HarborTanker_IsTheLandmark_DocksAtTheQuay_AndLeaksBurningOilOntoTheQuay_UntilDoused()
        {
            SurvivorSim sim = AtFinale(4);
            Assert.Equal(FinaleKind.Tanker, sim.Stage.Finale);
            Structure t = sim.Landmark;
            Assert.True(t != null && t.Tanker && t.Kind == StructureKind.Boat, "유조선이 랜드마크");
            Assert.True(t.Residents >= SurvivorSim.FinalePeople, "선원 " + t.Residents);
            Assert.Equal(SurvivorHarbor.BoatLane, t.Pos.Y);
            float until = sim.Time + 40f;
            while (!t.Docked && sim.Time < until) RunRaw(sim, sim.Time + SurvivorSim.Dt);
            Assert.True(t.Docked, "40초 안에 부두에 닿는다: " + t.Pos);
            RunRaw(sim, sim.Time + 10f);
            int Leaks() => sim.BurningGround.FindAll(p => p.Oil && p.Pos.Y < SurvivorHarbor.SeaFrom && p.Pos.Y > SurvivorHarbor.SeaFrom - 2f).Count;
            int leaks = Leaks();
            Assert.True(leaks >= 3, "부두 위 불기름 " + leaks);
            Assert.True(t.Burning);
            t.Fire = 0f;
            int before = Leaks();
            RunRaw(sim, sim.Time + 10f);
            Assert.True(Leaks() <= before, "끄면 더 안 흘린다: " + before + " → " + Leaks());
        }

        [Fact]
        public void MarketStorm_LightsEveryStand_RunsLanternLinesFromTheStage_AndTheStageFiresRockets()
        {
            SurvivorSim sim = AtFinale(5);
            Assert.Equal(FinaleKind.RocketStorm, sim.Stage.Finale);
            Assert.Equal(StructureKind.Depot, sim.Landmark.Kind);
            foreach (Structure s in sim.Structures)
            {
                if (s.Kind == StructureKind.Fireworks && !s.Collapsed) Assert.True(s.Burning && s.Fire >= 0.6f, "가판대 전부 점화 " + s.Fire);
            }
            bool storm = false;
            bool rockets = false;
            float until = sim.Time + 12f;
            while (sim.Time < until)
            {
                RunRaw(sim, sim.Time + SurvivorSim.Dt);
                if (sim.JustStorm || sim.Lanterns.Exists(l => l.Storm && l.Burn >= 0f)) storm = true;
                if (sim.Rockets.Count > 0 || sim.RocketBursts.Count > 0) rockets = true;
            }
            Assert.True(storm, "무대에서 가까운 등줄이 탄다");
            Assert.True(rockets, "무대가 로켓을 쏜다");
        }

        [Fact]
        public void TownFinale_IsUnchanged()
        {
            SurvivorSim sim = AtFinale(1);
            Assert.Equal(FinaleKind.Warehouse, sim.Stage.Finale);
            Assert.Equal(StructureKind.Depot, sim.Landmark.Kind);
            Assert.False(sim.Landmark.Tanker);
            Assert.False(sim.FrontY.HasValue);
            Assert.DoesNotContain(sim.Structures, s => s.Kind == StructureKind.Gas && s.Burning && s.Fuse > SurvivorSim.GasFuse);
        }
    }
}
