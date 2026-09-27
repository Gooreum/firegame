using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 재미 검증용 시험판을 띄운다. 에디터 메뉴(FireGame ▸ 시험판 A/B)로 고르면 본 게임 대신 시험판이 뜬다.
    ///
    /// 시험판은 버릴 각오로 만든 것이라 이 폴더(Prototypes) 밖에는 GameRoot.Boot의 조건 한 줄만 닿는다.
    /// 폴더를 지우고 그 한 줄을 되돌리면 흔적이 남지 않는다.
    /// </summary>
    public static class PrototypeLauncher
    {
        public const string Key = "firegame.prototype";

        /// <summary>에디터에서 시험판을 골랐는지. 빌드에서는 폰 시험판 빌드(FIREGAME_PROTO_BUILD)만 true다.</summary>
        public static bool TakesOver
        {
            get { return Selected != ""; }
        }

        /// <summary>"A", "B", 또는 빈 문자열(본 게임).</summary>
        public static string Selected
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetString(Key, "");
#elif FIREGAME_PROTO_BUILD
                // 폰 시험판 빌드(tools/ios-install.sh)만 이 표시를 붙인다. 본 게임 빌드는 아래처럼 "".
                return "C";
#else
                return "";
#endif
            }
            set
            {
#if UNITY_EDITOR
                if (string.IsNullOrEmpty(value)) UnityEditor.EditorPrefs.DeleteKey(Key);
                else UnityEditor.EditorPrefs.SetString(Key, value);
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!TakesOver) return;
            if (Object.FindAnyObjectByType<PrototypeHost>() != null) return;

            var root = new GameObject("Prototype");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<PrototypeHost>();
        }
    }
}
