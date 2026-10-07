using System.Collections.Generic;
using FireGame.Core.Sim;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 끄는 시간과 몸 압박(2026-10-02 "긴장도가 없다·피가 안 닳는다·건물 불이 너무 쉽다"):
    /// Lv1 물대포로 신고 불은 5초쯤, 다 탄 건물은 15초쯤 걸리고, 한 방 물은 건물 물 비율과 상관없이 한 칸을 확 채운다.
    /// </summary>
    public class DouseTimeTests
    {
        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        /// <summary>소방관 위쪽, 가장자리가 gap칸 떨어진 가게.</summary>
        private static Structure ShopAbove(SurvivorSim sim, float gap, int residents = 0)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X, sim.Player.Y + 1.5f + gap), Half = new Vec2(2f, 1.5f), Integrity = 100f, Residents = residents };
            sim.Structures.Add(s);
            return s;
        }

        private static void Take(SurvivorSim sim, UpgradeId id, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                sim.PendingChoices = new List<UpgradeId> { id };
                sim.Choose(0);
            }
        }

        /// <summary>가게를 겨누고 꺼질 때까지 뿌린 시간(초)과 그동안의 증기 폭발 수. 적은 매 틱 치우고 체력은 채운다.</summary>
        private static float DouseSeconds(SurvivorSim sim, Structure shop, float limit, out int bursts)
        {
            bursts = 0;
            int ticks = 0;
            int max = (int)(limit / SurvivorSim.Dt);
            while (shop.Burning && ticks < max)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Aim = new Vec2(shop.Pos.X - sim.Player.X, shop.Pos.Y - sim.Player.Y);
                sim.Spraying = true;
                sim.Step(0f, 0f);
                bursts += sim.SteamBursts.Count;
                ticks++;
            }
            return ticks * SurvivorSim.Dt;
        }

        [Fact]
        public void ReportFire_TakesAboutFiveSeconds_WithLv1Hose()
        {
            SurvivorSim sim = Quiet();
            Structure shop = ShopAbove(sim, 3f);
            sim.Ignite(shop, SurvivorSim.ReportFire);
            float t = DouseSeconds(sim, shop, 20f, out int bursts);
            Assert.False(shop.Burning, "신고 불이 20초를 뿌려도 안 꺼졌다");
            Assert.InRange(t, 4f, 8f);
        }

        [Fact]
        public void FullBlaze_TakesAboutFifteenSeconds_WithLv1Hose()
        {
            SurvivorSim sim = Quiet();
            Structure shop = ShopAbove(sim, 3f);
            sim.Ignite(shop, 1f);
            float t = DouseSeconds(sim, shop, 40f, out int bursts);
            Assert.False(shop.Burning, "다 탄 건물이 40초를 뿌려도 안 꺼졌다");
            Assert.InRange(t, 10f, 22f);
            Assert.True(bursts >= 2, "큰 불은 증기 폭발이 두 번은 터져야 꺼진다: " + bursts);
        }

        [Fact]
        public void HigherHose_DousesFaster()
        {
            SurvivorSim lv1 = Quiet();
            Structure a = ShopAbove(lv1, 3f);
            lv1.Ignite(a, 1f);
            float slow = DouseSeconds(lv1, a, 40f, out _);

            SurvivorSim lv5 = Quiet();
            Take(lv5, UpgradeId.Hose, Loadout.MaxLevel - lv5.Build.Level(UpgradeId.Hose));
            Assert.Equal(Loadout.MaxLevel, lv5.Build.Level(UpgradeId.Hose));
            Structure b = ShopAbove(lv5, 3f);
            lv5.Ignite(b, 1f);
            float fast = DouseSeconds(lv5, b, 40f, out _);

            Assert.False(b.Burning);
            Assert.True(fast <= slow * 0.7f, "Lv5 물대포(" + fast + "초)가 Lv1(" + slow + "초)보다 충분히 빠르지 않다");
        }

        [Fact]
        public void SmallMobs_TouchForFive()
        {
            // 불씨 한 마리가 1초 닿아 있으면 SmallTouch(5)만큼 닳는다(방화복 없음).
            SurvivorSim sim = Quiet();
            Enemy ember = sim.Spawn(EnemyKind.Ember, sim.Player);
            ember.Speed = 0f;
            ember.MaxHp = 999f;
            ember.Hp = 999f;
            float hp = sim.Hp;
            for (int i = 0; i < 60; i++)
            {
                for (int k = sim.Enemies.Count - 1; k >= 0; k--) if (sim.Enemies[k] != ember) sim.Enemies.RemoveAt(k);
                ember.Pos = sim.Player;
                ember.Knock = default;
                sim.Spraying = false;
                sim.Step(0f, 0f);
            }
            Assert.InRange(hp - sim.Hp, SurvivorSim.SmallTouch * 0.9f, SurvivorSim.SmallTouch * 1.1f);
            Assert.Equal(5f, SurvivorSim.SmallTouch);
        }

        [Fact]
        public void Rescue_TakesTwoSeconds()
        {
            SurvivorSim sim = Quiet();
            Structure shop = ShopAbove(sim, 0.2f, 2);
            sim.Player = new Vec2(shop.Door.X, shop.Door.Y);
            sim.Ignite(shop, 0.2f);
            Assert.Equal(2f, SurvivorSim.RescueTime);
            int before = (int)((SurvivorSim.RescueTime - 0.1f) / SurvivorSim.Dt);
            for (int i = 0; i < before; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
            Assert.Equal(0, sim.Rescued);
            for (int i = 0; i < 12; i++)
            {
                sim.Enemies.Clear();
                sim.Step(0f, 0f);
            }
            Assert.Equal(1, sim.Rescued);
            Assert.Equal(1, shop.Residents);
        }

        [Fact]
        public void Forest_UsesHalfBuildingWater_OtherStagesUseTheDefault()
        {
            Assert.Equal(SurvivorSim.BuildingWater, SurvivorStages.Get(1).BuildingWater);
            Assert.Equal(0.5f, SurvivorStages.Get(2).BuildingWater);
            Assert.Equal(SurvivorSim.BuildingWater, SurvivorStages.Get(3).BuildingWater);
            Assert.Equal(SurvivorSim.BuildingWater, SurvivorStages.Get(4).BuildingWater);
            Assert.Equal(SurvivorSim.BuildingWater, SurvivorStages.Get(5).BuildingWater);
            Assert.True(SurvivorSim.BuildingWater <= 0.3f, "건물 물 비율이 커서 끄는 시간이 짧다: " + SurvivorSim.BuildingWater);
        }
    }
}
