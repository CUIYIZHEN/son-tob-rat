using System;
using System.Collections.Generic;

namespace SonTob.Core
{
    public class Nail
    {
        public PersonId Owner;
        public NailMethod Method;
        public Pos Tile;
        public bool Dug;   // 묻음 손톱을 팠나
    }

    /// <summary>
    /// 밤: 박자 제한이 없습니다. 손톱 하나를 먹고 쥐구멍으로 돌아오면 아침입니다.
    /// 따질 것은 고양이 잠귀 범위와 내 냄새 고리뿐입니다.
    /// 잠귀 범위 + 냄새 고리 안에서 뛰거나, 손톱을 줍거나, 파면 고양이 귀가 쫑긋(2번이면 깸).
    /// </summary>
    public class NightPhase
    {
        public readonly GameConfig Config;
        public readonly MapData Map;
        public readonly List<Nail> Nails = new List<Nail>();
        public readonly Pos CatHome;
        public readonly Pos Hole;

        public Pos Player;
        public Pos Cat;
        public bool CatAwake;
        public int Twitches;
        public int ExtraRing;               // 파면 +1
        public Nail Eaten;
        public bool Morning;
        public EndReason Ended = EndReason.None;
        public int Moves;
        public readonly List<string> Events = new List<string>();

        public int ScentRing { get { return Math.Min(Config.ScentMax, Config.ScentStart + ExtraRing); } }
        public int ZoneRadius { get { return Config.CatHearing + ScentRing; } }

        public NightPhase(GameConfig config, MapData map, Random rng)
        {
            Config = config; Map = map;
            CatHome = map.Mark(Village.Cat);
            Hole = map.Mark(Village.RatHole);
            Player = Hole; Cat = CatHome;
            foreach (var person in Village.People)
            {
                float pt, pb, pc;
                person.Probabilities(config.ThrowShiftPercent, out pt, out pb, out pc);
                double u = rng.NextDouble();
                var method = u < pt ? NailMethod.Throw : u < pt + pb ? NailMethod.Bury : NailMethod.Burn;
                char at = method == NailMethod.Burn ? person.Hearth : person.Yard;
                Nails.Add(new Nail { Owner = person.Id, Method = method, Tile = map.Mark(at) });
            }
        }

        /// <summary>테스트와 실험용: 버리기 방법을 정해서 시작합니다.</summary>
        public NightPhase(GameConfig config, MapData map, NailMethod[] methods)
        {
            Config = config; Map = map;
            CatHome = map.Mark(Village.Cat);
            Hole = map.Mark(Village.RatHole);
            Player = Hole; Cat = CatHome;
            for (int i = 0; i < Village.People.Length; i++)
            {
                var person = Village.People[i];
                char at = methods[i] == NailMethod.Burn ? person.Hearth : person.Yard;
                Nails.Add(new Nail { Owner = person.Id, Method = methods[i], Tile = map.Mark(at) });
            }
        }

        public bool InHearing(Pos p) { return p.Manhattan(CatHome) <= ZoneRadius; }

        public Nail NailAt(Pos p)
        {
            if (Eaten != null) return null;
            foreach (var n in Nails) if (n.Tile == p) return n;
            return null;
        }

        public List<Move> Candidates()
        {
            var list = MoveRules.Candidates(Map, Player, true); // 밤의 쥐는 개구멍을 걸어서도 지납니다
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                m.Risky = !CatAwake && m.Kind == MoveKind.Run && (InHearing(m.Middle) || InHearing(m.Target));
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
            if (n.Method == NailMethod.Bury && !n.Dug) return "손톱 파내기";
            return Village.Get(n.Owner).Name + " 손톱 먹기";
        }

        void Twitch(string why)
        {
            if (CatAwake) return;
            Twitches++;
            if (Twitches >= Config.TwitchesToWake) { CatAwake = true; Events.Add(why + " 고양이가 깼다!"); }
            else Events.Add(why + " 고양이 귀가 쫑긋");
        }

        public void Apply(Move m)
        {
            if (Ended != EndReason.None || Morning) return;
            Events.Clear();
            Moves++;

            switch (m.Kind)
            {
                case MoveKind.Walk:
                    Player = m.Target;
                    break;
                case MoveKind.Run:
                    Player = m.Target;
                    if (InHearing(m.Middle) || InHearing(m.Target)) Twitch("뛰는 소리에");
                    break;
                case MoveKind.Act:
                    var nail = NailAt(Player);
                    if (nail == null) break;
                    if (nail.Method == NailMethod.Bury && !nail.Dug)
                    {
                        nail.Dug = true;
                        ExtraRing++;
                        Events.Add("손톱을 파냈다. 냄새 고리 +1");
                        if (InHearing(Player)) Twitch("흙 긁는 소리에");
                    }
                    else
                    {
                        if (InHearing(Player)) Twitch("손톱 줍는 소리에");
                        Eaten = nail;
                        Events.Add(Village.Get(nail.Owner).Name + "의 손톱을 먹었다. 쥐구멍으로!");
                    }
                    break;
            }

            if (Player == Hole)
            {
                if (CatAwake) { CatAwake = false; Twitches = 0; Cat = CatHome; Events.Add("고양이가 다시 잠들었다"); }
                if (Eaten != null) { Morning = true; return; }
            }

            if (CatAwake)
            {
                var path = Map.Path(Cat, Player, true);
                if (path != null)
                    for (int i = 0; i < Config.CatChaseSpeed && i < path.Count; i++) Cat = path[i];
                if (Cat == Player) { Ended = EndReason.CaughtByCat; Events.Add("고양이에게 잡혔다"); }
            }
        }
    }
}
