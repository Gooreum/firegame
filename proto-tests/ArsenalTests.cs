using System;
using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>
    /// 새 무기 9종(2026-10-07): 하나는 공간 하나를 맡고, 불 몹과 건물 불을 둘 다 맞힌다.
    /// 레벨업은 개수·크기·갈래로 보이게 오르고, 진화하면 모양이 바뀐다.
    /// </summary>
    public class ArsenalTests
    {
        private static SurvivorSim Quiet()
        {
            var sim = new SurvivorSim(1, 1) { Guardian = false };
            sim.Enemies.Clear();
            sim.Structures.Clear();
            sim.Reports = false;
            return sim;
        }

        /// <summary>레벨업 카드에서 이 카드를 times번 고른다(Choose 경로).</summary>
        private static void Take(SurvivorSim sim, UpgradeId id, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                sim.PendingChoices = new List<UpgradeId> { id };
                sim.Choose(0);
            }
        }

        /// <summary>무기 Lv5 + 짝 보조로 진화까지(카드 경로).</summary>
        private static void Evolve(SurvivorSim sim, UpgradeId evolution)
        {
            UpgradeId weapon = Loadout.BaseOf(evolution);
            while (sim.Build.Level(weapon) < Loadout.MaxLevel) Take(sim, weapon);
            while (sim.Build.Level(Loadout.PairOf(evolution)) < Loadout.EvolvePair) Take(sim, Loadout.PairOf(evolution));
            Take(sim, evolution);
            Assert.Equal(1, sim.Build.Level(evolution));
        }

        private static Enemy Dummy(SurvivorSim sim, EnemyKind kind, float dx, float dy, float hp = 999f)
        {
            Enemy e = sim.Spawn(kind, new Vec2(sim.Player.X + dx, sim.Player.Y + dy));
            e.Speed = 0f;
            e.MaxHp = hp;
            e.Hp = hp;
            return e;
        }

        private static Structure Shop(SurvivorSim sim, float dx, float dy, float fire = 0f, int residents = 0)
        {
            var s = new Structure { Kind = StructureKind.House, Name = "가게", Pos = new Vec2(sim.Player.X + dx, sim.Player.Y + dy), Half = new Vec2(2f, 1.5f), Residents = residents };
            s.Integrity = 100f;
            sim.Structures.Add(s);
            if (fire > 0f) sim.Ignite(s, fire);
            return s;
        }

        /// <summary>seconds 동안 흘린다. 가장자리에서 새로 나온 몹은 지운다(놓아 둔 몹만 남긴다). until이 참이면 멈춘다.</summary>
        private static void Run(SurvivorSim sim, float seconds, Func<bool> until = null, List<Enemy> keep = null)
        {
            var mine = keep ?? new List<Enemy>(sim.Enemies);
            for (int i = 0; i < (int)(seconds / SurvivorSim.Dt) && sim.Outcome == SOutcome.Playing; i++)
            {
                sim.Enemies.RemoveAll(e => !mine.Contains(e));
                sim.Hp = sim.MaxHp;
                if (sim.PendingChoices != null) sim.Choose(0);
                sim.Step(0f, 0f);
                if (until != null && until()) return;
            }
        }

        // ------------------------------------------------------------------
        // 소방견
        // ------------------------------------------------------------------

        [Fact]
        public void Dog_RunsToAnEmber_AndBitesItDown()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Sprinkler);
            Enemy ember = Dummy(sim, EnemyKind.Ember, 5f, 0f, 20f);
            bool bit = false;
            Run(sim, 4f, () => (bit |= sim.DogBites.Count > 0) && ember.Dead);
            Assert.True(bit, "개가 물지 않았다");
            Assert.True(ember.Dead, "개가 불씨를 잡지 못했다: 체력 " + ember.Hp);
            Assert.Contains(sim.Hits, h => h.Source == HitSource.Dog);
        }

        [Fact]
        public void Dog_PrefersTheRaider_ThatGoesForAHouse()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Sprinkler);
            Enemy chaser = Dummy(sim, EnemyKind.Ember, 3f, 0f);
            Enemy raider = Dummy(sim, EnemyKind.Ember, -6f, 0f);
            raider.Goal = Shop(sim, -10f, 0f);
            Run(sim, 0.6f);
            Assert.Same(raider, sim.Dogs[0].Target);
        }

        [Fact]
        public void Dog_CountGrowsWithLevel_OneOneTwoTwoThree()
        {
            SurvivorSim sim = Quiet();
            var seen = new List<int>();
            for (int lv = 1; lv <= Loadout.MaxLevel; lv++)
            {
                Take(sim, UpgradeId.Sprinkler);
                Run(sim, 0.1f);
                seen.Add(sim.Dogs.Count);
            }
            Assert.Equal(new List<int> { 1, 1, 2, 2, 3 }, seen);
        }

        [Fact]
        public void Dog_WithNothingToBite_BarksAtABurningShop_AndSoaksIt()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Sprinkler);
            Structure shop = Shop(sim, 0f, 5f, 0.6f);
            float before = shop.Fire;
            Run(sim, 5f);
            Assert.True(shop.Fire < before - 0.2f, "개가 짖으며 적셔야: " + before + " → " + shop.Fire);
        }

        [Fact]
        public void DogPack_FourDogs_PullPeopleOut()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.Crown);
            Structure shop = Shop(sim, 0f, 6f, 1f, 2);
            Run(sim, 0.1f);
            Assert.Equal(SurvivorSim.DogPackCount, sim.Dogs.Count);
            int rescued = sim.Rescued;
            Run(sim, 8f);
            Assert.True(sim.Rescued > rescued, "구조견이 사람을 물고 나와야: 남은 " + shop.Residents + " 불 " + shop.Fire);
        }

        // ------------------------------------------------------------------
        // 호스 채찍
        // ------------------------------------------------------------------

        [Fact]
        public void Whip_HitsAndPushesAnEmberInItsCircle()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Whip);
            Enemy ember = Dummy(sim, EnemyKind.Ember, 2f, 0f);
            Run(sim, 1f, () => ember.Hp < ember.MaxHp);
            Assert.True(ember.Hp < ember.MaxHp, "채찍에 안 맞았다");
            Assert.Contains(sim.Hits, h => h.Source == HitSource.Whip);
            Run(sim, 0.2f);
            Assert.True(ember.Pos.DistanceTo(sim.Player) > 2.2f, "바깥으로 밀려나야: " + ember.Pos.DistanceTo(sim.Player));
        }

        [Fact]
        public void Whip_DoesNotReachBeyondItsRadius()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Whip);
            Enemy far = Dummy(sim, EnemyKind.Ember, 4f, 0f);
            Run(sim, 2f);
            Assert.Equal(far.MaxHp, far.Hp);
        }

        [Fact]
        public void Whip_WidensAndSplitsWithLevel()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Whip);
            float r1 = sim.WhipRadius;
            Take(sim, UpgradeId.Whip, 4);
            Assert.True(sim.WhipRadius > r1 + 1f, "반경 " + r1 + " → " + sim.WhipRadius);
            Assert.Equal(new[] { 0, 1, 1, 2, 2, 3 }, SurvivorSim.WhipArms);
        }

        [Fact]
        public void Whirl_LeavesSpinningRings_ThatBurnEmbersLater()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.Whirl);
            Run(sim, 0.5f);
            Assert.NotEmpty(sim.WhirlMarks);
            // 고리가 남은 자리에 놓인 불씨는 채찍이 지나간 뒤에도 피해를 입는다.
            WhirlMark mark = sim.WhirlMarks[0];
            Enemy ember = sim.Spawn(EnemyKind.Ember, mark.Pos);
            ember.Speed = 0f;
            ember.MaxHp = ember.Hp = 999f;
            ember.WhipCool = 99f;
            Run(sim, 0.5f);
            Assert.True(ember.Hp < 999f, "고리가 지져야");
        }

        // ------------------------------------------------------------------
        // 사다리차
        // ------------------------------------------------------------------

        [Fact]
        public void Ladder_StrikesAWholeLineOfEmbers()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Chain);
            var row = new List<Enemy>();
            for (int k = 0; k < 4; k++) row.Add(Dummy(sim, EnemyKind.Ember, 1.5f + (1.2f * k), 0f));
            Run(sim, 1f, () => sim.LadderStrikes.Count > 0);
            Assert.NotEmpty(sim.LadderStrikes);
            Assert.All(row, e => Assert.True(e.Hp < e.MaxHp, "한 줄이 다 맞아야"));
        }

        [Fact]
        public void Ladder_SoaksTheShopItLandsOn_AndBringsSomeoneDown()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Chain);
            Structure shop = Shop(sim, 5f, 0f, 0.5f, 2);
            float fire = shop.Fire;
            Run(sim, 1f, () => sim.LadderStrikes.Count > 0);
            Assert.True(shop.Fire < fire, "사다리 물이 지붕에 닿아야");
            Assert.Equal(1, shop.Residents);
        }

        [Fact]
        public void Ladder_ReachesFurther_AndSplitsWithLevel()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Chain, Loadout.MaxLevel);
            Run(sim, 1f, () => sim.LadderStrikes.Count > 0);
            Assert.Equal(3, sim.LadderStrikes.Count);
            Assert.All(sim.LadderStrikes, l => Assert.Equal(10f, l.Len, 2));
        }

        [Fact]
        public void LadderBridge_StaysAndPushesEmbersOffTheLine()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.Surge);
            Run(sim, 1f, () => sim.LadderStrikes.Count > 0);
            Ladder l = sim.Ladders[0];
            Assert.Equal(SurvivorSim.BridgeLife, l.Life);
            // 다리 위에 놓인 불씨는 옆으로 밀려나며 지진다.
            var on = new Vec2(l.From.X + (l.Dir.X * 4f), l.From.Y + (l.Dir.Y * 4f));
            Enemy ember = sim.Spawn(EnemyKind.Ember, on);
            ember.Speed = 0f;
            ember.MaxHp = ember.Hp = 999f;
            Run(sim, 0.6f);
            Assert.True(ember.Hp < 999f);
            float off = Math.Abs(((ember.Pos.X - l.From.X) * -l.Dir.Y) + ((ember.Pos.Y - l.From.Y) * l.Dir.X));
            Assert.True(off > 0.5f, "다리에서 밀려나야: " + off);
        }

        // ------------------------------------------------------------------
        // 물풍선
        // ------------------------------------------------------------------

        [Fact]
        public void Balloon_BouncesOffAWall_AndSplashesEachTime()
        {
            SurvivorSim sim = Quiet();
            Shop(sim, 5f, 0f);
            Take(sim, UpgradeId.Balloon);
            Run(sim, 0.1f);
            Assert.Single(sim.Balloons);
            WaterBalloon b = sim.Balloons[0];
            float vx = b.Vel.X;
            int splashes = 0;
            Run(sim, 1.5f, () => (splashes += sim.BalloonSplashes.Count) > 0);
            Assert.True(splashes > 0, "벽에 튕기며 물보라가 터져야");
            Assert.True(Math.Sign(b.Vel.X) != Math.Sign(vx), "벽에서 되튕겨야: " + vx + " → " + b.Vel.X);
        }

        [Fact]
        public void Balloon_HitsEmbers_AndPopsAfterItsBounces()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Balloon);
            Enemy ember = Dummy(sim, EnemyKind.Ember, 3f, 0f);
            Run(sim, 0.05f);
            WaterBalloon first = sim.Balloons[0];
            Run(sim, 1f, () => ember.Hp < ember.MaxHp);
            Assert.True(ember.Hp < ember.MaxHp);
            Assert.Contains(sim.Hits, h => h.Source == HitSource.Balloon);
            // 튕길 횟수나 수명이 다하면 사라진다.
            Run(sim, SurvivorSim.BalloonLife + 0.5f, () => !sim.Balloons.Contains(first));
            Assert.DoesNotContain(first, sim.Balloons);
        }

        [Fact]
        public void Balloon_CountAndBouncesGrow()
        {
            Assert.Equal(new[] { 0, 1, 1, 2, 2, 3 }, SurvivorSim.BalloonCount);
            Assert.Equal(new[] { 0, 3, 4, 4, 5, 6 }, SurvivorSim.BalloonBounces);
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Balloon, Loadout.MaxLevel);
            Run(sim, 0.1f);
            Assert.Equal(3, sim.Balloons.Count);
            Assert.All(sim.Balloons, b => Assert.Equal(6, b.Bounces));
        }

        [Fact]
        public void BalloonStorm_SplitsIntoSmallBalloons_OnBounce()
        {
            SurvivorSim sim = Quiet();
            Shop(sim, 4f, 0f);
            Evolve(sim, UpgradeId.BalloonStorm);
            Run(sim, 2f, () => sim.Balloons.Exists(b => b.Small));
            Assert.Contains(sim.Balloons, b => b.Small);
        }

        [Fact]
        public void Balloon_SplashSoaksABurningShop()
        {
            SurvivorSim sim = Quiet();
            Structure shop = Shop(sim, 4f, 0f, 0.6f);
            Take(sim, UpgradeId.Balloon);
            float fire = shop.Fire;
            Run(sim, 1.5f, () => sim.BalloonSplashes.Count > 0);
            Assert.True(shop.Fire < fire, "물풍선 물보라가 불을 줄여야");
        }

        // ------------------------------------------------------------------
        // 소화기 부메랑
        // ------------------------------------------------------------------

        [Fact]
        public void Extinguisher_FliesOut_AndComesBack_PiercingEmbers()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Extinguisher);
            Enemy a = Dummy(sim, EnemyKind.Ember, 2f, 0f);
            Enemy b = Dummy(sim, EnemyKind.Ember, 4.5f, 0f);
            Run(sim, 0.1f);
            Assert.Single(sim.Boomerangs);
            Boomerang boom = sim.Boomerangs[0];
            float far = 0f;
            bool hit = false;
            Run(sim, 2f, () =>
            {
                far = Math.Max(far, boom.Pos.DistanceTo(sim.Player));
                hit |= sim.Hits.Exists(h => h.Source == HitSource.Extinguisher);
                return boom.Dead;
            });
            Assert.True(far > 5.5f, "6칸쯤 나가야: " + far);
            Assert.True(boom.Dead, "돌아와 손에 잡혀야");
            Assert.True(a.Hp < a.MaxHp && b.Hp < b.MaxHp, "가는 길의 둘 다 꿰뚫어야");
            Assert.True(hit);
        }

        [Fact]
        public void Extinguisher_MoreAndFurtherWithLevel()
        {
            Assert.Equal(new[] { 0, 1, 1, 2, 2, 3 }, SurvivorSim.BoomCount);
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Extinguisher, Loadout.MaxLevel);
            Run(sim, 0.1f);
            Assert.Equal(3, sim.Boomerangs.Count);
            Assert.All(sim.Boomerangs, b => Assert.Equal(9f, b.Range, 2));
        }

        [Fact]
        public void Tornado_WandersAndPullsEmbersIn()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.Tornado);
            Run(sim, 1.2f, () => sim.Tornadoes.Count > 0);
            Assert.NotEmpty(sim.Tornadoes);
            Tornado t = sim.Tornadoes[0];
            Enemy ember = sim.Spawn(EnemyKind.Ember, new Vec2(t.Pos.X + 2.5f, t.Pos.Y));
            ember.Speed = 0f;
            ember.MaxHp = ember.Hp = 999f;
            float before = ember.Pos.DistanceTo(t.Pos);
            Run(sim, 0.5f);
            Assert.True(ember.Hp < 999f, "회오리에 다쳐야");
            Assert.True(ember.Pos.DistanceTo(t.Pos) < before, "가운데로 끌려와야: " + before + " → " + ember.Pos.DistanceTo(t.Pos));
        }

        // ------------------------------------------------------------------
        // 거품 눈덩이
        // ------------------------------------------------------------------

        [Fact]
        public void Foam_RollsTowardTheCrowd_SwallowsAndGrows_ThenBursts()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Foam);
            var crowd = new List<Enemy>();
            for (int k = 0; k < 5; k++) crowd.Add(Dummy(sim, EnemyKind.Ember, 3f + (0.5f * k), 0f, 2f));
            Run(sim, 0.1f);
            Assert.Single(sim.FoamBalls);
            FoamBall f = sim.FoamBalls[0];
            Assert.True(f.Dir.X > 0.9f, "몹 쪽으로 굴러야: " + f.Dir.X);
            float r0 = f.R;
            float rMax = r0;
            bool burst = false;
            Run(sim, SurvivorSim.FoamLife + 0.5f, () =>
            {
                rMax = Math.Max(rMax, f.R);
                return burst |= sim.FoamBursts.Count > 0;
            });
            Assert.True(rMax > r0, "삼킨 만큼 커져야: " + r0 + " → " + rMax);
            Assert.True(burst, "끝에 터져야");
            Assert.All(crowd, e => Assert.True(e.Dead, "무리가 녹아야"));
        }

        [Fact]
        public void Foam_CountAndSizeGrow()
        {
            Assert.Equal(new[] { 0, 1, 1, 2, 2, 3 }, SurvivorSim.FoamCount);
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Foam, Loadout.MaxLevel);
            Run(sim, 0.1f);
            Assert.Equal(3, sim.FoamBalls.Count);
            Assert.All(sim.FoamBalls, f => Assert.Equal(2.4f, f.MaxR, 2));
        }

        [Fact]
        public void Avalanche_SweepsAcross_HittingEmbersAndSoakingShops()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.Avalanche);
            Enemy ember = Dummy(sim, EnemyKind.Ember, 6f, 3f);
            Structure shop = Shop(sim, -6f, -4f, 0.6f);
            float fire = shop.Fire;
            Run(sim, 2.5f + 2.5f);
            Assert.True(ember.Hp < ember.MaxHp, "파도에 맞아야");
            Assert.True(shop.Fire < fire, "파도가 불을 적셔야");
        }

        // ------------------------------------------------------------------
        // 액체질소 지뢰
        // ------------------------------------------------------------------

        [Fact]
        public void Mine_FreezesWhatStepsOnIt_ThenItShatters()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Mine);
            Run(sim, 0.5f);
            Assert.Single(sim.Mines);
            Mine m = sim.Mines[0];
            sim.Player = new Vec2(sim.Player.X - 6f, sim.Player.Y);
            Enemy ember = sim.Spawn(EnemyKind.Ember, m.Pos);
            ember.MaxHp = ember.Hp = 999f;
            Run(sim, 0.1f);
            Assert.True(ember.Frozen > 0f, "밟은 몹이 얼어야");
            Assert.DoesNotContain(m, sim.Mines);
            Vec2 at = ember.Pos;
            Run(sim, 1f);
            Assert.True(ember.Pos.DistanceTo(at) < 0.01f, "언 몹은 못 움직인다");
            Run(sim, SurvivorSim.FreezeTime, () => ember.Frozen <= 0f);
            Assert.True(ember.Frozen <= 0f);
            Assert.True(ember.Hp <= 999f - SurvivorSim.MineShatter + 0.01f, "풀리며 깨져야");
        }

        [Fact]
        public void FrozenEmber_DoesNotBurnTheFirefighter()
        {
            SurvivorSim sim = Quiet();
            Enemy ember = Dummy(sim, EnemyKind.Ember, 0.2f, 0f);
            ember.Frozen = 5f;
            float hp = sim.Hp;
            sim.Step(0f, 0f);
            sim.Step(0f, 0f);
            Assert.Equal(hp, sim.Hp);
        }

        [Fact]
        public void Mine_HoldsAtMostItsLevelPlusOne()
        {
            Assert.Equal(new[] { 0, 2, 3, 4, 5, 6 }, SurvivorSim.MineMax);
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Mine);
            // 걸으며 깐다: 1.5칸 넘게 떨어질 때마다 하나, 최대 2개.
            for (int i = 0; i < 60 * 6; i++)
            {
                sim.Enemies.Clear();
                sim.Hp = sim.MaxHp;
                sim.Step(i % 240 < 120 ? 1f : -1f, 0f);
            }
            Assert.Equal(2, sim.Mines.Count);
        }

        [Fact]
        public void IceField_LinksMines_AndFreezesWhatCrossesTheLine()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.IceField);
            sim.Mines.Clear();
            sim.Mines.Add(new Mine { Pos = new Vec2(sim.Player.X + 2f, sim.Player.Y + 4f) });
            sim.Mines.Add(new Mine { Pos = new Vec2(sim.Player.X + 6f, sim.Player.Y + 4f) });
            Enemy ember = Dummy(sim, EnemyKind.Ember, 4f, 4f);
            Run(sim, 0.1f);
            Assert.True(ember.Frozen > 0f, "얼음 길 위 몹이 얼어야");
            // 얼음 길로 얼린 것은 지뢰를 쓰지 않는다(두 지뢰가 그대로 남는다).
            Assert.Equal(2, sim.Mines.FindAll(m => m.Pos.DistanceTo(sim.Player) > 1f).Count);
        }

        // ------------------------------------------------------------------
        // 비눗방울
        // ------------------------------------------------------------------

        [Fact]
        public void Bubble_TrapsASmallFire_FloatsIt_AndPopsItDead()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Bubble);
            Enemy ember = Dummy(sim, EnemyKind.Ember, 4f, 0f, 4f);
            Run(sim, 1f, () => ember.Captured > 0f);
            Assert.True(ember.Captured > 0f, "방울에 갇혀야");
            int kills = sim.Kills;
            bool popped = false;
            Run(sim, SurvivorSim.BubbleHold + 0.2f, () => popped |= sim.BubblePops.Count > 0);
            Assert.True(popped);
            Assert.True(ember.Dead);
            Assert.True(sim.Kills > kills);
        }

        [Fact]
        public void Bubble_CannotTrapAHeavyFire_OnlyHitsIt()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Bubble);
            Enemy big = Dummy(sim, EnemyKind.Blaze, 4f, 0f);
            big.Heavy = true;
            Run(sim, 1f, () => big.Hp < big.MaxHp);
            Assert.Equal(0f, big.Captured);
            Assert.True(big.Hp < big.MaxHp);
        }

        [Fact]
        public void Bubble_GoesForTheRaiderFirst()
        {
            SurvivorSim sim = Quiet();
            Take(sim, UpgradeId.Bubble);
            Enemy near = Dummy(sim, EnemyKind.Ember, 2f, 0f, 4f);
            Enemy raider = Dummy(sim, EnemyKind.Ember, -6f, 0f, 4f);
            raider.Goal = Shop(sim, -10f, 0f);
            Run(sim, 1f, () => raider.Captured > 0f || near.Captured > 0f);
            Assert.True(raider.Captured > 0f, "마을을 노리는 몹부터");
        }

        [Fact]
        public void BubbleFall_GathersTrappedFires_IntoOneBigPop()
        {
            SurvivorSim sim = Quiet();
            Evolve(sim, UpgradeId.BubbleFall);
            var trapped = new List<Enemy>();
            for (int k = 0; k < 3; k++)
            {
                Enemy e = Dummy(sim, EnemyKind.Ember, 3f + k, 2f);
                e.Captured = 0.05f + (0.5f * k);
                trapped.Add(e);
            }
            Enemy bystander = Dummy(sim, EnemyKind.Blaze, 4f, 3.5f);
            Run(sim, 0.2f);
            Assert.All(trapped, e => Assert.True(e.Dead, "한꺼번에 터져야"));
            Assert.True(bystander.Hp < bystander.MaxHp, "큰 방울 물보라가 곁 불도 친다");
        }

        // ------------------------------------------------------------------
        // 맨홀 간헐천
        // ------------------------------------------------------------------

        [Fact]
        public void Manholes_LieOnOpenGround()
        {
            var sim = new SurvivorSim(1, 1) { Guardian = false };
            Assert.True(sim.Manholes.Count > 20, "맨홀 " + sim.Manholes.Count);
            foreach (Vec2 m in sim.Manholes) Assert.DoesNotContain(sim.Structures, s => s.Within(m, 0.5f));
        }

        [Fact]
        public void Manhole_EruptsUnderTheCrowd_AfterAWarning()
        {
            SurvivorSim sim = Quiet();
            sim.Manholes.Clear();
            var hole = new Vec2(sim.Player.X + 5f, sim.Player.Y);
            sim.Manholes.Add(hole);
            sim.Manholes.Add(new Vec2(sim.Player.X - 5f, sim.Player.Y));
            Take(sim, UpgradeId.Manhole);
            Enemy a = Dummy(sim, EnemyKind.Ember, 5.5f, 0f);
            Enemy b = Dummy(sim, EnemyKind.Ember, 4.5f, 0.5f);
            Run(sim, 0.1f);
            Assert.Single(sim.Geysers);
            Assert.Equal(hole.X, sim.Geysers[0].Pos.X, 2);
            Assert.Equal(a.MaxHp, a.Hp);
            bool burst = false;
            Run(sim, SurvivorSim.GeyserFuse + 0.1f, () => burst |= sim.GeyserBursts.Count > 0);
            Assert.True(burst);
            Assert.True(a.Hp < a.MaxHp && b.Hp < b.MaxHp);
        }

        [Fact]
        public void Manhole_MoreAtOnceWithLevel()
        {
            Assert.Equal(new[] { 0, 1, 1, 2, 3, 4 }, SurvivorSim.GeyserCount);
            SurvivorSim sim = Quiet();
            sim.Manholes.Clear();
            for (int k = 0; k < 6; k++)
            {
                var m = new Vec2(sim.Player.X - 7.5f + (3f * k), sim.Player.Y + 3f);
                sim.Manholes.Add(m);
                Dummy(sim, EnemyKind.Ember, m.X - sim.Player.X, 3f);
            }
            Take(sim, UpgradeId.Manhole, Loadout.MaxLevel);
            Run(sim, 0.1f);
            Assert.Equal(4, sim.Geysers.Count);
        }

        [Fact]
        public void Waterline_BurstsInARow_LikeDominoes()
        {
            SurvivorSim sim = Quiet();
            sim.Manholes.Clear();
            sim.Manholes.Add(new Vec2(sim.Player.X + 3f, sim.Player.Y));
            Evolve(sim, UpgradeId.Waterline);
            for (int k = 0; k < 4; k++) Dummy(sim, EnemyKind.Ember, 3.5f + (1.6f * k), 0f);
            Run(sim, 0.1f);
            Assert.Equal(6, sim.Geysers.Count);
            int bursts = 0;
            int ticksWithBurst = 0;
            Run(sim, 1.5f, () =>
            {
                if (sim.GeyserBursts.Count > 0) ticksWithBurst++;
                bursts += sim.GeyserBursts.Count;
                return false;
            });
            Assert.Equal(6, bursts);
            Assert.True(ticksWithBurst >= 5, "차례로 터져야: " + ticksWithBurst + "틱");
        }
    }
}
