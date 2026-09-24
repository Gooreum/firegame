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

        public readonly StageDef Def;
        public readonly FireGrid Grid;
        public readonly FireSim Sim;
        public readonly PlayerState Player;
        public readonly List<Civilian> Civilians = new List<Civilian>();
        /// <summary>소화전 위치. 지도 장식이다(호스는 어디서나 쏜다).</summary>
        public readonly List<GridPoint> Hydrants = new List<GridPoint>();
        public readonly List<GridPoint> Exits = new List<GridPoint>();

        /// <summary>건물 구획. 실내·실외 판정과 연기 배출, 시야 차단에 쓴다.</summary>
        public readonly BuildingMap Buildings;

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
        {
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (def == null) throw new ArgumentNullException(nameof(def));

            Def = def;

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

            TimeLeft -= dt;
            if (TimeLeft < 0f) TimeLeft = 0f;

            Player.ActiveSlot = input.Slot;
            Player.Update(dt, Grid, input.MoveX, input.MoveY);

            if (input.Fire) TryFire(input.Slot);

            DoorAtHand = FindDoorAtHand();
            if (input.Interact) TryInteract();

            AdvanceSimulation(dt);
            UpdateCivilians(input.Rescue);
            EvaluateOutcome();
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
                ShotsFired = ShotsFired,
                CellsExtinguished = CellsExtinguished,
                ElapsedSeconds = Def.TimeLimitSeconds - TimeLeft,
            };
        }
    }
}
