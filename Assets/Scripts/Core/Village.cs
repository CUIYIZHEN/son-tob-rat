using System.Collections.Generic;

namespace SonTob.Core
{
    public enum PersonId { Dolsoe = 0, Buni = 1, Hunjang = 2 }

    public enum NailMethod { Throw, Bury, Burn }

    /// <summary>둔갑할 수 있는 세 사람. 버릇과 임무는 기획서 5·6장.</summary>
    public class Person
    {
        public PersonId Id;
        public string Name;
        public int BeatSign;          // 둔갑 박자 = B + BeatSign × d
        public float Throw, Bury, Burn; // 손톱 버리기 비율
        public char Yard;             // 던짐·묻음 손톱이 놓이는 칸
        public char Hearth;           // 태움 손톱이 놓이는 칸(아궁이 앞)
        public char[] Missions;
        public string[] MissionNames;

        public int DisguiseBeats(GameConfig c) { return c.BaseDisguiseBeats + BeatSign * c.PersonBeatOffset; }

        /// <summary>던짐 비율을 shift(%p)만큼 옮기고 나머지를 같은 비율로 줄입니다(실험실과 같은 식).</summary>
        public void Probabilities(int shiftPercent, out float pThrow, out float pBury, out float pBurn)
        {
            float t = Throw + shiftPercent / 100f;
            if (t < 0) t = 0; if (t > 1) t = 1;
            float rest = Bury + Burn, left = 1 - t;
            if (rest <= 0) { pThrow = 1; pBury = 0; pBurn = 0; return; }
            pThrow = t; pBury = left * Bury / rest; pBurn = left * Burn / rest;
        }
    }

    public class NpcSpec
    {
        public string Name;
        public Pos[] Waypoints;      // 2개 이상이면 그 사이를 오감
        public Dir[] LookCycle;      // 서 있는 사람이 돌아보는 순서
        public int TurnEvery = 2;    // 몇 박자마다 고개를 돌리나
        public PersonId? RealPerson; // 둔갑 대상의 진짜라면 그 사람
    }

    public static class Village
    {
        /// <summary>마을 지도 v1. 쥐구멍 R에서 누구의 임무든 걸어서 30칸.</summary>
        public static readonly string[] MapRows =
        {
            "##f.i..#...b",
            "##h#...#.#.#",
            "...#.###.#..",
            ".#.#..W..d..",
            ".Ad3.##.##..",
            "K##..##..#..",
            "...#.R..##1a",
            "e2##.##.#...",
            ".g.d.##.#.##",
            "##.#...##...",
            "...#.#...##.",
            "j#...#.#.cc.",
        };

        public const char RatHole = 'R';
        public const char Well = 'W';
        public const char Cat = 'K';

        public static MapData CreateMap() { return new MapData(MapRows); }

        public static readonly Person[] People =
        {
            new Person { Id = PersonId.Dolsoe, Name = "돌쇠 아빠", BeatSign = -1, Throw = 0.7f, Bury = 0.3f, Burn = 0f,
                Yard = '1', Hearth = '1', Missions = new[] { 'a', 'b', 'c' }, MissionNames = new[] { "장작 패기", "소에게 여물 주기", "밭 갈기" } },
            new Person { Id = PersonId.Buni, Name = "분이", BeatSign = 0, Throw = 0.2f, Bury = 0.6f, Burn = 0.2f,
                Yard = '2', Hearth = 'g', Missions = new[] { 'e', 'f', 'g' }, MissionNames = new[] { "닭 모이 주기", "동생 데리러 가기", "어머니께 심부름 전하기" } },
            new Person { Id = PersonId.Hunjang, Name = "훈장 어른", BeatSign = 1, Throw = 0f, Bury = 0.4f, Burn = 0.6f,
                Yard = '3', Hearth = 'A', Missions = new[] { 'h', 'i', 'j' }, MissionNames = new[] { "서당 문 열기", "정자에서 시 읽기", "마을 어귀 장승 살피기" } },
        };

        public static Person Get(PersonId id) { return People[(int)id]; }

        /// <summary>낮에 밖에 있는 사람들. 진짜 세 사람은 자기 집 마당을 오갑니다.</summary>
        public static List<NpcSpec> DayNpcs()
        {
            return new List<NpcSpec>
            {
                new NpcSpec { Name = "우물가 아낙네", Waypoints = new[] { new Pos(5, 3) },
                    LookCycle = new[] { Dir.MinusX, Dir.PlusY, Dir.PlusX, Dir.MinusY }, TurnEvery = 2 },
                new NpcSpec { Name = "서당 아이", Waypoints = new[] { new Pos(3, 0), new Pos(6, 0), new Pos(6, 1), new Pos(4, 1) } },
                new NpcSpec { Name = "장꾼", Waypoints = new[] { new Pos(7, 3), new Pos(8, 0) } },
                new NpcSpec { Name = "농부", Waypoints = new[] { new Pos(4, 9), new Pos(8, 11) } },
                new NpcSpec { Name = "진짜 돌쇠 아빠", Waypoints = new[] { new Pos(10, 5), new Pos(10, 7) }, RealPerson = PersonId.Dolsoe },
                new NpcSpec { Name = "진짜 분이", Waypoints = new[] { new Pos(0, 8), new Pos(2, 8) }, RealPerson = PersonId.Buni },
                new NpcSpec { Name = "진짜 훈장 어른", Waypoints = new[] { new Pos(3, 4), new Pos(4, 5) }, RealPerson = PersonId.Hunjang },
            };
        }
    }
}
