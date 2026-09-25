using UnityEditor;

namespace FireGame.Prototypes.EditorTools
{
    /// <summary>에디터 메뉴에서 시험판 또는 본 게임을 골라 바로 Play 모드로 들어간다.</summary>
    public static class PrototypeMenu
    {
        [MenuItem("FireGame/시험판 A (액션)", false, 1)]
        private static void PlayActionPrototype()
        {
            Play("A");
        }

        [MenuItem("FireGame/시험판 B (전략)", false, 2)]
        private static void PlayTacticsPrototype()
        {
            Play("B");
        }

        [MenuItem("FireGame/본 게임", false, 20)]
        private static void PlayMainGame()
        {
            Play("");
        }

        private static void Play(string mode)
        {
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            PrototypeLauncher.Selected = mode;
            EditorApplication.EnterPlaymode();
        }
    }
}
