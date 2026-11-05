namespace SonTob.Core
{
    /// <summary>
    /// 모든 난이도 수치. 기획서 v3과 경제 실험실(nail-lab-v4)의 손잡이와 같은 이름을 씁니다.
    /// 숫자를 바꿀 곳은 여기 한 곳뿐입니다.
    /// </summary>
    public class GameConfig
    {
        // 낮
        public int BaseDisguiseBeats = 30;   // B: 분이 기준 둔갑 박자
        public int PersonBeatOffset = 2;     // d: 돌쇠 아빠 B-d, 훈장 어른 B+d
        public int WorkBeatsPerMission = 2;  // w: 임무마다 '하기' 횟수
        public int SightLength = 3;          // 사람 시선 길이(칸)
        public int RunSeenSuspicion = 35;    // 뛰다 시선에 걸리면
        public int RealPersonSuspicion = 40; // 진짜에게 보이면(걸어도)
        public float WalkHunger = 0.8f;      // 걷기·하기·기다리기 1박자
        public float RunHunger = 2f;         // 뛰기 1박자
        public int WashBeats = 2;            // 우물에서 씻기

        // 냄새 고리
        public int ScentStart = 1;
        public int ScentPerRunTiles = 6;
        public int ScentMax = 3;

        // 밤
        public int CatHearing = 2;           // 잠귀 범위(칸)
        public int ThrowShiftPercent = 0;    // 던짐 비율 조절(%p)
        public int NailHunger = 50;
        public int NightHunger = 20;
        public int NightCalm = 20;           // 밤마다 의심 −20
        public int CatChaseSpeed = 2;        // 내가 한 번 움직일 때 고양이 칸 수
        public int TwitchesToWake = 2;

        // 판 전체
        public int Days = 3;
        public float StartHunger = 70f;
        public float MaxHunger = 100f;
        public float MaxSuspicion = 100f;

        public static GameConfig Easy()
        {
            var c = new GameConfig();
            c.BaseDisguiseBeats = 32; c.CatHearing = 1; c.ThrowShiftPercent = 20;
            return c;
        }

        public static GameConfig Normal() { return new GameConfig(); }

        public static GameConfig Hard()
        {
            var c = new GameConfig();
            c.BaseDisguiseBeats = 28; c.CatHearing = 3; c.ThrowShiftPercent = -20;
            return c;
        }
    }
}
