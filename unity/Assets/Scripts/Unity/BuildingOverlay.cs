using System.Collections.Generic;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using UnityEngine;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 건물을 지붕으로 덮는다.
    ///
    /// 지붕이 불(20~22)·연기(30)보다 위에 그려지므로, 밖에서는 실내가 저절로 가려진다.
    /// 따로 불을 끄는 코드가 없다 — 그리는 순서 하나로 "들어가야 안다"가 성립한다.
    /// 소방관이 들어간 건물의 지붕만 걷힌다.
    /// </summary>
    public sealed class BuildingOverlay
    {
        /// <summary>지붕. 불꽃·연기보다 위라서 밖에서는 실내가 보이지 않는다.</summary>
        public const int OrderRoof = 40;

        /// <summary>
        /// 벽이 화면에서 위로 서는 길이(칸). 지붕은 이만큼 위로 올라가고 그 아래가 앞벽이 된다.
        ///
        /// 크게 잡을수록 건물이 웅장해지지만 <b>건물 뒤 격자 줄을 그만큼 더 가린다</b> —
        /// 상가 아래 블록은 하필 소방관 스폰이 있는 줄(10행) 바로 위에 있어서,
        /// 1.1칸이면 11행 한 줄만 먹고 스폰은 건드리지 않는다.
        /// 카메라 여백도 이 값을 보고 잡으므로 여기 하나만 고치면 화면 전체가 따라온다.
        /// </summary>
        public const float WallRise = 1.1f;

        /// <summary>처마 쪽 밝기. 이만큼 떨어뜨렸다가 용마루까지 밝혀 올린다.</summary>
        private const float EaveShade = 0.58f;

        /// <summary>
        /// 지붕면 밝기. 처마에서 여기까지 올라온다.
        /// <b>1을 넘기면 안 된다</b> — 재질 결의 밝은 줄이 이미 1.0이라 곱하는 순간 흰색으로 잘려
        /// 지붕 한가운데가 하얗게 뜬다(캡처로 확인).
        /// </summary>
        private const float RoofFace = 0.92f;

        /// <summary>
        /// 이만큼 안쪽부터는 더 밝아지지 않는다.
        /// 4단계로 뒀더니 34x12짜리 주택 지붕에 동심원 띠가 네 겹 생겨 과녁처럼 보였다(캡처로 확인).
        /// </summary>
        private const int RidgeDepth = 3;

        /// <summary>용마루 한 줄만 기준 색 그대로. 지붕이 어느 쪽으로 흐르는지를 이 선 하나가 말한다.</summary>
        private const float RidgeLine = 1f;

        /// <summary>처마 그림자. 지붕 바로 밑이라 건물이 땅 위에 놓인 것으로 보인다.</summary>
        private const int OrderEaves = OrderRoof - 1;

        /// <summary>지붕 위에 얹는 부속물. 지붕과 표시 사이.</summary>
        private const int OrderFixture = OrderRoof + 1;

        /// <summary>지붕 위 표시. 지붕보다 위에 그려야 보인다.</summary>
        private const int OrderRoofMark = OrderRoof + 2;

        /// <summary>한 건물에 세우는 불꽃 수. 더 많이 세우면 지붕이 불꽃으로 뒤덮인다.</summary>
        private const int MaxFireSigns = 3;

        private readonly Transform _root;
        private readonly StageRunner _runner;
        private readonly FireGrid _grid;
        private readonly BuildingMap _buildings;
        private readonly MissionWorldView _view;

        /// <summary>건물마다 그 건물을 덮는 지붕 칸들.</summary>
        private readonly List<SpriteRenderer>[] _roofs;

        /// <summary>
        /// 건물마다 처마 그림자·지붕 부속물·입구 간판.
        /// 지붕과 함께 켜고 꺼야 한다 — 들어간 건물에 굴뚝만 떠 있으면 안 된다.
        /// </summary>
        private readonly List<Renderer>[] _dressing;

        /// <summary>셀별 지붕 타일과 원래 색. 타는 칸의 지붕을 그을리는 데 쓴다.</summary>
        private readonly SpriteRenderer[] _roofByCell;
        private readonly Color[] _roofBaseColor;

        /// <summary>건물마다 지붕 위에 세우는 불꽃과 연기. 건물당 <see cref="MaxFireSigns"/>개.</summary>
        private readonly SpriteRenderer[,] _fireSigns;
        private readonly SpriteRenderer[,] _fireSmoke;

        private readonly Sprite _tongueSprite;
        private readonly Sprite _smokeSprite;

        /// <summary>타는 칸의 지붕 색. 지붕이 타 내려앉은 것처럼 보이게 한다.</summary>
        private static readonly Color CharredRoof = new Color(0.13f, 0.11f, 0.11f);

        /// <summary>시민 옆 칸에 불이 붙었을 때 말풍선이 오가는 두 색. 흰색을 거치지 않는다.</summary>
        private static readonly Color HelpUrgentDim = new Color(1f, 0.72f, 0.70f);
        private static readonly Color HelpUrgentHot = new Color(1f, 0.30f, 0.26f);

        /// <summary>건물마다 "Help!" 말풍선. 몸통·꼬리·글자 세 조각이다.</summary>
        private readonly SpriteRenderer[] _helpPanel;
        private readonly SpriteRenderer[] _helpTail;
        private readonly TextMesh[] _helpText;

        /// <summary>불이 났거나 사람이 남은 건물의 입구에 얹는 맥동.</summary>
        private readonly List<SpriteRenderer>[] _doorMarks;
        private readonly List<GridPoint> _burningSample = new List<GridPoint>(MaxFireSigns);

        public BuildingOverlay(Transform parent, StageRunner runner, BuildingMap buildings, MissionWorldView view)
        {
            _runner = runner;
            _grid = runner.Grid;
            _buildings = buildings;
            _view = view;

            _root = new GameObject("BuildingOverlay").transform;
            _root.SetParent(parent, false);

            _tongueSprite = Art.Get("Effects/flame_05");
            _smokeSprite = Art.Get("Effects/smoke_03");

            _roofByCell = new SpriteRenderer[_grid.Count];
            _roofBaseColor = new Color[_grid.Count];

            _roofs = new List<SpriteRenderer>[buildings.All.Length];
            _dressing = new List<Renderer>[buildings.All.Length];
            for (int i = 0; i < buildings.All.Length; i++)
            {
                _dressing[i] = new List<Renderer>();
                _roofs[i] = BuildRoof(buildings.All[i]);
                BuildDressing(buildings.All[i]);
            }

            _fireSigns = new SpriteRenderer[buildings.All.Length, MaxFireSigns];
            _fireSmoke = new SpriteRenderer[buildings.All.Length, MaxFireSigns];

            _helpPanel = new SpriteRenderer[buildings.All.Length];
            _helpTail = new SpriteRenderer[buildings.All.Length];
            _helpText = new TextMesh[buildings.All.Length];

            _doorMarks = new List<SpriteRenderer>[buildings.All.Length];
            for (int i = 0; i < buildings.All.Length; i++) _doorMarks[i] = BuildDoorMarks(buildings.All[i]);
        }

        /// <summary>
        /// 소방관이 들어간 건물의 지붕을 걷고, 덮인 건물의 상태를 지붕 위에 알린다.
        /// </summary>
        /// <param name="time">누적 시간(애니메이션 위상).</param>
        /// <param name="openBuildingId">지붕을 걷을 건물. 밖이면 <see cref="BuildingMap.None"/>.</param>
        public void Refresh(float time, int openBuildingId)
        {
            for (int i = 0; i < _roofs.Length; i++)
            {
                bool covered = i != openBuildingId;
                List<SpriteRenderer> roof = _roofs[i];
                for (int k = 0; k < roof.Count; k++) roof[k].enabled = covered;

                List<Renderer> dressing = _dressing[i];
                for (int k = 0; k < dressing.Count; k++) dressing[k].enabled = covered;

                // 들어간 건물은 진짜 불이 그대로 보인다. 지붕 위 표시를 겹쳐 그릴 이유가 없다.
                if (covered)
                {
                    Building building = _buildings.All[i];
                    CharRoof(building);
                    RefreshFireSigns(building, time);
                    int waiting = RefreshHelpSign(building, time);
                    RefreshDoorMarks(building, time, waiting > 0);
                }
                else
                {
                    HideFireSigns(i, 0);
                    HideHelpSign(i);
                    HideDoorMarks(i);
                }
            }
        }

        /// <summary>
        /// 타는 칸을 최대 세 곳 골라 그 자리 지붕 위에 불꽃을 세운다.
        /// 건물 가운데 한 곳에 몰아 찍으면 "탄다"만 알리지만, 타는 자리에 세우면
        /// 어느 쪽이 번지고 있는지까지 밖에서 읽힌다.
        /// </summary>
        private void RefreshFireSigns(Building building, float time)
        {
            int burning = SampleBurning(building);
            if (burning == 0)
            {
                HideFireSigns(building.Id, 0);
                return;
            }

            FirePalette palette = FireLook.Palette(building.DominantFireClass(_grid));

            // 많이 탈수록 크게. 맵 전체가 보이는 줌에서는 한 칸이 48px뿐이라
            // 한 칸 크기로 세우면 밖에서 보이지 않는다(캡처로 확인).
            float size = Mathf.Min(3.2f, 1.8f + (building.BurningCells(_grid) * 0.08f));

            for (int i = 0; i < _burningSample.Count; i++)
            {
                GridPoint at = _burningSample[i];
                Vector3 cell = _view.CellCenter(at.X, at.Y);
                float sway = Mathf.Sin((time * 6f) + (i * 1.7f));

                SpriteRenderer flame = EnsureSign(_fireSigns, building.Id, i, _tongueSprite, OrderRoofMark + 1);
                flame.color = palette.TongueHot;
                flame.transform.position = cell + new Vector3(sway * 0.1f, (size * 0.35f) + (sway * 0.15f), 0f);
                flame.transform.localScale = Vector3.one * (size / _tongueSprite.bounds.size.y);
                flame.enabled = true;

                SpriteRenderer smoke = EnsureSign(_fireSmoke, building.Id, i, _smokeSprite, OrderRoofMark + 2);
                Color tint = palette.Smoke;
                smoke.color = new Color(tint.r, tint.g, tint.b, 0.55f);
                smoke.transform.position = cell + new Vector3(sway * 0.25f, (size * 0.95f) + (sway * 0.2f), 0f);
                smoke.transform.localScale = Vector3.one * Art.FitWidth(_smokeSprite, size * 1.1f);
                smoke.enabled = true;
            }

            HideFireSigns(building.Id, _burningSample.Count);
        }

        /// <summary>
        /// 구조를 기다리는 사람이 남은 건물 위에 "Help!" 말풍선을 띄운다.
        ///
        /// 지붕을 덮으면 시민의 머리 위 표식도 함께 가려진다. 밖에서 "저 건물에 사람이 있다"를
        /// 알릴 방법이 사라지므로, 건물 단위로 다시 알린다.
        /// </summary>
        /// <returns>그 건물에서 아직 구조를 기다리는 사람 수.</returns>
        private int RefreshHelpSign(Building building, float time)
        {
            int waiting = 0;

            foreach (Civilian civilian in _runner.Civilians)
            {
                // 이미 업은 사람은 그 건물에 남은 게 아니다.
                if (!civilian.Pending || civilian.Carried) continue;
                if (_buildings.At((int)civilian.X, (int)civilian.Y) != building.Id) continue;

                waiting++;
            }

            // "사람이 남았는데 그 건물이 타고 있다"가 붉은 말풍선이다.
            //
            // 처음엔 시민 바로 옆 칸이 타는지로 잡았는데, 그러면 거의 켜지지 않는다.
            // 바닥은 타지 않는 재질이고 시민은 늘 트인 바닥에 서 있어서, 불이 두 칸 앞까지
            // 다가오는 순간이 2초쯤밖에 안 된다. 밖에서 "어디부터 갈까"를 고르는 표시인데
            // 고를 시간에 켜져 있지 않으면 쓸모가 없다.
            bool urgent = waiting > 0 && building.BurningCells(_grid) > 0;

            if (waiting == 0)
            {
                HideHelpSign(building.Id);
                return 0;
            }

            EnsureHelpSign(building.Id);

            // 건물 위 허공이 아니라 지붕 윗변 바로 안쪽에 얹는다.
            // 허공에 띄웠더니 맵 위쪽 건물의 말풍선이 HUD 상단 바에 가려졌다(캡처로 확인).
            float bob = Mathf.Sin(time * 3.2f) * 0.12f;
            var at = new Vector3(
                (building.MinX + building.MaxX + 1) * 0.5f,
                _grid.Height - building.MinY - 1f + bob,
                0f);

            // 시민 옆 칸에 불이 붙었으면 붉게 깜빡인다. 어느 건물부터 가야 하는지가 갈린다.
            // 흰색까지 갔다 오면 깜빡임의 절반이 평소와 같아 보여서, 옅은 붉은색과 진한 붉은색 사이만 오간다.
            Color back = urgent
                ? Color.Lerp(HelpUrgentDim, HelpUrgentHot, Mathf.Abs(Mathf.Sin(time * 8f)))
                : Color.white;

            SpriteRenderer panel = _helpPanel[building.Id];
            panel.color = back;
            panel.transform.position = at;
            panel.transform.localScale = new Vector3(Art.FitWidth(panel.sprite, 3.4f), Art.FitWidth(panel.sprite, 1.7f), 1f);
            panel.enabled = true;

            SpriteRenderer tail = _helpTail[building.Id];
            tail.color = back;
            tail.transform.position = at + new Vector3(0f, -0.85f, 0f);
            tail.enabled = true;

            TextMesh text = _helpText[building.Id];
            text.transform.position = at + new Vector3(0f, 0.05f, 0f);
            text.color = urgent ? new Color(0.65f, 0.05f, 0.05f) : new Color(0.75f, 0.13f, 0.1f);
            text.gameObject.SetActive(true);

            return waiting;
        }

        /// <summary>
        /// 들어갈 문을 알린다. 지붕으로 덮어 놓은 이상 문이 안 보이면 들어갈 길이 없다.
        /// 조용한 건물까지 깜빡이면 눈이 어지러워, 불이 났거나 사람이 남은 건물만 켠다.
        /// </summary>
        private void RefreshDoorMarks(Building building, float time, bool anyoneWaiting)
        {
            bool wanted = anyoneWaiting || building.BurningCells(_grid) > 0;
            List<SpriteRenderer> marks = _doorMarks[building.Id];

            if (!wanted)
            {
                for (int i = 0; i < marks.Count; i++) marks[i].enabled = false;
                return;
            }

            float pulse = Mathf.Abs(Mathf.Sin(time * 3f));
            var tint = new Color(1f, 0.95f, 0.5f, 0.25f + (0.25f * pulse));
            for (int i = 0; i < marks.Count; i++)
            {
                marks[i].color = tint;
                marks[i].enabled = true;
            }
        }

        private void EnsureHelpSign(int buildingId)
        {
            if (_helpPanel[buildingId] != null) return;

            var panel = new GameObject("HelpPanel").AddComponent<SpriteRenderer>();
            panel.transform.SetParent(_root, false);
            panel.sprite = Art.Get("UI/panel_grey");
            panel.sortingOrder = OrderRoofMark + 3;
            _helpPanel[buildingId] = panel;

            // 꼬리는 흰 사각형을 45도 돌려 만든다. 말풍선 전용 그림이 따로 없다.
            var tail = new GameObject("HelpTail").AddComponent<SpriteRenderer>();
            tail.transform.SetParent(_root, false);
            tail.sprite = Art.White;
            tail.sortingOrder = OrderRoofMark + 3;
            tail.transform.localScale = Vector3.one * 0.55f;
            tail.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            _helpTail[buildingId] = tail;

            var go = new GameObject("HelpText");
            go.transform.SetParent(_root, false);
            var text = go.AddComponent<TextMesh>();
            text.font = Art.Font;
            text.text = "Help!";
            text.fontSize = 64;
            text.characterSize = 0.13f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            go.GetComponent<MeshRenderer>().sharedMaterial = Art.Font.material;
            go.GetComponent<MeshRenderer>().sortingOrder = OrderRoofMark + 4;
            _helpText[buildingId] = text;
        }

        private void HideHelpSign(int buildingId)
        {
            if (_helpPanel[buildingId] != null) _helpPanel[buildingId].enabled = false;
            if (_helpTail[buildingId] != null) _helpTail[buildingId].enabled = false;
            if (_helpText[buildingId] != null) _helpText[buildingId].gameObject.SetActive(false);
        }

        private void HideDoorMarks(int buildingId)
        {
            List<SpriteRenderer> marks = _doorMarks[buildingId];
            for (int i = 0; i < marks.Count; i++) marks[i].enabled = false;
        }

        private List<SpriteRenderer> BuildDoorMarks(Building building)
        {
            var marks = new List<SpriteRenderer>(building.Entrances.Length);
            foreach (GridPoint door in building.Entrances)
            {
                var mark = new GameObject("DoorMark").AddComponent<SpriteRenderer>();
                mark.transform.SetParent(_root, false);
                mark.sprite = Art.White;
                mark.sortingOrder = OrderRoof + 1;
                mark.transform.position = _view.CellCenter(door.X, door.Y);
                mark.transform.localScale = Vector3.one;
                mark.enabled = false;
                marks.Add(mark);
            }
            return marks;
        }

        /// <summary>
        /// 타는 칸의 지붕을 검게 칠한다. 지붕이 타 내려앉은 것처럼 보이고,
        /// 그 위에 세운 불꽃이 어떤 지붕색 위에서도 또렷해진다.
        ///
        /// 처음엔 불꽃 밑에 검은 원판을 깔았는데, 둥근 후광 그림이라
        /// 지붕 밖 잔디까지 번져 얼룩처럼 보였다(캡처로 확인). 칸을 칠하면 건물 안에서 멈춘다.
        /// </summary>
        private void CharRoof(Building building)
        {
            foreach (GridPoint cell in building.Cells)
            {
                int index = _grid.Index(cell.X, cell.Y);
                SpriteRenderer tile = _roofByCell[index];
                if (tile == null) continue;

                // 완전히 새까맣게 하면 지붕에 구멍이 난 것처럼 보인다.
                // 원래 지붕색을 조금 남겨 "저 건물의 탄 지붕"으로 읽히게 한다.
                tile.color = _grid[cell.X, cell.Y].State == CellState.Burning
                    ? Color.Lerp(_roofBaseColor[index], CharredRoof, 0.86f)
                    : _roofBaseColor[index];
            }
        }

        /// <summary>
        /// 타는 칸을 고르게 <see cref="MaxFireSigns"/>곳까지 뽑는다.
        /// 앞에서부터 세 칸을 집으면 불꽃이 한쪽 구석에만 몰린다.
        /// </summary>
        private int SampleBurning(Building building)
        {
            _burningSample.Clear();

            int total = building.BurningCells(_grid);
            if (total == 0) return 0;

            int want = Mathf.Min(MaxFireSigns, total);
            int seen = 0;
            for (int i = 0; i < building.Cells.Length; i++)
            {
                GridPoint cell = building.Cells[i];
                if (_grid[cell.X, cell.Y].State != CellState.Burning) continue;

                // seen번째 타는 칸을 want개 구간 중 어디에 넣을지 정해 고르게 흩는다.
                if (_burningSample.Count < want && seen * want / total == _burningSample.Count)
                {
                    _burningSample.Add(cell);
                }
                seen++;
            }

            return total;
        }

        private SpriteRenderer EnsureSign(SpriteRenderer[,] layer, int buildingId, int index, Sprite sprite, int order)
        {
            if (layer[buildingId, index] == null)
            {
                var mark = new GameObject("RoofSign").AddComponent<SpriteRenderer>();
                mark.transform.SetParent(_root, false);
                mark.sprite = sprite;
                mark.sortingOrder = order;
                layer[buildingId, index] = mark;
            }
            return layer[buildingId, index];
        }

        private void HideFireSigns(int buildingId, int from)
        {
            for (int i = from; i < MaxFireSigns; i++)
            {
                if (_fireSigns[buildingId, i] != null) _fireSigns[buildingId, i].enabled = false;
                if (_fireSmoke[buildingId, i] != null) _fireSmoke[buildingId, i].enabled = false;
            }
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        /// <summary>
        /// 건물 칸을 모두 덮되 밖으로 통하는 문만 비운다.
        /// 입구가 보이지 않으면 지붕을 씌워 놓고 들어갈 길을 없애는 셈이 된다.
        /// </summary>
        private List<SpriteRenderer> BuildRoof(Building building)
        {
            SiteTheme theme = SiteTheme.Of(_runner.Def.Id);
            Color color = theme.RoofColor;
            Sprite material = Art.RoofTexture(theme.Roof);

            var entrances = new HashSet<int>();
            foreach (GridPoint door in building.Entrances) entrances.Add(_grid.Index(door.X, door.Y));

            var member = new HashSet<int>();
            foreach (GridPoint cell in building.Cells) member.Add(_grid.Index(cell.X, cell.Y));

            // 칸마다 "가장자리에서 몇 칸 안쪽인가". 위에서 본 지붕이 입체로 읽히는 이유는
            // 처마 쪽 면이 어둡고 용마루가 밝기 때문이다. 생성 때 한 번만 재므로 프레임 비용이 없다.
            Dictionary<int, int> depth = RoofDepth(building, member);

            // 용마루는 긴 쪽을 따라 한 줄로 흐른다. 짝수 폭이면 가운데 두 줄이 용마루다.
            bool alongX = building.Width >= building.Height;
            int ridgeA = alongX ? (building.MinY + building.MaxY) / 2 : (building.MinX + building.MaxX) / 2;
            int ridgeB = alongX ? (building.MinY + building.MaxY + 1) / 2 : (building.MinX + building.MaxX + 1) / 2;

            var tiles = new List<SpriteRenderer>(building.Cells.Length);

            foreach (GridPoint cell in building.Cells)
            {
                int index = _grid.Index(cell.X, cell.Y);
                if (entrances.Contains(index)) continue;

                int along = alongX ? cell.Y : cell.X;
                bool onRidge = (along == ridgeA || along == ridgeB) && depth[index] >= 2;

                var tile = new GameObject("Roof").AddComponent<SpriteRenderer>();
                tile.transform.SetParent(_root, false);
                tile.sprite = material;
                tile.sortingOrder = OrderRoof;
                tile.transform.position = _view.CellCenter(cell.X, cell.Y);
                tile.transform.localScale = Vector3.one;
                tile.color = Slope(color, depth[index], onRidge);
                tiles.Add(tile);

                _roofByCell[index] = tile;
                _roofBaseColor[index] = tile.color;
            }

            return tiles;
        }

        /// <summary>
        /// 처마(깊이 1)에서 용마루(깊이 <see cref="RidgeDepth"/> 이상)까지의 밝기.
        /// 알파는 건드리지 않는다 — 곱해 버리면 지붕이 반투명해져 밑의 벽이 비친다.
        /// </summary>
        private static Color Slope(Color color, int depth, bool onRidge)
        {
            float t = Mathf.Clamp01((depth - 1f) / (RidgeDepth - 1f));
            float k = onRidge ? RidgeLine : Mathf.Lerp(EaveShade, RoofFace, t);
            return new Color(color.r * k, color.g * k, color.b * k, color.a);
        }

        /// <summary>
        /// 지붕 칸마다 가장자리에서의 거리. 지붕 바깥과 맞닿은 칸이 1이고 안쪽으로 갈수록 는다.
        /// 지붕 아닌 칸에서 동시에 퍼져 나가는 BFS라 어떤 모양이든 한 번에 잰다.
        /// </summary>
        private Dictionary<int, int> RoofDepth(Building building, HashSet<int> member)
        {
            var depth = new Dictionary<int, int>(building.Cells.Length);
            var queue = new Queue<GridPoint>();

            foreach (GridPoint cell in building.Cells)
            {
                if (!OnRim(member, cell)) continue;
                depth[_grid.Index(cell.X, cell.Y)] = 1;
                queue.Enqueue(cell);
            }

            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { -1, 1, 0, 0 };

            while (queue.Count > 0)
            {
                GridPoint p = queue.Dequeue();
                int here = depth[_grid.Index(p.X, p.Y)];

                for (int k = 0; k < 4; k++)
                {
                    int nx = p.X + dx[k];
                    int ny = p.Y + dy[k];
                    if (!_grid.InBounds(nx, ny)) continue;

                    int n = _grid.Index(nx, ny);
                    if (!member.Contains(n) || depth.ContainsKey(n)) continue;

                    depth[n] = here + 1;
                    queue.Enqueue(new GridPoint(nx, ny));
                }
            }

            // 가장자리가 없는 건물은 없지만, 혹시 비면 처마 색 하나로 균일하게 칠한다.
            foreach (GridPoint cell in building.Cells)
            {
                int i = _grid.Index(cell.X, cell.Y);
                if (!depth.ContainsKey(i)) depth[i] = 1;
            }

            return depth;
        }

        /// <summary>
        /// 밝기만 낮춘다. <c>Color * float</c>는 알파까지 곱해 버려서
        /// 어둡게 하려던 테두리가 반투명해지고 지붕 밑 벽이 비친다(캡처로 확인).
        /// </summary>
        private static Color Shade(Color color, float amount)
        {
            return new Color(color.r * amount, color.g * amount, color.b * amount, color.a);
        }


        // ------------------------------------------------------------------
        // 건물 치장 — 처마 그림자, 지붕 부속물, 입구 간판
        // ------------------------------------------------------------------

        /// <summary>
        /// 지붕만으로는 "땅에 그린 사각형"이다. 그림자로 띄우고, 부속물로 무슨 건물인지 말하고,
        /// 간판으로 어디로 들어가는지 알린다. 셋 다 생성 때 한 번만 만들고 지붕과 함께 켜고 끈다.
        /// </summary>
        private void BuildDressing(Building building)
        {
            var member = new HashSet<int>();
            foreach (GridPoint cell in building.Cells) member.Add(_grid.Index(cell.X, cell.Y));

            SiteTheme theme = SiteTheme.Of(_runner.Def.Id);

            BuildEaveShadow(building, member);
            BuildFixtures(building, theme);
            BuildSigns(building, theme);
        }

        /// <summary>
        /// 남·동쪽으로 삐져나온 지붕 칸 밑에 반투명 검정을 깐다.
        /// 이 한 가지가 납작한 색판을 "땅 위에 놓인 덩어리"로 바꾼다.
        /// 북·서는 빛이 오는 쪽이라 그림자가 없다.
        /// </summary>
        private void BuildEaveShadow(Building building, HashSet<int> member)
        {
            foreach (GridPoint cell in building.Cells)
            {
                bool south = !Inside(member, cell.X, cell.Y + 1);
                bool east = !Inside(member, cell.X + 1, cell.Y);
                if (!south && !east) continue;

                SpriteRenderer shadow = Piece("Eaves", Art.White, OrderEaves, building.Id);
                shadow.color = new Color(0f, 0f, 0f, 0.20f);
                shadow.transform.position = _view.CellCenter(cell.X, cell.Y)
                    + new Vector3(0.24f, -0.24f * MissionWorldView.Squash, 0f);
                shadow.transform.localScale = new Vector3(1f, MissionWorldView.Squash, 1f);
            }
        }

        /// <summary>
        /// 현장별 지붕 부속물. 굴뚝 하나로 상자가 집이 되고, 덕트 하나로 공장이 된다.
        /// 경계 상자 안의 비율 좌표라 건물 크기가 달라도 같은 자리에 앉는다.
        /// </summary>
        private void BuildFixtures(Building building, SiteTheme theme)
        {
            switch (theme.Fixture)
            {
                case RoofFixture.Chimney:       // 주택 — 굴뚝과 지붕창
                    Block(building, 0.18f, 0.30f, 1.1f, 1.1f, new Color(0.46f, 0.26f, 0.22f));
                    Block(building, 0.18f, 0.30f, 0.6f, 0.6f, new Color(0.20f, 0.15f, 0.14f));
                    Block(building, 0.72f, 0.32f, 1.6f, 0.9f, new Color(0.64f, 0.80f, 0.90f));
                    break;

                case RoofFixture.Units:         // 상가 — 옥상 실외기
                    for (int i = 0; i < 3; i++)
                    {
                        Block(building, 0.28f + (i * 0.22f), 0.30f, 1.1f, 1.0f, new Color(0.82f, 0.83f, 0.85f));
                        Block(building, 0.28f + (i * 0.22f), 0.30f, 0.7f, 0.7f, new Color(0.55f, 0.57f, 0.60f));
                    }

                    break;

                case RoofFixture.Pillars:       // 주유소 — 캐노피를 떠받치는 기둥
                    foreach (Vector2 at in FixtureCorners)
                    {
                        Block(building, at.x, at.y, 0.9f, 0.9f, new Color(0.33f, 0.34f, 0.36f));
                    }

                    break;

                case RoofFixture.Skylight:      // 창고 — 톱니 채광창
                    // 폭 0.8칸에 높이 62%로 뒀더니 흰 기둥 네 개가 지붕을 덮어 버렸다(캡처로 확인).
                    for (int i = 0; i < 4; i++)
                    {
                        Block(building, 0.24f + (i * 0.18f), 0.5f, 0.45f, building.Height * 0.44f,
                            new Color(0.78f, 0.85f, 0.92f));
                    }

                    break;

                case RoofFixture.Duct:          // 공장 — 굴뚝과 덕트 배관
                    Block(building, 0.45f, 0.52f, building.Width * 0.44f, 0.7f, new Color(0.64f, 0.66f, 0.68f));
                    Block(building, 0.80f, 0.28f, 1.7f, 1.7f, new Color(0.52f, 0.53f, 0.56f));
                    Block(building, 0.80f, 0.28f, 1.0f, 1.0f, new Color(0.17f, 0.17f, 0.18f));
                    break;

                default:                        // 항구 — 환기구
                    Block(building, 0.28f, 0.34f, 1.2f, 1.2f, new Color(0.56f, 0.50f, 0.44f));
                    Block(building, 0.66f, 0.64f, 1.2f, 1.2f, new Color(0.56f, 0.50f, 0.44f));
                    break;
            }
        }

        /// <summary>캐노피 기둥 자리. 네 귀퉁이에서 살짝 안쪽.</summary>
        private static readonly Vector2[] FixtureCorners =
        {
            new Vector2(0.20f, 0.24f), new Vector2(0.80f, 0.24f),
            new Vector2(0.20f, 0.76f), new Vector2(0.80f, 0.76f),
        };

        /// <summary>
        /// 경계 상자 안의 비율 좌표(0~1)에 <paramref name="w"/>x<paramref name="h"/>칸짜리 판을 놓는다.
        /// y는 화면 기준이 아니라 건물 기준이다 — 0이 북쪽 처마다.
        /// </summary>
        private void Block(Building building, float u, float v, float w, float h, Color color)
        {
            float x = building.MinX + (u * building.Width);

            // 부속물은 지붕면 위에 놓인다. 지붕이 눌리고 올라간 만큼 똑같이 따라가야
            // 굴뚝이 건물 밖으로 튀어나가지 않는다.
            float y = ((_grid.Height - building.MinY - (v * building.Height)) * MissionWorldView.Squash)
                + WallRise;

            SpriteRenderer piece = Piece("Fixture", Art.White, OrderFixture, building.Id);
            piece.color = color;
            piece.transform.position = new Vector3(x, y, 0f);
            piece.transform.localScale = new Vector3(w, h * MissionWorldView.Squash, 1f);
        }

        /// <summary>
        /// 입구 바깥 한 칸에 간판을 세운다. 지붕이 덮인 밖에서 "여기로 들어간다"를 말한다.
        /// 지붕이 없는 자리라 가려지지 않는다.
        /// </summary>
        private void BuildSigns(Building building, SiteTheme theme)
        {
            foreach (Vector3 gate in Gateways(building))
            {
                var door = new GridPoint(Mathf.FloorToInt(gate.x), Mathf.FloorToInt(gate.y));
                Vector3 outward = OutwardFrom(building, door);

                SpriteRenderer board = Piece("Sign", Art.Get("UI/panel_grey"), OrderFixture, building.Id);
                board.color = new Color(0.99f, 0.96f, 0.88f);
                // 마주 보는 두 건물(공장 배전동과 공장동)은 문이 세 칸 간격이라
                // 간판이 넓으면 "공장 공장"으로 겹친다(캡처로 확인). 좁게, 가깝게 붙인다.
                board.transform.position = _view.ToWorld(gate.x, gate.y) + (outward * 0.72f);
                board.transform.localScale = new Vector3(
                    Art.FitWidth(board.sprite, 1.5f), Art.FitWidth(board.sprite, 0.8f), 1f);

                var go = new GameObject("SignText");
                go.transform.SetParent(_root, false);
                var text = go.AddComponent<TextMesh>();
                text.font = Art.Font;
                text.text = theme.SignLabel;
                text.fontSize = 64;
                text.characterSize = 0.062f;
                text.fontStyle = FontStyle.Bold;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = new Color(0.32f, 0.20f, 0.12f);
                text.transform.position = board.transform.position;

                var mesh = go.GetComponent<MeshRenderer>();
                mesh.sharedMaterial = Art.Font.material;
                mesh.sortingOrder = OrderFixture + 1;
                _dressing[building.Id].Add(mesh);
            }
        }


        /// <summary>
        /// 붙어 있는 입구를 한 출입구로 묶은 가운데 좌표(격자 단위).
        /// 창고·주택처럼 문이 <c>DD</c>로 두 칸이면 간판이 두 장 겹쳐 글자가 뭉개진다(캡처로 확인).
        /// </summary>
        private List<Vector3> Gateways(Building building)
        {
            var left = new List<GridPoint>(building.Entrances);
            var gates = new List<Vector3>();

            while (left.Count > 0)
            {
                var clump = new List<GridPoint> { left[0] };
                left.RemoveAt(0);

                for (bool grew = true; grew;)
                {
                    grew = false;
                    for (int i = left.Count - 1; i >= 0; i--)
                    {
                        for (int k = 0; k < clump.Count; k++)
                        {
                            if (Mathf.Abs(left[i].X - clump[k].X) + Mathf.Abs(left[i].Y - clump[k].Y) != 1) continue;
                            clump.Add(left[i]);
                            left.RemoveAt(i);
                            grew = true;
                            break;
                        }
                    }
                }

                float sx = 0f;
                float sy = 0f;
                foreach (GridPoint c in clump)
                {
                    sx += c.X + 0.5f;
                    sy += c.Y + 0.5f;
                }

                gates.Add(new Vector3(sx / clump.Count, sy / clump.Count, 0f));
            }

            return gates;
        }

        /// <summary>문에서 마당 쪽으로 나가는 방향. 간판을 건물 안쪽에 세우면 지붕에 가린다.</summary>
        private Vector3 OutwardFrom(Building building, GridPoint door)
        {
            if (door.X <= building.MinX) return Vector3.left;
            if (door.X >= building.MaxX) return Vector3.right;
            if (door.Y <= building.MinY) return Vector3.up;
            return Vector3.down;
        }

        private SpriteRenderer Piece(string name, Sprite sprite, int order, int buildingId)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            _dressing[buildingId].Add(renderer);
            return renderer;
        }

        /// <summary>덩어리 바깥과 맞닿은 칸인지. 이 칸만 어둡게 칠해 테두리를 만든다.</summary>
        private bool OnRim(HashSet<int> member, GridPoint cell)
        {
            return !Inside(member, cell.X, cell.Y - 1)
                || !Inside(member, cell.X, cell.Y + 1)
                || !Inside(member, cell.X - 1, cell.Y)
                || !Inside(member, cell.X + 1, cell.Y);
        }

        private bool Inside(HashSet<int> member, int x, int y)
        {
            if (!_grid.InBounds(x, y)) return false;
            return member.Contains(_grid.Index(x, y));
        }
    }
}
