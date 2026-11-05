using System;
using System.Collections.Generic;

namespace SonTob.Core
{
    public class Nail
    {
        public PersonId Owner;
        public NailMethod Method;
        public Pos Tile;
        public bool Found;   // 냄새로 찾았나(찾은 손톱만 화면에 보임)
        public int Dug;      // 묻은 손톱을 판 박자 수
    }

    /// <summary>
    /// 밤: 쥐 시간(박자) 안에, 보이지 않는 손톱을 냄새로 찾아 먹습니다(기획서 v4 5장).
    /// 손톱에서 SniffRange칸 안에 들어가면 킁킁 하고 그 칸이 보입니다.
    /// 고양이는 정해진 길을 1박자에 1칸씩 돌고, 냄새 고리 안에 들어가면 쫑긋한 뒤 1박자에 1칸씩 쫓아옵니다.
    /// 고리의 CatLoseFactor배 밖으로 벗어나거나 쥐구멍에 들어가면 놓치고, 같은 칸에 닿으면 잡힙니다.
    /// 손톱을 먹는 순간 해가 뜹니다. 쥐 시간이 다하도록 못 먹으면 실패입니다.
    /// </summary>
    public class NightPhase
    {
        public readonly GameConfig Config;
        public readonly MapData Map;
        public readonly List<Nail> Nails = new List<Nail>();
        public readonly Pos CatHome;     // 장독대(막힌 칸)
        public readonly Pos Hole;
        public readonly Npc CatPatrol;   // 고양이가 도는 길
        public readonly int BeatLimit;

        public Pos Player;
        public Pos Cat;
        public bool CatChasing;
        public bool CatReturning;
        public bool RanLastBeat;
        public int BeatsUsed;
        public Nail Eaten;
        public EndReason Ended = EndReason.None;
        public readonly List<string> Events = new List<string>();

        public int BeatsLeft { get { return BeatLimit - BeatsUsed; } }

        /// <summary>지금 고양이 냄새 고리 반지름. 뛴 박자에는 발소리로 넓어집니다.</summary>
        public int CatRingNow { get { return Config.CatRing + (RanLastBeat ? Config.RunNoiseRing : 0); } }

        public NightPhase(GameConfig config, MapData map, Random rng) : this(config, map)
        {
            foreach (var person in Village.People)
            {
                float pt, pb, pc;
                person.Probabilities(config.ThrowShiftPercent, out pt, out pb, out pc);
                double u = rng.NextDouble();
                var method = u < pt ? NailMethod.Throw : u < pt + pb ? NailMethod.Bury : NailMethod.Burn;
                AddNail(person, method);
            }
            Sniff();
        }

        /// <summary>테스트와 실험용: 버리기 방법을 정해서 시작합니다.</summary>
        public NightPhase(GameConfig config, MapData map, NailMethod[] methods) : this(config, map)
        {
            for (int i = 0; i < Village.People.Length; i++) AddNail(Village.People[i], methods[i]);
            Sniff();
        }

        NightPhase(GameConfig config, MapData map)
        {
            Config = config; Map = map;
            BeatLimit = config.NightBeats;
            CatHome = map.Mark(Village.Cat);
            Hole = map.Mark(Village.RatHole);
            CatPatrol = new Npc(Village.CatRoute(map), map);
            Player = Hole;
            Cat = CatPatrol.Pos;
        }

        void AddNail(Person person, NailMethod method)
        {
            char at = method == NailMethod.Burn ? person.Hearth : person.Yard;
            Nails.Add(new Nail { Owner = person.Id, Method = method, Tile = Map.Mark(at) });
        }

        public Nail NailAt(Pos p)
        {
            if (Eaten != null) return null;
            foreach (var n in Nails) if (n.Tile == p) return n;
            return null;
        }

        /// <summary>고양이 냄새 고리 안인가(담장으로 막히지 않음).</summary>
        public bool InCatRing(Pos p, int ring) { return p != Hole && p.Manhattan(Cat) <= ring; }

        public List<Move> Candidates()
        {
            var list = MoveRules.Candidates(Map, Player, true); // 밤의 쥐는 개구멍을 걸어서도 지납니다
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                int ring = Config.CatRing + (m.Kind == MoveKind.Run ? Config.RunNoiseRing : 0);
                m.Risky = !CatChasing && (InCatRing(m.Target, ring) || (m.Kind == MoveKind.Run && InCatRing(m.Middle, ring)));
                list[i] = m;
            }
            return list;
        }

        public Move ActHere()
        {
            return new Move { Kind = NailAt(Player) != null ? MoveKind.Act : MoveKind.Wait, Target = Player, Middle = Player };
        }

        public string ActLabel()
        {
            var n = NailAt(Player);
            if (n == null) return "기다리기";
            if (n.Method == NailMethod.Bury) return "손톱 파기 (" + n.Dug + "/" + Config.DigBeats + ")";
            return Village.Get(n.Owner).Name + " 손톱 먹기";
        }

        void Sniff()
        {
            foreach (var n in Nails)
                if (!n.Found && n.Tile.Manhattan(Player) <= Config.SniffRange)
                {
                    n.Found = true;
                    Events.Add("킁킁! " + Village.Get(n.Owner).Name + "의 손톱 냄새");
                }
        }

        void Eat(Nail nail)
        {
            Eaten = nail;
            Events.Add(Village.Get(nail.Owner).Name + "의 손톱을 먹었다. 해가 뜬다!");
        }

        public void Apply(Move m)
        {
            if (Ended != EndReason.None || Eaten != null) return;
            Events.Clear();
            bool ran = false;

            switch (m.Kind)
            {
                case MoveKind.Walk:
                    Player = m.Target;
                    break;
                case MoveKind.Run:
                    Player = m.Target;
                    ran = true;
                    break;
                case MoveKind.Act:
                    var nail = NailAt(Player);
                    if (nail == null) break;
                    if (nail.Method == NailMethod.Bury)
                    {
                        nail.Dug++;
                        if (nail.Dug >= Config.DigBeats) Eat(nail);
                        else Events.Add("흙을 판다 (" + nail.Dug + "/" + Config.DigBeats + ")");
                    }
                    else Eat(nail);
                    break;
            }
            BeatsUsed++;
            RanLastBeat = ran;
            if (Eaten != null) return; // 먹는 순간 아침

            Sniff();
            if (Player == Cat && Player != Hole) { Caught(); return; }
            MoveCat();
            if (Ended != EndReason.None) return;

            if (BeatsUsed >= BeatLimit)
            {
                Ended = EndReason.Sunrise;
                Events.Add("손톱을 못 먹었는데 해가 떴다");
            }
        }

        /// <summary>고양이가 다음 박자에 설 칸(화면의 점선 칸).</summary>
        public Pos CatNextPos()
        {
            List<Pos> path = null;
            if (CatChasing) path = Map.Path(Cat, Player, true);
            else if (CatReturning) path = Map.Path(Cat, CatPatrol.Pos, true);
            else return CatPatrol.NextPos();
            return path != null && path.Count > 0 ? path[0] : Cat;
        }

        void Caught()
        {
            Ended = EndReason.CaughtByCat;
            Events.Add("고양이에게 잡혔다");
        }

        void MoveCat()
        {
            if (CatChasing)
            {
                if (Player == Hole)
                {
                    StopChase("쥐구멍에 숨었다. 고양이가 놓쳤다");
                    return;
                }
                if (Player.Manhattan(Cat) > Config.CatLoseFactor * Config.CatRing)
                {
                    StopChase("고양이를 따돌렸다");
                    return;
                }
                var path = Map.Path(Cat, Player, true);
                if (path != null && path.Count > 0) Cat = path[0];
                if (Cat == Player) Caught();
                return;
            }

            if (CatReturning)
            {
                var back = Map.Path(Cat, CatPatrol.Pos, true);
                if (back == null || back.Count <= 1) { Cat = CatPatrol.Pos; CatReturning = false; }
                else Cat = back[0];
            }
            else
            {
                CatPatrol.Step();
                Cat = CatPatrol.Pos;
            }

            if (InCatRing(Player, CatRingNow))
            {
                CatChasing = true;
                CatReturning = false;
                Events.Add("고양이가 쫑긋! 쫓아온다");
                if (Cat == Player) Caught();
            }
        }

        void StopChase(string why)
        {
            CatChasing = false;
            CatReturning = true;
            Events.Add(why);
        }
    }
}
