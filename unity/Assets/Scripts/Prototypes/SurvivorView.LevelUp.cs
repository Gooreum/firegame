using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using FireGame.UnityLayer.Feel;
using UnityEngine;
using UnityEngine.UI;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 숲 개편(2026-10-08): 사용자가 승인한 레벨업 샘플(tools/levelup-art의 core.js levelUp·doBurst·drawRays·drawFxFront) 그대로.
    /// 레벨이 오를수록 광선·고리·별·꽃가루·흔들림·슬로모션이 한 단계씩 커지고, Lv6 최고급은 암전 → 빛 흡입 → 무지개 폭발 → 이름 띠.
    /// 무기 등급(샘플 TIER): 레벨마다 크기·빛·흰 심·금테·무지개. 그림은 Art/LevelUp(tools/bake-levelup-art.sh)으로 구웠다.
    /// 숲(Build.Free)에서만 쓴다.
    /// </summary>
    public sealed partial class SurvivorView
    {
        // --- 샘플 표(인덱스 = 레벨, 6 = 최고급) ---
        private static readonly int[] RayCount = { 0, 8, 12, 16, 20, 28, 40 };
        private static readonly float[] RayLife = { 0f, 1f, 1.1f, 1.2f, 1.3f, 1.7f, 2.6f };
        private static readonly int[] StarCount = { 0, 18, 28, 40, 56, 90, 160 };
        private static readonly float[] LvShake = { 0f, 0.15f, 0.2f, 0.25f, 0.3f, 0.45f, 0.7f };
        private static readonly float[] LvFlash = { 0f, 0.25f, 0.3f, 0.35f, 0.4f, 0.45f, 0.45f };
        private static readonly float[] LvStop = { 0f, 0.04f, 0.05f, 0.06f, 0.07f, 0.08f, 0.08f };
        private static readonly float[] LvSlow = { 0f, 0.55f, 0.6f, 0.7f, 0.8f, 1.1f, 1.9f };

        /// <summary>샘플 TIER: 크기·핵 색·빛 세기·흰 심.</summary>
        private static readonly float[] TierScale = { 1f, 1f, 1.18f, 1.4f, 1.6f, 1.8f, 2.3f };
        private static readonly float[] TierGlowA = { 0f, 0f, 0.18f, 0.32f, 0.45f, 0.6f, 0.8f };
        private static readonly float[] TierWhite = { 0f, 0f, 0.15f, 0.45f, 0.65f, 0.85f, 1f };
        private static readonly Color[] TierCore =
        {
            new Color(70 / 255f, 150 / 255f, 1f), new Color(70 / 255f, 150 / 255f, 1f), new Color(90 / 255f, 170 / 255f, 1f), new Color(120 / 255f, 200 / 255f, 1f),
            new Color(150 / 255f, 220 / 255f, 1f), new Color(190 / 255f, 235 / 255f, 1f), new Color(230 / 255f, 248 / 255f, 1f),
        };

        private static readonly Color LvGold = new Color(1f, 214 / 255f, 110 / 255f);
        private static readonly Color LvCream = new Color(1f, 248 / 255f, 220 / 255f);
        private static readonly Color LvIce = new Color(235 / 255f, 248 / 255f, 1f);

        /// <summary>샘플 px → 월드 칸(샘플 소방관 키 ≈ 30px ≈ 1.6칸).</summary>
        private const float Px = 0.05f;

        private Pool _lvRays;
        private Pool _lvHalo;
        private Pool _lvArcs;

        private RectTransform _lvUi;
        private Image _lvDark;
        private RectTransform _ovGroup;
        private Image _ovIcon;
        private Image _ovGlow;
        private Image _ovText;
        private Image _ovNext;
        private readonly Image[] _ovStars = new Image[6];
        private RectTransform _bandGroup;
        private Image _bandBack;
        private Image _bandTop;
        private Image _bandBottom;
        private Image _bandTitle;
        private Image _bandName;
        private RectTransform _flyGroup;
        private Image _flyIcon;
        private readonly Image[] _flyTrail = new Image[8];

        private UpgradeId _lvId;
        private int _lvLv;
        private float _lvAge = -1f;
        private float _lvBurstAt;
        private bool _lvBurstDone;

        private bool Free
        {
            get { return _sim != null && _sim.Build.Free; }
        }

        private static Sprite LvArt(string name)
        {
            return Art.Get("LevelUp/" + name);
        }

        /// <summary>무지개 색(시간에 따라 돈다). off는 색상환 위치(0~1).</summary>
        private Color Rainbow(float off = 0f, float sat = 0.75f)
        {
            return Color.HSVToRGB(Mathf.Repeat((_time * 140f / 360f) + off, 1f), sat, 1f);
        }

        /// <summary>무기의 등급(1~6): 숲이 아니면 쓰는 레벨 그대로(최대 5).</summary>
        private int TierLv(UpgradeId weapon)
        {
            Loadout b = _sim.Build;
            if (!b.Free) return b.PowerOf(weapon);
            UpgradeId? evo = Loadout.EvolutionOf(weapon);
            if (evo.HasValue && b.Level(evo.Value) > 0) return 6;
            return b.Level(weapon);
        }

        /// <summary>무기 색에 등급을 입힌다: Lv3부터 흰 심, Lv5 금빛, Lv6 무지개.</summary>
        private Color TierTint(Color c, int lv)
        {
            if (!Free || lv <= 1) return c;
            if (lv >= 6) return Color.Lerp(c, Rainbow(), 0.55f);
            if (lv >= 5) return Color.Lerp(c, LvGold, 0.35f);
            return Color.Lerp(c, Color.white, TierWhite[lv] * 0.45f);
        }

        /// <summary>투사체 그림 크기 배율(판정은 그대로). Lv6 ×1.4.</summary>
        private float TierSize(int lv)
        {
            if (!Free || lv <= 1) return 1f;
            return lv >= 6 ? 1.4f : 1f + (0.1f * (lv - 1));
        }

        /// <summary>샘플 tierGlow: 가산 원광 + Lv5 금 호 두 개(돈다) + Lv6 무지개 호. r은 투사체 반지름(칸).</summary>
        private void TierHalo(Vector3 at, float r, int lv)
        {
            if (!Free || lv < 2 || _lvHalo == null) return;
            float a = TierGlowA[Mathf.Min(lv, 6)];
            Color glow = lv >= 6 ? new Color(1f, 0.86f, 0.55f) : TierCore[lv];
            _lvHalo.Put(at, r * 2f * (1.4f + (lv * 0.18f)), 0f, new Color(glow.r, glow.g, glow.b, a));
            if (lv < 5) return;
            Color arc1 = lv >= 6 ? Rainbow() : new Color(LvGold.r, LvGold.g, LvGold.b, 0.85f);
            Color arc2 = lv >= 6 ? Rainbow(0.5f) : new Color(1f, 0.94f, 0.75f, 0.7f);
            _lvArcs.Put(at, r * 2.24f, (_time * 230f) % 360f, arc1);
            _lvArcs.Put(at, r * 2.52f, -(_time * 170f) % 360f, arc2);
        }

        private void BuildLevelUpFx()
        {
            _lvRays = AddPool("LvRay", "LevelUp/ray", 16, true);
            _lvHalo = AddPool("LvHalo", "Skills/soft_glow", 8, true);
            _lvArcs = AddPool("LvArc", "Skills/ring_dashed", 9, true);

            _lvUi = UiKit.Node(_hud, "LevelUpFx");
            UiKit.Stretch(_lvUi);
            _lvDark = UiKit.Image(_lvUi, "Dark", Art.White, new Color(0.02f, 0f, 0.06f, 0f));
            _lvDark.raycastTarget = false;
            UiKit.Stretch(_lvDark.rectTransform);

            _ovGroup = UiKit.Node(_lvUi, "Overhead");
            UiKit.Place(_ovGroup, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            _ovGlow = LvImage(_ovGroup, "Glow", Art.Get("Effects/glow"), new Vector2(0f, 0f), new Vector2(230f, 230f));
            _ovIcon = LvImage(_ovGroup, "Icon", null, new Vector2(0f, 0f), new Vector2(140f, 140f));
            for (int i = 0; i < 6; i++) _ovStars[i] = LvImage(_ovGroup, "Star" + i, LvArt("star_off"), new Vector2((i - 2.5f) * 46f, -98f), new Vector2(i == 5 ? 58f : 48f, i == 5 ? 58f : 48f));
            _ovText = LvImage(_ovGroup, "Text", null, new Vector2(0f, 150f), new Vector2(380f, 95f));
            _ovNext = LvImage(_lvUi, "Next", LvArt("txt_next_top"), Vector2.zero, new Vector2(360f, 90f));
            _ovGroup.gameObject.SetActive(false);
            _ovNext.gameObject.SetActive(false);

            _bandGroup = UiKit.Node(_lvUi, "Band");
            UiKit.Stretch(_bandGroup);
            _bandBack = UiKit.Image(_bandGroup, "Back", Art.White, new Color(0.08f, 0.03f, 0.16f, 0.85f));
            _bandBack.raycastTarget = false;
            UiKit.Place(_bandBack.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 230f));
            _bandTop = LvImage(_bandGroup, "Top", Art.White, new Vector2(0f, 108f), new Vector2(4000f, 8f));
            _bandBottom = LvImage(_bandGroup, "Bottom", Art.White, new Vector2(0f, -108f), new Vector2(4000f, 8f));
            _bandTitle = LvImage(_bandGroup, "Title", LvArt("txt_top"), new Vector2(0f, 52f), new Vector2(300f, 75f));
            _bandName = LvImage(_bandGroup, "Name", null, new Vector2(0f, -28f), new Vector2(960f, 160f));
            _bandGroup.gameObject.SetActive(false);

            _flyGroup = UiKit.Node(_lvUi, "Fly");
            UiKit.Place(_flyGroup, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            for (int i = 0; i < _flyTrail.Length; i++) _flyTrail[i] = LvImage(_lvUi, "Trail" + i, Art.Get("Effects/glow"), Vector2.zero, new Vector2(80f, 80f));
            LvImage(_flyGroup, "Card", LvArt("card_mini"), Vector2.zero, new Vector2(120f, 150f));
            _flyIcon = LvImage(_flyGroup, "Icon", null, new Vector2(0f, -4f), new Vector2(110f, 110f));
            HideLevelUpUi();
        }

        private static Image LvImage(Transform parent, string name, Sprite sprite, Vector2 at, Vector2 size)
        {
            Image img = UiKit.Image(parent, name, sprite != null ? sprite : Art.White, Color.white);
            img.raycastTarget = false;
            img.preserveAspect = true;
            UiKit.Place(img.rectTransform, new Vector2(0.5f, 0.5f), at, size);
            return img;
        }

        private void HideLevelUpUi()
        {
            if (_lvUi == null) return;
            _ovGroup.gameObject.SetActive(false);
            _ovNext.gameObject.SetActive(false);
            _bandGroup.gameObject.SetActive(false);
            _flyGroup.gameObject.SetActive(false);
            foreach (Image t in _flyTrail) t.gameObject.SetActive(false);
            _lvDark.color = new Color(0.02f, 0f, 0.06f, 0f);
        }

        /// <summary>카드를 고른 순간(숲). lv는 새 레벨(최고급 = 6).</summary>
        private void PlayLevelUp(UpgradeId id, int lv)
        {
            _lvId = id;
            _lvLv = Mathf.Clamp(lv, 1, 6);
            _lvAge = 0f;
            _lvBurstDone = false;
            _lvBurstAt = _lvLv == 1 ? 0.35f : _lvLv == 6 ? 0.55f : 0f;
            _slowmo = Mathf.Max(_slowmo, LvSlow[_lvLv]);
            string icon = SurvivorUpgrades.IconOf(id);
            Sprite iconArt = icon != null ? LvArt("icon_" + icon + (_lvLv >= 5 ? "_6" : "_1")) : null;
            _ovIcon.sprite = iconArt;
            _flyIcon.sprite = icon != null ? LvArt("icon_" + icon + "_1") : null;
            _ovText.sprite = _lvLv == 1 ? LvArt("txt_new") : _lvLv == 5 ? LvArt("txt_lv5max") : _lvLv <= 4 ? LvArt("txt_lv" + _lvLv) : null;
            _bandName.sprite = icon != null ? LvArt("name_" + icon) : null;
            GameAudio.Play(Cue.PickUp);
            if (_lvLv == 6)
            {
                // 0~0.55초: 빛 알갱이가 둘레에서 빨려 들어온다(샘플 mote 90개).
                Vector3 p = W(_sim.Player) + Up(0.8f);
                for (int i = 0; i < 90; i++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    float r = Random.Range(120f, 300f) * Px;
                    var from = p + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.7f, 0f);
                    float life = Random.Range(0.35f, 0.55f);
                    Color c = Rainbow(Random.value, 0.6f);
                    EmitSprite(LvArt("mote"), from, (p - from) / life, 0f, life, Random.Range(0.15f, 0.32f), 0.1f, c, new Color(c.r, c.g, c.b, 0.6f), 0f, true);
                }
                GameAudio.Play(Cue.Rescued);
            }
        }

        /// <summary>터지는 순간(샘플 doBurst): 고리·빛·별·꽃가루·물방울, 흔들림·번쩍·멈춤.</summary>
        private void LevelBurstFx()
        {
            int lv = _lvLv;
            bool gold = lv >= 5;
            bool rb = lv >= 6;
            Vector3 at = W(_sim.Player) + Up(0.3f);
            Color col = rb ? Color.white : gold ? LvGold : TierCore[lv];
            for (int i = 0; i < Mathf.Min(lv, 4); i++)
            {
                float size = (60f + (i * 34f) + (lv * 10f)) * Px * 2f;
                Color ring = rb ? Rainbow(i / 3f) : i == 0 ? Color.white : col;
                Shockwave(at, ring, size, 0.45f + (i * 0.12f), i * 0.06f);
            }
            if (rb)
            {
                for (int j = 0; j < 3; j++) Shockwave(at, Rainbow(j / 3f), 220f * Px * (1f + (j * 0.18f)), 0.6f, 0.1f + (j * 0.05f));
            }
            Color glow = rb ? new Color(1f, 0.94f, 0.78f) : gold ? new Color(1f, 0.86f, 0.55f) : TierCore[lv];
            EmitSprite(Art.Get("Effects/glow"), at, Vector3.zero, 0f, rb ? 0.3f : 0.45f, 2f, (rb ? 110f : 70f + (lv * 22f)) * Px * 2f, glow, new Color(glow.r, glow.g, glow.b, 0f), 0f, true);

            Sprite star = LvArt("spark_star");
            for (int i = 0; i < StarCount[lv]; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float v = Random.Range(90f, 220f + (lv * 40f)) * Px;
                Color c = rb ? Rainbow(Random.value) : gold ? (i % 2 == 0 ? LvGold : LvCream) : (i % 2 == 0 ? TierCore[lv] : LvIce);
                EmitSprite(star, at, new Vector3(Mathf.Cos(a) * v, Mathf.Sin(a) * v * 0.75f, 0f), 2.5f, Random.Range(0.5f, 0.9f + (lv * 0.1f)),
                    Random.Range(3f, 4f + lv) * Px * 3f, 0.05f, c, new Color(c.r, c.g, c.b, 0f), Random.Range(-360f, 360f), true);
            }
            if (lv >= 4)
            {
                Sprite conf = LvArt("confetti");
                for (int i = 0; i < (lv - 3) * 40; i++)
                {
                    float a = Random.Range(Mathf.PI * 0.05f, Mathf.PI * 0.95f);
                    float v = Random.Range(120f, 300f) * Px;
                    Color c = lv == 6 ? Rainbow(Random.value, 0.85f) : lv == 5 ? (Random.value < 0.5f ? LvGold : LvCream) : Rainbow(Random.value, 0.7f);
                    EmitSprite(conf, at + Up(0.5f), new Vector3(Mathf.Cos(a) * v, Mathf.Sin(a) * v, 0f), 1.5f, Random.Range(1f, 1.8f),
                        Random.Range(2f, 3.6f) * Px * 4f, 0.2f, c, new Color(c.r, c.g, c.b, 0f), Random.Range(-600f, 600f), false, -5f);
                }
            }
            Splash(at, 6 + (lv * 3), 1.4f);

            _trauma = Mathf.Min(1f, _trauma + LvShake[lv]);
            Flash(rb ? new Color(1f, 0.98f, 0.92f) : gold ? new Color(1f, 0.92f, 0.67f) : LvIce, LvFlash[lv]);
            HitStop(LvStop[lv]);
            _zoomKick = Mathf.Max(_zoomKick, 0.25f + (lv * 0.1f));
            if (lv >= 6)
            {
                GameAudio.Play(Cue.Won);
                GameAudio.Play(Cue.Won);
            }
            else if (lv >= 5) GameAudio.Play(Cue.Won);
            else GameAudio.Play(Cue.Rescued);
        }

        /// <summary>캔버스 좌표(가운데 원점): 월드 점이 화면 어디에 있나.</summary>
        private Vector2 CanvasOf(Vector3 world)
        {
            Vector3 v = _worldCam.WorldToViewportPoint(world);
            Rect r = _lvUi.rect;
            return new Vector2((v.x - 0.5f) * r.width, (v.y - 0.5f) * r.height);
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - ((1f - t) * (1f - t) * (1f - t));
        }

        /// <summary>매 프레임(풀 그리기 안에서): 광선·머리 위 아이콘·별·글자·이름 띠·날아오는 카드.</summary>
        private void TickLevelUp(float dt)
        {
            if (_lvAge < 0f || _lvUi == null) return;
            _lvAge += dt;
            int lv = _lvLv;
            if (!_lvBurstDone && _lvAge >= _lvBurstAt)
            {
                _lvBurstDone = true;
                LevelBurstFx();
            }
            float a = _lvAge - _lvBurstAt;
            Vector3 chest = W(_sim.Player) + Up(1f);

            // 광선: 개수·길이가 레벨마다 늘고 플레이어 뒤에서 돈다.
            float life = RayLife[lv];
            if (a >= 0f && a <= life)
            {
                float k = a / life;
                float alpha = (a < 0.12f ? a / 0.12f : 1f) * (1f - (k * k));
                int n = RayCount[lv];
                float len = (80f + (lv * 40f)) * Px * (0.6f + (Ease(a / 0.3f) * 0.4f)) * (lv == 6 ? 2.6f : 1f);
                float wide = len * 2f * (lv >= 5 ? 0.09f : 0.07f);
                float spin = _lvAge * (0.6f + (lv * 0.15f)) * Mathf.Rad2Deg;
                for (int i = 0; i < n; i++)
                {
                    float deg = (i * 360f / n) + spin;
                    Color c = lv == 6 ? Rainbow(i / (float)n) : lv == 5 ? (i % 2 == 1 ? LvGold : LvCream) : (i % 2 == 1 ? TierCore[lv] : LvIce);
                    c.a = (lv == 6 ? 0.45f : 0.75f) * alpha;
                    _lvRays.PutRot(chest, Billboard * Quaternion.Euler(0f, 0f, deg), wide, len, c);
                }
            }

            // Lv6 암전: 0~0.55초 어두워지고 폭발 뒤 걷힌다.
            float dark = lv == 6 ? (_lvAge < 0.55f ? _lvAge / 0.55f * 0.55f : Mathf.Max(0f, 0.55f - ((_lvAge - 0.55f) * 0.9f))) : 0f;
            _lvDark.color = new Color(0.02f, 0f, 0.06f, dark);

            // Lv1: 카드가 화면 아래에서 빛 꼬리를 끌고 날아와 머리 위에 박힌다.
            Vector2 head = CanvasOf(chest + Up(PersonTall));
            bool flying = lv == 1 && _lvAge < 0.35f;
            _flyGroup.gameObject.SetActive(flying);
            Vector2 start = new Vector2(0f, -_lvUi.rect.height * 0.5f - 80f);
            for (int i = 0; i < _flyTrail.Length; i++)
            {
                _flyTrail[i].gameObject.SetActive(flying);
                if (!flying) continue;
                float uu = Ease(Mathf.Max(0f, _lvAge - ((i + 1) * 0.02f)) / 0.35f);
                Vector2 tp = Vector2.Lerp(start, head, uu) + new Vector2(0f, Mathf.Sin(uu * Mathf.PI) * 230f);
                _flyTrail[i].rectTransform.anchoredPosition = tp;
                _flyTrail[i].rectTransform.sizeDelta = Vector2.one * (90f - (i * 8f));
                _flyTrail[i].color = new Color(150 / 255f, 215 / 255f, 1f, 0.5f * (1f - ((i + 1) / 9f)));
            }
            if (flying)
            {
                float u = Ease(_lvAge / 0.35f);
                _flyGroup.anchoredPosition = Vector2.Lerp(start, head, u) + new Vector2(0f, Mathf.Sin(u * Mathf.PI) * 230f);
                _flyGroup.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_lvAge * 14f) * 11f);
                _flyGroup.localScale = Vector3.one * 1.1f;
            }

            // 머리 위 아이콘 + 별 칸 + 레벨 글자.
            float show = lv == 6 ? 1.8f : 1.25f;
            bool over = a > 0f && a < show;
            _ovGroup.gameObject.SetActive(over);
            if (over)
            {
                float pop = a < 0.14f ? Ease(a / 0.14f) * 1.3f : a < 0.26f ? Mathf.Lerp(1.3f, 1f, (a - 0.14f) / 0.12f) : 1f;
                float fade = a > show - 0.3f ? (show - a) / 0.3f : 1f;
                _ovGroup.anchoredPosition = head + new Vector2(0f, 60f + (a * 40f));
                _ovGroup.localScale = Vector3.one * pop;
                Color g = lv >= 6 ? Rainbow() : lv >= 5 ? LvGold : TierCore[Mathf.Max(3, lv)];
                _ovGlow.color = new Color(g.r, g.g, g.b, 0.55f * fade);
                _ovIcon.color = new Color(1f, 1f, 1f, fade);
                for (int i = 0; i < 6; i++)
                {
                    bool on = i < lv;
                    string kind = !on ? "star_off" : i == 5 ? "star_rainbow" : lv >= 5 ? "star_gold" : "star_blue";
                    _ovStars[i].sprite = LvArt(kind);
                    float sc = i == lv - 1 ? 1f + (Mathf.Max(0f, 0.6f - a) * 1.5f) : 1f;
                    _ovStars[i].rectTransform.localScale = Vector3.one * sc;
                    _ovStars[i].color = i == 5 && on ? Color.Lerp(Color.white, Rainbow(), 0.4f) * new Color(1f, 1f, 1f, fade) : new Color(1f, 1f, 1f, fade);
                }
                _ovText.gameObject.SetActive(_ovText.sprite != null);
                _ovText.color = new Color(1f, 1f, 1f, fade);
                _ovText.rectTransform.localScale = Vector3.one * (0.8f + (lv * 0.08f));
            }
            bool next = lv == 5 && a > 0.35f && a < show;
            _ovNext.gameObject.SetActive(next);
            if (next)
            {
                float fade = Mathf.Min(1f, (a - 0.35f) * 4f) * (a > show - 0.3f ? (show - a) / 0.3f : 1f);
                _ovNext.rectTransform.anchoredPosition = CanvasOf(W(_sim.Player)) + new Vector2(0f, -90f);
                _ovNext.color = new Color(1f, 1f, 1f, fade);
            }

            // Lv6 이름 띠: 짙은 보라 띠 + 위아래 무지개 줄 + "최고급!" + 이름이 오른쪽에서 미끄러져 들어온다.
            bool band = lv == 6 && a > 0f && a < 1.8f;
            _bandGroup.gameObject.SetActive(band);
            if (band)
            {
                float k = a < 0.2f ? Ease(a / 0.2f) : 1f;
                float fade = a > 1.4f ? (1.8f - a) / 0.4f : 1f;
                _bandBack.rectTransform.localScale = new Vector3(1f, k, 1f);
                _bandBack.color = new Color(0.08f, 0.03f, 0.16f, 0.85f * fade);
                _bandTop.color = Rainbow(0f) * new Color(1f, 1f, 1f, 0.9f * fade);
                _bandBottom.color = Rainbow(0.5f) * new Color(1f, 1f, 1f, 0.9f * fade);
                _bandTop.rectTransform.anchoredPosition = new Vector2(0f, 108f * k);
                _bandBottom.rectTransform.anchoredPosition = new Vector2(0f, -108f * k);
                _bandTitle.color = new Color(1f, 1f, 1f, fade);
                _bandName.color = Color.Lerp(Color.white, Rainbow(), 0.25f) * new Color(1f, 1f, 1f, fade);
                _bandName.rectTransform.anchoredPosition = new Vector2((1f - k) * 1200f, -28f);
            }

            if (_lvAge > Mathf.Max(show + _lvBurstAt, RayLife[lv] + _lvBurstAt) + 0.1f)
            {
                _lvAge = -1f;
                HideLevelUpUi();
            }
        }
    }
}
