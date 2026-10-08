using System;
using System.Collections.Generic;
using FireGame.Core.Sim;

namespace FireGame.Prototypes.Logic
{
    /// <summary>대원 한 명(샘플 items-e.js crew): 샘플 px 자리, 줄 자리 번호, 물 쏘기 쿨다운, 노린 요괴.</summary>
    public sealed class Crew
    {
        public float X, Y;
        public int Idx, Slot;
        public float Cd, AimAge, Born;
        public Enemy Aim;
        public float Face = 1f;
        public bool Moving;

        /// <summary>월드 자리(화면 3D 몸용).</summary>
        public Vec2 Pos
        {
            get { return SurvivorSim.FromS(X, Y); }
        }
    }

    /// <summary>구해 낸 사람(샘플 civ): 문에서 튀어나옴(0.45초) → 변신(0.3초) → 대원.</summary>
    public sealed class SCiv
    {
        public float X, Y, Z, Sx, Sy, Tx, Ty, Age;
        public int Idx;

        /// <summary>0 튀어나옴, 1 변신, 2 끝.</summary>
        public int State;
    }

    /// <summary>합류 연출(샘플 joinFx·banner): 실제 시간으로 흐른다.</summary>
    public sealed class SJoin
    {
        public float X, Y, Age;
        public int N;
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

        // --- 구조대원(샘플 items-e.js CrewScene) ---
        public const int MaxCrew = 8;

        /// <summary>샘플 CREW_SLOTS: 내 뒤(아래쪽) 반원 두 줄 [반지름 px, 각도].</summary>
        public static readonly float[,] CrewSlots =
        {
            { 30f, 0.55f }, { 30f, 2.59f }, { 32f, 1.25f }, { 32f, 1.89f },
            { 50f, 0.32f }, { 50f, 2.82f }, { 52f, 0.95f }, { 52f, 2.19f },
        };

        public readonly List<Crew> CrewList = new List<Crew>();
        public readonly List<SCiv> Civs = new List<SCiv>();
        public readonly List<SJoin> CrewJoins = new List<SJoin>();

        /// <summary>합류 배너(샘플 banner): null이면 없음.</summary>
        public SJoin CrewBanner;

        /// <summary>HUD 헬멧 칸 반짝임(합류 순간 1 → 0).</summary>
        public float CrewHudGlow;

        /// <summary>이번 틱 대원이 합류해 몇 명이 됐나(0이면 없음).</summary>
        public int JustCrewJoined;

        /// <summary>이번 틱 합류한 자리(월드).</summary>
        public Vec2 CrewJoinedAt;

        private int _rescuedCount;

        /// <summary>샘플 대원 물줄기 등급: clamp(1 + 대원/2, 1, 5).</summary>
        public int CrewTier
        {
            get { return Math.Max(1, Math.Min(5, 1 + (CrewList.Count / 2))); }
        }

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

        /// <summary>
        /// 샘플 rescue(door): 문에서 사람이 튀어나온다(별 16·고리·연기). 대원이 다 찼으면 false(예전처럼 뛰어 나간다).
        /// </summary>
        private bool JoinCrew(Vec2 door)
        {
            if (!Build.Free || CrewList.Count + Civs.Count >= MaxCrew) return false;
            float dx = SX(door), dy = SY(door);
            int idx = _rescuedCount++;
            float side = dx < PX ? -26f : 26f;
            Civs.Add(new SCiv { X = dx, Y = dy - 2f, Sx = dx, Sy = dy - 2f, Tx = dx + side, Ty = dy + 14f, Idx = idx });
            for (int i = 0; i < 16; i++)
            {
                float a = Rnd(0f, Tau), v = Rnd(60f, 150f);
                Star(dx, dy - 8f, (float)Math.Cos(a) * v, (float)Math.Sin(a) * v * 0.7f, Rnd(0.4f, 0.7f), Rnd(2.5f, 4f), new Rgb(255, 240, 190));
            }
            Ring(dx, dy, 0.35f, 30f, new Rgb(255, 230, 160), 3f);
            for (int i = 0; i < 6; i++)
            {
                SPart p = Part(SPartKind.Smoke, dx + Rnd(-6f, 6f), dy - 6f);
                p.Vx = Rnd(-20f, 20f);
                p.Vy = Rnd(-30f, -10f);
                p.Life = 0.6f;
                p.Size = Rnd(5f, 8f);
                p.Color = new Rgb(120, 110, 120);
                p.HasColor = true;
            }
            return true;
        }

        /// <summary>샘플 transform(c): 평상복 → 방화복. 합류 수가 늘수록 더 화려하다.</summary>
        private void CrewTransform(SCiv c)
        {
            int n = CrewList.Count + 1;
            float x = c.X, y = c.Y - 8f;
            bool gold = n >= 5, rb = n >= MaxCrew;
            Rgb col = rb ? Rgb.None : gold ? new Rgb(255, 215, 110) : new Rgb(255, 236, 180);
            CrewJoins.Add(new SJoin { X = x, Y = c.Y, N = n });
            for (int i = 0; i < Math.Min(4, 1 + (n / 2)); i++)
            {
                if (rb) Prism(x, c.Y, 0.45f + (i * 0.12f), 34f + (i * 18f) + (n * 3f));
                else Ring(x, c.Y, 0.45f + (i * 0.12f), 34f + (i * 18f) + (n * 3f), col, 5f - i);
            }
            Glow(x, y, 0.45f, 50f + (n * 7f), rb ? new Rgb(255, 240, 210) : new Rgb(255, 210, 120));
            int ns = 18 + (n * 10);
            for (int i = 0; i < ns; i++)
            {
                float a = Rnd(0f, Tau), v = Rnd(70f, 170f + (n * 15f));
                Star(x, y, (float)Math.Cos(a) * v, (float)Math.Sin(a) * v * 0.75f, Rnd(0.45f, 0.9f), Rnd(2.5f, 3.5f + (n * 0.35f)), col);
            }
            if (n >= 4)
            {
                for (int i = 0; i < (n - 3) * 14; i++)
                {
                    float a = Rnd(-(float)Math.PI, 0f), v = Rnd(100f, 230f);
                    SPart p = Part(SPartKind.Confetti, x, y - 8f);
                    p.Vx = (float)Math.Cos(a) * v;
                    p.Vy = (float)Math.Sin(a) * v;
                    p.Life = Rnd(0.9f, 1.5f);
                    p.Size = Rnd(1.8f, 3f);
                    p.Rot = Rnd(0f, Tau);
                    p.Vr = Rnd(-8f, 8f);
                    if (!rb)
                    {
                        p.Color = Rand() < 0.5f ? new Rgb(255, 140, 40) : new Rgb(255, 215, 110);
                        p.HasColor = true;
                    }
                }
            }
            // 둘레 요괴를 밀어낸다.
            foreach (Enemy m in SAlive().ToArray())
            {
                float mx = SX(m.Pos), my = SY(m.Pos);
                float d = Hypot(mx - x, my - y);
                if (d < 50f + (n * 6f))
                {
                    float l = d == 0f ? 1f : d;
                    SHit(m, 1f, (mx - x) / l * 220f, (my - y) / l * 220f);
                }
            }
            SShake = Math.Max(SShake, 2.5f + (n * 0.7f));
            SFlash = Math.Max(SFlash, 0.12f + (n * 0.035f));
            SFlashColor = rb ? new Rgb(255, 250, 240) : new Rgb(255, 235, 190);
            SStop = Math.Max(SStop, 0.03f + (n * 0.008f));
            SSlow = Math.Max(SSlow, 0.25f + (n * 0.05f));
            CrewBanner = new SJoin { N = n };
            CrewHudGlow = 1f;
            CrewList.Add(new Crew { X = c.X, Y = c.Y, Idx = c.Idx, Slot = CrewList.Count, Cd = Rnd(0.2f, 0.5f), Born = ST });
            JustCrewJoined = n;
            CrewJoinedAt = FromS(c.X, c.Y);
        }

        /// <summary>샘플 대원 줄 자리(px).</summary>
        public void CrewSlotS(int slot, out float tx, out float ty)
        {
            float r = CrewSlots[slot, 0], a = CrewSlots[slot, 1];
            tx = PX + ((float)Math.Cos(a) * r);
            ty = PY + 6f + ((float)Math.Sin(a) * r * 0.62f);
        }

        private void TickCrew()
        {
            float real = SSlow > 0f ? Dt * 4f : Dt;
            // 구한 사람: 튀어나옴(0.45초) → 변신(0.3초) → 대원.
            for (int i = 0; i < Civs.Count; i++)
            {
                SCiv c = Civs[i];
                c.Age += Dt;
                if (c.State == 0)
                {
                    float u = Ease(c.Age / 0.45f);
                    c.X = Lerp(c.Sx, c.Tx, u);
                    c.Y = Lerp(c.Sy, c.Ty, u);
                    c.Z = (float)Math.Sin(Math.Min(1f, c.Age / 0.45f) * Math.PI) * 22f;
                    if (c.Age >= 0.45f)
                    {
                        c.State = 1;
                        c.Age = 0f;
                        c.Z = 0f;
                    }
                }
                else if (c.State == 1)
                {
                    if (Rand() < 0.6f)
                    {
                        SPart p = Star(c.X + Rnd(-8f, 8f), c.Y - Rnd(0f, 24f), 0f, -30f, 0.4f, 2.5f, new Rgb(255, 250, 220));
                        p.Vx = 0f;
                    }
                    if (c.Age >= 0.3f)
                    {
                        c.State = 2;
                        CrewTransform(c);
                    }
                }
            }
            Civs.RemoveAll(c => c.State == 2);

            // 대원: 자리로 달려가 따라오고, 가까운 요괴에 물을 쏜다.
            int n = CrewList.Count;
            int lvl = CrewTier;
            foreach (Crew c in CrewList)
            {
                CrewSlotS(c.Slot, out float tx, out float ty);
                float ddx = tx - c.X, ddy = ty - c.Y, dl = Hypot(ddx, ddy);
                float sp = ST - c.Born < 1.2f ? 260f : 200f;
                if (dl > 2f)
                {
                    float v = Math.Min(dl, sp * Dt);
                    c.X += ddx / dl * v;
                    c.Y += ddy / dl * v;
                    c.Moving = true;
                }
                else c.Moving = false;
                c.Cd -= Dt;
                c.AimAge -= Dt;
                if (c.Cd <= 0f)
                {
                    Enemy m = SNearest(c.X, c.Y, 125f + (n * 3f));
                    if (m != null)
                    {
                        c.Aim = m;
                        c.AimAge = 0.26f;
                        c.Cd = 0.42f - (n * 0.015f);
                        float mx = SX(m.Pos), my = SY(m.Pos);
                        SHit(m, 1.5f, (mx - c.X) * 0.6f, (my - c.Y) * 0.6f, HitSource.Crew);
                        HitFx(mx, my - 4f, lvl, 0.55f);
                    }
                    else c.Cd = 0.15f;
                }
                if (c.Aim != null && c.AimAge <= 0f) c.Aim = null;
                float fx = c.Aim != null && !c.Aim.Dead ? SX(c.Aim.Pos) - c.X : ddx;
                if (Math.Abs(fx) > 1f) c.Face = fx > 0f ? 1f : -1f;
            }
            foreach (SJoin j in CrewJoins) j.Age += real;
            CrewJoins.RemoveAll(j => j.Age >= 1.6f);
            if (CrewBanner != null)
            {
                CrewBanner.Age += real;
                if (CrewBanner.Age > 1.5f) CrewBanner = null;
            }
            CrewHudGlow = Math.Max(0f, CrewHudGlow - (real * 1.2f));
        }
    }
}
