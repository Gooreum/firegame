using FireGame.Prototypes.Logic;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 몹 그림(2026-10-07 승인 샘플): 보라 요괴. 둥근 보라 몸·흰 눈·찡그린 눈썹·머리 위 작은 불꽃, 큰 놈은 뿔.
    /// 색 규칙 — 보라 = 몹, 파랑 = 물, 주황 = 불(집에만). 걸음마다 통통 튀고 숨 쉬듯 찌그러지며, 맞으면 하얗게 번쩍한다.
    /// </summary>
    public sealed partial class SurvivorView
    {
        private static readonly Color YokaiPurple = new Color(0.6f, 0.3f, 1f, 1f);

        /// <summary>요괴 몸(카메라를 본다)과 머리 불꽃(가산).</summary>
        private Pool _yokai;
        private Pool _yokaiFlame;

        /// <summary>요괴 스프라이트 한 장(30 샘플 단위)에서 몸 지름은 20 단위다.</summary>
        private const float YokaiBodyShare = 20f / 30f;

        private struct Flung
        {
            public Vector3 From;
            public Vector3 Vel;
            public EnemyKind Kind;
            public float Age;
        }

        /// <summary>하늘로 날아간 요괴(그림만): 0.7초 포물선 뒤 떨어진 자리에서 터진다.</summary>
        private readonly System.Collections.Generic.List<Flung> _flung = new System.Collections.Generic.List<Flung>();
        private const float FlingTime = 0.7f;

        private void Fling(Vector3 at, EnemyKind kind, Vec2 from)
        {
            if (_flung.Count > 40)
            {
                DeathBurst(at, kind, true);
                return;
            }
            var away = new Vector3(at.x - from.X, at.y - from.Y, 0f);
            away = away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitCircle.normalized;
            _flung.Add(new Flung { From = at, Vel = away * Random.Range(1.5f, 3f), Kind = kind, Age = 0f });
        }

        private void DrawFlung(float dt)
        {
            for (int k = _flung.Count - 1; k >= 0; k--)
            {
                Flung f = _flung[k];
                f.Age += dt;
                float t = f.Age / FlingTime;
                Vector3 ground = f.From + (f.Vel * f.Age);
                if (t >= 1f)
                {
                    _flung.RemoveAt(k);
                    YokaiPoof(ground, f.Kind, false);
                    continue;
                }
                _flung[k] = f;
                float h = Mathf.Sin(t * Mathf.PI) * 4f;
                bool big = f.Kind == EnemyKind.Blaze || f.Kind == EnemyKind.Bear;
                DrawYokai(f.Kind, ground + Up(h), k, t < 0.15f, big ? 1.1f : 0.75f, big, Mathf.Sin(f.Age * 30f) > 0f ? 1f : -1f, 30f);
            }
        }

        private void BuildYokaiPools()
        {
            _yokai = new Pool(_world, "Yokai", SkillSprite("yokai_small"), 9, null);
            _yokai.Upright = true;
            _pools.Add(_yokai);
            _yokaiFlame = new Pool(_world, "YokaiFlame", SkillSprite("flame_tuft"), 10, Additive);
            _yokaiFlame.Upright = true;
            _pools.Add(_yokaiFlame);
            _pets = new PersonPool(_world, 1f);
        }

        /// <summary>요괴로 그리는 몹: 가장자리 불씨·큰 불과 마을 몹 다섯.</summary>
        private static bool IsYokai(EnemyKind kind)
        {
            return kind == EnemyKind.Ember || kind == EnemyKind.Blaze || kind == EnemyKind.Rat || kind == EnemyKind.Goblin
                || kind == EnemyKind.FireBalloon || kind == EnemyKind.Bear || kind == EnemyKind.Hwama;
        }

        /// <summary>몹 모델 풀(Kenney Cube Pets, 2026-10-10): 키 1 = 몸 지름 1칸. 한 프레임 MaxPets까지, 넘치면 예전 스프라이트.</summary>
        private PersonPool _pets;
        private int _petsDrawn;
        private const int MaxPets = 90;

        /// <summary>불에 홀린 동물 요괴(10/8 제안): 몹 종류별 Cube Pets. null이면 예전 그림(불씨 화살·풍등은 동물이 아니다).</summary>
        private static string PetModel(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Ember: return "Pets/animal-chick";     // 불씨 = 불병아리
                case EnemyKind.Blaze: return "Pets/animal-tiger";     // 큰 불 = 불호랑이
                case EnemyKind.Rat: return "Pets/animal-beaver";
                case EnemyKind.Goblin: return "Pets/animal-monkey";   // 횃불 던지는 원숭이
                case EnemyKind.FireBalloon: return "Pets/animal-koala";
                case EnemyKind.Bear: return "Pets/animal-polar";
                case EnemyKind.Hwama: return "Pets/animal-lion";      // 갈기 = 불
                case EnemyKind.Squirrel: return "Pets/animal-fox";
                case EnemyKind.Bat: return "Pets/animal-bee";
                case EnemyKind.Oil: return "Pets/animal-hog";
                case EnemyKind.Popper: return "Pets/animal-bunny";
                case EnemyKind.Crab: return "Pets/animal-crab";
                case EnemyKind.Gull: return "Pets/animal-parrot";
            }
            return null;
        }

        /// <summary>
        /// 몹 몸을 Cube Pets 모델로(2026-10-10): 가는 쪽을 보되 살짝 카메라 쪽, 뛰는 속도에 맞춰 run/walk. 색 규칙(보라 = 몹)은 반만 —
        /// 동물이 읽히게 보라를 절반 섞고, 맞으면 하얗게 번쩍, 젖으면 제 색으로 돌아온다. 모델이 없거나 상한을 넘으면 예전 스프라이트.
        /// </summary>
        /// <param name="body">몸 지름(칸).</param>
        /// <param name="face">왼쪽을 보면 -1.</param>
        /// <returns>머리 꼭대기 자리(소품을 얹는다).</returns>
        private Vector3 DrawYokai(EnemyKind kind, Vector3 at, int i, bool hit, float body, bool big, float face, float hopRate = 9f, bool wet = false)
        {
            string path = PetModel(kind);
            GameObject pet = path != null && _pets != null && _petsDrawn < MaxPets ? _pets.Get(path, body) : null;
            if (pet == null) return DrawYokaiSprite(at, i, hit, body, big, face, hopRate);
            _petsDrawn++;
            float bob = Mathf.Abs(Mathf.Sin((_time * hopRate) + (i * 1.3f))) * 0.1f * body;
            _shadows.Put(at + new Vector3(0f, -0.1f, 0f), body * 0.95f, 0f, new Color(0f, 0f, 0f, 0.35f), null, 0.5f);
            Models3D.Pose(pet, at + Up(bob), new Vector3(face, -0.5f, 0f));
            Models3D.Play(pet, hopRate >= 5f ? "run" : "walk", Mathf.Clamp(hopRate / 9f, 0.5f, 2f), _time + i);
            Color tint = hit ? new Color(1.6f, 1.6f, 1.6f) : Color.Lerp(Color.white, YokaiPurple, wet ? 0f : 0.5f);
            Models3D.Tint(pet, tint);
            Vector3 top = at + Up(bob + body);
            float flick = 1f + (0.18f * Mathf.Sin((_time * 22f) + (i * 2.1f)));
            float tuft = body * 0.75f * flick;
            _yokaiFlame.Put(top + Up(tuft * 0.25f), tuft, 0f, Color.white, SkillSprite("flame_tuft"));
            return top;
        }

        /// <summary>예전 그림(10/8 승인 샘플의 보라 요괴 스프라이트): 모델이 없거나 상한을 넘을 때.</summary>
        private Vector3 DrawYokaiSprite(Vector3 at, int i, bool hit, float body, bool big, float face, float hopRate = 9f)
        {
            float size = body / YokaiBodyShare;
            float bob = Mathf.Abs(Mathf.Sin((_time * hopRate) + (i * 1.3f))) * 0.14f * body;
            float sq = 1f + (0.05f * Mathf.Sin((_time * 18f) + i));
            _shadows.Put(at + new Vector3(0f, -0.1f, 0f), body * 0.95f, 0f, new Color(0f, 0f, 0f, 0.35f), null, 0.5f);
            _yokai.Put(at + Up(bob), size * sq * face, 0f, Color.white, SkillSprite(big ? "yokai_big" : "yokai_small"), 1f / (sq * sq));
            // 맞으면 하얗게 번쩍(가산 덧칠): 물대포가 계속 맞혀도 얼굴은 보인다.
            if (hit) _yokaiFlame.Put(at + Up(bob), size * sq * face, 0f, new Color(1f, 1f, 1f, 0.45f), SkillSprite(big ? "yokai_big_white" : "yokai_white"), 1f / (sq * sq));

            // 머리 위 작은 불꽃: 몸 꼭대기(가운데에서 반지름 0.92)에 밑동을 대고 깜빡인다. 서 있는 그림이라 카메라 쪽 위(Billboard)로 잰다.
            Vector3 up = Billboard * Vector3.up;
            Vector3 top = at + Up(bob) + (up * ((size * 0.5f) + (body * 0.46f)));
            float flick = 1f + (0.18f * Mathf.Sin((_time * 22f) + (i * 2.1f)));
            float tuft = body * 0.75f * flick;
            _yokaiFlame.Put(top - (up * (tuft * 0.2f)), tuft, 0f, Color.white, SkillSprite("flame_tuft"));
            return top;
        }

        /// <summary>요괴가 쓰러질 때(샘플 poof): 주황·보라 불티, 연보라 연기, 흰 고리. 보석은 시뮬이 떨군다.</summary>
        private void YokaiPoof(Vector3 at, EnemyKind kind, bool crowded)
        {
            bool big = kind == EnemyKind.Blaze || kind == EnemyKind.Bear || kind == EnemyKind.Hwama;
            float k = big ? 1.6f : 1f;
            Emit("Effects/glow", at + Up(0.4f), Vector3.zero, 0f, 0.12f, 1.4f * k, 2f * k, new Color(1f, 0.95f, 1f, 0.9f), new Color(0.8f, 0.6f, 1f, 0f), 0f, true);
            int sparks = big ? 14 : crowded ? 5 : 10;
            for (int n = 0; n < sparks; n++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Color c = n % 2 == 0 ? new Color(1f, 0.7f, 0.25f) : new Color(0.8f, 0.5f, 1f);
                Emit(Sparks[Random.Range(0, Sparks.Length)], at + Up(0.4f), new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Random.Range(3f, 7f) * k, 5f, Random.Range(0.25f, 0.45f),
                    Random.Range(0.4f, 0.7f) * k, 0.05f, c, new Color(c.r, c.g, c.b, 0f), Random.Range(-600f, 600f), true);
            }
            for (int n = 0; n < (crowded ? 2 : 4); n++)
            {
                Emit(Smokes[Random.Range(0, Smokes.Length)], at + Up(0.4f) + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.2f), 0f), new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.6f, 1.4f), 0f),
                    0.8f, Random.Range(0.4f, 0.7f), 0.6f * k, 1.8f * k, new Color(0.78f, 0.7f, 0.88f, 0.7f), new Color(0.78f, 0.7f, 0.88f, 0f), Random.Range(-120f, 120f));
            }
            Shockwave(at, new Color(1f, 1f, 1f, 0.9f), 1.8f * k, 0.25f);
            Splash(at, crowded ? 2 : 4, 0.5f);
            if (big)
            {
                _trauma = Mathf.Min(1f, _trauma + 0.06f);
                HitStop(0.03f);
            }
        }
    }
}
