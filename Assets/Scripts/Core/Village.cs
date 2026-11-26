using System.Collections.Generic;

namespace SonTob.Core
{
    /// <summary>낮에 둔갑하는 도령과 그의 임무 셋(기획서 3.2).</summary>
    public class Lord
    {
        public string Name = "도령";
        public char[] Missions = { 'h', 'i', 'j' };
        public string[] MissionNames = { "서당 글 읽기", "정자 시 읽기", "장승 살피기" };
    }

    /// <summary>
    /// 마을 사람(고양이 포함). Loop를 주면 그 칸 목록을 그대로 돌고,
    /// 없으면 Waypoints 사이를 가장 짧은 길로 오가며 돕니다.
    /// </summary>
    public class NpcSpec
    {
        public string Name;
        public Pos[] Waypoints;      // 2개 이상이면 그 사이를 오감
        public Pos[] Loop;           // 한 바퀴 칸 목록(마지막 칸 다음은 첫 칸)
        public Dir[] LookCycle;      // 서 있는 사람이 돌아보는 순서
        public int TurnEvery = 2;    // 몇 걸음마다 고개를 돌리나
        public bool IsRealLord;      // 진짜 도령이면 마주친 것만으로 의심
    }

    public static class Village
    {
        /// <summary>
        /// 마을 지도 12칸 × 12줄(기획서 3.3). # 담장·집, . 길, d 개구멍(담 아래 반원, 달릴 때만 지남),
        /// R 쥐구멍, N 손톱, W 우물, h i j 임무 칸(서당 · 정자 · 장승).
        /// 쥐구멍에서 세 임무 칸을 도는 가장 짧은 걸음 길 L = 30.
        /// </summary>
        public static readonly string[] MapRows =
        {
            "##..i..#....",
            "##h#...#.#.#",
            "...#.###.#..",
            ".#.#..W..d..",
            "..d..##.##..",
            "###..##..#..",
            "...#.R..##..",
            "..##.##.#...",
            "...d.##.#.##",
            "##.#...##...",
            "...#.#.N.##.",
            "j#...#.#...."
        };

        public const char RatHole = 'R';
        public const char NailMark = 'N';
        public const char Well = 'W';

        public static MapData CreateMap() { return new MapData(MapRows); }

        public static readonly Lord TheLord = new Lord();

        /// <summary>고양이가 한 바퀴 도는 52칸. (10, 9)에서 시작해 오른쪽으로 갑니다. 8걸음에 손톱 칸을 지납니다.</summary>
        public static readonly Pos[] CatLoop =
        {
            new Pos(10, 9), new Pos(11, 9), new Pos(11, 10), new Pos(11, 11),
            new Pos(10, 11), new Pos(9, 11), new Pos(8, 11), new Pos(8, 10),
            new Pos(7, 10), new Pos(6, 10), new Pos(6, 9), new Pos(5, 9),
            new Pos(4, 9), new Pos(4, 8), new Pos(3, 8), new Pos(2, 8),
            new Pos(1, 8), new Pos(0, 8), new Pos(0, 7), new Pos(0, 6),
            new Pos(0, 5), new Pos(0, 6), new Pos(0, 7), new Pos(0, 6),
            new Pos(0, 5), new Pos(0, 4), new Pos(1, 4), new Pos(2, 4),
            new Pos(3, 4), new Pos(4, 4), new Pos(4, 3), new Pos(5, 3),
            new Pos(6, 3), new Pos(7, 3), new Pos(8, 3), new Pos(9, 3),
            new Pos(10, 3), new Pos(10, 2), new Pos(10, 1), new Pos(10, 0),
            new Pos(11, 0), new Pos(10, 0), new Pos(10, 1), new Pos(10, 2),
            new Pos(10, 3), new Pos(10, 4), new Pos(10, 5), new Pos(10, 6),
            new Pos(10, 7), new Pos(9, 7), new Pos(9, 8), new Pos(9, 9)
        };

        public static NpcSpec CatSpec() { return new NpcSpec { Name = "고양이", Loop = CatLoop }; }

        /// <summary>낮에 밖에 있는 사람들(기획서 3.4). 모두 같은 때에서 시작합니다.</summary>
        public static List<NpcSpec> DayNpcs()
        {
            return new List<NpcSpec>
            {
                new NpcSpec { Name = "우물가 아낙", Waypoints = new[] { new Pos(4, 3), new Pos(6, 3) } },
                new NpcSpec { Name = "서당 아이", Waypoints = new[] { new Pos(3, 0), new Pos(6, 0), new Pos(6, 1), new Pos(4, 1) } },
                new NpcSpec { Name = "장꾼", Waypoints = new[] { new Pos(7, 3), new Pos(8, 0) } },
                new NpcSpec { Name = "농부", Waypoints = new[] { new Pos(4, 9), new Pos(8, 11) } },
                new NpcSpec { Name = "진짜 도령", Waypoints = new[] { new Pos(3, 4), new Pos(4, 5) }, IsRealLord = true },
            };
        }
    }
}
