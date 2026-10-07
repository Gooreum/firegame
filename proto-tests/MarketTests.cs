using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>5스테이지 야시장: 맵, 등줄을 타는 불, 불꽃 가판대 로켓, 폭죽, 물 불꽃놀이·물안개.</summary>
    public class MarketTests
    {
        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1, 5) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Lanterns.Clear();
            sim.Reports = false;
            return sim;
        }

        private static Structure Stall(SurvivorSim sim, float x, float y)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "점포", Pos = new Vec2(x, y), Half = SurvivorMarket.StallHalf, Residents = 1 };
            sim.Structures.Add(s);
            return s;
        }

        private static Lantern Link(SurvivorSim sim, Structure a, Structure b)
        {
            var l = new Lantern { A = a, B = b };
            sim.Lanterns.Add(l);
            return l;
        }

        private static void Run(SurvivorSim sim, float seconds)
        {
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Step(0f, 0f);
                sim.Enemies.Clear();
            }
        }

        /// <summary>자리에 물방울 하나(한 틱에 터진다).</summary>
        private static void Splash(SurvivorSim sim, Vec2 at, float damage = 10f)
        {
            sim.Shots.Add(new Shot { Kind = ShotKind.Drop, Pos = at, Life = 0.5f, Damage = damage, Radius = 0.6f });
        }

        private static void Take(SurvivorSim sim, UpgradeId id)
        {
            sim.PendingChoices = new List<UpgradeId> { id };
            sim.Choose(0);
        }

        [Fact]
        public void LanternLine_CarriesFireToTheNextStall_AfterDelayAndRun()
        {
            var sim = Quiet();
            Structure a = Stall(sim, 20f, 30f);
            Structure b = Stall(sim, 27f, 30f);
            Lantern line = Link(sim, a, b);
            sim.Ignite(a, 0.7f);
            Run(sim, 5.5f);
            Assert.False(b.Burning, "2초 뒤 붙어 4초에 건너니 5.5초엔 아직");
            Assert.True(line.Burn > 0.5f, "줄 불이 건너는 중: " + line.Burn);
            Assert.Same(a, line.From);
            bool caught = false;
            for (int i = 0; i < 60 && !caught; i++)
            {
                sim.Step(0f, 0f);
                sim.Enemies.Clear();
                if (sim.LanternCaught.Contains(line)) { caught = true; Assert.Contains(a, sim.SpreadFrom); Assert.Contains(b, sim.Spread); }
            }
            Assert.True(caught, "6.5초 안에 건너간다");
            Assert.True(b.Burning);
            Assert.True(line.Cool > 0f);
        }

        [Fact]
        public void WaterOnTheLine_PutsOutTheRunningFire_AndKeepsItWet()
        {
            var sim = Quiet();
            Structure a = Stall(sim, 20f, 30f);
            Structure b = Stall(sim, 27f, 30f);
            Lantern line = Link(sim, a, b);
            sim.Ignite(a, 0.7f);
            Run(sim, 4f);
            Assert.True(line.Burn > 0f);
            Splash(sim, new Vec2(23.5f, 30f));
            sim.Step(0f, 0f);
            Assert.Contains(line, sim.LanternDoused);
            Assert.True(line.Burn < 0f);
            Assert.True(line.Wet > 0f);
            Run(sim, 5f);
            Assert.False(b.Burning, "젖은 줄로는 못 건넌다");
        }

        [Fact]
        public void DousingTheSourceStall_PutsOutTheLineFire()
        {
            var sim = Quiet();
            Structure a = Stall(sim, 20f, 30f);
            Structure b = Stall(sim, 27f, 30f);
            Lantern line = Link(sim, a, b);
            sim.Ignite(a, 0.7f);
            Run(sim, 4f);
            Assert.True(line.Burn > 0f);
            Splash(sim, a.Pos, 400f);
            sim.Step(0f, 0f);
            Assert.False(a.Burning);
            sim.Step(0f, 0f);
            Assert.True(line.Burn < 0f, "출발 점포가 꺼지면 줄 불도 꺼진다");
            Run(sim, 4f);
            Assert.False(b.Burning);
        }

        [Fact]
        public void Curtain_WetsLinesAroundTheFirefighter()
        {
            var sim = Quiet();
            Take(sim, UpgradeId.Curtain);
            Structure a = Stall(sim, sim.Player.X - 3f, sim.Player.Y + 2f);
            Structure b = Stall(sim, sim.Player.X + 3f, sim.Player.Y + 2f);
            Lantern line = Link(sim, a, b);
            bool burst = false;
            for (int i = 0; i < 60 * 5 && !burst; i++)
            {
                sim.Step(0f, 0f);
                burst = sim.JustCurtain;
            }
            Assert.True(burst);
            Assert.True(line.Wet > 0f, "장막이 터지면 곁 등줄이 젖는다");
        }

        [Fact]
        public void FireworkStand_LaunchesRocketsAfterItsFuse_AndTheyStartFires()
        {
            var sim = Quiet();
            Structure stand = sim.Structures.Find(s => s.Kind == StructureKind.Fireworks);
            Assert.Null(stand);
            stand = new Structure { Kind = StructureKind.Fireworks, Name = "불꽃 가판대", Pos = new Vec2(30f, 42f), Half = new Vec2(0.9f, 0.7f) };
            sim.Structures.Add(stand);
            for (int k = 0; k < 6; k++) Stall(sim, 18f + (k * 5f), 36f);
            sim.Ignite(stand, 0.5f);
            Run(sim, 2.3f);
            Assert.False(stand.Launching);
            bool started = false;
            for (int i = 0; i < 30 && !started; i++)
            {
                sim.Step(0f, 0f);
                sim.Enemies.Clear();
                started = sim.JustLaunching == stand;
            }
            Assert.True(started, "퓨즈 2.5초 뒤 쏘기 시작한다");
            Assert.True(stand.Launching);
            Run(sim, 1f);
            Assert.True(sim.Rockets.Count >= 1, "1초 안에 로켓이 난다");
            int bursts = 0;
            for (int i = 0; i < 60 * 6; i++)
            {
                sim.Step(0f, 0f);
                sim.Enemies.Clear();
                bursts += sim.RocketBursts.Count;
            }
            Assert.True(bursts >= 3, "6초면 서너 발이 떨어진다: " + bursts);
            bool litSomething = sim.Structures.Exists(s => s != stand && s.Burning) || sim.BurningGround.Count > 0;
            Assert.True(litSomething, "떨어진 로켓은 탈 것이나 바닥에 불을 낸다");
        }

        [Fact]
        public void DousingTheStand_StopsTheRockets()
        {
            var sim = Quiet();
            var stand = new Structure { Kind = StructureKind.Fireworks, Name = "불꽃 가판대", Pos = new Vec2(30f, 42f), Half = new Vec2(0.9f, 0.7f) };
            sim.Structures.Add(stand);
            sim.Ignite(stand, 0.5f);
            Run(sim, 3.5f);
            Assert.True(stand.Launching);
            Splash(sim, stand.Pos, 400f);
            sim.Step(0f, 0f);
            Assert.False(stand.Burning);
            Assert.False(stand.Launching);
            Run(sim, 1.5f);
            Assert.Empty(sim.Rockets);
            int bursts = 0;
            for (int i = 0; i < 60 * 3; i++)
            {
                sim.Step(0f, 0f);
                bursts += sim.RocketBursts.Count;
            }
            Assert.Equal(0, bursts);
        }

        [Fact]
        public void Popper_HopsThenPauses()
        {
            var sim = Quiet();
            Enemy popper = sim.Spawn(EnemyKind.Popper, new Vec2(sim.Player.X + 10f, sim.Player.Y));
            float x0 = popper.Pos.X;
            for (int i = 0; i < 21; i++) sim.Step(0f, 0f);
            float x1 = popper.Pos.X;
            Assert.True(x0 - x1 >= 2f, "0.35초 동안 뛴다: " + (x0 - x1));
            for (int i = 0; i < 36; i++) sim.Step(0f, 0f);
            Assert.InRange(popper.Pos.X, x1 - 0.05f, x1 + 0.05f);
        }

        [Fact]
        public void KillingAPopper_ThrowsTwoSeekerEmbers()
        {
            var sim = Quiet();
            Enemy popper = sim.Spawn(EnemyKind.Popper, new Vec2(40f, 30f));
            sim.Kill(popper);
            // 불씨는 틱 끝에 튄다(Kill이 적 목록을 도는 중에도 불리므로).
            sim.Step(0f, 0f);
            List<Enemy> embers = sim.Enemies.FindAll(e => !e.Dead && e.Kind == EnemyKind.Ember);
            Assert.Equal(SurvivorSim.PopperEmbers, embers.Count);
            Assert.All(embers, e => Assert.True(e.Seeker));
        }

        [Fact]
        public void MarketMap_LaysTwelveLanternLines()
        {
            var sim = new SurvivorSim(1, 5) { Guardian = false };
            Assert.Equal(12, sim.Lanterns.Count);
            Assert.All(sim.Lanterns, l => Assert.True(l.A.Kind == StructureKind.House && l.B.Kind == StructureKind.House && l.Burn < 0f));
            Assert.Empty(new SurvivorSim(1, 1) { Guardian = false }.Lanterns);
        }
        [Fact]
        public void MarketMap_HasTwelveStalls_AStage_ThreeFireworkStands_AndTwelveLanternLinks()
        {
            var sim = new SurvivorSim(1, 5) { Guardian = false };
            Assert.Equal("야시장", sim.Stage.Name);
            Assert.Equal(5, sim.Stage.Number);
            List<Structure> stalls = sim.Structures.FindAll(s => s.Kind == StructureKind.House);
            Assert.Equal(12, stalls.Count);
            Assert.All(stalls, s => Assert.Equal(SurvivorMarket.StallHalf, s.Half));
            Structure depot = Assert.Single(sim.Structures.FindAll(s => s.Kind == StructureKind.Depot));
            Assert.Equal("공연 무대", depot.Name);
            Assert.Equal(3, sim.Structures.FindAll(s => s.Kind == StructureKind.Fireworks).Count);
            Assert.Equal(3, sim.Structures.FindAll(s => s.Kind == StructureKind.Gas).Count);
            Assert.False(sim.HasWater);
            Assert.All(sim.Structures, s => Assert.True(s.DistanceTo(sim.Player) > 6f, s.Name + "가 출발점에 너무 가깝다"));
            Assert.All(sim.Structures, s => Assert.False(s.Burning));
            int residents = 0;
            foreach (Structure s in stalls) residents += s.Residents;
            Assert.Equal(18, residents);

            // 등줄 12쌍: 모두 점포 인덱스이고, 같은 줄 이웃(x 차이 7, 가운데 광장을 건너는 한 줄은 16)이거나 줄 양 끝의 위아래(y 차이 16)다.
            List<int[]> links = sim.Stage.Links(sim.Structures);
            Assert.Equal(12, links.Count);
            foreach (int[] ab in links)
            {
                Structure a = sim.Structures[ab[0]];
                Structure b = sim.Structures[ab[1]];
                Assert.Equal(StructureKind.House, a.Kind);
                Assert.Equal(StructureKind.House, b.Kind);
                bool neighbour = a.Pos.Y == b.Pos.Y && System.Math.Abs(a.Pos.X - b.Pos.X) <= 16.01f
                    && !stalls.Exists(s => s.Pos.Y == a.Pos.Y && s != a && s != b && (s.Pos.X - a.Pos.X) * (s.Pos.X - b.Pos.X) < 0f);
                bool endPost = a.Pos.X == b.Pos.X && System.Math.Abs(a.Pos.Y - b.Pos.Y) == SurvivorMarket.RowNorth - SurvivorMarket.RowSouth;
                Assert.True(neighbour || endPost, a.Name + "-" + b.Name + " 등줄이 이웃이 아니다");
            }
        }
            /// <summary>맵 특색 몹: 풍등은 하늘을 떠서 점포 지붕에 내려앉아 불 0.3을 내고(LanternLands) 구슬 없이 타 없어진다.</summary>
        [Fact]
        public void SkyLantern_DriftsToAStall_IgnitesIt_AndBurnsUp()
        {
            SurvivorSim sim = Quiet();
            Structure stall = Stall(sim, 30f, 38f);
            Enemy lantern = sim.Spawn(EnemyKind.SkyLantern, new Vec2(30f, 55f));
            Assert.True(lantern.Seeker && lantern.Touch == 0f);
            int lands = 0;
            int ticks = 0;
            while (!lantern.Dead && ticks++ < (int)(30f / SurvivorSim.Dt))
            {
                sim.Step(0f, 0f);
                lands += sim.LanternLands.Count;
                // 가장자리 스폰은 치운다(가만히 선 소방관이 죽으면 판이 멈춘다).
                sim.Enemies.RemoveAll(e => e != lantern);
            }
            Assert.True(lantern.Dead, "30초 안에 점포에 내려앉아야 한다: " + lantern.Pos);
            Assert.Equal(1, lands);
            Assert.True(stall.Burning && stall.Fire >= 0.3f, "점포 불 " + stall.Fire);
            Assert.Empty(sim.Gems);
        }

        /// <summary>쏘아 떨어뜨린 풍등은 구슬을 떨어뜨린다(Xp 2).</summary>
        [Fact]
        public void SkyLantern_ShotDown_DropsAGem()
        {
            SurvivorSim sim = Quiet();
            Stall(sim, 30f, 38f);
            Enemy lantern = sim.Spawn(EnemyKind.SkyLantern, new Vec2(30f, 46f));
            Splash(sim, lantern.Pos, 100f);
            sim.Step(0f, 0f);
            Assert.True(lantern.Dead);
            Assert.Single(sim.Gems);
        }
    }
}
