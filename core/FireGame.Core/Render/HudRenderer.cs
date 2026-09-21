using System.Globalization;
using FireGame.Core.Data;
using FireGame.Core.Game;

namespace FireGame.Core.Render
{
    /// <summary>
    /// 화면 아래 24픽셀의 상태 표시줄과 전체 화면 상점을 그린다.
    ///
    /// Unity UI를 쓰지 않고 같은 프레임버퍼에 직접 그린다.
    /// 캔버스도 폰트 에셋도 레이아웃 계산도 없이 픽셀 위치만 정하면 끝이라,
    /// UI 때문에 드로우콜이 늘지 않는다.
    /// </summary>
    public static class HudRenderer
    {
        private const int HudTop = FrameBuffer.PlayfieldHeight;   // 176
        private const int LineOne = HudTop + 2;                   // 178
        private const int LineTwo = HudTop + 13;                  // 189

        private const int BarX = 96;
        private const int BarWidth = 56;
        private const int BarHeight = 6;

        private const int SlotWidth = 104;

        /// <summary>상점에 진열되는 장비 수.</summary>
        public static int ShopEntryCount
        {
            get { return EquipmentCatalog.All.Length; }
        }

        public static void DrawHud(FrameBuffer buffer, StageRunner runner, int money)
        {
            if (buffer == null || runner == null) return;

            buffer.FillRect(0, HudTop, FrameBuffer.Width, FrameBuffer.HudHeight, Palette.Black);
            buffer.FillRect(0, HudTop, FrameBuffer.Width, 1, Palette.DarkGray);

            // 1행: 돈 / 체력 / 남은 시간 / 남은 불 / 구조 현황
            buffer.DrawText(2, LineOne, "$" + Pad(money, 5), Palette.Yellow);

            buffer.DrawText(BarX - 26, LineOne, "HP", Palette.LightGray);
            DrawBar(buffer, BarX, LineOne + 1, runner.Player.Hp / GameConfig.PlayerMaxHp);

            buffer.DrawText(BarX + BarWidth + 8, LineOne, "T" + Pad((int)runner.TimeLeft, 3), Palette.LightCyan);
            buffer.DrawText(BarX + BarWidth + 48, LineOne, "F" + Pad(runner.Grid.CountBurning(), 3), Palette.LightRed);
            buffer.DrawText(
                BarX + BarWidth + 88,
                LineOne,
                "S" + runner.RescuedCount.ToString(CultureInfo.InvariantCulture)
                    + "/" + runner.Civilians.Count.ToString(CultureInfo.InvariantCulture),
                Palette.LightGreen);

            DrawSlots(buffer, runner);
        }

        private static void DrawBar(FrameBuffer buffer, int x, int y, float ratio)
        {
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            buffer.FillRect(x - 1, y - 1, BarWidth + 2, BarHeight + 2, Palette.DarkGray);
            buffer.FillRect(x, y, BarWidth, BarHeight, Palette.Black);

            int filled = (int)(BarWidth * ratio);
            if (filled <= 0) return;

            // 위험할수록 눈에 띄는 색으로 바뀐다. 숫자를 읽지 않아도 상태가 보여야 한다.
            byte color = ratio > 0.5f ? Palette.LightGreen : ratio > 0.25f ? Palette.Yellow : Palette.LightRed;
            buffer.FillRect(x, y, filled, BarHeight, color);
        }

        private static void DrawSlots(FrameBuffer buffer, StageRunner runner)
        {
            for (int slot = 0; slot < PlayerState.SlotCount; slot++)
            {
                int x = 4 + (slot * SlotWidth);
                EquipmentDef def = EquipmentCatalog.ById(runner.Player.Slots[slot]);

                bool active = slot == runner.Player.ActiveSlot;
                byte foreground = def == null ? Palette.DarkGray : active ? Palette.Black : Palette.LightGray;
                int background = active && def != null ? Palette.White : -1;

                string label = (slot + 1).ToString(CultureInfo.InvariantCulture)
                               + " "
                               + (def == null ? "------" : def.Name);

                buffer.DrawText(x, LineTwo, label, foreground, background);

                if (def == null) continue;

                // 충전량 장비만 남은 횟수를 보여준다. 쿨다운 장비는 총량 제한이 없다.
                if (def.Resource == ResourceKind.Charges)
                {
                    // 가장 긴 이름("CO2 EXT")이 9칸이므로 그보다 뒤에서 시작해야
                    // 충전량 숫자가 이름에 달라붙지 않는다.
                    buffer.DrawText(
                        x + 80,
                        LineTwo,
                        Pad(runner.Player.Charges[slot], 2),
                        runner.Player.Charges[slot] > 0 ? Palette.Yellow : Palette.Red);
                }
            }
        }

        /// <summary>상점. 전체 화면을 덮으므로 이전 장면이 비치지 않는다.</summary>
        public static void DrawShop(FrameBuffer buffer, SaveData save, int cursor)
        {
            if (buffer == null || save == null) return;

            buffer.Clear(Palette.Blue);
            buffer.FillRect(8, 8, FrameBuffer.Width - 16, FrameBuffer.Height - 16, Palette.Black);

            buffer.DrawText(24, 20, "FIRE STATION SHOP", Palette.Yellow);
            buffer.DrawText(24, 38, "CASH $" + Pad(save.Money, 6), Palette.LightGreen);

            for (int i = 0; i < EquipmentCatalog.All.Length; i++)
            {
                EquipmentDef def = EquipmentCatalog.All[i];
                int y = 64 + (i * 20);

                bool owned = save.Owns(def.Id);
                bool affordable = !owned && save.Money >= def.Price;

                byte color = owned ? Palette.DarkGray : affordable ? Palette.White : Palette.Red;

                // 커서는 지금 고른 줄에만 표시한다.
                // 강조 막대를 어두운 회색으로 두면 "보유 중" 항목 글자색과 같아져
                // 화면에서 둘을 구분할 수 없다. 파란 막대로 분리한다.
                if (i == cursor)
                {
                    buffer.FillRect(16, y - 2, FrameBuffer.Width - 32, 12, Palette.Blue);
                    buffer.DrawText(24, y, ">", Palette.Yellow);
                }

                buffer.DrawText(40, y, def.Name, color);
                buffer.DrawText(160, y, "$" + Pad(def.Price, 5), color);
                buffer.DrawText(240, y, owned ? "OWNED" : affordable ? "BUY" : "NEED$", color);
            }

            buffer.DrawText(24, 168, "TAP ITEM TO BUY", Palette.LightCyan);
        }

        /// <summary>자리수를 고정해 숫자가 흔들리지 않게 한다. 넘치면 자르지 않고 그대로 둔다.</summary>
        private static string Pad(int value, int digits)
        {
            if (value < 0) value = 0;

            string text = value.ToString(CultureInfo.InvariantCulture);
            if (text.Length >= digits) return text;

            return new string('0', digits - text.Length) + text;
        }
    }
}
