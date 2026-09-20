using System;

namespace FireGame.Core.Grid
{
    /// <summary>
    /// 화재 등급. 어떤 소화 약제가 통하는지를 결정하며,
    /// 이 게임의 전술적 재미가 전부 여기서 나온다.
    /// </summary>
    public enum FireClass : byte
    {
        /// <summary>불연성 재질.</summary>
        None = 0,

        /// <summary>일반 가연물(목재·종이). 물이 가장 잘 듣는다.</summary>
        A = 1,

        /// <summary>유류. 물을 뿌리면 오히려 번진다.</summary>
        B = 2,

        /// <summary>전기. 물은 위험하고 CO2가 정답.</summary>
        C = 3,
    }

    /// <summary>재질 id. <see cref="Materials.All"/> 배열의 인덱스와 일치해야 한다.</summary>
    public enum MaterialId : byte
    {
        Floor = 0,
        Wood = 1,
        Concrete = 2,
        Oil = 3,
        Electric = 4,
        Hydrant = 5,
        Door = 6,
        Exit = 7,
    }

    /// <summary>재질 한 종류의 물성. 값이 바뀌지 않으므로 readonly struct.</summary>
    public readonly struct CellMaterial
    {
        /// <summary>디버깅·렌더링용 이름.</summary>
        public readonly string Name;

        /// <summary>발화에 필요한 누적 열. 불연성 재질은 PositiveInfinity.</summary>
        public readonly float Ignite;

        /// <summary>초당 소모하는 연료량.</summary>
        public readonly float BurnRate;

        /// <summary>초당 이웃으로 내보내는 열량.</summary>
        public readonly float HeatOutput;

        public readonly FireClass Class;

        /// <summary>플레이어와 시민이 통과할 수 있는지.</summary>
        public readonly bool Walkable;

        public CellMaterial(string name, float ignite, float burnRate, float heatOutput, FireClass fireClass, bool walkable)
        {
            Name = name;
            Ignite = ignite;
            BurnRate = burnRate;
            HeatOutput = heatOutput;
            Class = fireClass;
            Walkable = walkable;
        }

        /// <summary>탈 수 있는 재질인지. 발화점이 무한대면 불연성이다.</summary>
        public bool Flammable
        {
            get { return !float.IsPositiveInfinity(Ignite); }
        }
    }

    /// <summary>재질 테이블과 맵 문자 매핑.</summary>
    public static class Materials
    {
        private const float Inf = float.PositiveInfinity;

        /// <summary>
        /// 인덱스 = <see cref="MaterialId"/> 값. 순서를 바꾸면 저장된 맵이 전부 깨진다.
        ///
        /// 연소속도(BurnRate) 설계 원칙: <b>A급만 스스로 꺼진다.</b>
        /// 목재는 연료를 태워 없애고 저절로 진화되지만, 유류와 전기는 사실상
        /// 꺼지지 않는다(유류 200초, 전기 250초 — 어떤 스테이지 제한시간보다도 길다).
        ///
        /// 이 규칙이 장비 진행의 근거다. B·C급이 알아서 꺼지면 맞는 약제를 살 이유가
        /// 없어진다. 못 끄는 장비를 들고도 그냥 기다리면 이기기 때문이다.
        /// 실제로도 배전반 화재는 전원을 끊어야, 유류 풀 화재는 덮어야 꺼진다.
        /// </summary>
        public static readonly CellMaterial[] All =
        {
            //                 이름          발화점  연소속도  발열   등급              통행
            new CellMaterial("Floor",    Inf,   0f,     0f,   FireClass.None, true),
            new CellMaterial("Wood",     0.35f, 0.25f,  1.0f, FireClass.A,    false),
            new CellMaterial("Concrete", Inf,   0f,     0f,   FireClass.None, false),
            // 유류 풀 화재는 덮어서 질식시키기 전에는 꺼지지 않는다(200초).
            //
            // 발열 2.0 / 발화점 0.15 였을 때는 확산은 빨랐지만 군집 유지 열량이 너무 높아
            // 폼으로도 진압이 불가능했다. 발열을 1.2로 낮추고 발화점을 0.10으로 함께 낮춰
            // 확산 속도(5칸/초, 플레이어보다 빠르다)는 그대로 두고 진압만 가능하게 했다.
            new CellMaterial("Oil",      0.10f, 0.005f, 1.2f, FireClass.B,    true),
            // 배전반 화재는 전원을 끊거나 CO2로 덮기 전에는 사실상 꺼지지 않는다(250초).
            new CellMaterial("Electric", 0.25f, 0.004f, 0.6f, FireClass.C,    false),
            new CellMaterial("Hydrant",  Inf,   0f,     0f,   FireClass.None, false),
            new CellMaterial("Door",     0.40f, 0.20f,  0.8f, FireClass.A,    true),
            new CellMaterial("Exit",     Inf,   0f,     0f,   FireClass.None, true),
        };

        public static CellMaterial Of(byte materialId)
        {
            return All[materialId];
        }

        public static CellMaterial Of(MaterialId materialId)
        {
            return All[(int)materialId];
        }

        public static CellMaterial Of(in Cell cell)
        {
            return All[cell.Material];
        }

        /// <summary>
        /// 맵 문자를 재질로 변환한다. 맵을 문자열로 두면 에디터 툴 없이
        /// 소스에서 바로 레벨을 읽고 고칠 수 있다.
        /// </summary>
        /// <param name="mapChar">맵 문자.</param>
        /// <param name="materialId">대응하는 재질.</param>
        /// <returns>매핑이 존재하면 true.</returns>
        public static bool TryFromMapChar(char mapChar, out MaterialId materialId)
        {
            switch (mapChar)
            {
                case '.': materialId = MaterialId.Floor; return true;
                case '#': materialId = MaterialId.Concrete; return true;
                case 'W': materialId = MaterialId.Wood; return true;
                case '~': materialId = MaterialId.Oil; return true;
                case 'E': materialId = MaterialId.Electric; return true;
                case 'H': materialId = MaterialId.Hydrant; return true;
                case 'D': materialId = MaterialId.Door; return true;
                case 'X': materialId = MaterialId.Exit; return true;

                // 아래 세 문자는 재질이 아니라 배치 마커다.
                // 바닥으로 깔고 좌표만 따로 수집한다.
                case '@': materialId = MaterialId.Floor; return true;
                case '!': materialId = MaterialId.Floor; return true;

                // 초기 발화점. 재질을 깔고 곧바로 연소 상태로 만든다.
                case '*': materialId = MaterialId.Wood; return true;
                case '%': materialId = MaterialId.Oil; return true;
                case '$': materialId = MaterialId.Electric; return true;

                default:
                    materialId = MaterialId.Floor;
                    return false;
            }
        }
    }
}
