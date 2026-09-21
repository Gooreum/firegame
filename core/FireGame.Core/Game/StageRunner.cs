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

        public float TimeLeft;
        public StageOutcome Outcome = StageOutcome.InProgress;

        /// <summary>지금까지 실행한 고정 틱 수. 프레임레이트 독립성 검증에 쓴다.</summary>
        public int TicksElapsed;

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
            Sim = new FireSim(Grid) { Wind = def.Wind };
            Hydrants.AddRange(map.Hydrants);
            Exits.AddRange(map.Exits);

            foreach (GridPoint point in map.Civilians)
            {
                Civilians.Add(new Civilian { X = point.X + 0.5f, Y = point.Y + 0.5f });
            }

            Player = new PlayerState();
            Player.Spawn(map.PlayerSpawn);
            Player.SuitLevel = loadout.SuitLevel;
            Player.DamageMultiplier = GearStats.DamageMultiplier(loadout.SuitLevel);
            Player.SpeedMultiplier = GearStats.SpeedMultiplier(loadout.BootsLevel);
            EquipLoadout(loadout.Equipment);

            TimeLeft = def.TimeLimitSeconds;
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

            TimeLeft -= dt;
            if (TimeLeft < 0f) TimeLeft = 0f;

            Player.ActiveSlot = input.Slot;
            Player.Update(dt, Grid, input.MoveX, input.MoveY);

            if (input.Fire) TryFire(input.Slot);

            AdvanceSimulation(dt);
            UpdateCivilians();
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
                TicksElapsed++;
            }
        }

        private void TryFire(int slot)
        {
            if (slot < 0 || slot >= PlayerState.SlotCount) return;

            EquipmentDef def = SlotEquipment(slot);
            if (def == null) return;
            if (!Player.CanFire(slot, def)) return;

            Aiming.Resolve(Grid, Player.CellX, Player.CellY, Player.Aim, def.Pattern, def.Range, _hitBuffer);

            for (int i = 0; i < _hitBuffer.Count; i++)
            {
                Suppression.Apply(Grid, _hitBuffer[i].X, _hitBuffer[i].Y, def.Agent);
            }

            Player.ConsumeFire(slot, def);
        }

        private void UpdateCivilians()
        {
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
                    }

                    continue;
                }

                // 불이 덮친 시민은 더 이상 구조할 수 없다.
                int cx = (int)Math.Floor(civilian.X);
                int cy = (int)Math.Floor(civilian.Y);
                if (Grid.InBounds(cx, cy) && Grid[cx, cy].State == CellState.Burning)
                {
                    civilian.Lost = true;
                    continue;
                }

                if (Player.CarryingCivilian) continue;

                float dx = civilian.X - Player.X;
                float dy = civilian.Y - Player.Y;
                if ((dx * dx) + (dy * dy) <= PickupRadius * PickupRadius)
                {
                    civilian.Carried = true;
                    Player.CarryingCivilian = true;
                }
            }
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
            };
        }
    }
}
