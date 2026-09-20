namespace FireGame.Core.Game
{
    /// <summary>정산 내역. 결과 화면에 항목별로 보여주기 위한 것이다.</summary>
    public struct PayoutBreakdown
    {
        public int Base;
        public int Rescue;
        public int Integrity;
        public int Time;

        /// <summary>수손 피해 차감액. 음수로 담는다.</summary>
        public int WaterDamage;

        public int Total;
    }

    /// <summary>
    /// 한 판의 보상 계산.
    ///
    /// 수손 페널티가 이 게임 경제의 핵심이다.
    /// 이게 없으면 "맵 전체에 물을 퍼부으면 이김"이 최적해가 되어
    /// 장비 상성도 조준도 전부 무의미해진다.
    /// </summary>
    public static class Economy
    {
        public const int RescueBonus = 150;
        public const int IntactBonusMax = 500;
        public const int TimeBonusPerSecond = 5;
        public const int WaterDamagePerCell = 2;

        public static PayoutBreakdown Breakdown(in StageResult result)
        {
            var breakdown = default(PayoutBreakdown);

            // 진 판에는 보상이 없다. 양동이는 공짜이므로
            // 돈이 0이어도 다시 도전할 수 있어 막히지 않는다.
            if (!result.Won) return breakdown;

            breakdown.Base = result.BasePayout;
            breakdown.Rescue = result.Rescued * RescueBonus;
            breakdown.Integrity = (int)(result.IntactRatio * IntactBonusMax);
            breakdown.Time = (int)(result.TimeLeft * TimeBonusPerSecond);
            breakdown.WaterDamage = -(result.WetCellCount * WaterDamagePerCell);

            int total = breakdown.Base
                        + breakdown.Rescue
                        + breakdown.Integrity
                        + breakdown.Time
                        + breakdown.WaterDamage;

            breakdown.Total = total < 0 ? 0 : total;
            return breakdown;
        }

        public static int Payout(in StageResult result)
        {
            return Breakdown(result).Total;
        }
    }
}
