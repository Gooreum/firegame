using System.Collections.Generic;
using FireGame.UnityLayer.Feel;
using UnityEngine;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 효과음과 불 소리. <see cref="Art"/>처럼 정적으로 두고 처음 부를 때 스스로 자리를 잡는다.
    ///
    /// 언제 무슨 소리를 낼지는 <see cref="FeelTracker"/>가 정한다. 여기는 이름을 클립으로 바꿔 틀기만 한다.
    /// 클립이 없으면(에셋을 아직 안 받았으면) 조용히 넘어간다 — 소리 때문에 게임이 멈추면 안 된다.
    /// </summary>
    public static class GameAudio
    {
        /// <summary>불이 가장 클 때 루프 볼륨. 효과음을 덮지 않을 만큼만.</summary>
        private const float FireMaxVolume = 0.55f;

        /// <summary>불 소리가 목표 크기를 따라가는 시정수(초). 짧으면 걸음마다 소리가 튄다.</summary>
        private const float FireEase = 0.25f;

        private static AudioSource _oneShot;
        private static AudioSource _fire;
        private static float _fireVolume;
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        public static void Play(Cue cue)
        {
            switch (cue)
            {
                case Cue.SprayWater: Emit("spray_water", 0.45f, 0.08f); break;
                case Cue.SprayFoam: Emit("spray_foam", 0.45f, 0.08f); break;
                case Cue.SprayGas: Emit("spray_gas", 0.4f, 0.08f); break;
                case Cue.PutOut: Emit("putout", 0.8f, 0.1f); break;
                case Cue.Backfire: Emit("backfire", 1f, 0.05f); break;
                case Cue.PickUp: Emit("pickup", 0.8f, 0f); break;
                case Cue.Rescued: Emit("rescued", 0.9f, 0f); break;
                case Cue.CivilianLost: Emit("civ_lost", 0.9f, 0f); break;
                case Cue.Critical: Emit("critical", 0.8f, 0f); break;
                case Cue.SecondIgnition: Emit("ignite", 0.9f, 0.05f); break;
                case Cue.Collapse:
                    Emit(CollapseClip(), 1f, 0.06f);
                    Emit("door_jam", 0.7f, 0.06f);
                    break;
                case Cue.Won: Emit("jingle_win", 0.8f, 0f); break;
                case Cue.Failed: Emit("jingle_fail", 0.8f, 0f); break;
            }
        }

        /// <summary>
        /// 주변 불의 크기(0~1)를 루프 볼륨으로 옮긴다. 매 프레임 부른다.
        /// 불이 클수록 조금 낮고 거칠게(피치↓) — 작은 불은 가볍게 탁탁거린다.
        /// </summary>
        public static void SetFireLevel(float level, float dt)
        {
            if (_fire == null && level <= 0f) return;
            if (!EnsureSources() || _fire.clip == null) return;

            float target = Mathf.Clamp01(level) * FireMaxVolume;
            _fireVolume = Mathf.Lerp(_fireVolume, target, 1f - Mathf.Exp(-dt / FireEase));
            if (_fireVolume < 0.001f && target <= 0f) _fireVolume = 0f;

            _fire.volume = _fireVolume;
            _fire.pitch = 1.1f - (0.2f * Mathf.Clamp01(level));
            if (_fireVolume > 0f && !_fire.isPlaying) _fire.Play();
            if (_fireVolume <= 0f && _fire.isPlaying) _fire.Stop();
        }

        private static string CollapseClip()
        {
            return "collapse_" + Random.Range(0, 3);
        }

        /// <param name="pitchJitter">같은 소리가 연달아 나도 기계적으로 들리지 않게 피치를 이만큼 흔든다.</param>
        private static void Emit(string name, float volume, float pitchJitter)
        {
            if (!EnsureSources()) return;
            AudioClip clip = Load(name);
            if (clip == null) return;

            // PlayOneShot은 소스의 피치를 따른다. 겹쳐 나는 소리끼리 피치가 같아지지만 짧은 효과음이라 괜찮다.
            _oneShot.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            _oneShot.PlayOneShot(clip, volume);
        }

        private static AudioClip Load(string name)
        {
            if (Clips.TryGetValue(name, out AudioClip clip)) return clip;
            clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip == null) Debug.LogWarning("[FireGame] 소리가 없다: Audio/" + name + " — tools/import-audio.py를 실행하세요.");
            Clips[name] = clip;
            return clip;
        }

        private static bool EnsureSources()
        {
            if (_oneShot != null) return true;

            // 스크린샷 하니스처럼 에디터 스크립트가 화면을 그릴 때는 소리를 내지 않는다.
            // DontDestroyOnLoad가 플레이 중에만 되고, 씬에 소리 오브젝트가 남아서도 안 된다.
            if (!Application.isPlaying) return false;

            var host = new GameObject("Audio");
            Object.DontDestroyOnLoad(host);

            _oneShot = host.AddComponent<AudioSource>();
            _oneShot.playOnAwake = false;

            _fire = host.AddComponent<AudioSource>();
            _fire.playOnAwake = false;
            _fire.loop = true;
            _fire.volume = 0f;
            _fire.clip = Load("fire_loop");
            return true;
        }
    }
}
