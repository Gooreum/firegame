using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using FireGame.UnityLayer.Feel;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 숲 개편(2026-10-08) 그림: 물대포 보조 물줄기, 구조대원(합류·따라오기·물 쏘기), 보조 Lv6(초고압 펌프·제트 장화·불사조 방화복),
    /// 방화복 물결, 발밑 등급 고리(샘플 tierAura). 규칙은 SurvivorSim(SurvivorFree)이 정하고 여기선 읽기만 한다.
    /// </summary>
    public sealed partial class SurvivorView
    {
        /// <summary>대원은 빨간 방화복(플레이어 옷 단계 3).</summary>
        private const int CrewOutfit = 3;

        /// <summary>풀 그리기 안에서(DrawShots): 보조 물줄기·제트 물길·대원·발밑 고리.</summary>
        private void DrawFreeStreams()
        {
            int tier = Mathf.Max(TierLv(UpgradeId.Hose), TierLv(UpgradeId.Tank));
            Color core = TierCore[Mathf.Clamp(tier, 1, 6)];
            foreach (AuxStream a in _sim.HoseAux)
            {
                Vector3 from = W(a.From);
                Vector3 to = W(a.To);
                Color c = tier >= 6 ? Color.Lerp(core, Rainbow(a.To.X * 0.05f), 0.5f) : tier >= 5 ? Color.Lerp(core, LvGold, 0.3f) : core;
                SmallRibbon(from, to, 0.3f * TierSize(tier), c, HandHeight);
                TierHalo(to + Up(0.3f), 0.35f, tier);
            }

            // 제트 장화 물길: 점마다 반짝이는 파란 띠.
            for (int i = 0; i < _sim.JetTrail.Count; i++)
            {
                SurvivorSim.JetSpot j = _sim.JetTrail[i];
                float k = j.Life / SurvivorSim.JetLife;
                Vector3 at = W(j.Pos);
                _lvHalo.Put(at, SurvivorSim.JetWidth * 2.4f, 0f, new Color(0.35f, 0.75f, 1f, 0.55f * k));
                _lvHalo.Put(at, SurvivorSim.JetWidth * 1.1f, 0f, new Color(0.9f, 0.98f, 1f, 0.5f * k));
                if (Random.value < 0.08f) Sparkle(at, 1, Rainbow(i * 0.07f));
            }

            DrawCrew();

            // 발밑 등급 고리(샘플 tierAura): 가진 아이템 중 가장 높은 등급. Lv5 금, Lv6 무지개.
            int best = 0;
            foreach (UpgradeId id in _sim.Build.Owned())
            {
                UpgradeId baseId = Loadout.BaseOf(id);
                if (baseId == UpgradeId.Heal) continue;
                best = Mathf.Max(best, TierLv(baseId));
            }
            if (best >= 3)
            {
                Vector3 feet = W(_sim.Player) + Up(0.03f);
                Color ring = best >= 6 ? Rainbow() : best >= 5 ? LvGold : TierCore[best];
                float r = (14f + (best * 2f)) * Px * 2f * 1.4f;
                _lvArcs.Put(feet, r, _time * 60f, new Color(ring.r, ring.g, ring.b, 0.7f));
                if (best >= 5) _lvArcs.Put(feet, r * 1.18f, -_time * 140f, new Color(ring.r, ring.g, ring.b, 0.45f));
            }

            // 방화복 보호막(Lv1~5 두꺼워짐, Lv5 금빛, 최고급 무지개).
            int suit = TierLv(UpgradeId.Suit);
            if (suit > 0)
            {
                Color sc = suit >= 6 ? Rainbow(0.3f) : suit >= 5 ? LvGold : new Color(0.55f, 0.85f, 1f);
                _lvHalo.Put(W(_sim.Player) + Up(0.8f), 1.6f + (suit * 0.12f), 0f, new Color(sc.r, sc.g, sc.b, 0.1f + (suit * 0.04f)));
            }
        }

        private void DrawCrew()
        {
            for (int i = 0; i < _sim.CrewList.Count; i++)
            {
                Crew c = _sim.CrewList[i];
                Vector3 at = W(c.Pos);
                Vector3 me = W(_sim.Player);
                _shadows.Put(at + new Vector3(0.05f, -0.1f, 0f), 0.8f, 0f, new Color(0f, 0f, 0f, 0.4f), null, 0.5f);
                // 아군 표시: 발밑 하늘색 고리.
                _lvArcs.Put(at + Up(0.03f), 1.1f, _time * 90f + (i * 40f), new Color(0.5f, 0.85f, 1f, 0.75f));
                // 합류 직후 0.3초: 하얀 실루엣으로 번쩍이며 변신한다.
                float flash = Mathf.Clamp01(1f - (c.Age / 0.3f));
                if (flash > 0f) _civilianRings.Put(at, 2.2f * (1f + flash), 0f, new Color(1f, 1f, 1f, 0.8f * flash));
                GameObject person = _people.Get("People/Worker_Male");
                if (person == null) continue;
                Vector3 look = c.Target.HasValue ? W(c.Target.Value) - at : me - at;
                if (look.sqrMagnitude < 0.01f) look = new Vector3(1f, 0f, 0f);
                Models3D.Pose(person, at, look.normalized);
                Vector2 slot = new Vector2(_sim.CrewSlot(i).X, _sim.CrewSlot(i).Y);
                bool moving = Vector2.Distance(slot, new Vector2(c.Pos.X, c.Pos.Y)) > 0.05f;
                Models3D.Play(person, moving ? "Run" : "Idle", 1.1f, _time + i);
                Models3D.Tint(person, Color.white, SuitColor, CrewOutfit);
                if (c.Target.HasValue) SmallRibbon(at, W(c.Target.Value), 0.22f, new Color(0.55f, 0.82f, 1f, 1f), HandHeight * 0.9f);
            }
        }

        /// <summary>React에서: 대원 합류·펌프 폭발·불사조 부활·방화복 물결·숲 물보라.</summary>
        private void ReactFree()
        {
            if (!Free) return;
            if (_sim.JustCrewJoined > 0)
            {
                int n = _sim.JustCrewJoined;
                Vector3 at = W(_sim.CrewJoinedAt);
                bool all = n >= SurvivorSim.MaxCrew;
                Pillar(at, all ? LvGold : new Color(0.7f, 0.9f, 1f));
                Sparkle(at, 20 + (n * 3), all ? LvGold : new Color(0.8f, 0.95f, 1f));
                Shockwave(at, Color.white, 4f + n, 0.45f);
                if (all)
                {
                    for (int j = 0; j < 3; j++) Shockwave(at, Rainbow(j / 3f), 8f + (j * 2f), 0.6f, j * 0.08f);
                    _trauma = Mathf.Min(1f, _trauma + 0.45f);
                    Flash(new Color(1f, 0.92f, 0.67f), 0.35f);
                    _slowmo = Mathf.Max(_slowmo, 0.6f);
                    ShowAlert("전원 집결! 구조대원 " + n + "명", LvGold);
                    GameAudio.Play(Cue.Won);
                }
                else
                {
                    _trauma = Mathf.Min(1f, _trauma + 0.1f + (n * 0.03f));
                    Flash(Color.white, 0.12f + (n * 0.02f));
                    ShowAlert("구조대원 합류! ×" + n, new Color(0.7f, 0.92f, 1f));
                    GameAudio.Play(Cue.Rescued);
                }
                SpawnText(at + new Vector3(0f, 1.6f, 0f), "대원 ×" + n, all ? LvGold : new Color(0.8f, 0.95f, 1f), 1.8f);
            }
            if (_sim.JustPumpNova)
            {
                Vector3 at = W(_sim.Player);
                Shockwave(at, new Color(0.5f, 0.85f, 1f), SurvivorSim.PumpRadius * 2f, 0.45f);
                Shockwave(at, Color.white, SurvivorSim.PumpRadius * 2.4f, 0.5f, 0.06f);
                Splash(at, 24, 2.2f);
                Burst(at, 18, new Color(0.7f, 0.92f, 1f), 10f);
                _trauma = Mathf.Min(1f, _trauma + 0.25f);
                GameAudio.Play(Cue.PutOut);
            }
            if (_sim.JustPhoenix)
            {
                // 물 날개 두 장이 펼쳐지며 무지개 폭발.
                Vector3 at = W(_sim.Player) + Up(1.1f);
                Sprite wing = LvArt("wing");
                for (int s = -1; s <= 1; s += 2)
                {
                    EmitSprite(wing, at + new Vector3(s * 0.4f, 0f, 0f), new Vector3(s * 1.5f, 0.4f, 0f), 2f, 1.3f, 3.2f, 5.5f,
                        new Color(0.7f, 0.92f, 1f, 0.95f), new Color(0.7f, 0.92f, 1f, 0f), 0f, true, 0f, 1f, s < 0 ? 0f : 180f);
                }
                for (int j = 0; j < 3; j++) Shockwave(at, Rainbow(j / 3f), SurvivorSim.PhoenixRadius * 2f * (1f + (j * 0.15f)), 0.7f, j * 0.08f);
                Burst(at, 60, LvGold, 14f);
                Flash(new Color(1f, 0.97f, 0.9f), 0.45f);
                _trauma = 1f;
                _slowmo = Mathf.Max(_slowmo, 1.2f);
                ShowAlert("불사조 부활!", LvGold);
                GameAudio.Play(Cue.Won);
            }
            foreach (Vec2 r in _sim.SuitRipples)
            {
                Shockwave(W(r), new Color(0.55f, 0.85f, 1f), _sim.SuitRippleRadius * 2f, 0.4f);
            }
            foreach (Vec2 p in _sim.FreeSplashes)
            {
                Vector3 at = W(p);
                Shockwave(at, new Color(0.7f, 0.92f, 1f), 2.4f, 0.3f);
                Splash(at, 6, 1f);
            }
        }
    }
}
