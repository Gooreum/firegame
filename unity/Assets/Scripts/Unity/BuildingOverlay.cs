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

        /// <summary>덩어리 가장자리를 이만큼 어둡게 한다. 납작한 색판이 아니라 한 채로 읽힌다.</summary>
        private const float RimShade = 0.72f;

        /// <summary>
        /// 현장 id별 지붕 색. 현장마다 달라야 "저 건물"이 아니라 "저 상가"로 읽힌다.
        /// 없는 id는 주택(0)을 쓴다.
        /// </summary>
        private static readonly Dictionary<int, Color> RoofColors = new Dictionary<int, Color>
        {
            { 0, new Color(0.82f, 0.34f, 0.28f) },   // 주택: 붉은 기와
            { 1, new Color(0.32f, 0.54f, 0.82f) },   // 상가: 파란 차양
            { 2, new Color(0.92f, 0.74f, 0.28f) },   // 주유소: 노란 캐노피
            { 3, new Color(0.56f, 0.62f, 0.70f) },   // 창고: 회청 함석
            { 4, new Color(0.40f, 0.60f, 0.45f) },   // 공장: 초록 슬레이트
            { 5, new Color(0.70f, 0.42f, 0.30f) },   // 항구: 적갈 널판
        };

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
            for (int i = 0; i < buildings.All.Length; i++) _roofs[i] = BuildRoof(buildings.All[i]);

            _fireSigns = new SpriteRenderer[buildings.All.Length, MaxFireSigns];
            _fireSmoke = new SpriteRenderer[buildings.All.Length, MaxFireSigns];
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

                // 들어간 건물은 진짜 불이 그대로 보인다. 지붕 위 표시를 겹쳐 그릴 이유가 없다.
                if (covered)
                {
                    CharRoof(_buildings.All[i]);
                    RefreshFireSigns(_buildings.All[i], time);
                }
                else
                {
                    HideFireSigns(i, 0);
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
            Color color;
            if (!RoofColors.TryGetValue(_runner.Def.Id, out color)) color = RoofColors[0];

            var entrances = new HashSet<int>();
            foreach (GridPoint door in building.Entrances) entrances.Add(_grid.Index(door.X, door.Y));

            var member = new HashSet<int>();
            foreach (GridPoint cell in building.Cells) member.Add(_grid.Index(cell.X, cell.Y));

            var tiles = new List<SpriteRenderer>(building.Cells.Length);

            foreach (GridPoint cell in building.Cells)
            {
                if (entrances.Contains(_grid.Index(cell.X, cell.Y))) continue;

                // 지붕은 벽돌 그림 대신 단색판으로 깐다. 켄니 벽돌은 그 자체가 짙은 회색이라
                // 색을 곱하면 현장마다 다른 지붕색이 전부 회색으로 죽는다(캡처로 확인).
                // 바닥 타일처럼 몇 칸을 섞어 결을 주려 했더니 평평한 색 위에서는
                // 대각선 얼룩으로 보였다. 카툰 그림이니 단색에 테두리만 둔다.
                var tile = new GameObject("Roof").AddComponent<SpriteRenderer>();
                tile.transform.SetParent(_root, false);
                tile.sprite = Art.White;
                tile.sortingOrder = OrderRoof;
                tile.transform.position = _view.CellCenter(cell.X, cell.Y);
                tile.transform.localScale = Vector3.one;
                tile.color = OnRim(member, cell) ? Shade(color, RimShade) : color;
                tiles.Add(tile);

                int index = _grid.Index(cell.X, cell.Y);
                _roofByCell[index] = tile;
                _roofBaseColor[index] = tile.color;
            }

            return tiles;
        }

        /// <summary>
        /// 밝기만 낮춘다. <c>Color * float</c>는 알파까지 곱해 버려서
        /// 어둡게 하려던 테두리가 반투명해지고 지붕 밑 벽이 비친다(캡처로 확인).
        /// </summary>
        private static Color Shade(Color color, float amount)
        {
            return new Color(color.r * amount, color.g * amount, color.b * amount, color.a);
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
