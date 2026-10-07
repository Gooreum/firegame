using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using FireGame.UnityLayer.Feel;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 숲 개편(2026-10-08) 그림: 구조대원(합류·따라오기·물 쏘기). 규칙은 SurvivorSim(SurvivorFree)이 정하고 여기선 읽기만 한다.
    /// </summary>
    public sealed partial class SurvivorView
    {
        /// <summary>대원은 빨간 방화복(플레이어 옷 단계 3).</summary>
        private const int CrewOutfit = 3;

        private void DrawCrew()
        {
            for (int i = 0; i < _sim.CrewList.Count; i++)
            {
                Crew c = _sim.CrewList[i];
                Vector3 at = W(c.Pos);
                Vector3 me = W(_sim.Player);
                _shadows.Put(at + new Vector3(0.05f, -0.1f, 0f), 0.8f, 0f, new Color(0f, 0f, 0f, 0.4f), null, 0.5f);
                // 아군 표시: 발밑 하늘색 고리.
                _civilianRings.Put(at + Up(0.03f), 1.1f, 0f, new Color(0.5f, 0.85f, 1f, 0.75f));
                // 합류 직후 0.3초: 하얀 실루엣으로 번쩍이며 변신한다.
                float flash = Mathf.Clamp01(1f - (c.Age / 0.3f));
                if (flash > 0f) _civilianRings.Put(at, 2.2f * (1f + flash), 0f, new Color(1f, 1f, 1f, 0.8f * flash));
                GameObject person = _people.Get("People/Worker_Male");
                if (person == null) continue;
                person.transform.localScale = _personScale * SamplePersonScale;
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
                Pillar(at, all ? new Color(1f, 0.84f, 0.43f) : new Color(0.7f, 0.9f, 1f));
                Sparkle(at, 20 + (n * 3), all ? new Color(1f, 0.84f, 0.43f) : new Color(0.8f, 0.95f, 1f));
                Shockwave(at, Color.white, 4f + n, 0.45f);
                if (all)
                {
                    for (int j = 0; j < 3; j++) Shockwave(at, Hsla(SHue(j * 120f), 100f, 70f, 1f), 8f + (j * 2f), 0.6f, j * 0.08f);
                    _trauma = Mathf.Min(1f, _trauma + 0.45f);
                    Flash(new Color(1f, 0.92f, 0.67f), 0.35f);
                    _slowmo = Mathf.Max(_slowmo, 0.6f);
                    ShowAlert("전원 집결! 구조대원 " + n + "명", new Color(1f, 0.84f, 0.43f));
                    GameAudio.Play(Cue.Won);
                }
                else
                {
                    _trauma = Mathf.Min(1f, _trauma + 0.1f + (n * 0.03f));
                    Flash(Color.white, 0.12f + (n * 0.02f));
                    ShowAlert("구조대원 합류! ×" + n, new Color(0.7f, 0.92f, 1f));
                    GameAudio.Play(Cue.Rescued);
                }
                SpawnText(at + new Vector3(0f, 1.6f, 0f), "대원 ×" + n, all ? new Color(1f, 0.84f, 0.43f) : new Color(0.8f, 0.95f, 1f), 1.8f);
            }
        }
    }
}
