using System;
using System.Collections.Generic;

namespace SonTob.Core
{
    /// <summary>칸 좌표. x는 오른쪽, y는 아래쪽. 화면에서는 두 축이 45°/135°로 보입니다.</summary>
    [Serializable]
    public struct Pos : IEquatable<Pos>
    {
        public int X;
        public int Y;

        public Pos(int x, int y) { X = x; Y = y; }

        public static Pos operator +(Pos a, Pos b) { return new Pos(a.X + b.X, a.Y + b.Y); }
        public static Pos operator -(Pos a, Pos b) { return new Pos(a.X - b.X, a.Y - b.Y); }
        public static Pos operator *(Pos a, int k) { return new Pos(a.X * k, a.Y * k); }
        public static bool operator ==(Pos a, Pos b) { return a.X == b.X && a.Y == b.Y; }
        public static bool operator !=(Pos a, Pos b) { return !(a == b); }

        public bool Equals(Pos other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is Pos && Equals((Pos)obj); }
        public override int GetHashCode() { return X * 397 ^ Y; }
        public override string ToString() { return "(" + X + "," + Y + ")"; }

        public int Manhattan(Pos o) { return Math.Abs(X - o.X) + Math.Abs(Y - o.Y); }
    }

    /// <summary>움직일 수 있는 네 방향(화면의 ↗ ↙ ↖ ↘).</summary>
    public enum Dir { PlusX = 0, MinusX = 1, PlusY = 2, MinusY = 3 }

    public static class Dirs
    {
        public static readonly Dir[] All = { Dir.PlusX, Dir.MinusX, Dir.PlusY, Dir.MinusY };

        public static Pos Step(this Dir d)
        {
            switch (d)
            {
                case Dir.PlusX: return new Pos(1, 0);
                case Dir.MinusX: return new Pos(-1, 0);
                case Dir.PlusY: return new Pos(0, 1);
                default: return new Pos(0, -1);
            }
        }

        public static bool TryFromDelta(Pos delta, out Dir dir)
        {
            dir = Dir.PlusX;
            if (delta.X != 0 && delta.Y != 0) return false;
            if (delta.X > 0) { dir = Dir.PlusX; return true; }
            if (delta.X < 0) { dir = Dir.MinusX; return true; }
            if (delta.Y > 0) { dir = Dir.PlusY; return true; }
            if (delta.Y < 0) { dir = Dir.MinusY; return true; }
            return false;
        }
    }

    public enum Tile { Road, Wall, DogHole }

    /// <summary>
    /// 마을 지도. 문자 격자에서 읽습니다(기획서 7장, village-map-v1.txt).
    /// # 담장·집, . 길, d 개구멍(네 발로만), K 장독대(고양이 잠자리, 막힘), 그 밖의 글자는 길 위의 표식.
    /// </summary>
    public class MapData
    {
        public readonly int Width;
        public readonly int Height;
        readonly Tile[,] tiles;
        readonly Dictionary<char, List<Pos>> marks = new Dictionary<char, List<Pos>>();

        public MapData(string[] rows)
        {
            Height = rows.Length;
            Width = rows[0].Length;
            tiles = new Tile[Width, Height];
            for (int y = 0; y < Height; y++)
            {
                if (rows[y].Length != Width) throw new ArgumentException("row " + y + " has length " + rows[y].Length);
                for (int x = 0; x < Width; x++)
                {
                    char c = rows[y][x];
                    if (c == '#') tiles[x, y] = Tile.Wall;
                    else if (c == 'd') tiles[x, y] = Tile.DogHole;
                    else
                    {
                        tiles[x, y] = c == 'K' ? Tile.Wall : Tile.Road;
                        if (c != '.')
                        {
                            List<Pos> list;
                            if (!marks.TryGetValue(c, out list)) { list = new List<Pos>(); marks[c] = list; }
                            list.Add(new Pos(x, y));
                        }
                    }
                }
            }
        }

        public bool InBounds(Pos p) { return p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height; }
        public Tile TileAt(Pos p) { return InBounds(p) ? tiles[p.X, p.Y] : Tile.Wall; }

        /// <summary>걷기로 들어갈 수 있는 칸. 개구멍은 네 발(뛰기)이나 밤의 쥐만.</summary>
        public bool Walkable(Pos p, bool dogHoleOk)
        {
            var t = TileAt(p);
            return t == Tile.Road || (t == Tile.DogHole && dogHoleOk);
        }

        /// <summary>시선을 막는 칸. 개구멍은 담장에 난 구멍이라 시선을 막습니다.</summary>
        public bool BlocksSight(Pos p) { return TileAt(p) != Tile.Road; }

        public bool HasMark(char c) { return marks.ContainsKey(c); }
        public Pos Mark(char c) { return marks[c][0]; }
        public IList<Pos> Marks(char c) { List<Pos> l; return marks.TryGetValue(c, out l) ? l : new List<Pos>(); }
        public char MarkAt(Pos p)
        {
            foreach (var kv in marks) foreach (var q in kv.Value) if (q == p) return kv.Key;
            return '.';
        }

        public Dictionary<Pos, int> Distances(Pos from, bool dogHoleOk)
        {
            var d = new Dictionary<Pos, int>();
            var q = new Queue<Pos>();
            d[from] = 0; q.Enqueue(from);
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var dir in Dirs.All)
                {
                    var n = p + dir.Step();
                    if (!Walkable(n, dogHoleOk) || d.ContainsKey(n)) continue;
                    d[n] = d[p] + 1; q.Enqueue(n);
                }
            }
            return d;
        }

        /// <summary>가장 짧은 길(시작 칸 제외, 도착 칸 포함). 갈 수 없으면 null.</summary>
        public List<Pos> Path(Pos from, Pos to, bool dogHoleOk)
        {
            if (from == to) return new List<Pos>();
            var prev = new Dictionary<Pos, Pos>();
            var q = new Queue<Pos>();
            prev[from] = from; q.Enqueue(from);
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                if (p == to) break;
                foreach (var dir in Dirs.All)
                {
                    var n = p + dir.Step();
                    if (!(Walkable(n, dogHoleOk) || n == to) || prev.ContainsKey(n)) continue;
                    prev[n] = p; q.Enqueue(n);
                }
            }
            if (!prev.ContainsKey(to)) return null;
            var path = new List<Pos>();
            for (var c = to; c != from; c = prev[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        /// <summary>임무 칸을 모두 도는 가장 짧은 걸음 길 L (순서는 자유).</summary>
        public int ShortestTour(Pos start, IList<Pos> stops, bool dogHoleOk)
        {
            int best = int.MaxValue;
            var order = new List<int>();
            for (int i = 0; i < stops.Count; i++) order.Add(i);
            foreach (var perm in Permutations(order))
            {
                int total = 0; var cur = start; bool ok = true;
                foreach (var i in perm)
                {
                    var d = Distances(cur, dogHoleOk);
                    int v;
                    if (!d.TryGetValue(stops[i], out v)) { ok = false; break; }
                    total += v; cur = stops[i];
                }
                if (ok && total < best) best = total;
            }
            return best;
        }

        static IEnumerable<List<int>> Permutations(List<int> items)
        {
            if (items.Count <= 1) { yield return new List<int>(items); yield break; }
            for (int i = 0; i < items.Count; i++)
            {
                var rest = new List<int>(items); rest.RemoveAt(i);
                foreach (var p in Permutations(rest)) { p.Insert(0, items[i]); yield return p; }
            }
        }
    }
}
