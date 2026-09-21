using FireGame.Core.Game;
using UnityEngine;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 게임 진입점. 씬에 아무것도 배치하지 않아도 Play를 누르면 스스로 떠오른다.
    /// 카메라와 화면을 코드로 만들고, 매 프레임 GameFlow를 진행시켜 화면에 비춘다.
    /// 게임 규칙은 한 줄도 없다 — 전부 FireGame.Core에 있다.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        private const string SaveKey = "firegame.save";

        /// <summary>이전 버전이 쓰던 키. 진행 상황을 잃지 않게 한 번 읽어 옮긴다.</summary>
        private const string LegacySaveKey = "firegame.save.v1";

        /// <summary>화면 세로 절반에 들어가는 칸 수. 7.5면 세로로 15칸이 보인다.</summary>
        public const float CameraHalfHeight = 7.5f;

        private GameFlow _flow;
        private ScreenDirector _director;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<GameRoot>() != null) return;

            var root = new GameObject("FireGame");
            DontDestroyOnLoad(root);
            root.AddComponent<GameRoot>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Camera camera = SetUpCamera();
            Canvas canvas = UiKit.CreateCanvas(transform, camera, "Canvas", 0);
            UiKit.EnsureEventSystem(transform);

            _flow = new GameFlow(LoadSave()) { SaveWriter = WriteSave };
            _director = new ScreenDirector(transform, camera, canvas, _flow);
        }

        private void Update()
        {
            // 앱이 백그라운드에서 돌아오면 한 프레임이 수 초가 될 수 있다.
            // 그대로 넣으면 불이 한순간에 번지므로 상한을 둔다.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            KeyboardInput.Apply(_flow);
            _flow.Update(dt);
            _director.Sync(dt);
        }

        private static Camera SetUpCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = CameraHalfHeight;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        private static SaveData LoadSave()
        {
            string text = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrEmpty(text)) text = PlayerPrefs.GetString(LegacySaveKey, string.Empty);

            // 깨진 세이브 때문에 게임이 안 켜지면 안 된다. 읽지 못하면 새로 시작한다.
            if (SaveData.TryDeserialize(text, out SaveData save)) return save;

            if (!string.IsNullOrEmpty(text)) Debug.LogWarning("[FireGame] 세이브를 읽지 못해 새 게임으로 시작합니다.");
            return SaveData.NewGame();
        }

        private static void WriteSave(string text)
        {
            PlayerPrefs.SetString(SaveKey, text);
            PlayerPrefs.Save();
        }
    }
}
