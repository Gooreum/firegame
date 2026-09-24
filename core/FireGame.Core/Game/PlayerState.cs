using System;
using System.Collections.Generic;
using FireGame.Core.Grid;

namespace FireGame.Core.Game
{
    /// <summary>
    /// 소방관 한 명의 상태. 좌표는 셀 단위 실수라 격자보다 부드럽게 움직인다.
    /// </summary>
    public sealed class PlayerState
    {
        /// <summary>
        /// 장비 슬롯 수. 장비 종류(4)와 같아야 한다 — 칸이 모자라면 나중에 산 장비가
        /// 현장에 안 나온다(3칸이던 때 폼 소화기가 빠져 주유소를 끌 수 없었다).
        /// </summary>
        public const int SlotCount = 4;

        public float X;
        public float Y;
        public float Hp = GameConfig.PlayerMaxHp;
        public AimDirection Aim = AimDirection.E;

        /// <summary>슬롯별 장비 id. -1은 빈 슬롯.</summary>
        public readonly int[] Slots = { -1, -1, -1, -1 };

        /// <summary>충전량 방식 장비의 남은 횟수.</summary>
        public readonly int[] Charges = new int[SlotCount];

        /// <summary>슬롯별 남은 쿨다운(초).</summary>
        public readonly float[] Cooldowns = new float[SlotCount];

        public int ActiveSlot;

        /// <summary>소방화 레벨에 따른 이동 속도 배율.</summary>
        public float SpeedMultiplier = 1f;

        /// <summary>방화복 레벨에 따른 불 피해 배율.</summary>
        public float DamageMultiplier = 1f;

        /// <summary>방화복 레벨. 화면이 옷 그림을 고르는 데 쓴다.</summary>
        public int SuitLevel;

        /// <summary>업고 있는 시민이 있는지. 있으면 이동이 느려진다.</summary>
        public bool CarryingCivilian;

        /// <summary>마지막으로 피해를 받은 뒤 경과한 시간.</summary>
        public float TimeSinceDamage = float.MaxValue;

        /// <summary>
        /// 등에 진 물. 물 계열 장비가 한 발마다 소모한다.
        /// 소화전과 소방차 곁에서만 채운다.
        /// </summary>
        public float Water = GameConfig.WaterTankMax;

        /// <summary>남은 물의 비율 0..1. HUD 게이지가 쓴다.</summary>
        public float WaterRatio
        {
            get { return Water / GameConfig.WaterTankMax; }
        }

        public int CellX
        {
            get { return (int)Math.Floor(X); }
        }

        public int CellY
        {
            get { return (int)Math.Floor(Y); }
        }

        public bool IsAlive
        {
            get { return Hp > 0f; }
        }

        public void Spawn(GridPoint point)
        {
            // 셀 중앙에 세운다.
            X = point.X + 0.5f;
            Y = point.Y + 0.5f;
        }

        /// <summary>
        /// 슬롯에 장비를 장착하고 충전량을 가득 채운다.
        /// </summary>
        public void Equip(int slot, EquipmentDef def)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));

            Slots[slot] = def == null ? -1 : def.Id;
            Charges[slot] = def != null && def.Resource == ResourceKind.Charges ? def.MaxCharges : 0;
            Cooldowns[slot] = 0f;
        }

        /// <summary>
        /// 한 프레임 갱신. 이동 입력은 정규화된 방향(-1..1)으로 받는다.
        /// </summary>
        public void Update(float dt, FireGrid grid, float inputX, float inputY)
        {
            if (dt <= 0f) return;

            TickCooldowns(dt);

            if (!IsAlive) return;

            Move(dt, grid, inputX, inputY);
            ApplyFireDamage(dt, grid);
            ApplySmoke(dt, grid);
            ApplyRegen(dt, grid);
        }

        private void TickCooldowns(float dt)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (Cooldowns[i] <= 0f) continue;

                Cooldowns[i] -= dt;
                if (Cooldowns[i] < 0f) Cooldowns[i] = 0f;
            }
        }

        private void Move(float dt, FireGrid grid, float inputX, float inputY)
        {
            if (grid == null) return;
            if (inputX == 0f && inputY == 0f) return;

            float length = (float)Math.Sqrt((inputX * inputX) + (inputY * inputY));
            if (length > 1f)
            {
                inputX /= length;
                inputY /= length;
            }

            float speed = GameConfig.PlayerSpeed * SpeedMultiplier;
            if (CarryingCivilian) speed *= GameConfig.CarrySpeedMultiplier;

            float stepX = inputX * speed * dt;
            float stepY = inputY * speed * dt;

            // 축을 나눠서 처리해야 벽에 비스듬히 부딪쳤을 때
            // 완전히 멈추지 않고 벽을 따라 미끄러진다.
            if (IsWalkable(grid, X + stepX, Y)) X += stepX;
            if (IsWalkable(grid, X, Y + stepY)) Y += stepY;

            UpdateAim(inputX, inputY);
        }

        /// <summary>이동 방향을 8방위 조준으로 환산한다.</summary>
        private void UpdateAim(float inputX, float inputY)
        {
            if (inputX == 0f && inputY == 0f) return;

            // atan2 기준 각도를 8방위로 양자화한다. N이 0번이고 시계방향이다.
            double angle = Math.Atan2(inputX, -inputY);
            int octant = (int)Math.Round(angle / (Math.PI / 4.0));
            if (octant < 0) octant += 8;
            Aim = (AimDirection)(octant % 8);
        }

        private static bool IsWalkable(FireGrid grid, float x, float y)
        {
            int cellX = (int)Math.Floor(x);
            int cellY = (int)Math.Floor(y);

            if (!grid.InBounds(cellX, cellY)) return false;

            // 닫힌 문은 지나갈 수 없다. 열고 들어가야 한다 —
            // 그래야 "닫아 두고 간다"가 나중에 돌아올 길을 막는 대가를 치른다.
            return Materials.Of(grid[cellX, cellY].Material).Walkable && !grid[cellX, cellY].Shut;
        }

        private void ApplyFireDamage(float dt, FireGrid grid)
        {
            int cx = CellX;
            int cy = CellY;
            if (!grid.InBounds(cx, cy)) return;

            // 불 위에 서 있는 쪽이 훨씬 아프다.
            if (grid[cx, cy].State == CellState.Burning)
            {
                Damage(GameConfig.FireDamageInCell * DamageMultiplier * dt);
                return;
            }

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (!grid.InBounds(nx, ny)) continue;

                    if (grid[nx, ny].State == CellState.Burning)
                    {
                        Damage(GameConfig.FireDamageAdjacent * DamageMultiplier * dt);
                        return;
                    }
                }
            }
        }

        private void Damage(float amount)
        {
            Hp -= amount;
            if (Hp < 0f) Hp = 0f;
            TimeSinceDamage = 0f;
        }

        /// <summary>
        /// 연기 흡입. 불길에 닿지 않아도 연기 속에 오래 있으면 쓰러진다.
        /// 불은 피해 다닐 수 있지만 연기는 방을 통째로 채우므로,
        /// 이게 있어야 "어디까지 들어갈까"가 실제 판단이 된다.
        /// </summary>
        private void ApplySmoke(float dt, FireGrid grid)
        {
            int cx = CellX;
            int cy = CellY;
            if (!grid.InBounds(cx, cy)) return;

            float smoke = grid[cx, cy].Smoke;
            if (smoke <= GameConfig.SmokeChokeThreshold) return;

            // 임계 위에서부터 물린다. 농도에 그냥 비례시켰더니 마당에 떠도는
            // 옅은 연무(0.05)까지 체력을 갉아, 밖에 서 있어도 회복이 안 됐다.
            // 옅은 연기가 뺏는 것은 숨 돌릴 틈이고, 체력을 뺏는 것은 짙은 연기다.
            float bite = (smoke - GameConfig.SmokeChokeThreshold) / (1f - GameConfig.SmokeChokeThreshold);

            Damage(GameConfig.SmokeDamagePerSecond * bite * DamageMultiplier * dt);
        }

        /// <summary>불에서 충분히 떨어져 숨을 돌리면 서서히 회복한다.</summary>
        private void ApplyRegen(float dt, FireGrid grid)
        {
            if (TimeSinceDamage < float.MaxValue) TimeSinceDamage += dt;
            if (TimeSinceDamage < GameConfig.RegenDelaySeconds) return;
            if (Hp >= GameConfig.PlayerMaxHp) return;

            // 연기 속에서는 숨을 못 돌린다. 이게 없으면 물러났다 오는 것만으로
            // 언제나 풀피가 되어 건물 안이 위험하지 않다.
            int cx = CellX;
            int cy = CellY;
            if (grid.InBounds(cx, cy) && grid[cx, cy].Smoke > GameConfig.SmokeChokeThreshold) return;

            Hp += GameConfig.RegenPerSecond * dt;
            if (Hp > GameConfig.PlayerMaxHp) Hp = GameConfig.PlayerMaxHp;
        }

        /// <summary>
        /// 지금 이 슬롯을 쏠 수 있는지. 쿨다운·충전량을 한 곳에서 판단한다.
        /// </summary>
        public bool CanFire(int slot, EquipmentDef def)
        {
            if (!IsAlive) return false;
            if (def == null) return false;
            if (slot < 0 || slot >= SlotCount) return false;
            if (Slots[slot] != def.Id) return false;
            if (Cooldowns[slot] > 0f) return false;

            if (def.Resource == ResourceKind.Charges && Charges[slot] <= 0) return false;
            if (def.WaterCost > 0f && Water < def.WaterCost) return false;

            return true;
        }

        /// <summary>발사 자원을 소모한다. <see cref="CanFire"/> 통과 후에 호출한다.</summary>
        public void ConsumeFire(int slot, EquipmentDef def)
        {
            Cooldowns[slot] = def.CooldownSeconds;

            if (def.Resource == ResourceKind.Charges) Charges[slot]--;

            Water -= def.WaterCost;
            if (Water < 0f) Water = 0f;
        }
    }
}
