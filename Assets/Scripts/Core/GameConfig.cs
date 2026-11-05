using System;

namespace SonTob.Core
{
    /// <summary>
    /// 모든 난이도 수치. 기획서 v4(4장 날짜별 난이도)와 같은 이름을 씁니다.
    /// 숫자를 바꿀 곳은 여기 한 곳뿐입니다.
    /// 난이도는 시작 화면에서 고르지 않고 날마다 어려워집니다(1일 쉬움 → 2일 보통 → 3일 어려움).
    /// </summary>
    public class GameConfig
    {
        // 날짜별 값 (1일, 2일, 3일)
        public int[] DisguiseBeatsByDay = { 32, 30, 28 }; // B: 분이 기준 둔갑 시간
        public int[] CatRingByDay = { 1, 2, 3 };          // 밤 고양이 냄새 고리(칸)
        public int[] ThrowShiftByDay = { 20, 0, -20 };    // 던짐 비율 조절(%p)
        public int[] NightBeatsByDay = { 24, 22, 20 };    // 쥐 시간: 해 뜨기 전 박자

        // 아래 네 값은 그날의 값입니다. ForDay(day)가 위 표에서 채웁니다.
        public int BaseDisguiseBeats = 30;
        public int CatRing = 2;
        public int ThrowShiftPercent = 0;
        public int NightBeats = 22;

        // 낮
        public int PersonBeatOffset = 2;     // d: 돌쇠 아빠 B-d, 훈장 어른 B+d
        public int WorkBeatsPerMission = 2;  // w: 임무마다 '하기' 횟수
        public int SightLength = 3;          // 사람 시선 길이(칸), 사흘 모두 같음
        public int RunSeenSuspicion = 30;    // 뛰다 시선에 걸리면
        public int RealPersonSuspicion = 40; // 진짜에게 보이면(걸어도)
        public float WalkHunger = 0.8f;      // 걷기·하기·기다리기 1박자
        public float RunHunger = 2f;         // 뛰기 1박자
        public int WashBeats = 2;            // 우물에서 씻기

        // 냄새 고리(낮)
        public int ScentStart = 1;
        public int ScentPerRunTiles = 6;
        public int ScentMax = 3;

        // 밤
        public int NailHunger = 50;
        public int NightHunger = 20;
        public int NightCalm = 20;           // 밤마다 의심 −20 (2일부터)
        public int SniffRange = 2;           // 손톱에서 이 칸 안이면 킁킁
        public int DigBeats = 3;             // 묻은 손톱 파기
        public int BurnSuspicion = 10;       // 태운 손톱을 먹으면 다음 날 의심
        public int RunNoiseRing = 1;         // 뛴 박자에는 고양이 고리 +1
        public int CatLoseFactor = 2;        // 고리의 몇 배 밖으로 벗어나면 놓치나

        // 판 전체
        public int Days = 3;
        public float StartHunger = 70f;
        public float MaxHunger = 100f;
        public float MaxSuspicion = 100f;
        public int GaugeCells = 10;          // 게이지는 10칸으로 보여 줌(1칸 = 10)

        static int At(int[] table, int day)
        {
            int i = Math.Max(0, Math.Min(table.Length - 1, day - 1));
            return table[i];
        }

        /// <summary>그날(1부터)의 값을 채운 복사본.</summary>
        public GameConfig ForDay(int day)
        {
            var c = (GameConfig)MemberwiseClone();
            c.BaseDisguiseBeats = At(DisguiseBeatsByDay, day);
            c.CatRing = At(CatRingByDay, day);
            c.ThrowShiftPercent = At(ThrowShiftByDay, day);
            c.NightBeats = At(NightBeatsByDay, day);
            return c;
        }
    }
}
