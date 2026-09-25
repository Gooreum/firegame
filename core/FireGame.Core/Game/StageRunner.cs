using System;
using System.Collections.Generic;
using FireGame.Core.Data;
using FireGame.Core.Grid;
using FireGame.Core.Sim;

namespace FireGame.Core.Game
{
    /// <summary>
    /// 한 판의 진행을 맡는다. 화재 시뮬레이션, 플레이어, 시민, 승패 판정을 엮는다.
    ///
    /// 화재 시뮬레이션은 고정 틱으로만 돌린다. 프레임레이트에 따라 불이
    /// 더 빨리 번지거나 늦게 번지면 같은 맵이 기기마다 다른 난이도가 된다.
    /// </summary>
    public sealed class StageRunner
    {
        /// <summary>무결성이 이 아래로 떨어지면 건물을 포기한 것으로 본다.</summary>
        public const float BuildingLostThreshold = 0.2f;

        /// <summary>시민을 업기 위해 다가가야 하는 거리(셀).</summary>
        private const float PickupRadius = 0.7f;

        private readonly List<GridPoint> _hitBuffer = new List<GridPoint>();

        // 슬롯별 레벨 반영 장비. Player.Slots에는 id만 있다.
        private readonly EquipmentDef[] _slotDefs = new EquipmentDef[PlayerState.SlotCount];
        // float으로 누적하면 2분짜리 스테이지를 60fps로 돌릴 때 7200번 더해지며
        // 오차가 쌓여 실제 경과 시간과 틱 수가 어긋난다. double로 누적한다.
        private double _tickAccumulator;

        /// <summary>이번 프레임의 dt. 시민 쇠약처럼 프레임 단위로 재는 값이 쓴다.</summary>
        private float _lastDelta;

        public readonly StageDef Def;

        /// <summary>이 판을 만든 시드. 0이면 맵에 표시된 그대로다.</summary>
        public readonly int Seed;
        public readonly FireGrid Grid;
        public readonly FireSim Sim;
        public readonly PlayerState Player;
        public readonly List<Civilian> Civilians = new List<Civilian>();
        /// <summary>
        /// 소화전 위치. 물탱크가 생기면서 <b>실제 급수점</b>이 됐다 —
        /// 전에는 "호스는 어디서나 쏜다"라 지도 장식이었다.
        /// </summary>
        public readonly List<GridPoint> Hydrants = new List<GridPoint>();
        public readonly List<GridPoint> Exits = new List<GridPoint>();

        /// <summary>건물 구획. 실내·실외 판정과 연기 배출, 시야 차단에 쓴다.</summary>
        public readonly BuildingMap Buildings;

        /// <summary>진행 중 사건. 시드 0이면 아무 일도 안 일어난다.</summary>
        public readonly StageEvents Events;

        /// <summary>지금 보이는 범위. 화면과 HUD가 이걸 보고 무엇을 그릴지 정한다.</summary>
        public readonly VisionField Vision;

        public float TimeLeft;
        public StageOutcome Outcome = StageOutcome.InProgress;

        /// <summary>지금까지 실행한 고정 틱 수. 프레임레이트 독립성 검증에 쓴다.</summary>
        public int TicksElapsed;

        /// <summary>이번 출동에서 실제로 나간 발 수.</summary>
        public int ShotsFired;

        /// <summary>이번 출동에서 끈 불 칸 수.</summary>
        public int CellsExtinguished;

        /// <summary>마지막 한 발이 끈 칸 수. 화면이 "3칸 진압!"을 띄우는 데 쓴다.</summary>
        public int LastShotExtinguished;

        /// <summary>마지막 한 발에서 역효과가 난 칸 수. 화면이 "역효과!"를 띄우는 데 쓴다.</summary>
        public int LastShotBackfired;

        /// <summary>
        /// 지금 업을 수 있는 시민(사거리 안에서 가장 가까운 한 명). 없으면 null.
        /// HUD가 이걸 보고 구조 버튼을 켜고, 현장 화면이 그 시민 표식을 초록으로 바꾼다.
        /// </summary>
        public Civilian RescueTarget { get; private set; }

        /// <summary>이번 프레임에 막 업은 시민. 화면이 "구조!"를 띄우고 나면 다음 프레임에 비워진다.</summary>
        public Civilian JustPickedUp { get; private set; }

        /// <summary>이번 프레임에 막 출구로 데려나간 시민.</summary>
        public Civilian JustRescued { get; private set; }

        /// <summary>이번 프레임에 불에 휩싸여 잃은 시민.</summary>
        public Civilian JustLost { get; private set; }

        /// <summary>
        /// 출구에서 철수를 선언했는지. 불을 남긴 채 이기는 <b>유일한 길</b>이다.
        ///
        /// "시민을 다 구하면 자동으로 이긴다"로 두면 마지막 한 명을 내려놓는 순간
        /// 판이 끝나 버려 불을 끌 이유가 통째로 사라진다. 그래서 선언하게 한다 —
        /// 나갈지 더 싸울지를 고르는 것이 이 게임에서 처음 생기는 진짜 선택이다.
        /// </summary>
        public bool Withdrew { get; private set; }

        /// <summary>한 명도 잃지 않고 전원 데리고 나왔는지.</summary>
        public bool AllCiviliansSafe
        {
            get { return RescuedCount == Civilians.Count; }
        }

        /// <summary>
        /// 지금 철수를 선언할 수 있는지. HUD가 이걸 보고 구조 버튼을 "철수"로 바꾼다.
        /// </summary>
        public bool CanWithdraw
        {
            get
            {
                return !IsOver
                    && AllCiviliansSafe
                    && Civilians.Count > 0
                    && IsExit(Player.CellX, Player.CellY);
            }
        }

        /// <summary>이번 프레임의 문 조작 결과. 화면이 읽고 팝업으로 옮긴다.</summary>
        public InteractResult LastInteract { get; private set; }

        /// <summary>
        /// 지금 여닫을 수 있는 문. 없으면 null.
        /// HUD가 이걸 보고 문 버튼을 켠다 — 곁에 문이 없는데 버튼이 켜져 있으면
        /// 눌러 보고 나서야 아무 일도 안 일어난다는 걸 알게 된다.
        /// </summary>
        public GridPoint? DoorAtHand { get; private set; }

        /// <summary>장비 id 목록으로 시작한다. 전부 Lv1, 방화복·소방화 없음.</summary>
        public StageRunner(StageDef def, IReadOnlyList<int> unlockedEquipment)
            : this(def, Loadout.FromIds(unlockedEquipment))
        {
        }

        public StageRunner(StageDef def, Loadout loadout)
            : this(def, loadout, 0)
        {
        }

        /// <summary>
        /// <paramref name="seed"/> 0이면 맵에 표시된 자리에 그대로 불이 난다(기존 동작).
        /// 0이 아니면 그 시드로 발화점을 옮긴다 — 외운 순서가 안 통하게 된다.
        /// </summary>
        public StageRunner(StageDef def, Loadout loadout, int seed)
        {
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (def == null) throw new ArgumentNullException(nameof(def));

            Def = def;
            Seed = seed;

            ParsedMap map = MapLoader.Parse(def.Map);
            Grid = map.Grid;
            Sim = new FireSim(Grid) { Wind = def.Wind, Intensity = def.FireIntensity };
            Hydrants.AddRange(map.Hydrants);
            Exits.AddRange(map.Exits);

            foreach (GridPoint point in map.Civilians)
            {
                Civilians.Add(new Civilian { X = point.X + 0.5f, Y = point.Y + 0.5f });
            }

            // 건물 구획을 먼저 세운다. 연기가 어디로 빠지는지와
            // 무엇이 가려지는지가 둘 다 여기서 갈린다.
            Buildings = BuildingMap.From(Grid, map.PlayerSpawn);
            Sim.Outdoor = OutdoorMask(Grid, Buildings);

            if (seed != 0) RelocateIgnitions(map.IgnitionPoints, map.PlayerSpawn, new Rng(seed));
            Events = new StageEvents(seed, Buildings.All.Length);

            Player = new PlayerState();
            Player.Spawn(map.PlayerSpawn);
            Player.SuitLevel = loadout.SuitLevel;
            Player.DamageMultiplier = GearStats.DamageMultiplier(loadout.SuitLevel);
            Player.SpeedMultiplier = GearStats.SpeedMultiplier(loadout.BootsLevel);
            EquipLoadout(loadout.Equipment);

            TimeLeft = def.TimeLimitSeconds;

            Vision = new VisionField(Grid.Width, Grid.Height);
            Vision.Refresh(Grid, Buildings, Player.CellX, Player.CellY);
        }

        /// <summary>
        /// 맵에 표시된 발화점을 같은 재질의 다른 칸으로 옮긴다.
        ///
        /// 재질을 지키는 이유: 전기 불이 나무 불로 바뀌면 CO2가 필요 없어지고,
        /// 나무 불이 기름 불이 되면 양동이로 못 깬다. 현장마다 적어 둔 필요 장비가
        /// 거짓말이 된다. 건물도 지킨다 — 설계자가 고른 건물에서 방만 바뀐다.
        /// 실외 불은 실외에 남는다.
        /// </summary>
        private void RelocateIgnitions(List<GridPoint> marked, GridPoint spawn, Rng rng)
        {
            var candidates = new List<GridPoint>();

            foreach (GridPoint origin in marked)
            {
                byte material = Grid[origin.X, origin.Y].Material;
                int building = Buildings.At(origin.X, origin.Y);

                candidates.Clear();
                for (int y = 0; y < Grid.Height; y++)
                {
                    for (int x = 0; x < Grid.Width; x++)
                    {
                        if (Grid[x, y].Material != material || Grid[x, y].State != CellState.Intact) continue;
                        if (Buildings.At(x, y) != building) continue;
                        if (NearCivilian(x, y)) continue;
                        candidates.Add(new GridPoint(x, y));
                    }
                }

                // 옮길 곳이 없으면 표시된 자리 그대로 둔다.
                if (candidates.Count == 0) continue;

                // 스폰에서 먼 순. 거리가 같으면 좌표로 가른다 — List.Sort는 불안정 정렬이라
                // 동률 순서가 런타임마다 달라지면 같은 시드가 .NET과 Unity에서 다른 판이 된다.
                candidates.Sort((a, b) =>
                {
                    int byDistance = SquaredDistance(b, spawn).CompareTo(SquaredDistance(a, spawn));
                    if (byDistance != 0) return byDistance;
                    return a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
                });

                // 먼 쪽 절반에서 고른다. 문 앞에서 불이 나면 들어가자마자 끝난다.
                GridPoint target = candidates[rng.Next((candidates.Count + 1) / 2)];

                Grid[origin.X, origin.Y].State = CellState.Intact;
                Grid[target.X, target.Y].State = CellState.Burning;
            }
        }

        /// <summary>시민 두 칸 안인지. 시작하자마자 불이 덮쳐 잃는 판을 만들지 않는다.</summary>
        public bool NearCivilian(int x, int y)
        {
            foreach (Civilian civilian in Civilians)
            {
                int cx = (int)Math.Floor(civilian.X);
                int cy = (int)Math.Floor(civilian.Y);
                if (Math.Abs(cx - x) <= 2 && Math.Abs(cy - y) <= 2) return true;
            }
            return false;
        }

        private static int SquaredDistance(GridPoint a, GridPoint b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return (dx * dx) + (dy * dy);
        }

        /// <summary>칸별 실외 여부. 연기는 실외에서 고이지 않고 빠져나간다.</summary>
        private static bool[] OutdoorMask(FireGrid grid, BuildingMap buildings)
        {
            var mask = new bool[grid.Count];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    mask[grid.Index(x, y)] = buildings.IsOutdoor(x, y);
                }
            }
            return mask;
        }

        /// <summary>장비를 앞에서부터 슬롯에 채운다.</summary>
        private void EquipLoadout(IReadOnlyList<EquipmentDef> equipment)
        {
            int slot = 0;
            for (int i = 0; i < equipment.Count && slot < PlayerState.SlotCount; i++)
            {
                if (equipment[i] == null) continue;

                Player.Equip(slot, equipment[i]);
                _slotDefs[slot] = equipment[i];
                slot++;
            }
        }

        /// <summary>
        /// 슬롯의 레벨 반영 장비. 비었거나 범위 밖이면 null.
        /// 발사·HUD·화면 연출이 모두 이걸 써야 레벨이 어디서나 같게 보인다.
        /// </summary>
        public EquipmentDef SlotEquipment(int slot)
        {
            if (slot < 0 || slot >= PlayerState.SlotCount) return null;
            return _slotDefs[slot];
        }

        public bool IsOver
        {
            get { return Outcome != StageOutcome.InProgress; }
        }

        public int RescuedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Civilians.Count; i++)
                {
                    if (Civilians[i].Rescued) n++;
                }
                return n;
            }
        }

        /// <summary>아직 맵에 남아 구조를 기다리는 시민 수.</summary>
        public int PendingCivilianCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Civilians.Count; i++)
                {
                    if (Civilians[i].Pending) n++;
                }
                return n;
            }
        }

        public void Update(float dt, in StageInput input)
        {
            if (IsOver || dt <= 0f) return;

            // 한 프레임짜리 신호다. 화면이 이번 프레임에 읽고 팝업으로 옮긴다.
            RescueTarget = null;
            JustPickedUp = null;
            JustRescued = null;
            JustLost = null;
            LastInteract = InteractResult.None;

            _lastDelta = dt;

            TimeLeft -= dt;
            if (TimeLeft < 0f) TimeLeft = 0f;

            Player.ActiveSlot = input.Slot;
            Player.Update(dt, Grid, input.MoveX, input.MoveY);

            if (input.Fire) TryFire(input.Slot);

            DoorAtHand = FindDoorAtHand();
            if (input.Interact) TryInteract();

            Refill(dt);
            AdvanceSimulation(dt);
            Events.Update(this);
            UpdateCivilians(input.Rescue);
            TryWithdraw(input.Rescue);
            EvaluateOutcome();
        }

        /// <summary>
        /// 급수. 소화전이나 소방차(출구) 곁에 서 있으면 탱크를 채운다.
        ///
        /// 출구도 급수점으로 치는 이유는 맵마다 소화전이 하나뿐이어서다.
        /// 소화전 한 곳만 인정하면 판이 <b>맵 끝까지 왕복하기</b>가 된다.
        /// 출구는 어차피 시민을 데려가야 하는 자리라, 가는 김에 채우는 것이 자연스럽다.
        /// </summary>
        private void Refill(float dt)
        {
            if (Player.Water >= GameConfig.WaterTankMax) return;
            if (!NearWaterPoint(Player.X, Player.Y)) return;

            Player.Water += GameConfig.RefillPerSecond * dt;
            if (Player.Water > GameConfig.WaterTankMax) Player.Water = GameConfig.WaterTankMax;
        }

        /// <summary>급수를 받을 수 있는 자리인지. HUD와 봇이 같은 판정을 쓰도록 공개한다.</summary>
        public bool NearWaterPoint(float x, float y)
        {
            return Near(Hydrants, x, y) || Near(Exits, x, y);
        }

        private static bool Near(List<GridPoint> points, float x, float y)
        {
            float reach = GameConfig.RefillRadius * GameConfig.RefillRadius;

            for (int i = 0; i < points.Count; i++)
            {
                float dx = points[i].X + 0.5f - x;
                float dy = points[i].Y + 0.5f - y;
                if ((dx * dx) + (dy * dy) <= reach) return true;
            }

            return false;
        }

        /// <summary>
        /// 누적기로 고정 틱만 실행한다. 남는 시간은 다음 호출로 넘겨
        /// 어떤 프레임레이트에서도 같은 시간에 같은 만큼 번지게 한다.
        /// </summary>
        private void AdvanceSimulation(float dt)
        {
            _tickAccumulator += dt;

            while (_tickAccumulator >= SimConfig.TickDelta)
            {
                _tickAccumulator -= SimConfig.TickDelta;
                Sim.Tick();
                Vision.Refresh(Grid, Buildings, Player.CellX, Player.CellY);
                TicksElapsed++;
            }
        }

        private void TryFire(int slot)
        {
            if (slot < 0 || slot >= PlayerState.SlotCount) return;

            EquipmentDef def = SlotEquipment(slot);
            if (def == null) return;
            if (!Player.CanFire(slot, def)) return;

            Aiming.Resolve(Grid, Player.CellX, Player.CellY, Player.Aim, def.Pattern, def.Range, _hitBuffer, def.EndSpread);

            int putOut = 0;
            int backfired = 0;
            for (int i = 0; i < _hitBuffer.Count; i++)
            {
                SuppressionOutcome outcome = Suppression.Apply(Grid, _hitBuffer[i].X, _hitBuffer[i].Y, def.Agent);
                if (outcome == SuppressionOutcome.Extinguished) putOut++;
                else if (outcome == SuppressionOutcome.Backfired) backfired++;
            }

            ShotsFired++;
            CellsExtinguished += putOut;
            LastShotExtinguished = putOut;
            LastShotBackfired = backfired;

            Player.ConsumeFire(slot, def);
        }

        /// <summary>
        /// 손이 닿는 문 한 짝. 여덟 이웃 중 조준 방향에 가장 가까운 것을 고른다.
        ///
        /// 조준 칸 하나만 보면 대각으로 선 문을 못 잡아, 문을 여닫으려고
        /// 칸을 맞춰 서는 일이 생긴다. 반대로 서 있는 칸 자체는 보지 않는다 —
        /// 문간에 선 채로 닫으면 제 발로 벽 속에 갇힌다.
        /// </summary>
        private GridPoint? FindDoorAtHand()
        {
            int aimX = Aiming.OffsetX(Player.Aim);
            int aimY = Aiming.OffsetY(Player.Aim);

            GridPoint? best = null;
            int bestScore = int.MinValue;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int x = Player.CellX + dx;
                    int y = Player.CellY + dy;
                    if (!Grid.InBounds(x, y)) continue;
                    if (Grid[x, y].Material != (byte)MaterialId.Door) continue;

                    // 조준 방향과 얼마나 같은 쪽인지. 정면이 가장 높다.
                    int score = (dx * aimX) + (dy * aimY);
                    if (score <= bestScore) continue;

                    bestScore = score;
                    best = new GridPoint(x, y);
                }
            }

            return best;
        }

        /// <summary>곁의 문을 여닫는다. 불타는 문에는 손을 못 댄다.</summary>
        private void TryInteract()
        {
            if (DoorAtHand == null) return;

            GridPoint at = DoorAtHand.Value;
            ref Cell door = ref Grid[at.X, at.Y];

            if (door.State == CellState.Burning)
            {
                LastInteract = InteractResult.Burning;
                return;
            }

            if (door.Jammed)
            {
                LastInteract = InteractResult.Jammed;
                return;
            }

            door.Shut = !door.Shut;
            LastInteract = door.Shut ? InteractResult.Shut : InteractResult.Opened;
        }

        /// <summary>
        /// 시민을 살핀다. 사거리 안에 들어와도 저절로 업히지 않고,
        /// 구조 버튼을 누른 프레임에만 가장 가까운 한 명을 업는다.
        /// </summary>
        private void UpdateCivilians(bool rescuePressed)
        {
            float nearest = -1f;

            for (int i = 0; i < Civilians.Count; i++)
            {
                Civilian civilian = Civilians[i];
                if (!civilian.Pending) continue;

                if (civilian.Carried)
                {
                    civilian.X = Player.X;
                    civilian.Y = Player.Y;

                    if (IsExit(Player.CellX, Player.CellY))
                    {
                        civilian.Carried = false;
                        civilian.Rescued = true;
                        Player.CarryingCivilian = false;
                        JustRescued = civilian;
                    }

                    continue;
                }

                // 불이 덮친 시민은 더 이상 구조할 수 없다.
                int cx = (int)Math.Floor(civilian.X);
                int cy = (int)Math.Floor(civilian.Y);
                if (Vision.Visible(cx, cy)) civilian.Spotted = true;

                if (Grid.InBounds(cx, cy) && Grid[cx, cy].State == CellState.Burning)
                {
                    civilian.Lost = true;
                    JustLost = civilian;
                    continue;
                }

                // 불길이 닿기 전에도 연기로 잃을 수 있다. 그래야 순서가 의미를 갖는다.
                if (Grid.InBounds(cx, cy) && Grid[cx, cy].Smoke > 0f)
                {
                    civilian.Stamina -= Grid[cx, cy].Smoke * GameConfig.CivilianChokePerSecond * _lastDelta;
                    if (civilian.Stamina <= 0f)
                    {
                        civilian.Stamina = 0f;
                        civilian.Lost = true;
                        JustLost = civilian;
                        continue;
                    }
                }

                if (Player.CarryingCivilian) continue;

                float dx = civilian.X - Player.X;
                float dy = civilian.Y - Player.Y;
                float distance = (dx * dx) + (dy * dy);
                if (distance > PickupRadius * PickupRadius) continue;
                if (nearest >= 0f && distance >= nearest) continue;

                nearest = distance;
                RescueTarget = civilian;
            }

            if (!rescuePressed || RescueTarget == null || Player.CarryingCivilian) return;

            RescueTarget.Carried = true;
            Player.CarryingCivilian = true;
            JustPickedUp = RescueTarget;
        }

        /// <summary>
        /// 출구에서 철수를 선언한다. 시민을 막 내려놓은 프레임에는 받지 않는다 —
        /// 마지막 한 명을 내려놓으면서 같은 입력으로 철수까지 되면
        /// 고른 적도 없는데 판이 끝난다.
        /// </summary>
        private void TryWithdraw(bool rescuePressed)
        {
            if (!rescuePressed) return;
            if (JustRescued != null) return;
            if (!CanWithdraw) return;

            Withdrew = true;
        }

        private bool IsExit(int x, int y)
        {
            for (int i = 0; i < Exits.Count; i++)
            {
                if (Exits[i].X == x && Exits[i].Y == y) return true;
            }
            return false;
        }

        private void EvaluateOutcome()
        {
            // 패배 조건을 먼저 본다. 마지막 불을 끄면서 쓰러지는 경우
            // 승리로 처리되면 위험을 감수할 이유가 사라진다.
            if (!Player.IsAlive)
            {
                Outcome = StageOutcome.LostPlayerDown;
                return;
            }

            // 사람을 다 데리고 나왔으면 출구에서 철수할 수 있다.
            // 건물은 잃지만 그것이 소방의 판단이다.
            if (Withdrew)
            {
                Outcome = StageOutcome.Won;
                return;
            }

            // 건물 소실과 시간 초과는 그대로 패배다.
            // 이걸 "전원 생환했으면 승리"로 풀면 아무것도 안 하고 시계만 보내는 것이
            // 모든 현장의 공략이 되고, 장비를 살 이유도 불을 끌 이유도 사라진다.
            // 사람만 구하고 이기고 싶으면 <b>출구까지 걸어가 철수를 선언해야 한다</b>.
            if (Grid.IntactRatio() < BuildingLostThreshold)
            {
                Outcome = StageOutcome.LostBuildingDestroyed;
                return;
            }

            if (TimeLeft <= 0f)
            {
                Outcome = StageOutcome.LostTimeUp;
                return;
            }

            if (Grid.CountBurning() == 0 && PendingCivilianCount == 0)
            {
                Outcome = StageOutcome.Won;
            }
        }

        public StageResult BuildResult()
        {
            return new StageResult
            {
                StageId = Def.Id,
                BasePayout = Def.BasePayout,
                Won = Outcome == StageOutcome.Won,
                Rescued = RescuedCount,
                CiviliansTotal = Civilians.Count,
                IntactRatio = Grid.IntactRatio(),
                TimeLeft = TimeLeft,
                WetCellCount = Grid.CountWet(),
                BurningCells = Grid.CountBurning(),
                ShotsFired = ShotsFired,
                CellsExtinguished = CellsExtinguished,
                ElapsedSeconds = Def.TimeLimitSeconds - TimeLeft,
            };
        }
    }
}
