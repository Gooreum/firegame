using System;
using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 대화재 감독: 신고 틱마다 체력·남은 건물 여유를 보고 압력 단계(0~3)를 한 칸씩 올리거나 내린다.
    /// 잘하는 사람에겐 신고 둘씩·더 자주·불씨 더 많이, 무너질 사람에겐 숨을 준다.
    /// </summary>
    public class DirectorTests
    {
        /// <summary>한 틱: 불 몹은 치우고, 체력은 hp 비율로 맞추고, 카드는 첫 장, 불은 전부 끈다(감독이 보는 건 체력과 건물 여유뿐).</summary>
        private static void Tick(SurvivorSim sim, float hp = 1f)
        {
            sim.Enemies.Clear();
            sim.Hp = sim.MaxHp * hp;
            if (sim.PendingChoices != null) sim.Choose(0);
            foreach (Structure s in sim.Structures) s.Fire = 0f;
            sim.Step(0f, 0f);
        }

        private static void RunTo(SurvivorSim sim, float t, float hp = 1f)
        {
            while (sim.Outcome == SOutcome.Playing && sim.Time < t) Tick(sim, hp);
        }

        /// <summary>다음 압력 변화 틱까지 가서 그 틱의 신고(점화) 수를 돌려준다. 변화가 없으면 -1.</summary>
        private static int ToNextChange(SurvivorSim sim, float hp, float limit = 12f)
        {
            int before = sim.FinalePressure;
            float end = sim.Time + limit;
            while (sim.Outcome == SOutcome.Playing && sim.Time < end)
            {
                Tick(sim, hp);
                if (sim.FinalePressure != before) return sim.Ignited.Count;
            }
            return -1;
        }

        [Fact]
        public void Finale_RaisesPressure_WhenTheTownIsSafe()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            RunTo(sim, SurvivorSim.FinaleAt + 0.1f);
            Assert.True(sim.Finale);
            Assert.Equal(0, sim.FinalePressure);
            Assert.Equal(SurvivorSim.FinaleReportEvery, sim.FinaleReportGap);

            // 첫 신고 틱(8초 뒤): 체력 가득·건물 무사 → 1단계, 신고 1건.
            Assert.Equal(1, ToNextChange(sim, 1f));
            Assert.Equal(1, sim.FinalePressure);
            Assert.True(sim.JustPressureUp);
            Assert.InRange(sim.Time, SurvivorSim.FinaleAt + 7.9f, SurvivorSim.FinaleAt + 8.2f);
            Assert.Equal(6.5f, sim.FinaleReportGap);

            // 6.5초 뒤 2단계: 이 틱부터 신고가 둘씩.
            Assert.Equal(2, ToNextChange(sim, 1f));
            Assert.Equal(2, sim.FinalePressure);
            Assert.Equal(5f, sim.FinaleReportGap);

            // 5초 뒤 3단계(최대), 간격 3.5초.
            Assert.Equal(2, ToNextChange(sim, 1f));
            Assert.Equal(3, sim.FinalePressure);
            Assert.Equal(3.5f, sim.FinaleReportGap);
            Assert.Equal(3, sim.Stats.PressurePeak);

            // 최대에서는 더 안 오른다.
            Assert.Equal(-1, ToNextChange(sim, 1f, 8f));
            Assert.Equal(3, sim.FinalePressure);
        }

        [Fact]
        public void Finale_LowersPressure_WhenHpIsLow()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            RunTo(sim, SurvivorSim.FinaleAt + 0.1f);
            for (int i = 0; i < 3; i++) ToNextChange(sim, 1f);
            Assert.Equal(3, sim.FinalePressure);

            // 체력 20%: 틱마다 한 단계씩 내려간다. 2단계는 아직 둘씩, 1단계부터 하나씩.
            Assert.Equal(2, ToNextChange(sim, 0.2f));
            Assert.Equal(2, sim.FinalePressure);
            Assert.False(sim.JustPressureUp);
            Assert.Equal(1, ToNextChange(sim, 0.2f));
            Assert.Equal(1, sim.FinalePressure);
            Assert.Equal(1, ToNextChange(sim, 0.2f));
            Assert.Equal(0, sim.FinalePressure);
            // 0 밑으로는 안 간다.
            Assert.Equal(-1, ToNextChange(sim, 0.2f, 10f));
            Assert.Equal(0, sim.FinalePressure);
            // 최고 단계 기록은 남는다.
            Assert.Equal(3, sim.Stats.PressurePeak);
        }

        [Fact]
        public void Finale_LowersPressure_WhenOneMoreHouseLoses()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            RunTo(sim, SurvivorSim.FinaleAt + 0.1f);
            for (int i = 0; i < 2; i++) ToNextChange(sim, 1f);
            Assert.Equal(2, sim.FinalePressure);

            // 건물 9채: 한 채만 더 잃으면 패배인 상태(여유 1)로 만든다. 체력은 가득인데도 내려간다.
            int room = sim.HousesRoom;
            Assert.True(room >= 2, "여유 " + room);
            sim.HousesLost += room - 1;
            Assert.Equal(1, sim.HousesRoom);
            ToNextChange(sim, 1f);
            Assert.Equal(1, sim.FinalePressure);

            // 중간(체력 52%, 여유 넉넉): 오르지도 내리지도 않는다.
            sim.HousesLost = 0;
            Assert.Equal(-1, ToNextChange(sim, 0.52f, 10f));
            Assert.Equal(1, sim.FinalePressure);
        }

        [Fact]
        public void Pressure_ScalesBursts_AndForestWind()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            for (int p = 0; p <= SurvivorSim.PressureMax; p++)
            {
                sim.FinalePressure = p;
                Assert.Equal(8 + (4 * p), sim.FinaleBurstCount);
                Assert.Equal(8f - (1.5f * p), sim.FinaleReportGap);
            }
            Assert.Equal(0.1f, SurvivorSim.PressureWindStep);
            Assert.InRange(SurvivorStages.Get(2).FinaleWindChance + (SurvivorSim.PressureWindStep * SurvivorSim.PressureMax), 0.6f, 0.7f);

            // 3단계 대화재의 랜드마크 불씨 분출은 한 번에 20개(튕겨 나가는 세기 6으로 가장자리 스폰과 구분한다).
            sim = new SurvivorSim(2) { Guardian = false };
            RunTo(sim, SurvivorSim.FinaleAt + 0.1f);
            Assert.NotNull(sim.Landmark);
            sim.FinalePressure = SurvivorSim.PressureMax;
            int burst = -1;
            for (int i = 0; i < (int)(SurvivorSim.FinaleBurstEvery * 2f / SurvivorSim.Dt) && burst < 0; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Landmark.Fire = 1f;
                sim.Landmark.Integrity = 1f;
                sim.Step(0f, 0f);
                // 같은 틱에 튕김이 조금 줄어(×0.88) 6보다 작게 남는다. 가장자리 스폰은 0.
                if (sim.JustBurst) burst = sim.Enemies.FindAll(e => Math.Sqrt((e.Knock.X * e.Knock.X) + (e.Knock.Y * e.Knock.Y)) > 3f).Count;
            }
            Assert.Equal(20, burst);
        }

        [Fact]
        public void Pressure_MakesFiresBigger_HeatHotter_AndRingsOfHeavyBlazes()
        {
            // 신고 불 세기 0.35 → 0.5 → 0.65 → 0.8, 열기 1 → 2.5배.
            var sim = new SurvivorSim(1) { Guardian = false };
            for (int p = 0; p <= SurvivorSim.PressureMax; p++)
            {
                sim.FinalePressure = p;
                Assert.InRange(sim.FinaleReportFire, 0.35f + (0.15f * p) - 0.001f, 0.35f + (0.15f * p) + 0.001f);
                Assert.Equal(8 + (4 * p), sim.FinaleRingCount);
            }
            Assert.Equal(0.5f, SurvivorSim.PressureHeatStep);

            // 2단계부터 8초마다 큰 불 고리: 6칸 둘레에 16마리, 레벨만큼 질기고(1 + 0.15×Lv) 1.5배 빠르며 거의 안 밀린다.
            sim = new SurvivorSim(2) { Guardian = false };
            RunTo(sim, SurvivorSim.FinaleAt + 0.1f);
            ToNextChange(sim, 1f);
            ToNextChange(sim, 1f);
            Assert.Equal(2, sim.FinalePressure);
            sim.Enemies.Clear();
            sim.Hp = sim.MaxHp;
            foreach (Structure s in sim.Structures) s.Fire = 0f;
            bool rang = false;
            for (int i = 0; i < (int)(SurvivorSim.FinaleRingEvery / SurvivorSim.Dt) + 2 && !rang; i++)
            {
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                rang = sim.JustWave;
            }
            Assert.True(rang, "2단계인데 고리가 안 왔다");
            var heavy = sim.Enemies.FindAll(e => e.Heavy);
            Assert.Equal(sim.FinaleRingCount, heavy.Count);
            Enemy plain = sim.Spawn(EnemyKind.Blaze, sim.Player);
            foreach (Enemy e in heavy)
            {
                Assert.Equal(EnemyKind.Blaze, e.Kind);
                Assert.InRange(e.Pos.DistanceTo(sim.Player), SurvivorSim.FinaleRingRadius - 1f, SurvivorSim.FinaleRingRadius + 1f);
                // 같은 시각의 보통 큰 불보다 (1 + 0.15×Lv)배 질기다.
                Assert.InRange(e.MaxHp / plain.MaxHp, sim.FinaleRingToughness - 0.02f, sim.FinaleRingToughness + 0.02f);
                Assert.InRange(e.Speed, 1.5f * SurvivorSim.HeavySpeed - 0.01f, 1.5f * SurvivorSim.HeavySpeed + 0.01f);
            }
            // 1단계 밑에선 고리가 없다.
            sim.FinalePressure = 1;
            sim.Enemies.Clear();
            for (int i = 0; i < (int)(SurvivorSim.FinaleRingEvery / SurvivorSim.Dt) + 2; i++)
            {
                sim.Hp = sim.MaxHp * 0.2f; // 감독이 더 못 올리게
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                Assert.Empty(sim.Enemies.FindAll(e => e.Heavy));
            }
        }

        [Fact]
        public void HeavyBlaze_BarelyMoves_WhenKnocked()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            sim.Reports = false;
            sim.Structures.Clear();
            sim.Enemies.Clear();
            Vec2 p = sim.Player;
            Enemy light = sim.Spawn(EnemyKind.Blaze, new Vec2(p.X + 2f, p.Y));
            Enemy heavy = sim.Spawn(EnemyKind.Blaze, new Vec2(p.X + 2f, p.Y + 0.01f));
            heavy.Heavy = true;
            light.Hp = light.MaxHp = heavy.Hp = heavy.MaxHp = 999f;
            // 호스로 둘 다 민다.
            sim.Aim = new Vec2(1f, 0f);
            sim.Spraying = true;
            for (int i = 0; i < 90; i++) sim.Step(0f, 0f);
            float lightPush = light.Pos.X - (p.X + 2f);
            float heavyPush = heavy.Pos.X - (p.X + 2f);
            Assert.True(heavyPush < lightPush * 0.5f, "질긴 큰 불이 많이 밀렸다: " + heavyPush + " (보통 " + lightPush + ")");
        }

        [Fact]
        public void BeforeTheFinale_PressureStaysZero()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            RunTo(sim, 100f);
            Assert.False(sim.Finale);
            Assert.Equal(0, sim.FinalePressure);
            Assert.Equal(SurvivorSim.FinaleReportEvery, sim.FinaleReportGap);
            Assert.Equal(SurvivorSim.FinaleBurst, sim.FinaleBurstCount);
            Assert.Equal(0, sim.Stats.PressurePeak);
        }
    }
}
