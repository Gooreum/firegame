using System;
using System.Collections.Generic;
using FireGame.Core.Game;
using FireGame.Core.Grid;
using FireGame.Core.Sim;

namespace FireGame.UnityLayer.Feel
{
    /// <summary>한 프레임에 낼 소리 하나.</summary>
    public enum Cue : byte
    {
        SprayWater,
        SprayFoam,
        SprayGas,

        /// <summary>불이 꺼졌다 — "칙".</summary>
        PutOut,

        /// <summary>물을 기름·전기에 뿌려 불이 번졌다.</summary>
        Backfire,

        PickUp,
        Rescued,
        CivilianLost,

        /// <summary>시민이 위독해졌다. 시민마다 한 번.</summary>
        Critical,

        SecondIgnition,
        Collapse,
        Won,
        Failed,
    }

    /// <summary>
    /// 러너 상태가 어떻게 바뀌었는지 보고, 이번 프레임에 낼 소리와 줄 흔들림을 고른다.
    ///
    /// UnityEngine을 쓰지 않는다 — "언제 무슨 소리"는 규칙처럼 틀릴 수 있는 판단이라
    /// 코어 테스트에서 실제 판으로 검증한다. 소리를 내는 건 <c>GameAudio</c>, 흔드는 건 카메라가 맡는다.
    /// 러너는 읽기만 하고 절대 바꾸지 않는다.
    /// </summary>
    public sealed class FeelTracker
    {
        /// <summary>역효과 한 발의 흔들림. 실수는 크게 느껴져야 다시 안 한다.</summary>
        public const float BackfireShake = 0.6f;

        public const float SecondIgnitionShake = 0.3f;
        public const float CollapseShake = 0.5f;

        /// <summary>한 발에 여러 칸을 끄면 칸 수만큼 더 흔든다. 이 값을 넘지 않는다.</summary>
        public const float MaxPutOutShake = 0.5f;

        public readonly List<Cue> Cues = new List<Cue>();

        /// <summary>이번 프레임에 더할 흔들림(0~1).</summary>
        public float Shake { get; private set; }

        private readonly float[] _lastCooldowns = new float[PlayerState.SlotCount];
        private readonly bool[] _wasCritical;
        private int _lastShotsFired;
        private StageOutcome _lastOutcome;

        public FeelTracker(StageRunner runner)
        {
            // 시작 상태를 기억해 둔다. 처음부터 위독하거나 끝난 판이어도 첫 프레임에 소리가 나면 안 된다.
            Array.Copy(runner.Player.Cooldowns, _lastCooldowns, PlayerState.SlotCount);
            _wasCritical = new bool[runner.Civilians.Count];
            for (int i = 0; i < _wasCritical.Length; i++) _wasCritical[i] = runner.Civilians[i].Critical;
            _lastShotsFired = runner.ShotsFired;
            _lastOutcome = runner.Outcome;
        }

        public void Update(StageRunner runner)
        {
            Cues.Clear();
            Shake = 0f;

            DetectSprays(runner);
            DetectShotResult(runner);
            DetectRescues(runner);
            DetectCritical(runner);
            DetectEvents(runner);
            DetectOutcome(runner);

            if (Shake > 1f) Shake = 1f;
        }

        /// <summary>쿨다운이 새로 걸린 슬롯은 방금 쏜 것이다. 화면의 물줄기와 같은 판정.</summary>
        private void DetectSprays(StageRunner runner)
        {
            float[] cooldowns = runner.Player.Cooldowns;
            for (int slot = 0; slot < PlayerState.SlotCount; slot++)
            {
                bool fired = cooldowns[slot] > _lastCooldowns[slot] + 0.001f;
                _lastCooldowns[slot] = cooldowns[slot];
                if (!fired) continue;

                EquipmentDef def = runner.SlotEquipment(slot);
                if (def == null) continue;
                Cues.Add(SprayCue(def.Agent.Type));
            }
        }

        private static Cue SprayCue(AgentType agent)
        {
            switch (agent)
            {
                case AgentType.Foam: return Cue.SprayFoam;
                case AgentType.CO2: return Cue.SprayGas;
                default: return Cue.SprayWater;
            }
        }

        /// <summary>한 발의 결과. 역효과가 진압보다 먼저다 — 화면 팝업과 같은 순서.</summary>
        private void DetectShotResult(StageRunner runner)
        {
            if (runner.ShotsFired == _lastShotsFired) return;
            _lastShotsFired = runner.ShotsFired;

            if (runner.LastShotBackfired > 0)
            {
                Cues.Add(Cue.Backfire);
                Shake += BackfireShake;
                return;
            }

            int putOut = runner.LastShotExtinguished;
            if (putOut <= 0) return;

            Cues.Add(Cue.PutOut);
            // 한 칸은 소리만. 흔들림은 "N칸 진압!" 팝업이 뜨는 두 칸부터 — 매 발 흔들리면 멀미가 난다.
            if (putOut >= 2) Shake += Math.Min(MaxPutOutShake, 0.1f + (0.06f * putOut));
        }

        private void DetectRescues(StageRunner runner)
        {
            if (runner.JustPickedUp != null) Cues.Add(Cue.PickUp);
            if (runner.JustRescued != null) Cues.Add(Cue.Rescued);
            if (runner.JustLost != null) Cues.Add(Cue.CivilianLost);
        }

        private void DetectCritical(StageRunner runner)
        {
            for (int i = 0; i < _wasCritical.Length; i++)
            {
                Civilian civilian = runner.Civilians[i];
                bool critical = civilian.Critical && !civilian.Carried && !civilian.Rescued && !civilian.Lost;
                if (critical && !_wasCritical[i]) Cues.Add(Cue.Critical);

                // 한 번 위독했으면 다시 울리지 않는다. 경보가 반복되면 소음이 된다.
                _wasCritical[i] |= critical;
            }
        }

        private void DetectEvents(StageRunner runner)
        {
            switch (runner.Events.JustHappened)
            {
                case StageEventKind.SecondIgnition:
                    Cues.Add(Cue.SecondIgnition);
                    Shake += SecondIgnitionShake;
                    break;
                case StageEventKind.Collapse:
                    Cues.Add(Cue.Collapse);
                    Shake += CollapseShake;
                    break;
            }
        }

        private void DetectOutcome(StageRunner runner)
        {
            if (runner.Outcome == _lastOutcome) return;
            bool wasPlaying = _lastOutcome == StageOutcome.InProgress;
            _lastOutcome = runner.Outcome;
            if (!wasPlaying) return;

            Cues.Add(runner.Outcome == StageOutcome.Won ? Cue.Won : Cue.Failed);
        }
    }

    /// <summary>소리 크기와 흔들림에 쓰는 계산. UnityEngine 없이 검증할 수 있게 따로 둔다.</summary>
    public static class FeelMath
    {
        /// <summary>이 칸 수보다 먼 불은 들리지 않는다.</summary>
        public const int FireRadius = 7;

        /// <summary>trauma 1일 때 카메라가 밀리는 최대 칸 수.</summary>
        public const float MaxShakeOffset = 0.35f;

        /// <summary>trauma가 초당 줄어드는 양. 최대 흔들림도 0.6초면 멎는다.</summary>
        public const float TraumaDecayPerSecond = 1.6f;

        /// <summary>
        /// 소리 크기를 정하는 기준 불 크기. 작을수록 적은 불에도 크게 들린다.
        /// 6이었을 때는 창고에서 불과 싸우는 내내 0.18에 그쳐 거의 안 들렸다.
        /// 2.5면 두 칸 앞의 불 세 칸이 0.3을 넘고, 큰 불 한가운데는 거의 1이다.
        /// </summary>
        public const float FullFire = 2.5f;

        /// <summary>
        /// 소방관 주변 불의 크기 0~1. 가까운 칸일수록 크게 친다.
        /// 선형으로 더하면 큰 불에서 금방 천장에 붙으므로 1-exp로 눌러 준다.
        /// </summary>
        public static float FireLoudness(FireGrid grid, float px, float py)
        {
            int cx = (int)px;
            int cy = (int)py;
            float sum = 0f;

            for (int y = cy - FireRadius; y <= cy + FireRadius; y++)
            {
                for (int x = cx - FireRadius; x <= cx + FireRadius; x++)
                {
                    if (!grid.InBounds(x, y) || grid[x, y].State != CellState.Burning) continue;

                    float dx = (x + 0.5f) - px;
                    float dy = (y + 0.5f) - py;
                    float d2 = (dx * dx) + (dy * dy);
                    if (d2 > FireRadius * FireRadius) continue;
                    sum += 1f / (1f + (d2 * 0.15f));
                }
            }

            return 1f - (float)Math.Exp(-sum / FullFire);
        }

        public static float DecayTrauma(float trauma, float dt)
        {
            return Math.Max(0f, trauma - (dt * TraumaDecayPerSecond));
        }
    }
}
