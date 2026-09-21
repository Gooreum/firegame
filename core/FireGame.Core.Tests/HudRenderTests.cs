using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Render;
using Xunit;

namespace FireGame.Core.Tests
{
    public class HudRenderTests
    {
        private const int HudTop = FrameBuffer.PlayfieldHeight;

        private static StageRunner Runner(params int[] loadout)
        {
            return new StageRunner(StageCatalog.Residential, new List<int>(loadout));
        }

        private static int CountNonBlack(FrameBuffer buffer, int x, int y, int width, int height)
        {
            int count = 0;
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    if (buffer.Get(px, py) != Palette.Black) count++;
                }
            }
            return count;
        }

        private static int CountColor(FrameBuffer buffer, byte color, int x, int y, int width, int height)
        {
            int count = 0;
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    if (buffer.Get(px, py) == color) count++;
                }
            }
            return count;
        }

        private static bool RegionsDiffer(FrameBuffer a, FrameBuffer b, int x, int y, int width, int height)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    if (a.Get(px, py) != b.Get(px, py)) return true;
                }
            }
            return false;
        }

        // --- TC-1 ---
        [Fact]
        public void Hud_StaysInsideItsStrip_AndNeverTouchesThePlayfield()
        {
            StageRunner runner = Runner(EquipmentId.Bucket);
            var buffer = new FrameBuffer();

            // 격자 영역을 표식으로 칠해두고 HUD만 그린다.
            buffer.FillRect(0, 0, FrameBuffer.Width, FrameBuffer.PlayfieldHeight, Palette.Magenta);
            HudRenderer.DrawHud(buffer, runner, 1250);

            for (int y = 0; y < FrameBuffer.PlayfieldHeight; y++)
            {
                for (int x = 0; x < FrameBuffer.Width; x++)
                {
                    Assert.Equal(Palette.Magenta, buffer.Get(x, y));
                }
            }

            Assert.True(CountNonBlack(buffer, 0, HudTop, FrameBuffer.Width, FrameBuffer.HudHeight) > 0,
                "HUD 영역에는 실제로 뭔가 그려져야 한다");
        }

        // --- TC-2 ---
        [Fact]
        public void Money_ShowsUpInTheHud()
        {
            StageRunner runner = Runner(EquipmentId.Bucket);

            var poor = new FrameBuffer();
            var rich = new FrameBuffer();
            HudRenderer.DrawHud(poor, runner, 0);
            HudRenderer.DrawHud(rich, runner, 98765);

            Assert.True(RegionsDiffer(poor, rich, 0, HudTop, 64, 12),
                "금액이 바뀌면 표시가 달라져야 한다");
        }

        // --- TC-3 ---
        [Fact]
        public void HealthBar_ShrinksWithHealth()
        {
            StageRunner full = Runner(EquipmentId.Bucket);
            StageRunner half = Runner(EquipmentId.Bucket);
            half.Player.Hp = GameConfig.PlayerMaxHp * 0.5f;

            var fullBuffer = new FrameBuffer();
            var halfBuffer = new FrameBuffer();
            HudRenderer.DrawHud(fullBuffer, full, 0);
            HudRenderer.DrawHud(halfBuffer, half, 0);

            int fullBar = CountColor(fullBuffer, Palette.LightGreen, 90, HudTop, 70, 12);
            int halfBar = CountColor(halfBuffer, Palette.Yellow, 90, HudTop, 70, 12);

            Assert.True(fullBar > 0, "체력이 가득 차면 녹색 바가 보여야 한다");
            Assert.True(halfBar > 0, "체력이 절반이면 노란 바가 보여야 한다");
            Assert.True(halfBar < fullBar, "절반이면 바가 더 짧아야 한다");
        }

        // --- TC-4 ---
        [Fact]
        public void HealthBar_AtZero_DrawsNothingAndDoesNotGoNegative()
        {
            StageRunner runner = Runner(EquipmentId.Bucket);
            runner.Player.Hp = 0f;

            var buffer = new FrameBuffer();
            HudRenderer.DrawHud(buffer, runner, 0);

            Assert.Equal(0, CountColor(buffer, Palette.LightGreen, 90, HudTop, 70, 12));
            Assert.Equal(0, CountColor(buffer, Palette.Yellow, 90, HudTop, 70, 12));
            Assert.Equal(0, CountColor(buffer, Palette.LightRed, 90, HudTop, 70, 12));
        }

        // --- TC-5 ---
        [Fact]
        public void RemainingTime_IsShown()
        {
            StageRunner early = Runner(EquipmentId.Bucket);
            StageRunner late = Runner(EquipmentId.Bucket);
            late.TimeLeft = 7f;

            var earlyBuffer = new FrameBuffer();
            var lateBuffer = new FrameBuffer();
            HudRenderer.DrawHud(earlyBuffer, early, 0);
            HudRenderer.DrawHud(lateBuffer, late, 0);

            Assert.True(RegionsDiffer(earlyBuffer, lateBuffer, 152, HudTop, 48, 12),
                "남은 시간이 바뀌면 표시가 달라져야 한다");
        }

        // --- TC-6 ---
        [Fact]
        public void ActiveSlot_IsHighlighted()
        {
            StageRunner runner = Runner(EquipmentId.Bucket, EquipmentId.Extinguisher);

            var first = new FrameBuffer();
            runner.Player.ActiveSlot = 0;
            HudRenderer.DrawHud(first, runner, 0);

            var second = new FrameBuffer();
            runner.Player.ActiveSlot = 1;
            HudRenderer.DrawHud(second, runner, 0);

            Assert.True(RegionsDiffer(first, second, 0, HudTop, FrameBuffer.Width, FrameBuffer.HudHeight),
                "활성 슬롯이 바뀌면 강조 위치가 옮겨가야 한다");

            // 강조는 흰 배경으로 표시한다.
            Assert.True(CountColor(first, Palette.White, 0, HudTop, 108, FrameBuffer.HudHeight) > 0);
            Assert.True(CountColor(second, Palette.White, 108, HudTop, 108, FrameBuffer.HudHeight) > 0);
        }

        // --- TC-7 ---
        [Fact]
        public void ChargeCount_UpdatesAsItIsSpent()
        {
            StageRunner runner = Runner(EquipmentId.Bucket, EquipmentId.Extinguisher);

            var before = new FrameBuffer();
            HudRenderer.DrawHud(before, runner, 0);

            runner.Player.Charges[1] = 3;
            var after = new FrameBuffer();
            HudRenderer.DrawHud(after, runner, 0);

            Assert.True(RegionsDiffer(before, after, 108, HudTop, 104, FrameBuffer.HudHeight),
                "충전량이 줄면 표시가 달라져야 한다");

            // 다 쓰면 빨갛게 경고한다.
            runner.Player.Charges[1] = 0;
            var empty = new FrameBuffer();
            HudRenderer.DrawHud(empty, runner, 0);
            Assert.True(CountColor(empty, Palette.Red, 108, HudTop, 104, FrameBuffer.HudHeight) > 0);
        }

        // --- TC-8 ---
        [Fact]
        public void EmptySlots_AreHandledWithoutThrowing()
        {
            StageRunner runner = Runner(EquipmentId.Bucket);
            var buffer = new FrameBuffer();

            HudRenderer.DrawHud(buffer, runner, 500);

            Assert.Equal(-1, runner.Player.Slots[1]);
            Assert.Equal(-1, runner.Player.Slots[2]);
            Assert.True(CountNonBlack(buffer, 0, HudTop, FrameBuffer.Width, FrameBuffer.HudHeight) > 0);
        }

        // --- TC-9 ---
        [Fact]
        public void Shop_ListsEveryPieceOfEquipment()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 4000;

            var buffer = new FrameBuffer();
            HudRenderer.DrawShop(buffer, save, 0);

            Assert.Equal(4, HudRenderer.ShopEntryCount);

            // 장비 4종이 각각 자기 줄에 그려진다.
            for (int i = 0; i < 4; i++)
            {
                int y = 64 + (i * 20);
                Assert.True(CountNonBlack(buffer, 40, y, 100, 8) > 0,
                    i + "번째 장비 이름이 비어 있다");
            }
        }

        // --- TC-10 ---
        [Fact]
        public void Shop_ColourCodesOwnedAffordableAndUnaffordable()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 600;   // 소화기(500)는 사고 호스(3000)는 못 산다

            var buffer = new FrameBuffer();
            HudRenderer.DrawShop(buffer, save, -1);

            // 양동이는 보유 중 → 어두운 회색
            Assert.True(CountColor(buffer, Palette.DarkGray, 40, 64, 220, 8) > 0);

            // 소화기는 구매 가능 → 흰색
            Assert.True(CountColor(buffer, Palette.White, 40, 84, 220, 8) > 0);

            // 호스는 잔액 부족 → 빨강
            Assert.True(CountColor(buffer, Palette.Red, 40, 104, 220, 8) > 0);
        }

        // --- TC-11 ---
        [Fact]
        public void ShopCursor_HighlightsOnlyTheSelectedRow()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 20000;

            var first = new FrameBuffer();
            var third = new FrameBuffer();
            HudRenderer.DrawShop(first, save, 0);
            HudRenderer.DrawShop(third, save, 2);

            Assert.True(RegionsDiffer(first, third, 16, 62, 288, 12),
                "커서가 0번에 있을 때와 2번에 있을 때 첫 줄이 달라야 한다");
            Assert.True(RegionsDiffer(first, third, 16, 102, 288, 12),
                "세 번째 줄도 달라야 한다");

            Assert.True(CountColor(first, Palette.Blue, 16, 62, 288, 12) > 0,
                "커서가 놓인 줄에는 파란 강조 막대가 보여야 한다");
            Assert.Equal(0, CountColor(first, Palette.Blue, 16, 102, 288, 12));
        }

        // --- TC-12 ---
        [Fact]
        public void ShopCursor_OutOfRange_IsSafe()
        {
            SaveData save = SaveData.NewGame();

            var negative = new FrameBuffer();
            var tooLarge = new FrameBuffer();

            HudRenderer.DrawShop(negative, save, -5);
            HudRenderer.DrawShop(tooLarge, save, 99);

            // 커서 강조만 사라지고 목록은 그대로 그려진다.
            Assert.True(CountNonBlack(negative, 40, 64, 220, 8) > 0);
            Assert.True(CountNonBlack(tooLarge, 40, 64, 220, 8) > 0);

            for (int row = 0; row < HudRenderer.ShopEntryCount; row++)
            {
                int y = 62 + (row * 20);
                Assert.Equal(0, CountColor(negative, Palette.Blue, 16, y, 288, 12));
                Assert.Equal(0, CountColor(tooLarge, Palette.Blue, 16, y, 288, 12));
            }
        }

        // --- TC-13 ---
        [Fact]
        public void Shop_CoversTheWholeScreen_SoTheStageDoesNotShowThrough()
        {
            SaveData save = SaveData.NewGame();

            var buffer = new FrameBuffer();
            buffer.Clear(Palette.Magenta);
            HudRenderer.DrawShop(buffer, save, 0);

            for (int y = 0; y < FrameBuffer.Height; y++)
            {
                for (int x = 0; x < FrameBuffer.Width; x++)
                {
                    Assert.NotEqual(Palette.Magenta, buffer.Get(x, y));
                }
            }
        }

        // --- TC-14 ---
        [Fact]
        public void VeryLargeAmounts_DoNotOverflowTheScreen()
        {
            StageRunner runner = Runner(EquipmentId.Bucket);
            SaveData save = SaveData.NewGame();
            save.Money = int.MaxValue;

            var hud = new FrameBuffer();
            var shop = new FrameBuffer();

            HudRenderer.DrawHud(hud, runner, int.MaxValue);
            HudRenderer.DrawShop(shop, save, 0);

            // 프레임버퍼가 경계를 잘라내므로 예외 없이 끝나고, 화면 밖으로 새지 않는다.
            Assert.Equal(FrameBuffer.Width * FrameBuffer.Height, hud.Pixels.Length);
            Assert.True(CountNonBlack(hud, 0, HudTop, FrameBuffer.Width, FrameBuffer.HudHeight) > 0);
            Assert.True(CountNonBlack(shop, 24, 38, 200, 8) > 0);
        }
    }
}
