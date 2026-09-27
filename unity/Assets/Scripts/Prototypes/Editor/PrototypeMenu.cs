using UnityEditor;

namespace FireGame.Prototypes.EditorTools
{
    /// <summary>에디터 메뉴에서 시험판 또는 본 게임을 골라 바로 Play 모드로 들어간다.</summary>
    public static class PrototypeMenu
    {
        [MenuItem("FireGame/시험판 C (뱀서)", false, 0)]
        private static void PlaySurvivorPrototype()
        {
            Play("C");
        }

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
            PrototypeLauncher.Selected = mode;
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
                return;
            }

            // Play 중이면 먼저 멈추고, 편집 모드로 완전히 돌아온 뒤에 다시 들어간다(곧바로 Enter를 부르면 무시된다).
            EditorApplication.playModeStateChanged -= ReenterOnceStopped;
            EditorApplication.playModeStateChanged += ReenterOnceStopped;
            EditorApplication.ExitPlaymode();
        }

        private static void ReenterOnceStopped(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= ReenterOnceStopped;
            EditorApplication.EnterPlaymode();
        }
    }
}
