using System.Collections.Generic;

namespace SonTob.Core
{
    /// <summary>
    /// 낮(기획서 3.2, 2.4): 도령으로 둔갑한 쥐가 남은 걸음 안에 임무 3개를 끝낸다.
    /// 모든 행동은 1걸음이고, 한 걸음마다 마을 사람들도 한 칸 움직이거나 고개를 돌린다.
    /// 눈에 들어와 있는 칸을 달려 지나면 의심 +30, 진짜 도령에게는 걷고 있어도 +40.
    /// </summary>
    public class DayPhase
    {
        public readonly GameConfig Config;
        public readonly MapData Map;
        public readonly Lord As;
        public readonly List<Npc> Npcs = new List<Npc>();
        public readonly int StepLimit;

        public Pos Player;
        public int StepsUsed;
        public int Suspicion;
        public readonly bool[] Done;
        public EndReason Ended = EndReason.None;
        public bool Finished;
        public readonly List<string> Events = new List<string>();

        bool realSawLastStep;

        public DayPhase(GameConfig config, MapData map, int bonus)
        {
            Config = config; Map = map; As = Village.TheLord;
            StepLimit = config.DaySteps + bonus;
            Player = map.Mark(Village.RatHole);
            Done = new bool[As.Missions.Length];
            foreach (var spec in Village.DayNpcs()) Npcs.Add(new Npc(spec, map));
        }

        public int StepsLeft { get { return StepLimit - StepsUsed; } }
        public int DoneCount { get { int n = 0; foreach (var d in Done) if (d) n++; return n; } }

        public int MissionIndexAt(Pos p)
        {
            for (int i = 0; i < As.Missions.Length; i++)
                foreach (var q in Map.Marks(As.Missions[i])) if (q == p) return i;
            return -1;
        }

        public Pos MissionTile(int i) { return Map.Mark(As.Missions[i]); }

        /// <summary>지금 마을 사람들이 보는 칸 전부(진짜 도령 포함).</summary>
        public HashSet<Pos> WatchedTiles()
        {
            var set = new HashSet<Pos>();
            foreach (var n in Npcs) foreach (var p in n.Sight(Map, Config.SightLength)) set.Add(p);
            return set;
        }

        public Npc RealLord()
        {
            foreach (var n in Npcs) if (n.Spec.IsRealLord) return n;
            return null;
        }

        public List<Move> Candidates()
        {
            var list = MoveRules.Candidates(Map, Player, false);
            var watched = WatchedTiles();
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                m.Risky = m.Kind == MoveKind.Run && (watched.Contains(m.Middle) || watched.Contains(m.Target));
                list[i] = m;
            }
            return list;
        }

        public bool CanWork { get { int i = MissionIndexAt(Player); return i >= 0 && !Done[i]; } }

        /// <summary>「임무하기」(임무 칸 위에서만) 또는 「기다리기」.</summary>
        public Move ActHere()
        {
            return new Move { Kind = CanWork ? MoveKind.Act : MoveKind.Wait, Target = Player, Middle = Player };
        }

        public string ActLabel()
        {
            int i = MissionIndexAt(Player);
            return i >= 0 && !Done[i] ? As.MissionNames[i] : "기다리기";
        }

        public void Apply(Move m)
        {
            if (Ended != EndReason.None || Finished) return;
            Events.Clear();
            var watchedBefore = WatchedTiles();

            switch (m.Kind)
            {
                case MoveKind.Walk:
                    Player = m.Target;
                    break;
                case MoveKind.Run:
                    Player = m.Target;
                    if (watchedBefore.Contains(m.Middle) || watchedBefore.Contains(m.Target))
                    {
                        Suspicion += Config.RunSeenSuspicion;
                        Events.Add("달리다 눈에 들었다! 의심 +" + Config.RunSeenSuspicion);
                    }
                    break;
                case MoveKind.Act:
                    int mi = MissionIndexAt(Player);
                    if (mi >= 0 && !Done[mi])
                    {
                        Done[mi] = true;
                        Events.Add(As.MissionNames[mi] + " 끝");
                    }
                    break;
            }

            StepsUsed++;
            foreach (var n in Npcs) n.Step();

            // 진짜 도령에게 보이면 걸어도 의심. 같은 마주침이 이어지는 동안은 한 번만.
            var real = RealLord();
            bool realSees = real != null && real.Sight(Map, Config.SightLength).Contains(Player);
            if (realSees && !realSawLastStep)
            {
                Suspicion += Config.RealPersonSuspicion;
                Events.Add("진짜 도령과 마주쳤다! 의심 +" + Config.RealPersonSuspicion);
            }
            realSawLastStep = realSees;

            if (Suspicion >= Config.MaxSuspicion)
            {
                Suspicion = Config.MaxSuspicion;
                Ended = EndReason.Suspicion;
                return;
            }
            if (DoneCount == Done.Length) { Finished = true; return; }
            if (StepsUsed >= StepLimit) Ended = EndReason.StepsOut;
        }
    }
}
