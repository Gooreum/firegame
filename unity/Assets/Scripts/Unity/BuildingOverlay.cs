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

        private readonly Transform _root;
        private readonly StageRunner _runner;
        private readonly FireGrid _grid;
        private readonly BuildingMap _buildings;
        private readonly MissionWorldView _view;

        /// <summary>건물마다 그 건물을 덮는 지붕 칸들.</summary>
        private readonly List<SpriteRenderer>[] _roofs;

        public BuildingOverlay(Transform parent, StageRunner runner, BuildingMap buildings, MissionWorldView view)
        {
            _runner = runner;
            _grid = runner.Grid;
            _buildings = buildings;
            _view = view;

            _root = new GameObject("BuildingOverlay").transform;
            _root.SetParent(parent, false);

            _roofs = new List<SpriteRenderer>[buildings.All.Length];
            for (int i = 0; i < buildings.All.Length; i++) _roofs[i] = BuildRoof(buildings.All[i]);
        }

        /// <summary>
        /// 소방관이 들어간 건물의 지붕만 걷는다.
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
