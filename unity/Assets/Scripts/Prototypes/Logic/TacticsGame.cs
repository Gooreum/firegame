using System;
using System.Collections.Generic;
using System.Text;
using FireGame.Core.Grid;

namespace FireGame.Prototypes.Logic
{
    public enum TTile : byte
    {
        Wall,
        Floor,

        /// <summary>콘크리트 바닥. 걸을 수 있지만 타지 않는다 — 천연 방화선.</summary>
        Concrete,

        Door,
        Exit,
        Hydrant,

        /// <summary>가스통. 불이 닿으면 예고 뒤 다음 턴에 터진다.</summary>
        Gas,
    }

    public enum TAction : byte
    {
        Move,
        Spray,
        ToggleDoor,
        Refill,
    }

    public enum TOutcome : byte
    {
        Playing,
        Won,
        Lost,
    }

    public enum CivState : byte
    {
        /// <summary>제자리에서 떨고 있다. 소방관이 곁에 와야 움직인다.</summary>
        Waiting,

        /// <summary>출구로 스스로 걷는다(턴마다 2칸). 가는 길은 미리 보인다.</summary>
        Evacuating,

        Rescued,
        Lost,
    }

    /// <summary>다음 턴에 불이 옮겨 갈 칸과, 어느 쪽 불에서 오는지(화살표용).</summary>
    public readonly struct Spread
    {
        public readonly GridPoint To;
        public readonly Dir From;

        public Spread(GridPoint to, Dir from)
        {
            To = to;
            From = from;
        }
    }

    /// <summary>
    /// 시험판 B: 불이 다음 턴에 어디로 갈지 전부 보이는 턴제 퍼즐.
    ///
    /// 규칙은 전부 결정적이다 — <see cref="PreviewSpread"/>와 <see cref="PreviewExplosions"/>가
    /// 곧 <see cref="EndTurn"/>의 결과다. 미리보기가 거짓말을 하면 퍼즐이 아니라 운이 된다.
    ///
    /// 불의 규칙:
    /// - 바람이 부는 쪽 이웃에는 붙은 턴부터 번진다. 옆(수직) 이웃에는 한 턴 묵은 불부터 번진다. 바람을 거슬러서는 안 번진다.
    ///   바람이 없으면 한 턴 묵은 불이 네 방향으로 번진다.
    /// - 불은 네 턴을 타고 재가 된다(재는 다시 안 탄다).
    /// - 젖은 칸은 안 탄다(분사 후 두 번의 턴 넘김 동안). 닫힌 문은 불이 못 넘는다.
    /// - 가스통에 불이 닿으면 예고가 뜨고, 다음 턴에 주변 3×3을 날린다(젖음 무시).
    ///
    /// 사람은 업어 나르지 않는다 — 소방관이 곁(상하좌우)에 서면 스스로 출구로 걷는다.
    /// 그 길을 불에서 지켜 주는 것이 퍼즐이다(업어 나르면 한 명을 나르는 동안 다른 한 명이 반드시 죽었다).
    /// </summary>
    public sealed class TacticsGame
    {
        public const int ActionsPerTurn = 2;
        public const int TankMax = 4;
        public const int MaxHp = 3;
        public const int MoveRange = 2;

        /// <summary>대피하는 사람이 한 턴에 걷는 칸 수.</summary>
        public const int EvacSpeed = 2;
        public const int SprayRange = 3;
        public const int WetTurns = 2;
        public const int BurnTurns = 4;
        public const int ExplosionDamage = 2;

        public readonly TacticsLevel Level;
        public readonly int Width;
        public readonly int Height;

        private State _state;
        private State _turnStart;

        private TacticsGame(TacticsLevel level, State state)
        {
            Level = level;
            Width = state.Width;
            Height = state.Height;
            _state = state;
            _turnStart = state.Clone();
        }

        public static TacticsGame Load(TacticsLevel level)
        {
            return new TacticsGame(level, State.Parse(level));
        }

        /// <summary>지금 판을 통째로 복사한다(되돌리기 기준점 포함). 수를 시험해 보는 데 쓴다.</summary>
        public TacticsGame Clone()
        {
            var copy = new TacticsGame(Level, _state.Clone());
            copy._turnStart = _turnStart.Clone();
            return copy;
        }

        // ------------------------------------------------------------------
        // 읽기
        // ------------------------------------------------------------------

        public TTile Tile(int x, int y) { return _state.Tiles[Index(x, y)]; }
        public bool Burning(int x, int y) { return _state.FireAge[Index(x, y)] > 0; }

        /// <summary>붙은 뒤 지난 턴 수(0부터). 안 타면 -1.</summary>
        public int FireAge(int x, int y) { return _state.FireAge[Index(x, y)] - 1; }

        public bool Burnt(int x, int y) { return _state.Burnt[Index(x, y)]; }
        public int Wet(int x, int y) { return _state.Wet[Index(x, y)]; }
        public bool DoorClosed(int x, int y) { return _state.DoorClosed[Index(x, y)]; }
        public bool GasArmed(int x, int y) { return _state.Armed[Index(x, y)]; }

        public GridPoint Player { get { return _state.Player; } }
        public int Actions { get { return _state.Actions; } }
        public int Tank { get { return _state.Tank; } }
        public int Hp { get { return _state.Hp; } }
        public int Turn { get { return _state.Turn; } }
        public int TurnLimit { get { return Level.TurnLimit; } }
        public IReadOnlyList<GridPoint> CivilianPositions { get { return _state.CivPos; } }
        public IReadOnlyList<CivState> CivilianStates { get { return _state.CivStates; } }

        /// <summary>이번 턴 끝에 부는 바람. None이면 무풍.</summary>
        public Dir? Wind { get { return Level.WindAt(_state.Turn); } }

        /// <summary>다음 턴 바람(예고). 바람이 바뀌는 레벨에서 미리 알려 준다.</summary>
        public Dir? NextWind { get { return Level.WindAt(_state.Turn + 1); } }

        public int Rescued { get { return Count(CivState.Rescued); } }
        public int LostCount { get { return Count(CivState.Lost); } }
        public int Total { get { return _state.CivStates.Length; } }

        public TOutcome Outcome
        {
            get
            {
                if (_state.Hp <= 0) return TOutcome.Lost;
                // 사람이 없는 판은 규칙만 보는 연습판이다(테스트용). 끝나지 않는다.
                if (_state.CivStates.Length == 0) return TOutcome.Playing;
                int open = Count(CivState.Waiting) + Count(CivState.Evacuating);
                if (open == 0) return Rescued > 0 ? TOutcome.Won : TOutcome.Lost;
                if (_state.Turn > Level.TurnLimit) return TOutcome.Lost;
                return TOutcome.Playing;
            }
        }

        /// <summary>승리 별: 전원 3, 한 명 잃으면 2, 그 이하 1. 지면 0.</summary>
        public int Stars
        {
            get
            {
                if (Outcome != TOutcome.Won) return 0;
                if (Rescued == Total) return 3;
                return Rescued == Total - 1 ? 2 : 1;
            }
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        // ------------------------------------------------------------------
        // 미리보기
        // ------------------------------------------------------------------

        /// <summary>다음 <see cref="EndTurn"/>에 새로 붙을 칸(가스통 점화 포함). 폭발로 붙는 칸은 뺀다.</summary>
        public List<Spread> PreviewSpread()
        {
            return ComputeSpread(_state);
        }

        /// <summary>다음 <see cref="EndTurn"/>에 터질 가스통들의 폭발 범위.</summary>
        public List<GridPoint> PreviewExplosions()
        {
            return ComputeExplosions(_state);
        }

        /// <summary>대피 중인 사람마다 다음 턴에 밟을 칸들(없으면 null). 화면이 발자국으로 보여 준다.</summary>
        public List<GridPoint>[] PreviewWalks()
        {
            return ComputeWalks(_state);
        }

        /// <summary>
        /// 지금 이 방향으로 쏘면 적셔질 칸(최대 3칸, 벽·닫힌 문·소화전·가스통 아닌 장애물에서 멈춤).
        /// </summary>
        public List<GridPoint> SprayCells(Dir dir)
        {
            var cells = new List<GridPoint>();
            GridPoint p = _state.Player;
            for (int i = 1; i <= SprayRange; i++)
            {
                GridPoint c = Dirs.Step(p, dir, i);
                if (!InBounds(c.X, c.Y)) break;
                int idx = Index(c.X, c.Y);
                TTile tile = _state.Tiles[idx];
                if (tile == TTile.Wall || tile == TTile.Hydrant) break;
                if (tile == TTile.Door && _state.DoorClosed[idx]) break;
                cells.Add(c);
            }
            return cells;
        }

        /// <summary>
        /// 이 방향으로 쏘면 다음 턴에 막히는 확산. 화면이 회색 X로 보여 준다 —
        /// "이 수가 무엇을 막는가"가 보여야 고민이 된다.
        /// </summary>
        public List<GridPoint> SprayBlocks(Dir dir)
        {
            var before = new HashSet<GridPoint>();
            foreach (Spread s in ComputeSpread(_state)) before.Add(s.To);
            foreach (GridPoint p in ComputeExplosions(_state)) before.Add(p);

            State trial = _state.Clone();
            ApplySpray(trial, SprayCells(dir));

            var after = new HashSet<GridPoint>();
            foreach (Spread s in ComputeSpread(trial)) after.Add(s.To);
            foreach (GridPoint p in ComputeExplosions(trial)) after.Add(p);

            var blocked = new List<GridPoint>();
            foreach (GridPoint p in before)
            {
                if (!after.Contains(p)) blocked.Add(p);
            }
            blocked.Sort(Compare);
            return blocked;
        }

        /// <summary>이동으로 닿을 수 있는 칸과 걸음 수.</summary>
        public Dictionary<GridPoint, int> Reachable()
        {
            var result = new Dictionary<GridPoint, int>();
            int range = MoveRange;
            var queue = new Queue<GridPoint>();
            queue.Enqueue(_state.Player);
            result[_state.Player] = 0;

            while (queue.Count > 0)
            {
                GridPoint p = queue.Dequeue();
                int d = result[p];
                if (d >= range) continue;

                foreach (Dir dir in Dirs.All)
                {
                    GridPoint n = Dirs.Step(p, dir);
                    if (result.ContainsKey(n) || !Walkable(n)) continue;
                    result[n] = d + 1;
                    queue.Enqueue(n);
                }
            }

            result.Remove(_state.Player);
            return result;
        }

        private bool Walkable(GridPoint p)
        {
            if (!InBounds(p.X, p.Y)) return false;
            int i = Index(p.X, p.Y);
            TTile tile = _state.Tiles[i];
            if (tile == TTile.Wall || tile == TTile.Hydrant || tile == TTile.Gas) return false;
            if (_state.FireAge[i] > 0) return false;
            return true;
        }

        // ------------------------------------------------------------------
        // 행동
        // ------------------------------------------------------------------

        public bool CanDo(TAction action, GridPoint target, out string why)
        {
            why = null;
            if (Outcome != TOutcome.Playing) { why = "판이 끝났다"; return false; }
            if (_state.Actions <= 0) { why = "이번 턴 행동을 다 썼다 — Space로 턴 넘기기"; return false; }

            switch (action)
            {
                case TAction.Move:
                    if (!Reachable().ContainsKey(target)) { why = "거기까지 못 간다"; return false; }
                    return true;

                case TAction.Spray:
                    if (_state.Tank <= 0) { why = "물이 없다 — 소화전 옆에서 급수"; return false; }
                    if (!DirTo(target, out Dir dir)) { why = "한 줄로만 쏠 수 있다"; return false; }
                    if (SprayCells(dir).Count == 0) { why = "바로 앞이 막혔다"; return false; }
                    return true;

                case TAction.ToggleDoor:
                    if (!InBounds(target.X, target.Y) || _state.Tiles[Index(target.X, target.Y)] != TTile.Door) { why = "문이 아니다"; return false; }
                    if (!Adjacent(_state.Player, target)) { why = "문 바로 옆에서만"; return false; }
                    if (_state.FireAge[Index(target.X, target.Y)] > 0) { why = "타는 문은 못 닫는다"; return false; }
                    return true;

                case TAction.Refill:
                    if (!NextToHydrant()) { why = "소화전 바로 옆에서만"; return false; }
                    if (_state.Tank >= TankMax) { why = "이미 가득 찼다"; return false; }
                    return true;
            }

            why = "알 수 없는 행동";
            return false;
        }

        /// <summary>행동 하나. 할 수 없으면 false이고 상태는 그대로다.</summary>
        public bool Do(TAction action, GridPoint target)
        {
            if (!CanDo(action, target, out _)) return false;

            switch (action)
            {
                case TAction.Move:
                    _state.Player = target;
                    Rouse(_state);
                    break;

                case TAction.Spray:
                    DirTo(target, out Dir dir);
                    ApplySpray(_state, SprayCells(dir));
                    _state.Tank--;
                    break;

                case TAction.ToggleDoor:
                    int door = Index(target.X, target.Y);
                    _state.DoorClosed[door] = !_state.DoorClosed[door];
                    break;

                case TAction.Refill:
                    _state.Tank = TankMax;
                    break;
            }

            _state.Actions--;
            return true;
        }

        /// <summary>불이 움직인다. 결과는 직전 미리보기와 정확히 같다.</summary>
        public void EndTurn()
        {
            if (Outcome != TOutcome.Playing) return;

            State s = _state;
            List<GridPoint> blast = ComputeExplosions(s);
            List<Spread> spread = ComputeSpread(s);

            // 0) 대피 중인 사람이 먼저 걷는다. 불은 그 뒤에 번진다 — 미리보기 그대로.
            List<GridPoint>[] walks = ComputeWalks(s);
            for (int c = 0; c < walks.Length; c++)
            {
                if (walks[c] == null || walks[c].Count == 0) continue;
                GridPoint end = walks[c][walks[c].Count - 1];
                s.CivPos[c] = end;
                if (s.Tiles[Index(end.X, end.Y)] == TTile.Exit) s.CivStates[c] = CivState.Rescued;
            }

            // 1) 이미 타던 불은 한 턴 묵는다. 다 탄 불은 재가 된다.
            for (int i = 0; i < s.FireAge.Length; i++)
            {
                if (s.FireAge[i] == 0) continue;
                s.FireAge[i]++;
                if (s.FireAge[i] > BurnTurns)
                {
                    s.FireAge[i] = 0;
                    s.Burnt[i] = true;
                }
            }

            // 2) 터질 가스통은 터져 재가 된다.
            for (int i = 0; i < s.Armed.Length; i++)
            {
                if (!s.Armed[i]) continue;
                s.Armed[i] = false;
                s.Burnt[i] = true;
            }

            // 3) 새 불. 폭발은 젖음도 무시한다.
            var lit = new HashSet<GridPoint>();
            foreach (Spread sp in spread) lit.Add(sp.To);
            foreach (GridPoint p in blast)
            {
                if (Flammable(s, Index(p.X, p.Y), true)) lit.Add(p);
            }

            foreach (GridPoint p in lit)
            {
                int i = Index(p.X, p.Y);
                if (s.Tiles[i] == TTile.Gas) s.Armed[i] = true;
                else s.FireAge[i] = 1;
            }

            // 4) 불·폭발에 닿은 사람과 소방관.
            var hurt = new HashSet<GridPoint>(lit);
            for (int c = 0; c < s.CivPos.Length; c++)
            {
                bool exposed = s.CivStates[c] == CivState.Waiting || s.CivStates[c] == CivState.Evacuating;
                if (exposed && (hurt.Contains(s.CivPos[c]) || Contains(blast, s.CivPos[c])))
                {
                    s.CivStates[c] = CivState.Lost;
                }
            }
            if (lit.Contains(s.Player)) s.Hp--;
            if (Contains(blast, s.Player)) s.Hp -= ExplosionDamage;

            // 5) 젖음이 마른다.
            for (int i = 0; i < s.Wet.Length; i++)
            {
                if (s.Wet[i] > 0) s.Wet[i]--;
            }

            s.Turn++;
            s.Actions = ActionsPerTurn;
            _turnStart = s.Clone();
        }

        /// <summary>이번 턴에 한 행동을 전부 되돌린다.</summary>
        public void UndoTurn()
        {
            _state = _turnStart.Clone();
        }

        /// <summary>상태를 글자로. 되돌리기가 정확한지 비교하는 데 쓴다.</summary>
        public string Signature()
        {
            return _state.Signature();
        }

        /// <summary>디버그·테스트용 그림. 레벨을 설계할 때 본다.</summary>
        public string Draw()
        {
            var preview = new HashSet<GridPoint>();
            foreach (Spread s in PreviewSpread()) preview.Add(s.To);
            var sb = new StringBuilder();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var p = new GridPoint(x, y);
                    int i = Index(x, y);
                    char c;
                    int civAt = CivilianAt(_state, p);
                    if (p.Equals(_state.Player)) c = 'P';
                    else if (civAt >= 0) c = _state.CivStates[civAt] == CivState.Evacuating ? 'E' : 'C';
                    else if (_state.FireAge[i] > 0) c = (char)('0' + (_state.FireAge[i] - 1));
                    else if (_state.Armed[i]) c = '!';
                    else if (preview.Contains(p)) c = '+';
                    else if (_state.Burnt[i]) c = '_';
                    else if (_state.Wet[i] > 0) c = '~';
                    else
                    {
                        switch (_state.Tiles[i])
                        {
                            case TTile.Wall: c = '#'; break;
                            case TTile.Concrete: c = ','; break;
                            case TTile.Door: c = _state.DoorClosed[i] ? 'd' : 'D'; break;
                            case TTile.Exit: c = 'X'; break;
                            case TTile.Hydrant: c = 'H'; break;
                            case TTile.Gas: c = 'G'; break;
                            default: c = '.'; break;
                        }
                    }
                    sb.Append(c);
                }
                sb.Append('\n');
            }
            sb.Append("턴 ").Append(_state.Turn).Append('/').Append(Level.TurnLimit)
              .Append(" 행동 ").Append(_state.Actions).Append(" 물 ").Append(_state.Tank)
              .Append(" 체력 ").Append(_state.Hp).Append(" 구조 ").Append(Rescued).Append('/').Append(Total)
              .Append(" 잃음 ").Append(LostCount).Append(' ').Append(Outcome);
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // 규칙 계산
        // ------------------------------------------------------------------

        private List<Spread> ComputeSpread(State s)
        {
            var seen = new HashSet<GridPoint>();
            var result = new List<Spread>();
            Dir? wind = Level.WindAt(s.Turn);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int age = s.FireAge[Index(x, y)] - 1;
                    if (age < 0) continue;

                    foreach (Dir dir in Dirs.All)
                    {
                        if (!Spreads(wind, dir, age)) continue;
                        GridPoint to = Dirs.Step(new GridPoint(x, y), dir);
                        if (!InBounds(to.X, to.Y) || seen.Contains(to)) continue;
                        if (!Flammable(s, Index(to.X, to.Y), false)) continue;
                        seen.Add(to);
                        result.Add(new Spread(to, Dirs.Opposite(dir)));
                    }
                }
            }

            result.Sort((a, b) => Compare(a.To, b.To));
            return result;
        }

        /// <summary>
        /// 대피하는 사람은 지금 타는 칸을 피해 가장 가까운 출구로 최대 2칸 걷는다.
        /// 다음에 번질 칸은 모른다 — 그걸 아는 건 플레이어뿐이다.
        /// </summary>
        private List<GridPoint>[] ComputeWalks(State s)
        {
            var walks = new List<GridPoint>[s.CivPos.Length];
            for (int c = 0; c < s.CivPos.Length; c++)
            {
                if (s.CivStates[c] != CivState.Evacuating) continue;
                List<GridPoint> path = PathToExit(s, s.CivPos[c]);
                if (path == null) continue;
                walks[c] = path.GetRange(0, Math.Min(EvacSpeed, path.Count));
            }
            return walks;
        }

        private List<GridPoint> PathToExit(State s, GridPoint from)
        {
            var prev = new Dictionary<GridPoint, GridPoint>();
            var queue = new Queue<GridPoint>();
            queue.Enqueue(from);
            prev[from] = from;

            while (queue.Count > 0)
            {
                GridPoint p = queue.Dequeue();
                if (s.Tiles[Index(p.X, p.Y)] == TTile.Exit && !p.Equals(from))
                {
                    var path = new List<GridPoint>();
                    for (GridPoint q = p; !q.Equals(from); q = prev[q]) path.Add(q);
                    path.Reverse();
                    return path;
                }
                foreach (Dir dir in Dirs.All)
                {
                    GridPoint n = Dirs.Step(p, dir);
                    if (!InBounds(n.X, n.Y) || prev.ContainsKey(n)) continue;
                    int i = Index(n.X, n.Y);
                    TTile t = s.Tiles[i];
                    if (t == TTile.Wall || t == TTile.Hydrant || t == TTile.Gas || s.FireAge[i] > 0) continue;
                    if (t == TTile.Door && s.DoorClosed[i]) continue;
                    prev[n] = p;
                    queue.Enqueue(n);
                }
            }
            return null;
        }

        /// <summary>소방관 바로 곁(상하좌우 또는 같은 칸)에 있는 사람은 대피를 시작한다.</summary>
        private static void Rouse(State s)
        {
            for (int c = 0; c < s.CivPos.Length; c++)
            {
                if (s.CivStates[c] != CivState.Waiting) continue;
                GridPoint p = s.CivPos[c];
                if (Math.Abs(p.X - s.Player.X) + Math.Abs(p.Y - s.Player.Y) <= 1) s.CivStates[c] = CivState.Evacuating;
            }
        }

        private static bool Spreads(Dir? wind, Dir dir, int age)
        {
            if (wind == null) return age >= 1;
            if (dir == wind.Value) return true;
            if (dir == Dirs.Opposite(wind.Value)) return false;
            return age >= 1;
        }

        private List<GridPoint> ComputeExplosions(State s)
        {
            var result = new List<GridPoint>();
            var seen = new HashSet<GridPoint>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!s.Armed[Index(x, y)]) continue;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            var p = new GridPoint(x + dx, y + dy);
                            if (!InBounds(p.X, p.Y) || !seen.Add(p)) continue;
                            if (s.Tiles[Index(p.X, p.Y)] == TTile.Wall) continue;
                            result.Add(p);
                        }
                    }
                }
            }
            result.Sort(Compare);
            return result;
        }

        /// <summary>이 칸에 새로 불이 붙을 수 있는지. 폭발은 젖음을 무시한다.</summary>
        private static bool Flammable(State s, int i, bool ignoreWet)
        {
            if (s.FireAge[i] > 0 || s.Burnt[i] || s.Armed[i]) return false;
            if (!ignoreWet && s.Wet[i] > 0) return false;
            switch (s.Tiles[i])
            {
                case TTile.Floor:
                case TTile.Gas:
                    return true;
                case TTile.Door:
                    return !s.DoorClosed[i];
                default:
                    return false;
            }
        }

        private void ApplySpray(State s, List<GridPoint> cells)
        {
            foreach (GridPoint c in cells)
            {
                int i = Index(c.X, c.Y);
                s.FireAge[i] = 0;
                s.Armed[i] = false;
                if (!s.Burnt[i]) s.Wet[i] = WetTurns;
            }
        }

        private bool DirTo(GridPoint target, out Dir dir)
        {
            GridPoint p = _state.Player;
            dir = Dir.N;
            if (target.Equals(p)) return false;
            if (target.X == p.X) { dir = target.Y < p.Y ? Dir.N : Dir.S; return true; }
            if (target.Y == p.Y) { dir = target.X < p.X ? Dir.W : Dir.E; return true; }
            return false;
        }

        private bool NextToHydrant()
        {
            foreach (Dir d in Dirs.All)
            {
                GridPoint n = Dirs.Step(_state.Player, d);
                if (InBounds(n.X, n.Y) && _state.Tiles[Index(n.X, n.Y)] == TTile.Hydrant) return true;
            }
            return false;
        }

        private static bool Adjacent(GridPoint a, GridPoint b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
        }

        private static int CivilianAt(State s, GridPoint p)
        {
            for (int c = 0; c < s.CivPos.Length; c++)
            {
                if ((s.CivStates[c] == CivState.Waiting || s.CivStates[c] == CivState.Evacuating) && s.CivPos[c].Equals(p)) return c;
            }
            return -1;
        }

        private static bool Contains(List<GridPoint> list, GridPoint p)
        {
            foreach (GridPoint q in list) if (q.Equals(p)) return true;
            return false;
        }

        private int Count(CivState state)
        {
            int n = 0;
            foreach (CivState c in _state.CivStates) if (c == state) n++;
            return n;
        }

        private int Index(int x, int y)
        {
            return (y * Width) + x;
        }

        private static int Compare(GridPoint a, GridPoint b)
        {
            return a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
        }

        // ------------------------------------------------------------------

        private sealed class State
        {
            public int Width;
            public int Height;
            public TTile[] Tiles;
            public bool[] DoorClosed;
            public byte[] FireAge;   // 0 = 안 탐, n = 붙은 뒤 n-1턴
            public bool[] Burnt;
            public byte[] Wet;
            public bool[] Armed;
            public GridPoint[] CivPos;
            public CivState[] CivStates;
            public GridPoint Player;
            public int Actions = ActionsPerTurn;
            public int Tank = TankMax;
            public int Hp = MaxHp;
            public int Turn = 1;

            public static State Parse(TacticsLevel level)
            {
                string[] rows = level.Rows;
                var s = new State { Height = rows.Length, Width = rows[0].Length };
                int n = s.Width * s.Height;
                s.Tiles = new TTile[n];
                s.DoorClosed = new bool[n];
                s.FireAge = new byte[n];
                s.Burnt = new bool[n];
                s.Wet = new byte[n];
                s.Armed = new bool[n];
                var civs = new List<GridPoint>();

                for (int y = 0; y < s.Height; y++)
                {
                    if (rows[y].Length != s.Width) throw new ArgumentException(level.Name + ": " + y + "행 길이가 다르다");
                    for (int x = 0; x < s.Width; x++)
                    {
                        int i = (y * s.Width) + x;
                        char c = rows[y][x];
                        TTile tile = TTile.Floor;
                        switch (c)
                        {
                            case '#': tile = TTile.Wall; break;
                            case ',': tile = TTile.Concrete; break;
                            case 'D': tile = TTile.Door; break;
                            case 'd': tile = TTile.Door; s.DoorClosed[i] = true; break;
                            case 'X': tile = TTile.Exit; break;
                            case 'H': tile = TTile.Hydrant; break;
                            case 'G': tile = TTile.Gas; break;
                            case '*': s.FireAge[i] = 1; break;
                            case 'P': s.Player = new GridPoint(x, y); break;
                            case 'C': civs.Add(new GridPoint(x, y)); break;
                            case '.': break;
                            default: throw new ArgumentException(level.Name + ": 모르는 글자 '" + c + "'");
                        }
                        s.Tiles[i] = tile;
                    }
                }

                s.CivPos = civs.ToArray();
                s.CivStates = new CivState[civs.Count];
                return s;
            }

            public State Clone()
            {
                var c = (State)MemberwiseClone();
                c.DoorClosed = (bool[])DoorClosed.Clone();
                c.FireAge = (byte[])FireAge.Clone();
                c.Burnt = (bool[])Burnt.Clone();
                c.Wet = (byte[])Wet.Clone();
                c.Armed = (bool[])Armed.Clone();
                c.CivPos = (GridPoint[])CivPos.Clone();
                c.CivStates = (CivState[])CivStates.Clone();
                return c;
            }

            public string Signature()
            {
                var sb = new StringBuilder();
                sb.Append(Player.X).Append(',').Append(Player.Y).Append('|').Append(Actions).Append('|').Append(Tank)
                  .Append('|').Append(Hp).Append('|').Append(Turn).Append('|');
                for (int i = 0; i < FireAge.Length; i++)
                {
                    sb.Append(FireAge[i]).Append(Burnt[i] ? 'b' : '-').Append(Wet[i]).Append(Armed[i] ? 'a' : '-').Append(DoorClosed[i] ? 'c' : '-');
                }
                for (int c = 0; c < CivStates.Length; c++) sb.Append((int)CivStates[c]).Append(CivPos[c].X).Append(',').Append(CivPos[c].Y).Append(';');
                return sb.ToString();
            }
        }
    }
}
