using FireGame.UnityLayer;
using UnityEngine;
using UnityEngine.UI;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 시험판을 띄우고 R(다시 시작)·Tab(A↔B 전환)을 처리한다.
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

            _mode = PrototypeLauncher.Selected == "A" ? "A" : "B";
            Launch();
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            ProtoInput input = ProtoInput.Read();

            if (input.Switch)
            {
                _mode = _mode == "A" ? "B" : "A";
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
            return new Placeholder(canvas, "시험판 A — 액션 (준비 중)");
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

        /// <summary>시험판이 아직 없을 때 보이는 제목 화면.</summary>
        private sealed class Placeholder : IPrototype
        {
            private readonly Text _label;

            public Placeholder(Canvas canvas, string title)
            {
                _label = UiKit.OutlinedLabel(canvas.transform, "Title", title + "\nR 다시 시작 · Tab 전환", 48, Color.white, TextAnchor.MiddleCenter);
                UiKit.Stretch(_label.rectTransform);
            }

            public void Tick(float dt, in ProtoInput input)
            {
            }

            public void Destroy()
            {
                UiKit.Discard(_label.gameObject);
            }
        }
    }
}
