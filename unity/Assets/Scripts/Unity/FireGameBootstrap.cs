using FireGame.Core.Game;
using FireGame.Core.Render;
using UnityEngine;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 게임 진입점. 씬에 아무것도 배치하지 않아도 Play를 누르면 스스로 떠오른다.
    ///
    /// 카메라·스프라이트·입력을 전부 코드로 만든다. 씬이나 프리팹을 손으로 구성하게 하면
    /// 설치 절차가 길어지고 한 단계만 틀려도 검은 화면이 나오는데, 원인을 찾기 어렵다.
    /// 게임 로직은 한 줄도 없다 — 전부 FireGame.Core의 GameFlow가 한다.
    /// </summary>
    public sealed class FireGameBootstrap : MonoBehaviour
    {
        private const string SaveKey = "firegame.save.v1";

        private GameFlow _flow;
        private FrameBuffer _buffer;
        private FrameBufferPresenter _presenter;
        private TouchInput _input;
        private Camera _camera;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<FireGameBootstrap>() != null) return;

            var root = new GameObject("FireGame");
            DontDestroyOnLoad(root);
            root.AddComponent<FireGameBootstrap>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _camera = SetUpCamera();

            var spriteObject = new GameObject("Screen");
            spriteObject.transform.SetParent(transform, false);
            var spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();

            _buffer = new FrameBuffer();
            _presenter = new FrameBufferPresenter(spriteRenderer);
            _input = new TouchInput();

            _flow = new GameFlow(LoadSave());
            _flow.SaveWriter = WriteSave;

            FitCamera();
        }

        private void Update()
        {
            if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            {
                FitCamera();
            }

            _input.Poll(_flow);

            // 앱이 백그라운드에서 돌아올 때 한 프레임이 수 초가 될 수 있다.
            // 그대로 넣으면 불이 한순간에 번지므로 상한을 둔다.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _flow.Update(dt);

            _flow.Render(_buffer);
            _presenter.Present(_buffer);
        }

        private Camera SetUpCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        /// <summary>320x200을 비율 유지로 꽉 채운다. 남는 쪽은 검은 띠가 된다.</summary>
        private void FitCamera()
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            if (_lastScreenWidth <= 0 || _lastScreenHeight <= 0) return;

            _camera.orthographicSize = ScreenLayout.OrthographicSize(_lastScreenWidth, _lastScreenHeight);
        }

        private static SaveData LoadSave()
        {
            string text = PlayerPrefs.GetString(SaveKey, string.Empty);

            // 깨진 세이브 때문에 게임이 안 켜지면 안 된다. 읽지 못하면 새로 시작한다.
            if (SaveData.TryDeserialize(text, out SaveData save)) return save;

            if (!string.IsNullOrEmpty(text))
            {
                Debug.LogWarning("[FireGame] 세이브를 읽지 못해 새 게임으로 시작합니다.");
            }

            return SaveData.NewGame();
        }

        private static void WriteSave(string text)
        {
            PlayerPrefs.SetString(SaveKey, text);
            PlayerPrefs.Save();
        }
    }
}
