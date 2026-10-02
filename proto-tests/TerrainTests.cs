using System;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 지형: 물(강)은 소방관과 땅의 불 몹을 막고 절대 안 타며, 건물 불은 강을 못 건넌다. 불씨·박쥐는 날아 건넌다.
    /// </summary>
    public class TerrainTests
    {
        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1);
            sim.Reports = false;
            sim.Structures.Clear();
            sim.Enemies.Clear();
            return sim;
        }

        private static Structure Water(SurvivorSim sim, float x, float y, float hx, float hy)
        {
            var w = new Structure { Kind = StructureKind.Water, Name = "강", Pos = new Vec2(x, y), Half = new Vec2(hx, hy) };
            sim.Structures.Add(w);
            return w;
        }

        private static Structure House(SurvivorSim sim, float x, float y)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(x, y), Half = new Vec2(2f, 1.5f) };
            sim.Structures.Add(s);
            return s;
        }

        /// <summary>한 틱: 불 몹은 치우고 체력은 채운다(지형만 본다).</summary>
        private static void Walk(SurvivorSim sim, float mx, float my, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(mx, my);
            }
        }

        [Fact]
        public void Water_BlocksThePlayer_ButNotOnTheBridge()
        {
            var sim = Quiet();
            float x0 = sim.Player.X;
            // 오른쪽 3칸에 물(왼변 x0+2): 2초를 걸어도 물 앞에서 멈춘다.
            Water(sim, x0 + 3f, sim.Player.Y, 1f, 6f);
            Walk(sim, 1f, 0f, 120);
            Assert.InRange(sim.Player.X, x0 + 1f, x0 + 2f - SurvivorSim.PlayerRadius + 0.05f);

            // 다리: 물이 위아래로 갈라져 가운데 6칸이 비면 그 줄로는 건넌다.
            sim = Quiet();
            x0 = sim.Player.X;
            Water(sim, x0 + 3f, sim.Player.Y + 6f, 1f, 3f);
            Water(sim, x0 + 3f, sim.Player.Y - 6f, 1f, 3f);
            Walk(sim, 1f, 0f, 120);
            Assert.True(sim.Player.X > x0 + 5f, "다리를 못 건넜다: " + sim.Player.X);
        }

        [Fact]
        public void Water_BlocksBlaze_ButBatsAndSeekersCross()
        {
            var sim = Quiet();
            Vec2 p = sim.Player;
            Structure river = Water(sim, p.X + 3f, p.Y, 1f, 20f);
            Enemy blaze = sim.Spawn(EnemyKind.Blaze, new Vec2(p.X + 10f, p.Y));
            Enemy bat = sim.Spawn(EnemyKind.Bat, new Vec2(p.X + 10f, p.Y + 2f));
            // 불씨는 강 건너(소방관 쪽) 나무를 노린다(탈 것이 없으면 4초 뒤 사그라든다).
            sim.Structures.Add(new Structure { Kind = StructureKind.Tree, Name = "나무", Pos = new Vec2(p.X, p.Y - 2f), Half = new Vec2(0.6f, 0.6f) });
            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(p.X + 8f, p.Y - 2f));
            ember.Seeker = true;
            for (int i = 0; i < 300; i++)
            {
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                // 큰 불은 한 번도 물 안에 있지 않다.
                Assert.False(river.Within(blaze.Pos, 0f), "큰 불이 물 안: " + blaze.Pos.X + "," + blaze.Pos.Y);
            }
            Assert.True(blaze.Pos.X >= p.X + 4f - 0.01f, "큰 불이 강을 건넜다: " + blaze.Pos.X);
            Assert.True(bat.Pos.X < p.X + 2f, "박쥐가 못 건넜다: " + bat.Pos.X);
            Assert.True(ember.Pos.X < p.X + 2f, "불씨가 못 건넜다: " + ember.Pos.X);
        }

        [Fact]
        public void Water_NeverIgnites()
        {
            var sim = Quiet();
            Structure river = Water(sim, sim.Player.X + 5f, sim.Player.Y, 1f, 10f);
            Assert.False(river.Flammable);
            Assert.False(sim.Ignite(river, 1f));
            Assert.Equal(0f, river.Fire);
            Assert.False(river.Burning);

            // 불씨가 물에 닿아도 안 붙는다(탈 것이 없으면 소방관을 쫓다 물에 닿는다).
            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(sim.Player.X + 7f, sim.Player.Y));
            ember.Seeker = true;
            for (int i = 0; i < 180; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.Equal(0f, river.Fire);
            Assert.Empty(sim.Structures.FindAll(s => s.Burning));
        }

        [Fact]
        public void Fire_DoesNotSpreadAcrossTheRiver()
        {
            // 강 건너 8칸(번짐 사거리 9 안)의 집: 20초 동안 안 옮는다.
            var sim = Quiet();
            float y = sim.Player.Y + 14f;
            Structure a = House(sim, sim.Player.X - 8f, y);
            Structure b = House(sim, sim.Player.X + 4f, y);
            Water(sim, sim.Player.X - 2f, y, 1f, 10f);
            sim.Ignite(a, 1f);
            bool spread = Burn(sim, a, b, 20f);
            Assert.False(spread, "강 건너로 번졌다");
            Assert.Equal(0f, b.Fire);

            // 강이 없으면 같은 거리에서 번진다.
            sim = Quiet();
            a = House(sim, sim.Player.X - 8f, y);
            b = House(sim, sim.Player.X + 4f, y);
            sim.Ignite(a, 1f);
            Assert.True(Burn(sim, a, b, 20f), "강이 없는데 안 번졌다");
        }

        /// <summary>a를 1.0 불로 유지하며 seconds 동안 돌린다. b로 번지면 true.</summary>
        private static bool Burn(SurvivorSim sim, Structure a, Structure b, float seconds)
        {
            for (int i = 0; i < (int)(seconds / SurvivorSim.Dt); i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                a.Fire = 1f;
                a.Integrity = 1f;
                sim.Step(0f, 0f);
                if (b.Burning || sim.Spread.Contains(b)) return true;
            }
            return false;
        }

        [Fact]
        public void Forest_WindAlwaysBlowsSouthward()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var sim = new SurvivorSim(seed, 2);
                sim.Reports = false;
                int shifts = 0;
                Vec2 last = default;
                while (sim.Time < (SurvivorSim.WindShiftEvery * 3f) + 1f)
                {
                    sim.Enemies.Clear();
                    sim.Hp = sim.MaxHp;
                    if (sim.PendingChoices != null) sim.Choose(0);
                    sim.Step(0f, 0f);
                    Assert.True(sim.Wind.Y < -0.5f, "바람이 캠프 쪽이 아니다: " + sim.Wind.X + "," + sim.Wind.Y);
                    if (sim.JustWindShift)
                    {
                        shifts++;
                        Assert.False(sim.Wind.X == last.X && sim.Wind.Y == last.Y, "바뀌었는데 같은 방향");
                    }
                    last = sim.Wind;
                }
                Assert.Equal(3, shifts);
            }
        }

        [Fact]
        public void Factory_OneDrumLine_IgnitesItsPlant()
        {
            var sim = new SurvivorSim(1, 3);
            sim.Reports = false;
            // 북서 줄(인쇄소 앞): 끝 드럼 하나에 불을 붙이면 줄이 다 터지고 인쇄소에 불이 붙는다.
            Structure plant = sim.Structures.Find(s => s.Name == "인쇄소");
            var line = sim.Structures.FindAll(s => s.Kind == StructureKind.Gas && Math.Abs(s.Pos.X - plant.Pos.X) < 4f && s.Pos.Y < plant.Pos.Y);
            Assert.Equal(SurvivorFactory.DrumsPerLine, line.Count);
            sim.Ignite(line[0], 0.5f);
            for (int i = 0; i < (int)(12f / SurvivorSim.Dt); i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
            }
            Assert.All(line, d => Assert.True(d.Collapsed, "줄 드럼이 안 터졌다"));
            Assert.True(plant.Fire > 0f || plant.Collapsed, "인쇄소에 불이 안 붙었다");
            // 반대편 줄은 멀쩡하다(골목 건너 32칸).
            Assert.All(sim.Structures.FindAll(s => s.Kind == StructureKind.Gas && s.Pos.X > 40f), d => Assert.False(d.Collapsed));
        }

        [Fact]
        public void Bot_CrossesTheBridge()
        {
            var sim = new SurvivorSim(1);
            sim.Reports = false;
            var bot = new SurvivorBot(sim);
            // 봇은 강 서쪽 빵집 밑, 불은 동쪽 서점(1.0, 주민 있음 → 문 앞으로 간다).
            sim.Player = new Vec2(16f, 40f);
            Structure shop = sim.Structures.Find(s => s.Name == "서점");
            sim.Ignite(shop, 1f);
            bool crossed = false;
            for (int i = 0; i < (int)(60f / SurvivorSim.Dt) && !crossed; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                shop.Fire = 1f;
                shop.Integrity = 1f;
                Vec2 m = bot.Move();
                sim.Step(m.X, m.Y);
                crossed = sim.Player.X > SurvivorTown.RiverX + SurvivorTown.RiverHalf;
            }
            Assert.True(crossed, "봇이 60초 안에 강을 못 건넜다: " + sim.Player.X + "," + sim.Player.Y);
        }

        [Fact]
        public void EdgeSpawns_NeverLandInWater()
        {
            var sim = Quiet();
            // 소방관 17칸 오른쪽(스폰 고리)에 세로 물 띠: 거기 떨어진 몹은 물 밖으로 밀린다.
            Structure river = Water(sim, sim.Player.X + 17f, sim.Player.Y, 2f, 29f);
            for (int i = 0; i < 600; i++)
            {
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                foreach (Enemy e in sim.Enemies)
                {
                    if (e.Dead || e.Seeker || e.Kind == EnemyKind.Bat) continue;
                    Assert.False(river.Within(e.Pos, 0f), e.Kind + "이 물 안에 있다: " + e.Pos.X + "," + e.Pos.Y);
                }
            }
            Assert.True(sim.Enemies.Count > 10);
        }
    }
}
