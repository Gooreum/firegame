using System;

namespace FireGame.Prototypes.Logic
{
    /// <summary>숲 샘플 아이템을 만든다(아이템별 클래스는 SampleItems.*.cs). 아직 옮기지 않은 아이템은 null.</summary>
    public static partial class SampleItemsRegistry
    {
        public static SampleItem Make(UpgradeId id)
        {
            SampleItem made = null;
            MakeA(id, ref made);
            MakeB(id, ref made);
            MakeC(id, ref made);
            MakeD(id, ref made);
            if (made != null) made.Base = id;
            return made;
        }

        static partial void MakeA(UpgradeId id, ref SampleItem made);
        static partial void MakeB(UpgradeId id, ref SampleItem made);
        static partial void MakeC(UpgradeId id, ref SampleItem made);
        static partial void MakeD(UpgradeId id, ref SampleItem made);
    }
}
