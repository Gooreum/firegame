using System;
using System.Collections.Generic;
using FireGame.Core.Sim;

namespace FireGame.Prototypes.Logic
{
    /// <summary>대원 한 명(숲 개편: 구한 사람이 방화복 대원이 되어 따라오며 물을 쏜다).</summary>
    public sealed class Crew
    {
        public Vec2 Pos;
        public float Cool;

        /// <summary>이번 틱 물을 쏘고 있는 곳(화면용). null이면 쉬는 중.</summary>
        public Vec2? Target;

        /// <summary>합류 후 시간(변신 연출).</summary>
        public float Age;
    }

    /// <summary>
    /// 숲 개편(2026-10-08). Build.Free일 때만 돈다. 무기·보조의 레벨별 동작과 레벨업은 승인 샘플을 그대로 옮긴 SampleCore·SampleItems가 맡고,
    /// 여기는 구조대원(구한 사람이 대원이 되어 따라오며 물을 쏜다)과 숲 틱 순서만 둔다.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        // --- 레벨업(화면이 고른 카드를 안다) ---
        public UpgradeId? JustLeveledItem;
        public int JustLeveledTo;

        /// <summary>불사조 방화복(샘플 suit Lv6)이 쓰러짐을 막을 때 부른다. true면 이번 틱은 쓰러지지 않는다.</summary>
        public Func<bool> SampleRevive;

        // --- 구조대원 ---
        public const int MaxCrew = 8;
        public const float CrewRange = 8f;
        public const float CrewEvery = 0.5f;
        public const float CrewHit = 4f;
        public const float CrewSpeed = 7f;
        public const float CrewSoakRange = 2.5f;
        public readonly List<Crew> CrewList = new List<Crew>();

        /// <summary>이번 틱 대원이 합류해 몇 명이 됐나(0이면 없음).</summary>
        public int JustCrewJoined;

        /// <summary>이번 틱 합류한 대원이 나온 문(화면용).</summary>
        public Vec2 CrewJoinedAt;

        private void ClearFreeSignals()
        {
            JustLeveledItem = null;
            JustCrewJoined = 0;
        }

        /// <summary>FireWeapons 끝에서(숲): 샘플 장면·아이템 → 구조대원.</summary>
        private void TickFree()
        {
            TickSample();
            TickCrew();
        }

        /// <summary>체력 0: 샘플 불사조 방화복이 막을 수 있다.</summary>
        private void TryPhoenix()
        {
            if (Hp > 0f || !Build.Free || SampleRevive == null) return;
            SampleRevive();
        }

        /// <summary>대원 물줄기 대상: 가장 가까운 살아 있는 몹.</summary>
        private Enemy NearestAlive(Vec2 at, float radius, Enemy skip)
        {
            Enemy best = null;
            float bestD = radius;
            foreach (Enemy e in Enemies)
            {
                if (e.Dead || e == skip || e.Held || e.AirZ > 0f) continue;
                float d = e.Pos.DistanceTo(at);
                if (d <= bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        /// <summary>구한 사람이 대원이 된다(8명까지). 다 찼으면 false(예전처럼 뛰어 나간다).</summary>
        private bool JoinCrew(Vec2 door)
        {
            if (!Build.Free || CrewList.Count >= MaxCrew) return false;
            CrewList.Add(new Crew { Pos = door, Cool = CrewEvery });
            JustCrewJoined = CrewList.Count;
            CrewJoinedAt = door;
            return true;
        }

        /// <summary>i번째 대원의 자리: 내 뒤 반원(앞 줄 5, 뒷줄 3).</summary>
        public Vec2 CrewSlot(int i)
        {
            float face = (float)Math.Atan2(Facing.Y, Facing.X);
            int row = i < 5 ? 0 : 1;
            int inRow = row == 0 ? Math.Min(5, CrewList.Count) : CrewList.Count - 5;
            int k = row == 0 ? i : i - 5;
            float spread = 1.25f * (inRow <= 1 ? 0f : 1f);
            float a = face + (float)Math.PI + (inRow <= 1 ? 0f : -spread + (2f * spread * k / (inRow - 1)));
            float r = 2.2f + (row * 1.3f);
            return new Vec2(Player.X + ((float)Math.Cos(a) * r), Player.Y + ((float)Math.Sin(a) * r));
        }

        private void TickCrew()
        {
            for (int i = 0; i < CrewList.Count; i++)
            {
                Crew c = CrewList[i];
                c.Age += Dt;
                Vec2 slot = CrewSlot(i);
                float d = c.Pos.DistanceTo(slot);
                float step = CrewSpeed * Build.SpeedScale * Dt;
                if (d > step) c.Pos = new Vec2(c.Pos.X + ((slot.X - c.Pos.X) / d * step), c.Pos.Y + ((slot.Y - c.Pos.Y) / d * step));
                else c.Pos = slot;

                Enemy target = NearestAlive(c.Pos, CrewRange, null);
                c.Target = target?.Pos;
                if (target == null)
                {
                    // 몹이 없으면 가까운 타는 건물을 적신다.
                    foreach (Structure s in Structures)
                    {
                        if (!s.Burning || s.Collapsed || s.DistanceTo(c.Pos) > CrewSoakRange) continue;
                        c.Target = s.Pos;
                        Soak(s, 0.35f * Dt, false);
                        break;
                    }
                }
                c.Cool -= Dt;
                if (target == null || c.Cool > 0f) continue;
                c.Cool = CrewEvery;
                Damage(target, CrewHit, Knockback(c.Pos, target.Pos, 1.5f * KnockScale(target)), true, HitSource.Crew, c.Pos);
            }
        }
    }
}
