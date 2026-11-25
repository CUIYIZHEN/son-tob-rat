using System.Collections.Generic;

namespace SonTob.Core
{
    public enum MoveKind { Walk, Run, Act, Wait }

    /// <summary>플레이어가 고를 수 있는 한 수. 화면에서는 1칸/2칸 거리 칸과 내 칸으로 보입니다.</summary>
    public struct Move
    {
        public MoveKind Kind;
        public Dir Dir;
        public Pos Target;
        public Pos Middle;    // 달리기일 때 지나는 칸
        public bool Risky;    // 다음 걸음에 눈에 들 칸인가(붉게 표시)

        public override string ToString() { return Kind + " " + Target + (Risky ? " !" : ""); }
    }

    public enum EndReason
    {
        None,
        Win,            // 임무 3개를 끝냄
        Suspicion,      // 의심 100 (쥐불놀이)
        StepsOut,       // 낮 남은 걸음 0 (임무를 못 끝냄)
        CaughtByCat,    // 고양이 눈에 들어 잡힘 (밤)
        Sunrise,        // 밤 걸음이 다해 해가 뜸
    }

    public static class MoveRules
    {
        /// <summary>네 방향 각각 1칸(걷기)과 2칸(달리기) 후보를 만듭니다.</summary>
        public static List<Move> Candidates(MapData map, Pos at, bool walkThroughDogHoles)
        {
            var list = new List<Move>();
            foreach (var d in Dirs.All)
            {
                var one = at + d.Step();
                var two = at + d.Step() * 2;
                if (map.Walkable(one, walkThroughDogHoles))
                    list.Add(new Move { Kind = MoveKind.Walk, Dir = d, Target = one, Middle = one });
                // 달리기는 개구멍을 지날 수 있습니다.
                if (map.Walkable(one, true) && map.Walkable(two, true))
                    list.Add(new Move { Kind = MoveKind.Run, Dir = d, Target = two, Middle = one });
            }
            return list;
        }
    }
}
