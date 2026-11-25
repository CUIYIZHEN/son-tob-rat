using System;

namespace SonTob.Core
{
    /// <summary>
    /// 모든 난이도 수치. 기획서(2장 핵심 규칙, 4장 난이도 설계)와 같은 이름을 씁니다.
    /// 숫자를 바꿀 곳은 여기 한 곳뿐이고, 근거는 코드가 아니라 기획서에 적습니다.
    /// 프로토타입은 밤 1번과 낮 1번이며 난이도 선택은 없습니다.
    /// </summary>
    public class GameConfig
    {
        // 밤
        public int NightSteps = 24;          // 해가 뜨기 전 걸음
        public int BonusDivisor = 4;         // 남은 걸음 4당 낮 걸음 +1
        public int BonusCap = 3;             // 보너스 상한

        // 낮
        public int DaySteps = 26;            // 시작 남은 걸음 (보너스 전)
        public int SightLength = 3;          // 사람 · 고양이 · 쥐의 시선(칸)
        public int RunSeenSuspicion = 30;    // 달리다 눈에 들면
        public int RealPersonSuspicion = 40; // 진짜 도령에게 들키면(걸어도)
        public int MaxSuspicion = 100;       // 쥐불놀이
        public int GaugeCells = 10;          // 게이지는 10칸으로 보여 줌(1칸 = 10)

        /// <summary>손톱을 먹은 걸음에 따른 낮 보너스: min(상한, ⌊(밤 걸음 − 먹은 걸음) ÷ 나누는 수⌋).</summary>
        public int NightBonus(int eatenAtStep)
        {
            int left = Math.Max(0, NightSteps - eatenAtStep);
            return Math.Min(BonusCap, left / BonusDivisor);
        }
    }
}
