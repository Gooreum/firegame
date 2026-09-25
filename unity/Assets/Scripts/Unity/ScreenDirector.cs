using FireGame.Core.Game;
using UnityEngine;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// GameFlow의 상태에 맞춰 화면을 조립한다.
    ///
    /// 게임(GameRoot)과 스크린샷 하네스가 이 클래스 하나로 화면을 만든다.
    /// 둘이 조립 코드를 따로 가지면 캡처가 실제 게임과 달라져 검증이 의미를 잃는다.
    ///
    /// 화면 구성:
    ///   지도      = 지도 (+ 상점 패널)
    ///   브리핑    = 지도 + 브리핑
    ///   플레이    = 현장 + HUD
    ///   결과      = 멈춘 현장 + 결과
    /// </summary>
    public sealed class ScreenDirector
    {
        private readonly Transform _root;
        private readonly Camera _camera;
        private readonly Canvas _canvas;
        private readonly GameFlow _flow;

        private MissionWorldView _world;
        private StageRunner _worldRunner;

        private MapScreen _map;
        private BriefingScreen _briefing;
        private ResultScreen _result;
        private ShopPanel _shop;
        private MissionHud _hud;
        private string _layoutKey;

        public ScreenDirector(Transform root, Camera camera, Canvas canvas, GameFlow flow)
        {
            _root = root;
            _camera = camera;
            _canvas = canvas;
            _flow = flow;
        }

        public MissionHud Hud
        {
            get { return _hud; }
        }

        public MissionWorldView World
        {
            get { return _world; }
        }

        /// <summary>상태가 바뀌었으면 화면을 다시 조립하고, 매 프레임 움직이는 부분을 갱신한다.</summary>
        public void Sync(float dt)
        {
            SyncWorld();
            SyncLayout();

            if (_world != null)
            {
                _world.Refresh(_flow.Elapsed, dt);
                _world.FrameCamera(_camera, _world.PlayerWorld);
            }
            else
            {
                _camera.backgroundColor = MapScreen.Sea;
                GameAudio.SetFireLevel(0f, dt);
            }

            if (_hud != null) _hud.Refresh();
        }

        private void SyncWorld()
        {
            bool showWorld = _flow.Screen == GameScreen.Playing || _flow.Screen == GameScreen.Result;
            StageRunner runner = showWorld ? _flow.Runner : null;
            if (runner == _worldRunner) return;

            if (_world != null) _world.Destroy();
            _world = runner != null ? new MissionWorldView(_root, runner) : null;
            _worldRunner = runner;
        }

        /// <summary>
        /// 화면 구성에 영향을 주는 값이 바뀔 때만 UI를 새로 만든다.
        /// 돈과 별이 포함된 이유: 상점에서 사면 지도·상점 표시가 바뀌어야 하기 때문이다.
        /// </summary>
        private void SyncLayout()
        {
            string key = _flow.Screen + "|" + _flow.ShopOpen + "|" + _flow.Save.Money + "|" + _flow.Save.TotalStars + "|"
                         + (_flow.CurrentMission != null ? _flow.CurrentMission.Id : -1) + "|"
                         + (_flow.Runner != null ? _flow.Runner.GetHashCode() : 0);
            if (key == _layoutKey) return;
            _layoutKey = key;

            DestroyUi();

            switch (_flow.Screen)
            {
                case GameScreen.Map:
                    _map = new MapScreen(_canvas, _flow);
                    if (_flow.ShopOpen) _shop = new ShopPanel(_canvas, _flow);
                    break;

                case GameScreen.Briefing:
                    _map = new MapScreen(_canvas, _flow);
                    _briefing = new BriefingScreen(_canvas, _flow);
                    break;

                case GameScreen.Playing:
                    _hud = new MissionHud(_canvas, _flow);
                    break;

                case GameScreen.Result:
                    _result = new ResultScreen(_canvas, _flow);
                    break;
            }
        }

        private void DestroyUi()
        {
            if (_map != null) _map.Destroy();
            if (_briefing != null) _briefing.Destroy();
            if (_result != null) _result.Destroy();
            if (_shop != null) _shop.Destroy();
            if (_hud != null) _hud.Destroy();

            _map = null;
            _briefing = null;
            _result = null;
            _shop = null;
            _hud = null;
        }
    }
}
