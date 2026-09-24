using System.Collections.Generic;
using UnityEngine;

namespace FireGame.UnityLayer
{
    /// <summary>지붕 위에 얹는 부속물. 이것 하나로 같은 상자가 집이 되기도 공장이 되기도 한다.</summary>
    public enum RoofFixture
    {
        /// <summary>굴뚝 + 지붕창. 주택.</summary>
        Chimney = 0,

        /// <summary>옥상 실외기. 상가.</summary>
        Units = 1,

        /// <summary>캐노피 기둥 네 개. 주유소.</summary>
        Pillars = 2,

        /// <summary>톱니 채광창. 창고.</summary>
        Skylight = 3,

        /// <summary>굴뚝 + 덕트 배관. 공장.</summary>
        Duct = 4,

        /// <summary>환기구. 항구.</summary>
        Vent = 5,
    }

    /// <summary>
    /// 마당에 놓인 장식물 덩어리를 무엇으로 그릴지.
    ///
    /// 맵 문자에는 <c>o</c> 한 종류뿐이다. 무엇으로 보이는가는 현장과
    /// <b>붙어 있는 덩어리의 칸 수</b>가 정한다 — 맵 문자열의 <c>o</c>와 <c>oo</c>가
    /// 눈에 보이는 그대로 다른 물건이 된다.
    /// </summary>
    public sealed class PropKit
    {
        private readonly PropLook _small;    // 1칸
        private readonly PropLook _medium;   // 2~3칸
        private readonly PropLook _large;    // 4칸 이상

        public PropKit(PropLook small, PropLook medium, PropLook large)
        {
            _small = small;
            _medium = medium;
            _large = large;
        }

        public PropLook For(int cells)
        {
            if (cells >= 4) return _large;
            if (cells >= 2) return _medium;
            return _small;
        }
    }

    /// <summary>물건 하나의 생김새.</summary>
    public sealed class PropLook
    {
        private readonly string _resource;      // 켄니 그림 경로. null이면 코드로 찍는다.
        private readonly PropStyle _style;
        private readonly Color _tint;

        /// <summary>덩어리 상자를 얼마나 채울지. 1이면 꽉 채운다.</summary>
        public readonly float Fill;

        /// <summary>
        /// 위아래가 있는 그림인지. 세로로 긴 덩어리에 놓을 때 90도 돌린다.
        /// 나무처럼 어느 쪽에서 봐도 같은 것은 false.
        /// </summary>
        public readonly bool Upright;

        /// <summary>
        /// 덩어리 상자에 늘여 맞출지. 코드로 찍은 소품은 칸을 채우라고 만든 것이라 늘인다.
        /// 켄니 그림은 제 비율이 있어 늘이면 찌그러지므로 비율을 지킨다.
        /// </summary>
        public readonly bool Stretch;

        private PropLook(string resource, PropStyle style, Color tint, float fill, bool upright, bool stretch)
        {
            _resource = resource;
            _style = style;
            _tint = tint;
            Fill = fill;
            Upright = upright;
            Stretch = stretch;
        }

        /// <summary>켄니 팩에서 가져온 그림.</summary>
        public static PropLook Art(string resource, float fill, bool upright)
        {
            return new PropLook(resource, PropStyle.Pump, Color.white, fill, upright, false);
        }

        /// <summary>같은 그림을 색만 바꿔 쓴다(파란 드럼통 / 붉은 드럼통).</summary>
        public static PropLook Tinted(string resource, Color tint, float fill, bool upright)
        {
            return new PropLook(resource, PropStyle.Pump, tint, fill, upright, false);
        }

        /// <summary>켄니에 없어서 코드로 찍는 그림. 칸을 채우라고 만든 것이라 늘여 맞춘다.</summary>
        public static PropLook Drawn(PropStyle style, float fill, bool upright)
        {
            return new PropLook(null, style, Color.white, fill, upright, true);
        }

        public Sprite Sprite
        {
            get { return _resource != null ? UnityLayer.Art.Get(_resource) : UnityLayer.Art.PropTexture(_style); }
        }

        public Color Tint
        {
            get { return _tint; }
        }
    }

    /// <summary>
    /// 현장 하나의 생김새를 한 곳에 모은다.
    ///
    /// 여섯 현장을 갈라 놓던 것이 바닥 스프라이트 두 장과 지붕 색 하나뿐이라,
    /// 주유소와 항구가 "바탕색만 다른 같은 그림"으로 보였다.
    /// 지붕 재질·부속물·바닥·경계·소품·간판을 전부 여기서 정한다.
    /// </summary>
    public sealed class SiteTheme
    {
        public readonly RoofStyle Roof;
        public readonly Color RoofColor;
        public readonly RoofFixture Fixture;
        public readonly GroundStyle Ground;
        public readonly BorderStyle Border;
        public readonly PropKit Props;

        /// <summary>
        /// 앞벽 재질과 바탕색. 시선을 눕혀 건물 앞면이 보이게 되면서 새로 필요해졌다 —
        /// 지붕만 현장별이면 벽은 어느 현장이나 같은 회색이 된다.
        /// </summary>
        public readonly FacadeStyle Facade;
        public readonly Color WallColor;

        /// <summary>창틀·문틀. 벽보다 진해야 창이 창으로 보인다.</summary>
        public readonly Color TrimColor;

        /// <summary>입구 간판 글자. 지붕이 덮인 밖에서 "여기로 들어간다"를 말한다.</summary>
        public readonly string SignLabel;

        private SiteTheme(
            RoofStyle roof, Color roofColor, RoofFixture fixture,
            GroundStyle ground, BorderStyle border, PropKit props, string signLabel,
            FacadeStyle facade, Color wallColor, Color trimColor)
        {
            Roof = roof;
            RoofColor = roofColor;
            Fixture = fixture;
            Ground = ground;
            Border = border;
            Props = props;
            SignLabel = signLabel;
            Facade = facade;
            WallColor = wallColor;
            TrimColor = trimColor;
        }

        private static readonly Dictionary<int, SiteTheme> Table = new Dictionary<int, SiteTheme>
        {
            // 0 주택가 — 붉은 기와에 굴뚝. 마당에 나무.
            {
                0, new SiteTheme(
                    RoofStyle.Tile, new Color(0.82f, 0.34f, 0.28f), RoofFixture.Chimney,
                    GroundStyle.Lawn, BorderStyle.Hedge,
                    new PropKit(
                        PropLook.Art("Props/tree_small", 1.1f, false),
                        PropLook.Art("Props/tree_large", 1.15f, false),
                        PropLook.Art("Props/tree_large", 1.15f, false)),
                    "주택",
                    FacadeStyle.Plaster, new Color(0.94f, 0.89f, 0.79f), new Color(0.52f, 0.33f, 0.24f))
            },

            // 1 상가 — 파란 차양에 옥상 실외기. 앞에 화단과 주차 차량.
            {
                1, new SiteTheme(
                    RoofStyle.Awning, new Color(0.32f, 0.54f, 0.82f), RoofFixture.Units,
                    GroundStyle.Paving, BorderStyle.Planter,
                    new PropKit(
                        PropLook.Drawn(PropStyle.Planter, 1f, false),
                        PropLook.Art("Vehicles/car_blue", 0.9f, true),
                        PropLook.Art("Vehicles/car_black", 0.9f, true)),
                    "상가",
                    FacadeStyle.Glass, new Color(0.86f, 0.90f, 0.94f), new Color(0.34f, 0.38f, 0.44f))
            },

            // 2 주유소 — 흰 캐노피에 기둥. 마당에 주유기 섬과 유조차.
            //   지붕을 거의 흰색으로 두는 것이 주유소 캐노피답다.
            {
                2, new SiteTheme(
                    RoofStyle.Canopy, new Color(0.93f, 0.95f, 0.96f), RoofFixture.Pillars,
                    GroundStyle.Asphalt, BorderStyle.Guardrail,
                    new PropKit(
                        PropLook.Art("Vehicles/cone", 0.8f, false),
                        PropLook.Drawn(PropStyle.Pump, 1f, true),
                        PropLook.Drawn(PropStyle.Pump, 1f, true)),
                    "주유소",
                    FacadeStyle.Tile, new Color(0.96f, 0.96f, 0.95f), new Color(0.84f, 0.26f, 0.22f))
            },

            // 3 물류창고 — 회청 함석에 채광창. 야적장에 컨테이너와 팔레트.
            {
                3, new SiteTheme(
                    RoofStyle.Metal, new Color(0.56f, 0.62f, 0.70f), RoofFixture.Skylight,
                    GroundStyle.Yard, BorderStyle.Fence,
                    new PropKit(
                        PropLook.Drawn(PropStyle.Pallet, 0.95f, false),
                        PropLook.Drawn(PropStyle.Pallet, 0.95f, true),
                        PropLook.Drawn(PropStyle.Container, 1f, true)),
                    "물류",
                    FacadeStyle.Ribbed, new Color(0.70f, 0.74f, 0.79f), new Color(0.36f, 0.41f, 0.47f))
            },

            // 4 공장 — 청회 슬레이트에 굴뚝과 덕트. 마당에 드럼통과 배관.
            {
                4, new SiteTheme(
                    RoofStyle.Panel, new Color(0.34f, 0.46f, 0.62f), RoofFixture.Duct,
                    GroundStyle.Concrete, BorderStyle.Fence,
                    new PropKit(
                        PropLook.Art("Props/barrel_red", 0.95f, false),
                        PropLook.Art("Props/barrier", 0.95f, true),
                        PropLook.Tinted("Props/barrel_blue", Color.white, 0.95f, false)),
                    "공장",
                    FacadeStyle.Precast, new Color(0.72f, 0.72f, 0.70f), new Color(0.40f, 0.44f, 0.50f))
            },

            // 5 항구 — 적갈 널판에 환기구. 부두에 컨테이너 스택과 계선주.
            {
                5, new SiteTheme(
                    RoofStyle.Plank, new Color(0.70f, 0.42f, 0.30f), RoofFixture.Vent,
                    GroundStyle.Dock, BorderStyle.Seawall,
                    new PropKit(
                        PropLook.Drawn(PropStyle.Bollard, 0.9f, false),
                        PropLook.Art("Props/tires", 0.9f, false),
                        PropLook.Drawn(PropStyle.Container, 1f, true)),
                    "부두",
                    FacadeStyle.Board, new Color(0.74f, 0.56f, 0.42f), new Color(0.40f, 0.26f, 0.18f))
            },
        };

        /// <summary>
        /// 출동해서 내린 소방차. 스폰에 가장 가까운 덩어리는 어느 현장이든 이것으로 그린다.
        /// 크기로 고르면 두 칸짜리 주유기 섬과 구별되지 않는다.
        /// </summary>
        public static PropLook FireTruck
        {
            get { return PropLook.Art("Vehicles/firetruck", 0.95f, true); }
        }

        /// <summary>현장 테마. 모르는 현장은 주택으로 둔다 — 화면이 비는 것보다 낫다.</summary>
        public static SiteTheme Of(int stageId)
        {
            SiteTheme theme;
            return Table.TryGetValue(stageId, out theme) ? theme : Table[0];
        }
    }
}
