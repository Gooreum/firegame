using FireGame.UnityLayer;
using UnityEngine;

namespace FireGame.Prototypes
{
    /// <summary>
    /// 스킬 그림(2026-10-07): 사용자가 승인한 웹 캔버스 샘플의 그리기 함수로 구운 스프라이트(tools/bake-skill-art.sh → Art/Skills).
    /// 단색 픽셀 칠(PaintSprite) 대신 그라데이션·외곽선이 있는 그림을 쓴다. 256px 한 장 = 샘플 단위 2×ext.
    /// </summary>
    public sealed partial class SurvivorView
    {
        private static Sprite SkillSprite(string name)
        {
            return Art.Get("Skills/" + name);
        }
    }
}
