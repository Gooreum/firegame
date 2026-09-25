using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 시드. 같은 현장을 두 번째 뛸 때 외운 순서가 안 통하게 하되,
    /// 시드 0은 지금과 완전히 같아야 한다 — 기존 테스트 전부가 그 증거다.
    /// </summary>
    public class SeedTests
    {
        public static IEnumerable<object[]> StageIndexes()
        {
            for (int i = 0; i < StageCatalog.All.Length; i++) yield return new object[] { i };
        }

        private static StageRunner Runner(StageDef stage, int seed)
        {
            return new StageRunner(stage, Loadout.FromIds(new[] { EquipmentId.Bucket }), seed);
        }

        private static List<GridPoint> Burning(StageRunner runner)
        {
            var points = new List<GridPoint>();
            for (int y = 0; y < runner.Grid.Height; y++)
            {
                for (int x = 0; x < runner.Grid.Width; x++)
                {
                    if (runner.Grid[x, y].State == CellState.Burning) points.Add(new GridPoint(x, y));
                }
            }
            return points;
        }

        // --- TC-1 ---
        [Theory]
        [MemberData(nameof(StageIndexes))]
        public void SeedZero_IsExactlyTheMarkedMap(int index)
        {
            StageDef stage = StageCatalog.All[index];
            var unseeded = new StageRunner(stage, Loadout.FromIds(new[] { EquipmentId.Bucket }));

            Assert.Equal(Burning(unseeded), Burning(Runner(stage, 0)));
            Assert.Equal(0, unseeded.Seed);
        }

        // --- TC-2 ---
        [Theory]
        [MemberData(nameof(StageIndexes))]
        public void SameSeed_PlaysOutTheSameFireTwice(int index)
        {
            StageDef stage = StageCatalog.All[index];
            StageRunner first = Runner(stage, 4812);
            StageRunner second = Runner(stage, 4812);

            Assert.Equal(Burning(first), Burning(second));

            // 소방관이 가만히 서 있어도 불은 번진다. 20초 뒤에도 한 칸 어긋나면 안 된다.
            for (int frame = 0; frame < 400; frame++)
            {
                first.Update(0.05f, default);
                second.Update(0.05f, default);
            }

            Assert.Equal(first.TicksElapsed, second.TicksElapsed);
            Assert.Equal(first.Grid.IntactRatio(), second.Grid.IntactRatio());
            Assert.Equal(Burning(first), Burning(second));
        }

        // --- TC-3 ---
        [Theory]
        [MemberData(nameof(StageIndexes))]
        public void OtherSeeds_MoveTheFire(int index)
        {
            StageDef stage = StageCatalog.All[index];
            List<GridPoint> marked = Burning(Runner(stage, 0));

            var starts = new HashSet<string>();
            for (int seed = 1; seed <= 5; seed++)
            {
                List<GridPoint> fire = Burning(Runner(stage, seed));
                Assert.NotEqual(marked, fire);
                starts.Add(string.Join(";", fire));
            }

            // 다섯 번 뛰면 적어도 두 가지 판은 봐야 변주라고 부를 수 있다.
            Assert.True(starts.Count >= 2, stage.Name + ": 시드 다섯 개가 모두 같은 자리에 불을 냈다");
        }

        // --- TC-4 ---
        [Theory]
        [MemberData(nameof(StageIndexes))]
        public void Seeds_KeepEachFiresMaterial_SoTheGearTableStaysTrue(int index)
        {
            StageDef stage = StageCatalog.All[index];

            for (int seed = 1; seed <= 10; seed++)
            {
                StageRunner seeded = Runner(stage, seed);
                StageRunner marked = Runner(stage, 0);

                Assert.Equal(CountByMaterial(marked), CountByMaterial(seeded));
            }
        }

        private static Dictionary<byte, int> CountByMaterial(StageRunner runner)
        {
            var counts = new Dictionary<byte, int>();
            foreach (GridPoint point in Burning(runner))
            {
                byte material = runner.Grid[point.X, point.Y].Material;
                counts.TryGetValue(material, out int count);
                counts[material] = count + 1;
            }
            return counts;
        }

        // --- TC-5 ---
        [Theory]
        [MemberData(nameof(StageIndexes))]
        public void Seeds_NeverStartAFireOnTopOfSomeone(int index)
        {
            StageDef stage = StageCatalog.All[index];

            for (int seed = 1; seed <= 10; seed++)
            {
                StageRunner runner = Runner(stage, seed);
                List<GridPoint> marked = Burning(Runner(stage, 0));

                foreach (GridPoint fire in Burning(runner))
                {
                    // 맵에 원래 표시된 자리는 설계자가 고른 것이라 그대로 둬도 된다.
                    if (marked.Contains(fire)) continue;

                    foreach (Civilian civilian in runner.Civilians)
                    {
                        int dx = System.Math.Abs((int)civilian.X - fire.X);
                        int dy = System.Math.Abs((int)civilian.Y - fire.Y);
                        Assert.True(dx > 2 || dy > 2, stage.Name + " 시드 " + seed + ": 시민 곁에서 불이 났다");
                    }
                }
            }
        }

        // --- TC-6 ---
        [Fact]
        public void Rng_StaysInRange_AndHandlesDegenerateBounds()
        {
            var rng = new Rng(7);
            Assert.Equal(0, rng.Next(0));
            Assert.Equal(0, rng.Next(1));

            var seen = new HashSet<int>();
            for (int i = 0; i < 1000; i++)
            {
                int value = rng.Next(6);
                Assert.InRange(value, 0, 5);
                seen.Add(value);
            }
            Assert.Equal(6, seen.Count);
        }

        // --- TC-7 ---
        [Fact]
        public void Rng_SameSeed_SameSequence()
        {
            var a = new Rng(-12345);
            var b = new Rng(-12345);
            for (int i = 0; i < 100; i++) Assert.Equal(a.Next(1000), b.Next(1000));
        }

        // --- TC-8 ---
        /// <summary>
        /// 변주판도 대부분은 필요 장비로 깨진다.
        ///
        /// "전부"가 아니라 "절반 이상"인 이유: 공장은 빠듯하게 맞춘 현장이라 발화점이 옮겨지면
        /// 필요 장비만으로는 시간 안에 못 끄는 판이 나온다(시드 1~10 중 넷). 그래서 변주는
        /// <b>한 번 깬 현장을 다시 뛸 때만</b> 쓴다 — 첫 클리어는 언제나 시드 0이고,
        /// 장비 표의 약속은 <see cref="RequiredGearTests"/>가 시드 0으로 지킨다.
        /// </summary>
        [Theory]
        [MemberData(nameof(RequiredGearTests.MissionIndexes), MemberType = typeof(RequiredGearTests))]
        public void EveryMission_IsMostlyBeatenWithItsRequiredGear_OnOtherSeeds(int index)
        {
            MissionDef mission = Campaign.Missions[index];
            SaveData save = RequiredGearTests.SaveWithRequiredGear(index);

            int wins = 0;
            string log = "";
            for (int seed = 1; seed <= 10; seed++)
            {
                var runner = new StageRunner(mission.Stage, Loadout.From(save), seed);
                StageOutcome outcome = new GreedyBot(runner).Play();
                if (outcome == StageOutcome.Won) wins++;
                log += " " + seed + "=" + outcome;
            }

            Assert.True(wins >= 5, mission.Title + ": 변주판 열 개 중 " + wins + "개만 깼다." + log);
        }
    }
}
