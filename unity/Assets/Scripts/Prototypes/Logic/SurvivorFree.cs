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

    /// <summary>보조 물줄기 한 줄(화면용): 플레이어에서 노린 요괴까지.</summary>
    public struct AuxStream
    {
        public Vec2 From;
        public Vec2 To;
    }

    /// <summary>
    /// 숲 개편(2026-10-08, 사용자가 승인한 레벨업 샘플 그대로). Build.Free일 때만 돈다.
    /// 레벨별 동작을 샘플 문구에 맞추고(물대포 1→5줄기, 장화 밀치기, 방화복 물결), 보조 Lv6 셋, 레벨업 밀치기, 구조대원.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        // --- 숲 레벨 표(샘플 lvText). 다른 스테이지는 SurvivorWeapons의 원래 표 ---
        public static readonly int[] BalloonCountFree = { 0, 1, 2, 2, 3, 3 };
        public static readonly int[] BalloonBouncesFree = { 0, 4, 5, 6, 7, 7 };
        public static readonly int[] BubbleCountFree = { 0, 1, 2, 3, 3, 4 };
        public static readonly int[] GeyserCountFree = { 0, 1, 2, 2, 3, 4 };

        private int[] Table(int[] old, int[] free)
        {
            return Build.Free ? free : old;
        }

        // --- 물대포 보조 물줄기 ---
        public const float AuxRange = 6f;
        public const float AuxEvery = 0.25f;
        public const float AuxHit = 2.4f;
        public const float AuxSplash = 1.2f;

        /// <summary>이번 틱 보조 물줄기들(화면용). 쏘는 동안 매 틱 채운다.</summary>
        public readonly List<AuxStream> HoseAux = new List<AuxStream>();

        /// <summary>보조 물줄기 끝 물보라(Lv5)·펌프 폭발·장화 물보라 자리(화면용).</summary>
        public readonly List<Vec2> FreeSplashes = new List<Vec2>();

        private float _auxClock;
        private readonly List<Enemy> _auxNear = new List<Enemy>();
        private readonly List<Enemy> _auxTaken = new List<Enemy>();

        /// <summary>물대포 줄기 수: 물대포 레벨(방수포면 5) + 고압 펌프 Lv4부터 하나 더.</summary>
        public int HoseStreams
        {
            get
            {
                int lv = Build.PowerOf(UpgradeId.Hose);
                if (lv <= 0) return 0;
                return lv + (Build.PowerOf(UpgradeId.Tank) >= 4 ? 1 : 0);
            }
        }

        // --- 장화·방화복 ---
        public const float BootsBumpCool = 0.35f;
        public const float SuitRippleHit = 3f;

        /// <summary>방화복 물결이 퍼진 자리와 반경(화면용).</summary>
        public readonly List<Vec2> SuitRipples = new List<Vec2>();
        public float SuitRippleRadius
        {
            get { return 2f + (0.3f * Build.PowerOf(UpgradeId.Suit)); }
        }

        private float _rippleClock;
        private Vec2 _lastPlayer;
        public bool Running;

        // --- 보조 Lv6 ---
        public const float PumpEvery = 4f;
        public const float PumpRadius = 4f;
        public const float PumpHit = 20f;
        public const float JetDrop = 0.1f;
        public const float JetLife = 1.6f;
        public const float JetDps = 12f;
        public const float JetWidth = 0.7f;
        public const float PhoenixHp = 0.6f;
        public const float PhoenixRadius = 5f;
        public const float PhoenixHit = 40f;

        /// <summary>초고압 펌프가 이번 틱 터졌다(화면용).</summary>
        public bool JustPumpNova;

        /// <summary>제트 장화 물길(점마다 남은 시간).</summary>
        public readonly List<JetSpot> JetTrail = new List<JetSpot>();

        public struct JetSpot
        {
            public Vec2 Pos;
            public float Life;
        }

        /// <summary>불사조 방화복으로 이번 틱 되살아났다.</summary>
        public bool JustPhoenix;
        public bool PhoenixUsed;

        private float _pumpClock = PumpEvery;
        private float _jetClock2;

        // --- 레벨업 ---
        /// <summary>이번 틱 고른 카드와 그 카드의 새 레벨(최고급 = 6). 화면이 레벨업 연출을 고른다.</summary>
        public UpgradeId? JustLeveledItem;
        public int JustLeveledTo;

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
            HoseAux.Clear();
            FreeSplashes.Clear();
            SuitRipples.Clear();
            JustPumpNova = false;
            JustPhoenix = false;
            JustLeveledItem = null;
            JustCrewJoined = 0;
        }

        /// <summary>FireWeapons 끝에서: 숲 전용 동작.</summary>
        private void TickFree()
        {
            if (!Build.Free) return;
            Running = Player.DistanceTo(_lastPlayer) > 0.02f;
            _lastPlayer = Player;
            TickHoseAux();
            TickBootsBump();
            TickSuitRipple();
            TickOverPump();
            TickJetBoots();
            TickCrew();
        }

        /// <summary>
        /// 물대포 보조 물줄기(샘플: 레벨 = 물줄기 수). 주 물줄기는 겨눈 쪽, 나머지는 6칸 안 가까운 요괴를 하나씩 따로 노린다.
        /// 쥐고 있지 않아도 쏜다(샘플의 자동 물줄기). Lv3부터 뒤의 한 마리까지 꿰뚫고, Lv5는 끝에서 물보라가 터진다.
        /// </summary>
        private void TickHoseAux()
        {
            int aux = HoseStreams - 1;
            if (aux <= 0) return;
            int lv = Build.PowerOf(UpgradeId.Hose);
            Around(Player, AuxRange, _auxNear);
            _auxNear.RemoveAll(e => e.Dead);
            _auxNear.Sort((a, b) => a.Pos.DistanceTo(Player).CompareTo(b.Pos.DistanceTo(Player)));
            _auxTaken.Clear();
            for (int i = 0; i < _auxNear.Count && _auxTaken.Count < aux; i++) _auxTaken.Add(_auxNear[i]);
            foreach (Enemy e in _auxTaken) HoseAux.Add(new AuxStream { From = Player, To = e.Pos });

            _auxClock -= Dt;
            if (_auxClock > 0f) return;
            _auxClock = AuxEvery;
            float hit = AuxHit * (1f + (0.25f * (lv - 1))) * Build.HosePower;
            foreach (Enemy e in _auxTaken)
            {
                Vec2 dir = Knockback(Player, e.Pos, 1.5f);
                Damage(e, hit, dir, true, HitSource.Hose, Player);
                if (lv >= 3)
                {
                    // 꿰뚫기: 같은 방향 1.5칸 뒤의 한 마리.
                    Vec2 behind = new Vec2(e.Pos.X + (dir.X * 1f), e.Pos.Y + (dir.Y * 1f));
                    Enemy back = NearestAlive(behind, 0.9f, e);
                    if (back != null) Damage(back, hit * 0.7f, dir, true, HitSource.Hose, Player);
                }
                if (lv >= 5) Splash(e.Pos, AuxSplash, hit * 0.6f);
            }
        }

        private Enemy NearestAlive(Vec2 at, float radius, Enemy skip)
        {
            Enemy best = null;
            float bestD = radius;
            foreach (Enemy e in Enemies)
            {
                if (e.Dead || e == skip) continue;
                float d = e.Pos.DistanceTo(at);
                if (d <= bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        private readonly List<Enemy> _splashNear = new List<Enemy>();

        /// <summary>반경 안의 살아 있는 몹(목록을 직접 훑는다: 카드 선택·Sweep 뒤엔 공간 해시가 낡았다).</summary>
        private void Around(Vec2 at, float radius, List<Enemy> into)
        {
            into.Clear();
            foreach (Enemy e in Enemies)
            {
                if (!e.Dead && e.Pos.DistanceTo(at) <= radius + e.Radius) into.Add(e);
            }
        }

        /// <summary>둘레 물보라: 반경 안 몹에 피해 + 바깥으로 밀치기. 화면용 자리를 남긴다.</summary>
        private void Splash(Vec2 at, float radius, float damage, float push = 2f, HitSource source = HitSource.Hose)
        {
            FreeSplashes.Add(at);
            Around(at, radius, _splashNear);
            foreach (Enemy e in _splashNear)
            {
                if (e.Dead) continue;
                Damage(e, damage, Knockback(at, e.Pos, push * KnockScale(e)), true, source, at);
            }
        }

        /// <summary>장화: 달리는 몸에 닿은 몹을 밀친다(Lv마다 세게). Lv5는 부딪힌 자리에 물보라.</summary>
        private void TickBootsBump()
        {
            int lv = Build.PowerOf(UpgradeId.Boots);
            if (lv <= 0 || !Running) return;
            Around(Player, PlayerRadius + 0.35f, _splashNear);
            foreach (Enemy e in _splashNear)
            {
                if (e.Dead || e.BounceCool > 0f) continue;
                e.BounceCool = BootsBumpCool;
                Vec2 k = Knockback(Player, e.Pos, (3f + lv) * KnockScale(e));
                e.Knock.X += k.X;
                e.Knock.Y += k.Y;
                SuitBounces.Add(e.Pos);
                if (lv >= 5) Splash(e.Pos, 1.2f, 4f);
            }
        }

        /// <summary>방화복 Lv3부터: 몇 초마다 보호막에서 물결이 퍼져 둘레 몹을 밀치고 약하게 친다.</summary>
        private void TickSuitRipple()
        {
            int lv = Build.PowerOf(UpgradeId.Suit);
            if (lv < 3) return;
            _rippleClock -= Dt;
            if (_rippleClock > 0f) return;
            _rippleClock = 3.5f - (0.4f * (lv - 3));
            SuitRipples.Add(Player);
            Splash(Player, SuitRippleRadius, SuitRippleHit, 4f);
        }

        /// <summary>초고압 펌프: 4초마다 내 둘레 물 대폭발.</summary>
        private void TickOverPump()
        {
            if (Build.Level(UpgradeId.OverPump) <= 0) return;
            _pumpClock -= Dt;
            if (_pumpClock > 0f) return;
            _pumpClock = PumpEvery;
            JustPumpNova = true;
            Splash(Player, PumpRadius, PumpHit, 7f);
            foreach (Structure s in Structures)
            {
                if (s.Burning && s.DistanceTo(Player) <= PumpRadius) Soak(s, 1.5f, false);
            }
        }

        /// <summary>제트 장화: 달리는 동안 0.1초마다 물길 점을 남긴다. 그 위의 몹은 녹는다.</summary>
        private void TickJetBoots()
        {
            for (int i = JetTrail.Count - 1; i >= 0; i--)
            {
                JetSpot j = JetTrail[i];
                j.Life -= Dt;
                if (j.Life <= 0f) JetTrail.RemoveAt(i);
                else JetTrail[i] = j;
            }
            if (Build.Level(UpgradeId.JetBoots) <= 0) return;
            _jetClock2 -= Dt;
            if (Running && _jetClock2 <= 0f)
            {
                _jetClock2 = JetDrop;
                JetTrail.Add(new JetSpot { Pos = Player, Life = JetLife });
            }
            foreach (Enemy e in Enemies)
            {
                if (e.Dead) continue;
                foreach (JetSpot j in JetTrail)
                {
                    if (e.Pos.DistanceTo(j.Pos) > JetWidth) continue;
                    Damage(e, JetDps * Dt, new Vec2(0f, 0f), false, HitSource.Hose, j.Pos);
                    break;
                }
            }
        }

        /// <summary>불사조 방화복: 체력이 0이 된 순간 한 번 되살아나며 둘레를 터뜨린다.</summary>
        private void TryPhoenix()
        {
            if (Hp > 0f || PhoenixUsed || Build.Level(UpgradeId.PhoenixSuit) <= 0) return;
            PhoenixUsed = true;
            JustPhoenix = true;
            Hp = MaxHp * PhoenixHp;
            Splash(Player, PhoenixRadius, PhoenixHit, 9f);
        }

        /// <summary>
        /// 레벨업 밀치기(샘플 doBurst): 고른 순간 둘레 몹을 밀어낸다. 반경·힘이 레벨마다 크고, 최고급(Lv6)은 그 안의 몹을 쓰러뜨린다.
        /// </summary>
        private void LevelBurst(UpgradeId id)
        {
            int lv = Loadout.IsEvolution(id) ? 6 : Build.Level(id);
            JustLeveledItem = id;
            JustLeveledTo = lv;
            float radius = 2.5f + (0.6f * lv);
            Around(Player, radius, _splashNear);
            foreach (Enemy e in _splashNear)
            {
                if (e.Dead) continue;
                Vec2 k = Knockback(Player, e.Pos, (6f + (1.5f * lv)) * KnockScale(e));
                if (lv >= 6) Damage(e, e.Hp + 1f, k, true, HitSource.Hose, Player);
                else
                {
                    e.Knock.X += k.X;
                    e.Knock.Y += k.Y;
                }
            }
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
