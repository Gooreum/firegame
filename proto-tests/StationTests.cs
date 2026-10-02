using System.Collections.Generic;
using FireGame.Prototypes.Logic;
using Xunit;

namespace FireGame.Prototypes.Tests
{
    /// <summary>소방서: 별이 남고, 별로 시작 장비가 다른 소방관을 해금한다.</summary>
    public class StationTests
    {
        [Fact]
        public void Station_StartsWithTheRookieOnly()
        {
            var station = new FireStation();
            Assert.Equal(0, station.Stars);
            Assert.Equal(new HashSet<string> { "rookie" }, station.Unlocked);
            Assert.Equal("rookie", station.Selected);
            Assert.Equal(new[] { UpgradeId.Hose }, station.Current.Start);
            Assert.All(station.Best, b => Assert.Equal(0, b));
        }

        [Fact]
        public void StartFor_WithoutPrep_IsTheFirefightersStart()
        {
            var station = new FireStation();
            Assert.Null(station.Prep);
            for (int n = 1; n <= SurvivorStages.Count; n++) Assert.Equal(station.Current.Start, station.StartFor(SurvivorStages.Get(n)));
            // 저장 글엔 대비 장비가 없다(스테이지마다 다시 고른다).
            station.Prep = UpgradeId.Boots;
            Assert.DoesNotContain("Boots", station.Serialize());
            Assert.Null(FireStation.Parse(station.Serialize()).Prep);
        }

        [Fact]
        public void Winning_BanksStars_AndKeepsTheBest()
        {
            var station = new FireStation();
            Assert.Equal(2, station.RecordResult(1, 2));
            Assert.Equal(3, station.RecordResult(1, 3));
            Assert.Equal(1, station.RecordResult(2, 1));
            Assert.Equal(0, station.RecordResult(3, 0));
            Assert.Equal(6, station.Stars);
            Assert.Equal(3, station.Best[1]);
            Assert.Equal(1, station.Best[2]);
            Assert.Equal(0, station.Best[3]);
        }

        [Fact]
        public void Unlock_SpendsStars_AndSelects()
        {
            var station = new FireStation();
            station.RecordResult(1, 3);
            Assert.True(station.CanUnlock("rescue"));
            Assert.True(station.Unlock("rescue"));
            Assert.Equal(0, station.Stars);
            Assert.True(station.IsUnlocked("rescue"));
            Assert.Equal("rescue", station.Selected);
            Assert.Contains(UpgradeId.Partner, station.Current.Start);
        }

        [Fact]
        public void Unlock_RefusesWithoutEnoughStars_OrForBadIds()
        {
            var station = new FireStation();
            station.RecordResult(1, 2);
            Assert.False(station.CanUnlock("rescue"));
            Assert.False(station.Unlock("rescue"));
            Assert.False(station.Unlock("rookie"));
            Assert.False(station.Unlock("nobody"));
            Assert.Equal(2, station.Stars);
            Assert.Equal("rookie", station.Selected);
        }

        [Fact]
        public void Select_OnlyUnlocked()
        {
            var station = new FireStation();
            Assert.False(station.Select("pilot"));
            Assert.Equal("rookie", station.Selected);
            station.RecordResult(1, 5);
            Assert.True(station.Unlock("pilot"));
            Assert.True(station.Select("rookie"));
            Assert.True(station.Select("pilot"));
            Assert.Equal("pilot", station.Selected);
        }

        [Fact]
        public void Serialize_RoundTrips()
        {
            var station = new FireStation();
            station.RecordResult(1, 3);
            station.RecordResult(2, 2);
            station.RecordResult(1, 3);
            station.Unlock("rescue");
            station.Unlock("pump");
            station.Select("rescue");
            string text = station.Serialize();
            Assert.Equal("stars=0;best=3,2,0;unlocked=rookie,rescue,pump;selected=rescue", text);

            FireStation back = FireStation.Parse(text);
            Assert.Equal(station.Stars, back.Stars);
            Assert.Equal(station.Best, back.Best);
            Assert.Equal(station.Unlocked, back.Unlocked);
            Assert.Equal(station.Selected, back.Selected);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("garbage=1;;x")]
        [InlineData("stars=abc;best=1,2,3,4,5,6;unlocked=ghost;selected=ghost")]
        [InlineData("stars=-5;selected=pilot")]
        public void Parse_GarbageGivesASafeStation(string text)
        {
            FireStation station = FireStation.Parse(text);
            Assert.Equal(0, station.Stars);
            Assert.Equal(new HashSet<string> { "rookie" }, station.Unlocked);
            Assert.Equal("rookie", station.Selected);
        }

        [Fact]
        public void Roster_StartsFitTheSlots_AndHaveNoEvolutionsOrSpecials()
        {
            var ids = new HashSet<string>();
            int lastCost = -1;
            foreach (Firefighter f in Roster.All)
            {
                Assert.True(ids.Add(f.Id), f.Id + " 중복");
                Assert.True(f.Cost >= lastCost, f.Name + " 비용이 앞사람보다 싸다");
                lastCost = f.Cost;
                Assert.Contains(UpgradeId.Hose, f.Start);
                var build = new Loadout();
                foreach (UpgradeId id in f.Start)
                {
                    Assert.False(Loadout.IsEvolution(id) || Loadout.IsSpecial(id) || id == UpgradeId.Heal, f.Name + "의 시작 장비 " + id + "는 들 수 없다");
                    Assert.True(build.CanTake(id), f.Name + "의 시작 장비 " + id + "가 칸을 넘친다");
                    build.Add(id);
                }
            }
            Assert.Equal(0, Roster.All[0].Cost);
            Assert.Equal("rookie", Roster.Default);
        }
    }
}
