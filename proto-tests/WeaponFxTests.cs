using System;
using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 무기 타격 연출용 신호: 적중마다 어느 무기가 어디서 쳤는지, 포탑 설치·공중 소화탄·장막 차오름.
    /// 판정은 그대로이고 화면이 무기마다 탄환·번쩍임을 그리는 데만 쓴다.
    /// </summary>
    public class WeaponFxTests
    {
        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1, 1) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        /// <summary>플레이어가 레벨업 카드에서 이 카드를 골랐을 때(Choose 경로).</summary>
        private static void Take(SurvivorSim sim, UpgradeId id, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                sim.PendingChoices = new List<UpgradeId> { id };
                sim.Choose(0);
            }
        }

        /// <summary>움직이지 않고 잘 안 죽는 불 몹.</summary>
        private static Enemy Dummy(SurvivorSim sim, float dx, float dy)
        {
            Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + dx, sim.Player.Y + dy));
            e.Speed = 0f;
            e.MaxHp = e.Hp = 9999f;
            return e;
        }

        /// <summary>조건이 맞는 적중이 나올 때까지 돈다(최대 seconds초).</summary>
        private static Hit? WaitHit(SurvivorSim sim, HitSource source, float seconds)
        {
            for (int i = 0; i < (int)(seconds / SurvivorSim.Dt); i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                foreach (Hit h in sim.Hits)
                {
                    if (h.Source == source) return h;
                }
            }
            return null;
        }

        [Fact]
        public void Partner_HitsCarryThePartnerAsSource()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Partner);
            Dummy(sim, -1.2f, 1.5f);
            Hit? hit = null;
            for (int i = 0; i < 60 * 3 && hit == null; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                foreach (Hit h in sim.Hits)
                {
                    if (h.Source == HitSource.Partner)
                    {
                        hit = h;
                        Assert.True(h.From.DistanceTo(sim.Partners[0]) < 0.1f, "탄환이 대원에게서 나가야 한다: " + h.From.X + "," + h.From.Y);
                    }
                }
            }
            Assert.NotNull(hit);
        }

        [Fact]
        public void Turret_SignalsPlacement_AndItsHits()
        {
            SurvivorSim sim = Quiet();
            Vec2 spot = sim.Player;
            Dummy(sim, 2f, 0f);
            Take(sim, UpgradeId.Turret);
            int placed = 0;
            Hit? hit = null;
            for (int i = 0; i < 60 * 2; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                placed += sim.TurretsPlaced.Count;
                if (sim.TurretsPlaced.Count > 0) Assert.True(sim.TurretsPlaced[0].DistanceTo(spot) < 0.1f);
                foreach (Hit h in sim.Hits)
                {
                    if (h.Source == HitSource.Turret && hit == null) hit = h;
                }
            }
            Assert.Equal(1, placed);
            Assert.NotNull(hit);
            Assert.True(hit.Value.From.DistanceTo(spot) < 0.1f, "탄환이 포탑에서 나가야 한다");
        }

        [Fact]
        public void Drone_AndCurtain_HitsCarryTheirSource()
        {
            SurvivorSim drone = Quiet();
            Take(drone, UpgradeId.Drone);
            Dummy(drone, 2.3f, 0f);
            Dummy(drone, -2.3f, 0f);
            Hit? d = WaitHit(drone, HitSource.Drone, 4f);
            Assert.NotNull(d);
            Assert.True(d.Value.From.DistanceTo(drone.Drones[0]) < 0.3f, "드론 자리에서 쳐야 한다");

            SurvivorSim curtain = Quiet();
            Take(curtain, UpgradeId.Curtain);
            Dummy(curtain, 2f, 0f);
            Hit? c = WaitHit(curtain, HitSource.Curtain, 2f);
            Assert.NotNull(c);
            Assert.True(curtain.JustCurtain);
            Assert.True(c.Value.From.DistanceTo(curtain.Player) < 0.1f);
        }

        [Fact]
        public void WaterBomb_HitsAreBomb_AirBombLandsInAirBlasts()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.WaterBomb);
            Dummy(sim, 5f, 0f);
            bool exploded = false;
            Hit? hit = null;
            for (int i = 0; i < 60 * 4 && hit == null; i++)
            {
                sim.Hp = sim.MaxHp;
                sim.Step(0f, 0f);
                if (sim.Explosions.Count > 0) exploded = true;
                Assert.Empty(sim.AirBlasts);
                foreach (Hit h in sim.Hits)
                {
                    if (h.Source == HitSource.Bomb) hit = h;
                }
            }
            Assert.True(exploded);
            Assert.NotNull(hit);

            // 공중 소화탄(진화): 떨어진 곳은 AirBlasts에만.
            SurvivorSim air = Quiet();
            while (air.Build.Level(UpgradeId.WaterBomb) < Loadout.MaxLevel) Take(air, UpgradeId.WaterBomb);
            Take(air, Loadout.PairOf(UpgradeId.AirBomb));
            Take(air, UpgradeId.AirBomb);
            Assert.True(air.Build.Level(UpgradeId.AirBomb) > 0);
            var shop = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(air.Player.X + 20f, air.Player.Y + 15f), Half = new Vec2(2f, 1.5f) };
            air.Structures.Add(shop);
            air.Ignite(shop, 0.8f);
            bool landed = false;
            for (int i = 0; i < 60 * 5 && !landed; i++)
            {
                air.Hp = air.MaxHp;
                air.Step(0f, 0f);
                landed = air.AirBlasts.Exists(p => p.DistanceTo(shop.Pos) < 0.1f);
                Assert.DoesNotContain(air.Explosions, p => p.DistanceTo(shop.Pos) < 0.1f);
            }
            Assert.True(landed, "공중 소화탄이 AirBlasts로 떨어져야 한다");
        }

        [Fact]
        public void CurtainIn_CountsDownToTheBurst()
        {
            SurvivorSim none = Quiet();
            Assert.Equal(float.MaxValue, none.CurtainIn);

            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Curtain);
            float before = float.MaxValue;
            for (int i = 0; i < 60 * 2; i++)
            {
                float last = sim.CurtainIn;
                sim.Step(0f, 0f);
                if (sim.JustCurtain)
                {
                    before = last;
                    break;
                }
            }
            Assert.True(before <= SurvivorSim.Dt + 0.001f, "터지기 직전 남은 시간 " + before);
            Assert.True(sim.CurtainIn > 1f, "터진 뒤에는 다음 간격으로 돌아간다: " + sim.CurtainIn);
        }
    }
}
