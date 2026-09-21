namespace FireGame.Core.Render
{
    /// <summary>
    /// 화면 요소의 위치. 그리는 쪽(HudRenderer)과 터치를 판정하는 쪽(GameFlow)이
    /// 같은 숫자를 쓰게 하려고 한 곳에 모았다. 따로 두면 보이는 버튼과
    /// 눌리는 위치가 조금씩 어긋나는 버그가 생긴다.
    /// 모든 좌표는 320x200 프레임버퍼 기준이다.
    /// </summary>
    public static class ScreenLayout
    {
        // ---- 공통 ----
        public const int RowHeight = 20;

        /// <summary>줄 하나를 탭으로 인정하는 세로 범위(글자 위아래 여유 포함).</summary>
        public const int RowHitHeight = 16;

        // ---- 허브 ----
        public const int HubStageTop = 56;
        public const int HubShopButtonX = 24;
        public const int HubShopButtonY = 140;
        public const int HubShopButtonWidth = 96;
        public const int HubShopButtonHeight = 16;

        // ---- 상점 ----
        public const int ShopRowTop = 64;
        public const int ShopBackButtonX = 232;
        public const int ShopBackButtonY = 164;
        public const int ShopBackButtonWidth = 64;
        public const int ShopBackButtonHeight = 16;

        // ---- 플레이 ----
        /// <summary>이 x보다 왼쪽을 누르면 조이스틱, 오른쪽을 누르면 발사.</summary>
        public const int PlaySplitX = FrameBuffer.Width / 2;

        public const int HudSlotLeft = 4;
        public const int HudSlotWidth = 104;

        /// <summary>조이스틱을 끝까지 민 것으로 보는 드래그 거리(픽셀).</summary>
        public const float JoystickRadius = 16f;

        public static int HubStageRowY(int index)
        {
            return HubStageTop + (index * RowHeight);
        }

        public static int ShopRowY(int index)
        {
            return ShopRowTop + (index * RowHeight);
        }

        /// <summary>y가 몇 번째 줄에 해당하는지. 어느 줄도 아니면 -1.</summary>
        public static int RowAt(float y, int top, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int rowY = top + (i * RowHeight) - 4;
                if (y >= rowY && y < rowY + RowHitHeight) return i;
            }

            return -1;
        }

        public static bool Inside(float x, float y, int left, int top, int width, int height)
        {
            return x >= left && x < left + width && y >= top && y < top + height;
        }

        /// <summary>
        /// 320x200 화면을 기기 화면에 비율 유지로 꽉 채웠을 때의 배율.
        /// 남는 쪽은 검은 띠(레터박스)가 된다.
        /// </summary>
        public static float FitScale(float screenWidth, float screenHeight)
        {
            float scaleX = screenWidth / FrameBuffer.Width;
            float scaleY = screenHeight / FrameBuffer.Height;
            return scaleX < scaleY ? scaleX : scaleY;
        }

        /// <summary>
        /// 기기 화면 좌표(원점 왼쪽 아래, Unity 방식)를 프레임버퍼 좌표(원점 왼쪽 위)로 바꾼다.
        /// 레터박스 띠를 누르면 버퍼 밖 좌표가 나오는데, 그대로 돌려주고 판정 쪽에서 무시한다.
        /// </summary>
        public static void ScreenToFrameBuffer(
            float screenX, float screenY, float screenWidth, float screenHeight,
            out float bufferX, out float bufferY)
        {
            float scale = FitScale(screenWidth, screenHeight);
            float offsetX = (screenWidth - (FrameBuffer.Width * scale)) * 0.5f;
            float offsetY = (screenHeight - (FrameBuffer.Height * scale)) * 0.5f;

            bufferX = (screenX - offsetX) / scale;
            bufferY = FrameBuffer.Height - ((screenY - offsetY) / scale);
        }

        /// <summary>
        /// 같은 레터박스가 되도록 하는 정사영 카메라 크기(화면 세로의 절반, 월드 단위).
        /// 스프라이트는 100 PPU 기준 3.2 x 2.0 월드 단위다.
        /// </summary>
        public static float OrthographicSize(float screenWidth, float screenHeight)
        {
            const float spriteHalfHeight = FrameBuffer.Height / 200f;
            const float spriteAspect = (float)FrameBuffer.Width / FrameBuffer.Height;

            float screenAspect = screenWidth / screenHeight;

            // 화면이 더 넓으면 세로를 맞추고, 더 좁으면 가로를 맞춘다.
            return screenAspect >= spriteAspect
                ? spriteHalfHeight
                : spriteHalfHeight * (spriteAspect / screenAspect);
        }

        /// <summary>HUD 슬롯 줄의 x가 몇 번째 슬롯인지. 범위 밖이면 -1.</summary>
        public static int SlotAt(float x, int slotCount)
        {
            if (x < HudSlotLeft) return -1;

            int slot = (int)((x - HudSlotLeft) / HudSlotWidth);
            return slot < slotCount ? slot : -1;
        }
    }
}
