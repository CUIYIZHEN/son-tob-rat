using System.Collections.Generic;

namespace SonTob.Core
{
    /// <summary>
    /// 밤(기획서 3.1, 4.2): 쥐가 고양이 눈을 피해 늘 같은 칸의 손톱을 먹는다.
    /// 모든 행동은 1걸음이고, 한 걸음마다 고양이도 길을 따라 한 칸 움직인다.
    /// 고양이는 걷는 방향 앞 3칸(과 자기 칸)을 본다. 거기에 쥐가 있으면 잡힌다.
    /// 먹으면 곧 낮이 되고, 걸음이 다하도록 못 먹으면 해가 떠서 실패한다.
    /// </summary>
    public class NightPhase
    {
        public readonly GameConfig Config;
        public readonly MapData Map;
        public readonly Pos Hole;
        public readonly Pos NailTile;
        public readonly Npc CatNpc;

        public Pos Player;
        public int StepsUsed;
        public int EatenAt;                  // 손톱을 먹은 걸음(못 먹었으면 0)
        public EndReason Ended = EndReason.None;
        public readonly List<string> Events = new List<string>();

        public NightPhase(GameConfig config, MapData map)
        {
            Config = config; Map = map;
            Hole = map.Mark(Village.RatHole);
            NailTile = map.Mark(Village.NailMark);
            CatNpc = new Npc(Village.CatSpec(), map);
            Player = Hole;
        }

        public int StepsLeft { get { return Config.NightSteps - StepsUsed; } }
        public bool Eaten { get { return EatenAt > 0; } }
        public Pos Cat { get { return CatNpc.Pos; } }

        /// <summary>손톱을 먹은 걸음에 따른 낮 보너스.</summary>
        public int Bonus { get { return Eaten ? Config.NightBonus(EatenAt) : 0; } }

        public bool CanEat { get { return !Eaten && Player == NailTile; } }

        /// <summary>지금 고양이 눈에 보이는 칸(고양이 자기 칸 포함).</summary>
        public HashSet<Pos> CatCone()
        {
            var set = new HashSet<Pos>(CatNpc.Sight(Map, Config.SightLength));
            set.Add(CatNpc.Pos);
            return set;
        }

        /// <summary>다음 걸음에 고양이 눈에 보일 칸(화면의 붉은 칸 미리보기).</summary>
        public HashSet<Pos> NextCatCone()
        {
            var set = new HashSet<Pos>(CatNpc.NextSight(Map, Config.SightLength));
            set.Add(CatNpc.NextPos());
            return set;
        }

        /// <summary>이 칸에 가면(또는 서 있으면) 다음 걸음에 잡히는가.</summary>
        public bool WouldBeSeen(Pos p) { return NextCatCone().Contains(p); }

        public List<Move> Candidates()
        {
            var list = MoveRules.Candidates(Map, Player, true); // 밤의 쥐는 개구멍을 걸어서도 지난다
            var next = NextCatCone();
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                bool risky = next.Contains(m.Target);
                if (m.Kind == MoveKind.Run && (m.Middle == Cat || m.Middle == CatNpc.NextPos() || m.Target == Cat)) risky = true;
                m.Risky = risky;
                list[i] = m;
            }
            return list;
        }

        /// <summary>「먹기」 또는 「기다리기」(손톱 칸 위에서만 먹을 수 있다).</summary>
        public Move ActHere()
        {
            return new Move { Kind = CanEat ? MoveKind.Act : MoveKind.Wait, Target = Player, Middle = Player,
                              Risky = WouldBeSeen(Player) };
        }

        public string ActLabel() { return CanEat ? "먹기" : "기다리기"; }

        public void Apply(Move m)
        {
            if (Ended != EndReason.None || Eaten) return;
            Events.Clear();

            var before = Player;
            var catBefore = Cat;
            if (m.Kind == MoveKind.Walk || m.Kind == MoveKind.Run) Player = m.Target;
            StepsUsed++;
            CatNpc.Step();

            bool caught = Player == Cat || CatCone().Contains(Player);
            if (!caught && m.Kind == MoveKind.Run)
                caught = m.Middle == Cat || m.Middle == catBefore || m.Target == catBefore;
            if (!caught && m.Kind == MoveKind.Walk)
                caught = m.Target == catBefore && Cat == before; // 마주 보고 엇갈림
            if (caught)
            {
                Ended = EndReason.CaughtByCat;
                Events.Add("고양이 눈에 들었다. 잡혔다!");
                return;
            }

            if (m.Kind == MoveKind.Act && Player == NailTile)
            {
                EatenAt = StepsUsed;
                Events.Add("손톱을 먹었다. 낮 걸음 +" + Bonus);
                return;
            }
            if (StepsUsed >= Config.NightSteps)
            {
                Ended = EndReason.Sunrise;
                Events.Add("손톱을 못 먹었는데 해가 떴다");
            }
        }
    }
}
