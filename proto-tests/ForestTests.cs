using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>2스테이지 산불 숲: 맵, 바람 따라 나무끼리 번지는 불, 불다람쥐, 재 박쥐 무리.</summary>
    public class ForestTests
    {
        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1, 2);
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        private static Structure Tree(SurvivorSim sim, float dx, float dy)
        {
            var s = new Structure { Kind = StructureKind.Tree, Name = "나무", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(0.6f, 0.6f) };
            sim.Structures.Add(s);
            return s;
        }

        /// <summary>불씨가 끼어들지 않게 적을 치우며 시간을 보낸다(바람 번짐만 본다).</summary>
        private static void RunClear(SurvivorSim sim, float seconds)
        {
            int ticks = (int)(seconds / SurvivorSim.Dt);
            for (int i = 0; i < ticks; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
        }

        [Fact]
        public void ForestMap_HasSevenBuildings_ManyTrees_AndAClearCenter()
        {
            var sim = new SurvivorSim(1, 2);
            Assert.Equal("산불 숲", sim.Stage.Name);
            Assert.Equal(7, sim.HousesTotal);
            Assert.Single(sim.Structures.FindAll(s => s.Kind == StructureKind.Depot));
            Assert.True(sim.Structures.FindAll(s => s.Kind == StructureKind.Tree).Count >= 60, "나무 " + sim.Structures.FindAll(s => s.Kind == StructureKind.Tree).Count);
            Assert.All(sim.Structures, s => Assert.True(s.DistanceTo(sim.Player) > 6f, s.Name + "가 출발점에 너무 가깝다"));
            Assert.All(sim.Structures, s => Assert.False(s.Burning));
        }

        [Fact]
        public void BurningTree_SpreadsDownwind()
        {
            SurvivorSim sim = Quiet();
            RunClear(sim, 0.1f);
            sim.Wind = new Vec2(1f, 0f);
            // 옮을 확률이 30%라 서로 멀리 떨어진 나무 쌍 여섯을 두고, 끄지 않은 채 다 탈 때까지 본다.
            var downwind = new System.Collections.Generic.List<Structure>();
            for (int k = 0; k < 6; k++)
            {
                float dy = -25f + (k * 10f);
                Structure fire = Tree(sim, -20f, dy);
                downwind.Add(Tree(sim, -17f, dy));
                sim.Ignite(fire, 0.5f);
            }
            RunClear(sim, 30f);
            Assert.Contains(downwind, t => t.Burning || t.Collapsed);
        }

        [Fact]
        public void BurningTree_DoesNotSpreadUpwind()
        {
            SurvivorSim sim = Quiet();
            RunClear(sim, 0.1f);
            sim.Wind = new Vec2(1f, 0f);
            Structure fire = Tree(sim, 10f, 10f);
            Structure upwind = Tree(sim, 7f, 10f);
            sim.Ignite(fire, 0.5f);
            RunClear(sim, (SurvivorSim.WindSpreadEvery * 2f) + 0.5f);
            Assert.False(upwind.Burning, "바람 반대쪽으로는 번지지 않는다");
        }

        [Fact]
        public void Wind_ShiftsEveryMinute_ToANewDirection()
        {
            SurvivorSim sim = Quiet();
            RunClear(sim, 0.1f);
            Vec2 before = sim.Wind;
            Assert.Equal(1f, (before.X * before.X) + (before.Y * before.Y), 3);
            bool shifted = false;
            float at = 0f;
            for (int i = 0; i < 61 * 60 && !shifted; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
                if (sim.JustWindShift)
                {
                    shifted = true;
                    at = sim.Time;
                }
            }
            Assert.True(shifted);
            Assert.InRange(at, SurvivorSim.WindShiftEvery - 0.05f, SurvivorSim.WindShiftEvery + 0.05f);
            Assert.False(before.X == sim.Wind.X && before.Y == sim.Wind.Y, "바람이 같은 방향으로 다시 뽑혔다");
        }

        [Fact]
        public void Squirrel_RunsToATree_AndSetsItOnFire()
        {
            SurvivorSim sim = Quiet();
            Structure tree = Tree(sim, 7f, 0f);
            sim.Spawn(EnemyKind.Squirrel, new Vec2(sim.Player.X + 1.5f, sim.Player.Y - 2f));
            for (int i = 0; i < 60 * 5 && !tree.Burning; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
            }
            Assert.True(tree.Burning, "다람쥐가 나무에 불을 붙여야 한다");
        }

        /// <summary>보스 시각까지 건너뛰고 한 틱 돌려 멧돼지를 부른다(스폰 감독 경로).</summary>
        private static Enemy CallBoar(SurvivorSim sim)
        {
            sim.Time = SurvivorSim.BossAt - SurvivorSim.Dt;
            sim.Step(0f, 0f);
            Assert.NotNull(sim.Boss);
            return sim.Boss;
        }

        /// <summary>보스만 남기고 체력을 채운 채 한 틱(시간을 건너뛴 탓에 밀려 나온 박쥐 떼를 치운다).</summary>
        private static void BossOnly(SurvivorSim sim)
        {
            sim.Hp = sim.MaxHp;
            sim.Enemies.RemoveAll(e => e != sim.Boss);
            sim.Step(0f, 0f);
        }

        [Fact]
        public void Boar_WarnsThenChargesAlongTheWarnedLine()
        {
            SurvivorSim sim = Quiet();
            Enemy boar = CallBoar(sim);
            Assert.Equal(EnemyKind.Boar, boar.Kind);
            Assert.Equal(sim.Stage.BossHp, boar.MaxHp);

            bool warned = false;
            for (int i = 0; i < 60 * 8 && !warned; i++)
            {
                BossOnly(sim);
                warned = sim.JustBoarWindup;
            }
            Assert.True(warned, "7초 안에 돌진 경고가 와야 한다");
            Vec2 aim = sim.BoarAim;
            Vec2 toPlayer = new Vec2(sim.Player.X - boar.Pos.X, sim.Player.Y - boar.Pos.Y);
            float len = (float)System.Math.Sqrt((toPlayer.X * toPlayer.X) + (toPlayer.Y * toPlayer.Y));
            Assert.True(((aim.X * toPlayer.X) + (aim.Y * toPlayer.Y)) / len > 0.99f, "경고 방향이 소방관 쪽이어야 한다");

            // 경고 동안은 멈춰 있다.
            Vec2 before = boar.Pos;
            for (int i = 0; i < 30; i++) BossOnly(sim);
            Assert.True(boar.Pos.DistanceTo(before) < 0.3f, "경고 중에 움직였다");

            bool charged = false;
            for (int i = 0; i < 60 && !charged; i++)
            {
                BossOnly(sim);
                charged = sim.JustBoarCharge;
            }
            Assert.True(charged);
            Vec2 start = boar.Pos;
            for (int i = 0; i < 20; i++) BossOnly(sim);
            float moved = boar.Pos.DistanceTo(start);
            Assert.True(moved > 3.5f, "돌진이 빨라야 한다: " + moved);
            float along = ((boar.Pos.X - start.X) * aim.X) + ((boar.Pos.Y - start.Y) * aim.Y);
            Assert.True(along / moved > 0.95f, "경고한 선을 따라 돌진해야 한다");
        }

        [Fact]
        public void TiredBoar_TakesMoreWater()
        {
            SurvivorSim sim = Quiet();
            Enemy boar = CallBoar(sim);
            boar.MaxHp = boar.Hp = 1e6f;
            // 소방관 옆에 두고 물을 쏘며 한 번 돌진이 끝날 때까지 기다린다(플레이어처럼 겨누고 쏜다).
            float walking = 0f;
            float tired = 0f;
            for (int i = 0; i < 60 * 12; i++)
            {
                sim.Aim = new Vec2(boar.Pos.X - sim.Player.X, boar.Pos.Y - sim.Player.Y);
                sim.Spraying = true;
                bool isTired = sim.BoarTired > 0f;
                bool isWalking = sim.BoarWindup <= 0f && sim.BoarCharge <= 0f && !isTired;
                BossOnly(sim);
                foreach (Hit h in sim.Hits)
                {
                    if (h.Kind != EnemyKind.Boar || h.Crit) continue;
                    if (isTired && tired == 0f) tired = h.Damage;
                    if (isWalking && walking == 0f) walking = h.Damage;
                }
                if (tired > 0f && walking > 0f) break;
            }
            Assert.True(walking > 0f && tired > 0f, "걷는 때와 지친 때 둘 다 맞혀야 한다: " + walking + " / " + tired);
            Assert.Equal(walking * SurvivorSim.BoarTiredDamage, tired, 2);
        }

        [Fact]
        public void Bats_ArriveAsAFlockOfSix()
        {
            SurvivorSim sim = Quiet();
            bool came = false;
            for (int i = 0; i < (int)((SurvivorSim.FirstBats + 1f) * 60) && !came; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.JustBats)
                {
                    came = true;
                    Assert.Equal(6, sim.Enemies.FindAll(e => e.Kind == EnemyKind.Bat).Count);
                }
            }
            Assert.True(came);
            Assert.InRange(sim.Time, SurvivorSim.FirstBats - 0.1f, SurvivorSim.FirstBats + 0.1f);
        }
    }
}
