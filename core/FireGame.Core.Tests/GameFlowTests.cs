using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Render;
using Xunit;

namespace FireGame.Core.Tests
{
    public class GameFlowTests
    {
        // 화면 요소 중심 좌표. 렌더와 같은 ScreenLayout에서 가져온다.
        private static float StageRowY(int index)
        {
            return ScreenLayout.HubStageRowY(index) + 4;
        }

        private static float ShopRowY(int index)
        {
            return ScreenLayout.ShopRowY(index) + 4;
        }

        private static void TapShopButton(GameFlow flow)
        {
            flow.PointerDown(0,
                ScreenLayout.HubShopButtonX + 10,
                ScreenLayout.HubShopButtonY + 8);
            flow.PointerUp(0);
        }

        private static void TapBackButton(GameFlow flow)
        {
            flow.PointerDown(0,
                ScreenLayout.ShopBackButtonX + 10,
                ScreenLayout.ShopBackButtonY + 8);
            flow.PointerUp(0);
        }

        private static void Tap(GameFlow flow, float x, float y)
        {
            flow.PointerDown(0, x, y);
            flow.PointerUp(0);
        }

        // --- TC-1 ---
        [Fact]
        public void NewGame_StartsOnTheHub()
        {
            var flow = new GameFlow(SaveData.NewGame());

            Assert.Equal(GameScreen.Hub, flow.Screen);
            Assert.Null(flow.Runner);
        }

        // --- TC-2 ---
        [Fact]
        public void TappingAnUnlockedStage_StartsIt()
        {
            var flow = new GameFlow(SaveData.NewGame());

            Tap(flow, 60, StageRowY(0));

            Assert.Equal(GameScreen.Playing, flow.Screen);
            Assert.NotNull(flow.Runner);
            Assert.Same(StageCatalog.Residential, flow.Runner.Def);
        }

        // --- TC-3 ---
        [Fact]
        public void TappingALockedStage_DoesNothing()
        {
            var flow = new GameFlow(SaveData.NewGame());

            Tap(flow, 60, StageRowY(2));   // 주유소는 2클리어가 필요하다

            Assert.Equal(GameScreen.Hub, flow.Screen);
            Assert.Null(flow.Runner);
        }

        // --- TC-4 ---
        [Fact]
        public void ShopButton_AndBackButton_MoveBetweenScreens()
        {
            var flow = new GameFlow(SaveData.NewGame());

            TapShopButton(flow);
            Assert.Equal(GameScreen.Shop, flow.Screen);

            TapBackButton(flow);
            Assert.Equal(GameScreen.Hub, flow.Screen);
        }

        // --- TC-5 ---
        [Fact]
        public void TappingAnAffordableItem_BuysItAndSaves()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 800;

            var flow = new GameFlow(save);
            string written = null;
            flow.SaveWriter = text => written = text;

            TapShopButton(flow);
            Tap(flow, 60, ShopRowY(1));   // 소화기

            Assert.Equal(1, flow.ShopCursor);
            Assert.True(save.Owns(EquipmentId.Extinguisher));
            Assert.Equal(800 - EquipmentCatalog.Extinguisher.Price, save.Money);

            Assert.NotNull(written);
            Assert.True(SaveData.TryDeserialize(written, out SaveData restored));
            Assert.True(restored.Owns(EquipmentId.Extinguisher));

            // 못 사는 물건을 탭하면 커서만 옮겨지고 저장은 일어나지 않는다.
            written = null;
            Tap(flow, 60, ShopRowY(3));   // 폼(10000)
            Assert.Equal(3, flow.ShopCursor);
            Assert.False(save.Owns(EquipmentId.FoamExtinguisher));
            Assert.Null(written);
        }

        // --- TC-6 ---
        [Fact]
        public void FinishingAStage_SettlesPaysAndSaves()
        {
            var flow = new GameFlow(SaveData.NewGame());
            string written = null;
            flow.SaveWriter = text => written = text;

            Tap(flow, 60, StageRowY(0));

            // 봇에게 대신 플레이시키고, 끝나는 순간 GameFlow가 정산하는지 본다.
            // 봇은 러너를 직접 몰기 때문에 마지막 한 번은 GameFlow.Update로 넘겨야 정산이 일어난다.
            var bot = new GreedyBot(flow.Runner);
            bot.Play();
            Assert.True(flow.Runner.IsOver);

            flow.Update(0.016f);

            Assert.Equal(GameScreen.Result, flow.Screen);
            Assert.Equal(StageOutcome.Won, flow.LastOutcome);
            Assert.True(flow.LastPayout.Total > 0);
            Assert.Equal(flow.LastPayout.Total, flow.Save.Money);
            Assert.Equal(1, flow.Save.ClearedStages);
            Assert.Equal("RESIDENTIAL", flow.LastStageName);

            Assert.NotNull(written);
            Assert.True(SaveData.TryDeserialize(written, out SaveData restored));
            Assert.Equal(flow.Save.Money, restored.Money);
            Assert.Equal(1, restored.ClearedStages);
        }

        // --- TC-7 ---
        [Fact]
        public void TappingTheResultScreen_ReturnsToTheHub()
        {
            var flow = new GameFlow(SaveData.NewGame());
            Tap(flow, 60, StageRowY(0));

            new GreedyBot(flow.Runner).Play();
            flow.Update(0.016f);
            Assert.Equal(GameScreen.Result, flow.Screen);

            Tap(flow, 160, 100);

            Assert.Equal(GameScreen.Hub, flow.Screen);

            // 한 판을 깼으니 이제 상가가 열려 있어야 한다.
            Tap(flow, 60, StageRowY(1));
            Assert.Equal(GameScreen.Playing, flow.Screen);
            Assert.Same(StageCatalog.Shopping, flow.Runner.Def);
        }

        // --- TC-8 ---
        [Fact]
        public void LeftSideDrag_ActsAsAVirtualJoystick()
        {
            var flow = new GameFlow(SaveData.NewGame());
            Tap(flow, 60, StageRowY(0));

            float startX = flow.Runner.Player.X;

            // 절반만 밀면 절반 속도.
            flow.PointerDown(7, 60, 100);
            flow.PointerMove(7, 60 + (ScreenLayout.JoystickRadius * 0.5f), 100);
            flow.Update(0.1f);
            float halfStep = flow.Runner.Player.X - startX;

            Assert.Equal(GameConfig.PlayerSpeed * 0.1f * 0.5f, halfStep, 3);

            // 끝까지 넘게 밀어도 최대 속도에서 멈춘다.
            float beforeFull = flow.Runner.Player.X;
            flow.PointerMove(7, 60 + (ScreenLayout.JoystickRadius * 5f), 100);
            flow.Update(0.1f);
            float fullStep = flow.Runner.Player.X - beforeFull;

            Assert.Equal(GameConfig.PlayerSpeed * 0.1f, fullStep, 3);

            // 떼면 멈춘다.
            flow.PointerUp(7);
            float beforeRelease = flow.Runner.Player.X;
            flow.Update(0.1f);

            Assert.Equal(beforeRelease, flow.Runner.Player.X, 4);
        }

        // --- TC-9 ---
        [Fact]
        public void RightSidePress_FiresOnlyWhileHeld()
        {
            var flow = new GameFlow(SaveData.NewGame());
            Tap(flow, 60, StageRowY(0));

            PlayerState player = flow.Runner.Player;

            flow.PointerDown(3, 250, 100);
            flow.Update(0.016f);
            Assert.True(player.Cooldowns[0] > 0f, "누르고 있으면 발사돼서 쿨다운이 걸려야 한다");

            flow.PointerUp(3);
            player.Cooldowns[0] = 0f;
            flow.Update(0.016f);
            Assert.Equal(0f, player.Cooldowns[0]);
        }

        // --- TC-10 ---
        [Fact]
        public void TappingTheHudSlotRow_SelectsThatSlot_ButIgnoresEmptySlots()
        {
            SaveData save = SaveData.NewGame();
            save.Unlocked.Add(EquipmentId.Extinguisher);

            var flow = new GameFlow(save);
            Tap(flow, 60, StageRowY(0));

            float hudY = FrameBuffer.PlayfieldHeight + 16;
            float slotTwoX = ScreenLayout.HudSlotLeft + ScreenLayout.HudSlotWidth + 20;
            float slotThreeX = ScreenLayout.HudSlotLeft + (ScreenLayout.HudSlotWidth * 2) + 20;

            Tap(flow, slotTwoX, hudY);
            Assert.Equal(1, flow.ActiveSlot);

            // 3번 슬롯은 비어 있으므로 선택이 바뀌지 않는다.
            Tap(flow, slotThreeX, hudY);
            Assert.Equal(1, flow.ActiveSlot);

            flow.Update(0.016f);
            Assert.Equal(1, flow.Runner.Player.ActiveSlot);
        }

        // --- TC-11 ---
        [Fact]
        public void KeyboardInput_MovesAndFires()
        {
            var flow = new GameFlow(SaveData.NewGame());
            Tap(flow, 60, StageRowY(0));

            float startY = flow.Runner.Player.Y;

            flow.SetKeyboard(0f, 1f, false, -1);
            flow.Update(0.1f);
            Assert.True(flow.Runner.Player.Y > startY, "아래 방향키로 움직여야 한다");

            flow.SetKeyboard(0f, 0f, true, -1);
            flow.Update(0.016f);
            Assert.True(flow.Runner.Player.Cooldowns[0] > 0f, "발사키로 쏴야 한다");
        }

        // --- TC-12 ---
        [Fact]
        public void JoystickAndFire_WorkAtTheSameTimeWithTwoFingers()
        {
            var flow = new GameFlow(SaveData.NewGame());
            Tap(flow, 60, StageRowY(0));

            float startX = flow.Runner.Player.X;

            flow.PointerDown(1, 60, 100);
            flow.PointerMove(1, 60 + ScreenLayout.JoystickRadius, 100);
            flow.PointerDown(2, 260, 100);

            flow.Update(0.1f);

            Assert.True(flow.Runner.Player.X > startX, "왼손으로는 움직여야 한다");
            Assert.True(flow.Runner.Player.Cooldowns[0] > 0f, "오른손으로는 쏴야 한다");

            // 두 번째 손가락이 왼쪽을 눌러도 조이스틱을 빼앗지 않는다.
            flow.PointerDown(9, 20, 20);
            flow.PointerMove(9, 0, 0);
            float beforeSteal = flow.Runner.Player.X;
            flow.Update(0.1f);
            Assert.True(flow.Runner.Player.X > beforeSteal, "첫 손가락의 조이스틱이 유지돼야 한다");
        }

        // --- TC-13 ---
        [Fact]
        public void EveryScreen_RendersWithoutThrowing()
        {
            var flow = new GameFlow(SaveData.NewGame());
            var buffer = new FrameBuffer();

            flow.Render(buffer);
            Assert.Equal(GameScreen.Hub, flow.Screen);
            Assert.Contains(buffer.Pixels, p => p != Palette.Black);

            TapShopButton(flow);
            flow.Render(buffer);
            Assert.Equal(GameScreen.Shop, flow.Screen);

            TapBackButton(flow);
            Tap(flow, 60, StageRowY(0));
            buffer.Clear();
            flow.Render(buffer);
            Assert.Equal(GameScreen.Playing, flow.Screen);

            // 플레이 화면에는 HUD가 붙는다.
            bool hudDrawn = false;
            for (int x = 0; x < FrameBuffer.Width && !hudDrawn; x++)
            {
                for (int y = FrameBuffer.PlayfieldHeight; y < FrameBuffer.Height; y++)
                {
                    if (buffer.Get(x, y) != Palette.Black)
                    {
                        hudDrawn = true;
                        break;
                    }
                }
            }
            Assert.True(hudDrawn);

            new GreedyBot(flow.Runner).Play();
            flow.Update(0.016f);
            flow.Render(buffer);
            Assert.Equal(GameScreen.Result, flow.Screen);
        }

        [Fact]
        public void StartingAStage_ClearsFingersLeftOverFromTheMenu()
        {
            var flow = new GameFlow(SaveData.NewGame());

            // 허브에서 스테이지를 누른 손가락을 떼지 않은 채 플레이가 시작된다.
            flow.PointerDown(5, 60, StageRowY(0));
            Assert.Equal(GameScreen.Playing, flow.Screen);

            float startX = flow.Runner.Player.X;
            flow.PointerMove(5, 200, StageRowY(0));
            flow.Update(0.1f);

            // 메뉴에서 누른 손가락이 조이스틱으로 둔갑하면 안 된다.
            Assert.Equal(startX, flow.Runner.Player.X, 4);
            Assert.Equal(0f, flow.Runner.Player.Cooldowns[0]);
        }

        [Fact]
        public void LostStage_PaysNothingAndDoesNotRecordAClear()
        {
            var flow = new GameFlow(SaveData.NewGame());
            Tap(flow, 60, StageRowY(0));

            // 불 옆에 가만히 두면 결국 진다.
            for (int i = 0; i < 20000 && flow.Screen == GameScreen.Playing; i++)
            {
                flow.Update(0.05f);
            }

            Assert.Equal(GameScreen.Result, flow.Screen);
            Assert.NotEqual(StageOutcome.Won, flow.LastOutcome);
            Assert.Equal(0, flow.Save.Money);
            Assert.Equal(0, flow.Save.ClearedStages);
        }
    }
}

namespace FireGame.Core.Tests
{
    public class ScreenMappingTests
    {
        [Fact]
        public void WidePhoneScreen_MapsCornersOfThePictureToBufferCorners()
        {
            // 2340x1080 가로 화면. 세로가 꽉 차고 좌우에 검은 띠가 생긴다.
            const float w = 2340f;
            const float h = 1080f;
            float scale = ScreenLayout.FitScale(w, h);
            Assert.Equal(1080f / 200f, scale, 4);

            float offsetX = (w - (320f * scale)) / 2f;

            // 그림의 왼쪽 위 = 화면 (offsetX, h)
            ScreenLayout.ScreenToFrameBuffer(offsetX, h, w, h, out float x0, out float y0);
            Assert.Equal(0f, x0, 3);
            Assert.Equal(0f, y0, 3);

            // 그림의 오른쪽 아래 = 화면 (w - offsetX, 0)
            ScreenLayout.ScreenToFrameBuffer(w - offsetX, 0f, w, h, out float x1, out float y1);
            Assert.Equal(320f, x1, 3);
            Assert.Equal(200f, y1, 3);

            // 왼쪽 검은 띠를 누르면 버퍼 밖(음수)이 나온다.
            ScreenLayout.ScreenToFrameBuffer(10f, 500f, w, h, out float xBar, out _);
            Assert.True(xBar < 0f);
        }

        [Fact]
        public void TallScreen_FitsWidthAndLetterboxesTopAndBottom()
        {
            const float w = 1080f;
            const float h = 2340f;

            Assert.Equal(1080f / 320f, ScreenLayout.FitScale(w, h), 4);

            ScreenLayout.ScreenToFrameBuffer(w / 2f, h / 2f, w, h, out float cx, out float cy);
            Assert.Equal(160f, cx, 3);
            Assert.Equal(100f, cy, 3);
        }

        [Fact]
        public void OrthographicSize_MatchesTheSameLetterbox()
        {
            // 넓은 화면: 스프라이트 세로(2.0)가 딱 맞으므로 절반인 1.0
            Assert.Equal(1.0f, ScreenLayout.OrthographicSize(2340f, 1080f), 4);

            // 정확히 16:10: 역시 1.0
            Assert.Equal(1.0f, ScreenLayout.OrthographicSize(1600f, 1000f), 4);

            // 세로 화면: 가로 3.2를 맞추려면 세로 절반이 1.6 / 화면비
            float aspect = 1080f / 2340f;
            Assert.Equal(1.6f / aspect, ScreenLayout.OrthographicSize(1080f, 2340f), 3);
        }
    }
}
