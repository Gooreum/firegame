using System;
using System.Collections.Generic;
using FireGame.Core.Grid;
using FireGame.Core.Sim;

namespace FireGame.Prototypes.Logic
{
    public struct Vec2
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float DistanceTo(Vec2 o)
        {
            float dx = X - o.X;
            float dy = Y - o.Y;
            return (float)Math.Sqrt((dx * dx) + (dy * dy));
        }
    }

    public struct AInput
    {
        /// <summary>이동 방향(격자 기준, 아래가 +y). 길이 1 이하.</summary>
        public float MoveX;
        public float MoveY;

        /// <summary>조준 각도(라디안, 격자 기준: 0 = 동쪽, π/2 = 남쪽).</summary>
        public float AimRadians;

        public bool Spraying;
    }

    public enum AOutcome : byte
    {
        Playing,
        Won,
        Lost,
    }

    public enum WarningKind : byte
    {
        /// <summary>한 무리가 곧 확 솟는다(반경 2).</summary>
        FlareUp,

        /// <summary>방 전체가 곧 한꺼번에 붙는다.</summary>
        Flashover,
    }

    public struct Warning
    {
        public WarningKind Kind;
        public GridPoint At;
        public int Room;
        public float SecondsLeft;
    }

    public sealed class Person
    {
        public Vec2 Pos;
        public float Hp = 100f;
        public bool Following;
        public bool Rescued;
        public bool Lost;

        /// <summary>뒤따르는 순서(0 = 소방관 바로 뒤).</summary>
        public int Slot = -1;
    }

    /// <summary>
    /// 시험판 A: 불이 적처럼 덮쳐 오는 실시간 한 판. 60Hz 고정 스텝, 시드가 같으면 같은 판이다.
    ///
    /// 불의 규칙:
    /// - 칸마다 열(0~1). 타는 칸은 열이 오르고 이웃을 데운다. 열 0.5를 넘은 칸은 붙는다.
    ///   소방관 쪽 이웃을 더 빨리 데운다 — 불이 사람을 쫓아오는 것처럼 느껴지게.
    /// - 물은 처음 닿은 불에서 멈춘다. 열이 0.3 밑으로 내려가야 꺼진다. 덜 끄면 열이 다시 올라 살아난다.
    ///   꺼져도 남은 열과 이웃 불 때문에 젖음(2초)이 마르면 다시 붙을 수 있다 — 끝까지 밀어붙여야 한다.
    /// - 솟음: 몇 초마다 불 무리 하나가 1초 예고 뒤 반경 2로 확 번진다. 예고 중에 그 칸을 끄면 취소된다.
    /// - 플래시오버: 방의 60%가 4초 넘게 타면 2초 예고 뒤 방 전체가 붙는다(복도처럼 큰 공간은 제외).
    /// - 탱크는 8초 연속 분사 분량. 소화전 옆에 서 있으면 1.5초에 가득 찬다.
    /// </summary>
    public sealed class ActionSim
    {
        public const float Dt = 1f / 60f;
        public const float PlayerSpeed = 4f;
        public const float SprayRange = 6f;
        public const float SpraySpreadRadians = 8f * (float)Math.PI / 180f;
        public const int RaysPerTick = 3;
        public const float TankSeconds = 8f;
        public const float RefillSeconds = 1.5f;
        public const float RefillReach = 1.6f;
        public const float IgniteHeat = 0.5f;
        public const float PutOutHeat = 0.3f;
        public const float CoolingPerSecond = 2.4f;
        public const float WetSeconds = 2f;
        public const float SpreadPerSecond = 0.04f;
        public const float ChaseBoost = 1.8f;
        public const float BurnSeconds = 30f;
        public const float FlareWarning = 1f;
        public const float FlashoverWarning = 2f;
        public const float FlashoverRatio = 0.6f;
        public const float FlashoverHold = 4f;

        /// <summary>이보다 큰 공간(복도)은 한꺼번에 붙지 않는다 — 판 전체가 한 번에 끝나면 안 된다.</summary>
        public const int FlashoverMaxCells = 50;
        public const float TimeLimit = 150f;

        public readonly ActionLevel Level;
        public readonly int Width;
        public readonly int Height;

        private readonly char[] _tiles;
        private readonly float[] _heat;
        private readonly float[] _fuel;
        private readonly float[] _wet;
        private readonly bool[] _burning;
        private readonly bool[] _burnt;
        private readonly int[] _room;
        private readonly int _roomCount;
        private readonly float[] _roomHot;
        private readonly bool[] _roomFlashed;
        private readonly List<GridPoint> _exits = new List<GridPoint>();
        private readonly List<GridPoint> _hydrants = new List<GridPoint>();
        private readonly List<Vec2> _trail = new List<Vec2>();
        private readonly List<Warning> _warnings = new List<Warning>();
        private Rng _rng;
        private float _nextFlare;

        public Vec2 Player;
        public float Hp = 100f;
        public float Tank = 1f;
        public float Aim;
        public bool SprayingNow;
        public float Elapsed;
        public readonly List<Person> People = new List<Person>();

        /// <summary>한 틱짜리 신호(화면·소리용).</summary>
        public int JustPutOut;
        public int JustIgnited;
        public bool JustFlaredUp;
        public bool JustFlashover;
        public GridPoint JustAt;

        /// <summary>이번 틱 물줄기가 닿은 끝점들(화면이 물방울을 그린다).</summary>
        public readonly List<Vec2> SprayHits = new List<Vec2>();

        public ActionSim(ActionLevel level, int seed)
        {
            Level = level;
            Height = level.Rows.Length;
            Width = level.Rows[0].Length;
            int n = Width * Height;
            _tiles = new char[n];
            _heat = new float[n];
            _fuel = new float[n];
            _wet = new float[n];
            _burning = new bool[n];
            _burnt = new bool[n];
            _rng = new Rng(seed == 0 ? 1 : seed);
            _nextFlare = 6f + (Rand() * 3f);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = Index(x, y);
                    char c = level.Rows[y][x];
                    _fuel[i] = 1f;
                    switch (c)
                    {
                        case 'P': Player = new Vec2(x + 0.5f, y + 0.5f); c = '.'; break;
                        case 'C': People.Add(new Person { Pos = new Vec2(x + 0.5f, y + 0.5f) }); c = '.'; break;
                        case '*': _burning[i] = true; _heat[i] = 1f; c = '.'; break;
                        case 'X': _exits.Add(new GridPoint(x, y)); break;
                        case 'H': _hydrants.Add(new GridPoint(x, y)); break;
                    }
                    _tiles[i] = c;
                }
            }

            _room = new int[n];
            _roomCount = LabelRooms();
            _roomHot = new float[_roomCount];
            _roomFlashed = new bool[_roomCount];
        }

        // ------------------------------------------------------------------
        // 읽기
        // ------------------------------------------------------------------

        public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < Width && y < Height; }
        public char Tile(int x, int y) { return _tiles[Index(x, y)]; }
        public bool Burning(int x, int y) { return _burning[Index(x, y)]; }
        public bool Burnt(int x, int y) { return _burnt[Index(x, y)]; }
        public float Heat(int x, int y) { return _heat[Index(x, y)]; }
        public float Wet(int x, int y) { return _wet[Index(x, y)]; }
        public int Room(int x, int y) { return _room[Index(x, y)]; }
        public IReadOnlyList<Warning> Warnings { get { return _warnings; } }
        public IReadOnlyList<GridPoint> Exits { get { return _exits; } }
        public IReadOnlyList<GridPoint> Hydrants { get { return _hydrants; } }

        public int BurningCount
        {
            get
            {
                int n = 0;
                foreach (bool b in _burning) if (b) n++;
                return n;
            }
        }

        public int Rescued
        {
            get
            {
                int n = 0;
                foreach (Person p in People) if (p.Rescued) n++;
                return n;
            }
        }

        public int LostCount
        {
            get
            {
                int n = 0;
                foreach (Person p in People) if (p.Lost) n++;
                return n;
            }
        }

        public AOutcome Outcome
        {
            get
            {
                if (Hp <= 0f) return AOutcome.Lost;
                int open = 0;
                foreach (Person p in People) if (!p.Rescued && !p.Lost) open++;
                if (open == 0) return Rescued > 0 ? AOutcome.Won : AOutcome.Lost;
                if (Elapsed >= TimeLimit) return AOutcome.Lost;
                return AOutcome.Playing;
            }
        }

        public bool Walkable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            char t = _tiles[Index(x, y)];
            return t != '#' && t != 'H';
        }

        public bool Flammable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            char t = _tiles[Index(x, y)];
            return t == '.' || t == 'D';
        }

        // ------------------------------------------------------------------
        // 한 틱
        // ------------------------------------------------------------------

        public void Step(in AInput input)
        {
            if (Outcome != AOutcome.Playing) return;

            JustPutOut = 0;
            JustIgnited = 0;
            JustFlaredUp = false;
            JustFlashover = false;
            SprayHits.Clear();
            Elapsed += Dt;

            MovePlayer(input);
            Spray(input);
            Refill();
            Burn();
            UpdateWarnings();
            UpdateFlashover();
            UpdatePeople();
            HurtPlayer();
        }

        private void MovePlayer(in AInput input)
        {
            float mx = input.MoveX;
            float my = input.MoveY;
            float len = (float)Math.Sqrt((mx * mx) + (my * my));
            if (len > 1f) { mx /= len; my /= len; }

            // 축을 나눠 움직여 벽을 따라 미끄러진다.
            const float radius = 0.3f;
            float nx = Player.X + (mx * PlayerSpeed * Dt);
            if (Free(nx, Player.Y, radius)) Player.X = nx;
            float ny = Player.Y + (my * PlayerSpeed * Dt);
            if (Free(Player.X, ny, radius)) Player.Y = ny;

            if (mx != 0f || my != 0f || _trail.Count == 0) Remember();
        }

        private bool Free(float x, float y, float r)
        {
            return Walkable((int)(x - r), (int)(y - r)) && Walkable((int)(x + r), (int)(y - r))
                && Walkable((int)(x - r), (int)(y + r)) && Walkable((int)(x + r), (int)(y + r));
        }

        /// <summary>소방관이 지나간 자리. 따라오는 사람은 이 길을 그대로 밟는다(벽에 안 걸린다).</summary>
        private void Remember()
        {
            if (_trail.Count == 0 || _trail[_trail.Count - 1].DistanceTo(Player) >= 0.1f) _trail.Add(Player);
            if (_trail.Count > 400) _trail.RemoveRange(0, 100);
        }

        private void Spray(in AInput input)
        {
            Aim = input.AimRadians;
            SprayingNow = input.Spraying && Tank > 0f;
            if (!SprayingNow) return;

            Tank = Math.Max(0f, Tank - (Dt / TankSeconds));

            for (int r = 0; r < RaysPerTick; r++)
            {
                float angle = input.AimRadians + (((Rand() * 2f) - 1f) * SpraySpreadRadians);
                float dx = (float)Math.Cos(angle);
                float dy = (float)Math.Sin(angle);
                Vec2 end = Player;

                for (float d = 0.4f; d <= SprayRange; d += 0.25f)
                {
                    float px = Player.X + (dx * d);
                    float py = Player.Y + (dy * d);
                    int cx = (int)px;
                    int cy = (int)py;
                    if (!InBounds(cx, cy) || _tiles[Index(cx, cy)] == '#') break;
                    end = new Vec2(px, py);

                    int i = Index(cx, cy);
                    _wet[i] = WetSeconds;
                    if (!_burning[i]) continue;

                    // 물은 처음 닿은 불에 먹힌다.
                    _heat[i] = Math.Max(0f, _heat[i] - (CoolingPerSecond * Dt / RaysPerTick));
                    if (_heat[i] < PutOutHeat)
                    {
                        _burning[i] = false;
                        JustPutOut++;
                        JustAt = new GridPoint(cx, cy);
                    }
                    break;
                }

                SprayHits.Add(end);
            }
        }

        private void Refill()
        {
            foreach (GridPoint h in _hydrants)
            {
                if (Player.DistanceTo(new Vec2(h.X + 0.5f, h.Y + 0.5f)) <= RefillReach)
                {
                    Tank = Math.Min(1f, Tank + (Dt / RefillSeconds));
                    return;
                }
            }
        }

        private void Burn()
        {
            int pcx = (int)Player.X;
            int pcy = (int)Player.Y;
            var gain = new float[_heat.Length];

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = Index(x, y);
                    if (_wet[i] > 0f) _wet[i] = Math.Max(0f, _wet[i] - Dt);

                    if (!_burning[i]) continue;

                    _heat[i] = Math.Min(1f, _heat[i] + (0.25f * Dt));
                    _fuel[i] -= Dt / BurnSeconds;
                    if (_fuel[i] <= 0f)
                    {
                        _burning[i] = false;
                        _burnt[i] = true;
                        _heat[i] = 0f;
                        continue;
                    }

                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + (d == 1 ? 1 : d == 3 ? -1 : 0);
                        int ny = y + (d == 2 ? 1 : d == 0 ? -1 : 0);
                        if (!Flammable(nx, ny)) continue;
                        // 소방관 쪽으로 더 빨리 — 불이 쫓아온다.
                        bool toward = Math.Abs(nx - pcx) + Math.Abs(ny - pcy) < Math.Abs(x - pcx) + Math.Abs(y - pcy);
                        gain[Index(nx, ny)] += SpreadPerSecond * _heat[i] * (toward ? ChaseBoost : 1f) * Dt;
                    }
                }
            }

            for (int i = 0; i < _heat.Length; i++)
            {
                if (_burning[i] || _burnt[i]) continue;

                // 옆에 불이 없는 칸만 천천히 식는다(옆 불이 있으면 계속 달아오른다).
                if (gain[i] <= 0f)
                {
                    if (_heat[i] > 0f) _heat[i] = Math.Max(0f, _heat[i] - (0.04f * Dt));
                    continue;
                }
                if (_wet[i] > 0f) continue;
                _heat[i] = Math.Min(1f, _heat[i] + gain[i]);
                if (_heat[i] >= IgniteHeat) Ignite(i);
            }
        }

        private void Ignite(int i)
        {
            if (_burning[i] || _burnt[i] || _wet[i] > 0f) return;
            int x = i % Width;
            int y = i / Width;
            if (!Flammable(x, y)) return;
            _burning[i] = true;
            _heat[i] = Math.Max(_heat[i], IgniteHeat + 0.1f);
            JustIgnited++;
        }

        private void UpdateWarnings()
        {
            for (int w = _warnings.Count - 1; w >= 0; w--)
            {
                Warning warning = _warnings[w];
                if (warning.Kind != WarningKind.FlareUp) continue;

                // 예고 중에 그 칸을 꺼 버리면 취소 — 경고를 본 보람이 있어야 한다.
                if (!_burning[Index(warning.At.X, warning.At.Y)])
                {
                    _warnings.RemoveAt(w);
                    continue;
                }

                warning.SecondsLeft -= Dt;
                if (warning.SecondsLeft > 0f)
                {
                    _warnings[w] = warning;
                    continue;
                }

                _warnings.RemoveAt(w);
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if ((dx * dx) + (dy * dy) > 5) continue;
                        int x = warning.At.X + dx;
                        int y = warning.At.Y + dy;
                        if (InBounds(x, y)) Ignite(Index(x, y));
                    }
                }
                JustFlaredUp = true;
                JustAt = warning.At;
            }

            _nextFlare -= Dt;
            if (_nextFlare > 0f) return;
            _nextFlare = 6f + (Rand() * 4f);

            // 이웃에 불이 둘 이상인 칸(무리)에서만 솟는다.
            var candidates = new List<GridPoint>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!_burning[Index(x, y)]) continue;
                    int around = 0;
                    if (InBounds(x + 1, y) && _burning[Index(x + 1, y)]) around++;
                    if (InBounds(x - 1, y) && _burning[Index(x - 1, y)]) around++;
                    if (InBounds(x, y + 1) && _burning[Index(x, y + 1)]) around++;
                    if (InBounds(x, y - 1) && _burning[Index(x, y - 1)]) around++;
                    if (around >= 2) candidates.Add(new GridPoint(x, y));
                }
            }
            if (candidates.Count == 0) return;
            GridPoint at = candidates[_rng.Next(candidates.Count)];
            _warnings.Add(new Warning { Kind = WarningKind.FlareUp, At = at, SecondsLeft = FlareWarning, Room = _room[Index(at.X, at.Y)] });
        }

        private void UpdateFlashover()
        {
            var total = new int[_roomCount];
            var burning = new int[_roomCount];
            for (int i = 0; i < _room.Length; i++)
            {
                int r = _room[i];
                if (r < 0 || !Flammable(i % Width, i / Width)) continue;
                total[r]++;
                if (_burning[i]) burning[r]++;
            }

            for (int r = 0; r < _roomCount; r++)
            {
                if (_roomFlashed[r]) continue;
                int pending = PendingFlashover(r);

                if (pending >= 0)
                {
                    Warning w = _warnings[pending];
                    w.SecondsLeft -= Dt;
                    if (w.SecondsLeft > 0f)
                    {
                        _warnings[pending] = w;
                        continue;
                    }

                    _warnings.RemoveAt(pending);
                    _roomFlashed[r] = true;
                    for (int i = 0; i < _room.Length; i++)
                    {
                        if (_room[i] == r) Ignite(i);
                    }
                    JustFlashover = true;
                    JustAt = w.At;
                    continue;
                }

                bool hot = total[r] > 0 && total[r] <= FlashoverMaxCells && burning[r] >= total[r] * FlashoverRatio;
                _roomHot[r] = hot ? _roomHot[r] + Dt : 0f;
                if (_roomHot[r] < FlashoverHold) continue;

                _warnings.Add(new Warning { Kind = WarningKind.Flashover, Room = r, At = RoomCenter(r), SecondsLeft = FlashoverWarning });
            }
        }

        private int PendingFlashover(int room)
        {
            for (int w = 0; w < _warnings.Count; w++)
            {
                if (_warnings[w].Kind == WarningKind.Flashover && _warnings[w].Room == room) return w;
            }
            return -1;
        }

        private void UpdatePeople()
        {
            foreach (Person p in People)
            {
                if (p.Rescued || p.Lost) continue;

                if (!p.Following && p.Pos.DistanceTo(Player) <= 1.1f)
                {
                    p.Following = true;
                    p.Slot = CountFollowing() - 1;
                }

                if (p.Following)
                {
                    // 소방관 발자국을 그대로 밟는다. 앞 사람과 0.8칸 간격.
                    int back = (p.Slot + 1) * 8;
                    int index = Math.Max(0, _trail.Count - 1 - back);
                    if (_trail.Count > 0) p.Pos = _trail[index];

                    foreach (GridPoint e in _exits)
                    {
                        if (p.Pos.DistanceTo(new Vec2(e.X + 0.5f, e.Y + 0.5f)) <= 1.2f || Player.DistanceTo(new Vec2(e.X + 0.5f, e.Y + 0.5f)) <= 0.9f)
                        {
                            p.Rescued = true;
                            p.Following = false;
                        }
                    }
                    if (p.Rescued) continue;
                }

                int cx = (int)p.Pos.X;
                int cy = (int)p.Pos.Y;
                if (InBounds(cx, cy) && _burning[Index(cx, cy)]) p.Hp -= 20f * Dt;
                if (p.Hp <= 0f)
                {
                    p.Lost = true;
                    p.Following = false;
                }
            }

            // 구조·실종으로 빈 자리를 당긴다.
            int slot = 0;
            foreach (Person p in People)
            {
                if (p.Following) p.Slot = slot++;
            }
        }

        private int CountFollowing()
        {
            int n = 0;
            foreach (Person p in People) if (p.Following) n++;
            return n;
        }

        private void HurtPlayer()
        {
            int cx = (int)Player.X;
            int cy = (int)Player.Y;
            float damage = 0f;
            if (InBounds(cx, cy) && _burning[Index(cx, cy)]) damage += 25f;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx != 0 || dy != 0) && InBounds(cx + dx, cy + dy) && _burning[Index(cx + dx, cy + dy)]) damage += 2f;
                }
            }
            Hp -= damage * Dt;
        }

        // ------------------------------------------------------------------

        /// <summary>문(D)을 경계로 방을 나눈다. 벽·문·소화전·출구는 방이 없다(-1).</summary>
        private int LabelRooms()
        {
            for (int i = 0; i < _room.Length; i++) _room[i] = -1;
            int count = 0;
            var queue = new Queue<int>();

            for (int start = 0; start < _room.Length; start++)
            {
                if (_room[start] >= 0 || _tiles[start] != '.') continue;
                _room[start] = count;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    int x = i % Width;
                    int y = i / Width;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + (d == 1 ? 1 : d == 3 ? -1 : 0);
                        int ny = y + (d == 2 ? 1 : d == 0 ? -1 : 0);
                        if (!InBounds(nx, ny)) continue;
                        int j = Index(nx, ny);
                        if (_room[j] >= 0 || _tiles[j] != '.') continue;
                        _room[j] = count;
                        queue.Enqueue(j);
                    }
                }
                count++;
            }
            return count;
        }

        private GridPoint RoomCenter(int room)
        {
            long sx = 0, sy = 0, n = 0;
            for (int i = 0; i < _room.Length; i++)
            {
                if (_room[i] != room) continue;
                sx += i % Width;
                sy += i / Width;
                n++;
            }
            return n == 0 ? new GridPoint(0, 0) : new GridPoint((int)(sx / n), (int)(sy / n));
        }

        private float Rand()
        {
            return _rng.Next(10000) / 10000f;
        }

        private int Index(int x, int y)
        {
            return (y * Width) + x;
        }
    }
}
