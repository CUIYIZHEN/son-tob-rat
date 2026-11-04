using System.Collections.Generic;

namespace SonTob.Core
{
    /// <summary>
    /// 마을 사람. 걸을 때는 가는 방향을, 서 있을 때는 정해진 순서대로 돌아보며 직선 앞 몇 칸만 봅니다.
    /// 내가 한 번 움직일 때(1박자) 한 칸 움직이거나 고개를 돌립니다.
    /// </summary>
    public class Npc
    {
        public readonly NpcSpec Spec;
        public Pos Pos;
        public Dir Facing;
        readonly List<Pos> loop = new List<Pos>(); // 한 바퀴 칸 목록
        int index;
        int beat;

        public string Name { get { return Spec.Name; } }
        public bool IsWalker { get { return loop.Count > 1; } }

        public Npc(NpcSpec spec, MapData map)
        {
            Spec = spec;
            Pos = spec.Waypoints[0];
            loop.Add(Pos);
            if (spec.Waypoints.Length > 1)
            {
                // 길목 사이를 가장 짧은 길로 잇고, 마지막 길목에서 처음으로 돌아옵니다.
                var all = new List<Pos>(spec.Waypoints);
                for (int i = 0; i < all.Count; i++)
                {
                    var from = all[i];
                    var to = all[(i + 1) % all.Count];
                    if (all.Count == 2 && i == 1) to = all[0];
                    var path = map.Path(from, to, false);
                    if (path == null) continue;
                    foreach (var p in path) if (!(i == all.Count - 1 && p == all[0])) loop.Add(p);
                }
            }
            Facing = spec.LookCycle != null && spec.LookCycle.Length > 0 ? spec.LookCycle[0] : FacingToward(NextLoopPos());
        }

        Pos NextLoopPos() { return loop.Count > 1 ? loop[(index + 1) % loop.Count] : Pos; }

        Dir FacingToward(Pos next)
        {
            Dir d;
            return Dirs.TryFromDelta(next - Pos, out d) ? d : Facing;
        }

        /// <summary>다음 박자에 볼 방향(머리 위 화살표용).</summary>
        public Dir NextFacing()
        {
            if (IsWalker)
            {
                var next = NextLoopPos();
                var after = loop[(index + 2) % loop.Count];
                Dir d;
                return Dirs.TryFromDelta(after - next, out d) ? d : Facing;
            }
            if (Spec.LookCycle == null || Spec.LookCycle.Length == 0) return Facing;
            int k = Spec.TurnEvery <= 0 ? 1 : Spec.TurnEvery;
            int nextBeat = beat + 1;
            return Spec.LookCycle[(nextBeat / k) % Spec.LookCycle.Length];
        }

        public void Step()
        {
            beat++;
            if (IsWalker)
            {
                var next = NextLoopPos();
                Facing = FacingToward(next);
                index = (index + 1) % loop.Count;
                Pos = loop[index];
                // 도착하면 다음 칸 쪽을 봅니다.
                Facing = FacingToward(NextLoopPos());
            }
            else if (Spec.LookCycle != null && Spec.LookCycle.Length > 0)
            {
                int k = Spec.TurnEvery <= 0 ? 1 : Spec.TurnEvery;
                Facing = Spec.LookCycle[(beat / k) % Spec.LookCycle.Length];
            }
        }

        public List<Pos> Sight(MapData map, int length)
        {
            return SightLine(map, Pos, Facing, length);
        }

        public static List<Pos> SightLine(MapData map, Pos from, Dir facing, int length)
        {
            var result = new List<Pos>();
            var p = from;
            for (int i = 0; i < length; i++)
            {
                p = p + facing.Step();
                if (!map.InBounds(p) || map.BlocksSight(p)) break;
                result.Add(p);
            }
            return result;
        }
    }
}
