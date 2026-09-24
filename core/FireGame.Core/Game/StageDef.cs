using FireGame.Core.Sim;

namespace FireGame.Core.Game
{
    /// <summary>레벨 하나의 정의. 맵과 난이도 설정을 담는다.</summary>
    public sealed class StageDef
    {
        public readonly int Id;
        public readonly string Name;

        /// <summary>문자열 맵. <see cref="Grid.MapLoader"/> 문자 규칙을 따른다.</summary>
        public readonly string[] Map;

        /// <summary>바람. 사실상 이 스테이지의 난이도 다이얼이다.</summary>
        public readonly Wind Wind;

        public readonly float TimeLimitSeconds;
        public readonly int BasePayout;

        /// <summary>
        /// 화재 규모. 타는 칸이 스스로 유지하는 열의 배율로, 클수록 한 칸을 끄는 데 약제가 더 든다.
        /// 기름·전기 불은 스스로 꺼지지 않으니 이 값이 곧 "몇 레벨 소화기가 있어야 끌 수 있는가"가 된다.
        /// </summary>
        public readonly float FireIntensity;

        public StageDef(
            int id,
            string name,
            string[] map,
            Wind wind,
            float timeLimitSeconds,
            int basePayout,
            float fireIntensity = 1f)
        {
            Id = id;
            Name = name;
            Map = map;
            Wind = wind;
            TimeLimitSeconds = timeLimitSeconds;
            BasePayout = basePayout;
            FireIntensity = fireIntensity;
        }
    }

    /// <summary>한 판의 진행 상태.</summary>
    public enum StageOutcome : byte
    {
        InProgress = 0,
        Won = 1,

        /// <summary>건물이 너무 많이 타버렸다.</summary>
        LostBuildingDestroyed = 2,

        /// <summary>소방관이 쓰러졌다.</summary>
        LostPlayerDown = 3,

        LostTimeUp = 4,
    }

    /// <summary>한 프레임의 플레이어 입력.</summary>
    public struct StageInput
    {
        public float MoveX;
        public float MoveY;

        /// <summary>이번 프레임에 발사를 시도하는지.</summary>
        public bool Fire;

        /// <summary>사용할 장비 슬롯.</summary>
        public int Slot;

        /// <summary>
        /// 이번 프레임에 구조 버튼을 눌렀는지.
        /// 시민은 저절로 업히지 않는다 — 지나가다 말없이 업히면 구조한 느낌이 없다.
        /// </summary>
        public bool Rescue;
    }

    /// <summary>구조 대상 시민.</summary>
    public sealed class Civilian
    {
        public float X;
        public float Y;

        /// <summary>플레이어가 업고 있는 중.</summary>
        public bool Carried;

        public bool Rescued;

        /// <summary>불에 휩싸여 구조하지 못한 상태.</summary>
        public bool Lost;

        /// <summary>
        /// 한 번이라도 눈에 들어왔는지. 보고 나면 계속 표시된다.
        /// 화면은 이걸 보고 말풍선을 띄울지, 방향만 알려줄지 고른다 —
        /// 아직 못 찾은 사람의 자리를 화면이 먼저 알려주면 찾을 이유가 없어진다.
        /// </summary>
        public bool Spotted;

        /// <summary>아직 맵 위에서 구조를 기다리는 중인지.</summary>
        public bool Pending
        {
            get { return !Rescued && !Lost; }
        }
    }

    /// <summary>한 판이 끝난 뒤 정산에 넘기는 집계값.</summary>
    public struct StageResult
    {
        public int StageId;
        public int BasePayout;
        public bool Won;
        public int Rescued;
        public int CiviliansTotal;
        public float IntactRatio;
        public float TimeLeft;

        /// <summary>물을 머금은 셀 수. 수손 피해로 정산에서 차감된다.</summary>
        public int WetCellCount;

        public int ShotsFired;
        public int CellsExtinguished;

        /// <summary>출동에서 끝날 때까지 걸린 시간(초).</summary>
        public float ElapsedSeconds;
    }
}
