using System.Collections.Generic;
using System.IO;
using FireGame.Core.Data;
using FireGame.Core.Game;
using FireGame.Core.Render;
using Xunit;

namespace FireGame.Core.Tests
{
    /// <summary>
    /// 렌더 결과를 PPM 이미지로 떨궈 눈으로 확인하기 위한 도구.
    /// Unity 없이도 화면이 어떻게 보이는지 검증할 수 있는 유일한 경로다.
    /// </summary>
    public class ScreenshotProbe
    {
        private static string OutputDirectory
        {
            get
            {
                string fromEnv = System.Environment.GetEnvironmentVariable("FIREGAME_SHOT_DIR");
                return string.IsNullOrEmpty(fromEnv) ? Path.GetTempPath() : fromEnv;
            }
        }

        [Fact]
        public void DumpEachStageToPpm()
        {
            foreach (StageDef stage in StageCatalog.All)
            {
                var runner = new StageRunner(stage, new List<int>
                {
                    EquipmentId.Bucket, EquipmentId.Extinguisher, EquipmentId.FoamExtinguisher,
                });

                // 불이 어느 정도 번진 뒤의 장면을 찍는다.
                var bot = new GreedyBot(runner);
                for (int i = 0; i < 120 && !runner.IsOver; i++)
                {
                    runner.Update(0.05f, default);
                }

                var buffer = new FrameBuffer();
                SceneRenderer.Render(buffer, runner, 0);
                HudRenderer.DrawHud(buffer, runner, 1250 + (stage.Id * 3000));

                WritePpm(buffer, Path.Combine(OutputDirectory, "stage" + stage.Id + ".ppm"));
                Assert.True(bot != null);
            }
        }

        [Fact]
        public void DumpShopScreenToPpm()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 3800;
            Shop.Buy(save, EquipmentId.Extinguisher);

            var buffer = new FrameBuffer();
            HudRenderer.DrawShop(buffer, save, 2);

            WritePpm(buffer, Path.Combine(OutputDirectory, "shop.ppm"));
        }

        [Fact]
        public void DumpHubAndResultScreensToPpm()
        {
            SaveData save = SaveData.NewGame();
            save.Money = 913;
            save.RecordClear(0);

            var hub = new FrameBuffer();
            HudRenderer.DrawHub(hub, save);
            WritePpm(hub, Path.Combine(OutputDirectory, "hub.ppm"));

            var runner = new StageRunner(StageCatalog.Residential, new List<int> { EquipmentId.Bucket });
            new GreedyBot(runner).Play();

            var result = new FrameBuffer();
            HudRenderer.DrawResult(
                result, StageCatalog.Residential.Name, runner.Outcome, Economy.Breakdown(runner.BuildResult()));
            WritePpm(result, Path.Combine(OutputDirectory, "result.ppm"));
        }

        private static void WritePpm(FrameBuffer buffer, string path)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                byte[] header = System.Text.Encoding.ASCII.GetBytes(
                    "P6\n" + FrameBuffer.Width + " " + FrameBuffer.Height + "\n255\n");
                writer.Write(header);

                foreach (byte index in buffer.Pixels)
                {
                    writer.Write(Palette.R(index));
                    writer.Write(Palette.G(index));
                    writer.Write(Palette.B(index));
                }
            }
        }
    }
}
