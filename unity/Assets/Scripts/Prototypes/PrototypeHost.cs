using FireGame.UnityLayer;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 시험판을 띄우고 R(다시 시작)·Tab(A→B→C 전환)을 처리한다.
    /// 카메라·캔버스는 본 게임과 같은 방식으로 코드로 만든다.
    /// </summary>
    public sealed class PrototypeHost : MonoBehaviour
    {
        private Camera _camera;
        private Canvas _canvas;
        private IPrototype _current;
        private string _mode;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _camera = SetUpCamera();
            _canvas = UiKit.CreateCanvas(transform, _camera, "Canvas", 0);
            UiKit.EnsureEventSystem(transform);

            string selected = PrototypeLauncher.Selected;
            _mode = selected == "A" || selected == "B" || selected == "C+" ? selected : "C";
            Launch();
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            ProtoInput input = ProtoInput.Read();

            if (input.Switch)
            {
                _mode = _mode == "A" ? "B" : _mode == "B" ? "C" : "A";
                PrototypeLauncher.Selected = _mode;
                Launch();
                return;
            }

            if (input.Restart)
            {
                Launch();
                return;
            }

            _current.Tick(dt, input);
        }

        private void Launch()
        {
            if (_current != null) _current.Destroy();
            _current = Create(_mode, transform, _camera, _canvas);
        }

        /// <summary>시험판 하나를 만든다. 스크린샷 하니스도 이 함수로 만든다.</summary>
        public static IPrototype Create(string mode, Transform parent, Camera camera, Canvas canvas)
        {
            if (mode == "B") return new TacticsView(parent, camera, canvas);
            if (mode == "C") return new SurvivorView(parent, camera, canvas);
            if (mode == "C+") return new SurvivorView(parent, camera, canvas, true);
            return new ActionView(parent, camera, canvas);
        }

        public static Camera SetUpCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = cameraObject.AddComponent<Camera>();
            }
            if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();

            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }
    }
}
