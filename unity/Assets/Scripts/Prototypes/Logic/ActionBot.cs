using System;
using System.Collections.Generic;
using FireGame.Core.Grid;

namespace FireGame.Prototypes.Logic
{
    /// <summary>
    /// 시험판 A를 대신 뛰는 단순한 봇. "이길 수는 있는 판인가"를 재고, 캡처용 교전 장면을 만든다.
    /// 사람에게 가서(불은 되도록 돌아가고) 길을 막는 불을 쏘며, 다 모으면 출구로 간다. 물이 떨어지면 소화전.
    /// </summary>
    public sealed class ActionBot
    {
        private readonly ActionSim _sim;
        private bool _refilling;

        public ActionBot(ActionSim sim)
        {
            _sim = sim;
        }

        public AInput Next()
        {
            var input = new AInput();

            if (_sim.Tank < 0.12f) _refilling = true;
            if (_sim.Tank > 0.95f) _refilling = false;

            GridPoint me = new GridPoint((int)_sim.Player.X, (int)_sim.Player.Y);
            GridPoint? goal = _refilling ? NearestHydrantSide(me) : Goal();
            if (goal == null) return input;

            List<GridPoint> path = Path(me, goal.Value);

            // 길 위 가까운 불, 또는 뒤따르는 사람 곁의 불을 쏜다.
            GridPoint? threat = Threat(path);
            if (threat.HasValue && _sim.Tank > 0f)
            {
                input.Spraying = true;
                input.AimRadians = (float)Math.Atan2((threat.Value.Y + 0.5f) - _sim.Player.Y, (threat.Value.X + 0.5f) - _sim.Player.X);
            }

            if (path != null && path.Count > 0)
            {
                GridPoint next = path[0];
                bool blocked = _sim.Burning(next.X, next.Y) && input.Spraying;
                if (!blocked)
                {
                    float dx = (next.X + 0.5f) - _sim.Player.X;
                    float dy = (next.Y + 0.5f) - _sim.Player.Y;
                    float len = (float)Math.Sqrt((dx * dx) + (dy * dy));
                    if (len > 0.01f)
                    {
                        input.MoveX = dx / len;
                        input.MoveY = dy / len;
                    }
                }
            }

            return input;
        }

        private GridPoint? Goal()
        {
            Person target = null;
            float best = float.MaxValue;
            bool anyFollowing = false;
            foreach (Person p in _sim.People)
            {
                if (p.Following) anyFollowing = true;
                if (p.Following || p.Rescued || p.Lost) continue;
                float d = p.Pos.DistanceTo(_sim.Player);
                if (d < best) { best = d; target = p; }
            }

            if (target != null) return new GridPoint((int)target.Pos.X, (int)target.Pos.Y);
            if (anyFollowing && _sim.Exits.Count > 0) return _sim.Exits[0];
            return null;
        }

        private GridPoint? NearestHydrantSide(GridPoint me)
        {
            foreach (GridPoint h in _sim.Hydrants)
            {
                for (int d = 0; d < 4; d++)
                {
                    int x = h.X + (d == 1 ? 1 : d == 3 ? -1 : 0);
                    int y = h.Y + (d == 2 ? 1 : d == 0 ? -1 : 0);
                    if (_sim.Walkable(x, y)) return new GridPoint(x, y);
                }
            }
            return null;
        }

        private GridPoint? Threat(List<GridPoint> path)
        {
            GridPoint? best = null;
            float bestD = float.MaxValue;

            // 길 앞 네 칸 안의 불
            if (path != null)
            {
                for (int i = 0; i < Math.Min(4, path.Count); i++)
                {
                    if (_sim.Burning(path[i].X, path[i].Y)) return path[i];
                }
            }

            // 소방관 곁이나 따라오는 사람 곁의 불
            for (int y = 0; y < _sim.Height; y++)
            {
                for (int x = 0; x < _sim.Width; x++)
                {
                    if (!_sim.Burning(x, y)) continue;
                    var c = new Vec2(x + 0.5f, y + 0.5f);
                    float d = c.DistanceTo(_sim.Player);
                    bool near = d <= 2.2f;
                    foreach (Person p in _sim.People) if (p.Following && c.DistanceTo(p.Pos) <= 2f) near = true;
                    if (near && d <= ActionSim.SprayRange - 0.5f && d < bestD) { bestD = d; best = new GridPoint(x, y); }
                }
            }
            return best;
        }

        /// <summary>다익스트라. 타는 칸은 비싸게(되도록 돌아가되 막히면 뚫는다).</summary>
        private List<GridPoint> Path(GridPoint from, GridPoint to)
        {
            int w = _sim.Width;
            var dist = new float[w * _sim.Height];
            var prev = new int[dist.Length];
            for (int i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            var open = new SortedSet<(float, int)>();
            int s = (from.Y * w) + from.X;
            dist[s] = 0f;
            open.Add((0f, s));

            while (open.Count > 0)
            {
                (float d, int i) = open.Min;
                open.Remove(open.Min);
                if (i == (to.Y * w) + to.X) break;
                int x = i % w;
                int y = i / w;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 1 ? 1 : k == 3 ? -1 : 0);
                    int ny = y + (k == 2 ? 1 : k == 0 ? -1 : 0);
                    bool target = nx == to.X && ny == to.Y;
                    if (!_sim.Walkable(nx, ny) && !target) continue;
                    int j = (ny * w) + nx;
                    float cost = 1f + (_sim.Burning(nx, ny) ? 8f : 0f);
                    if (d + cost >= dist[j]) continue;
                    if (dist[j] != float.MaxValue) open.Remove((dist[j], j));
                    dist[j] = d + cost;
                    prev[j] = i;
                    open.Add((dist[j], j));
                }
            }

            int end = (to.Y * w) + to.X;
            if (prev[end] < 0 && end != s) return null;
            var path = new List<GridPoint>();
            for (int i = end; i != s && i >= 0; i = prev[i]) path.Add(new GridPoint(i % w, i / w));
            path.Reverse();
            return path;
        }
    }
}
