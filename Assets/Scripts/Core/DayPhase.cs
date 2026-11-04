using System;
using System.Collections.Generic;

namespace SonTob.Core
{
    /// <summary>
    /// 낮: 둔갑한 사람의 임무 셋을 둔갑 박자 안에 끝냅니다.
    /// 한 수 = 1박자. 수를 둘 때마다 마을 사람도 한 칸 움직이거나 고개를 돌립니다.
    /// </summary>
    public class DayPhase
    {
        public readonly GameConfig Config;
        public readonly MapData Map;
        public readonly Person As;
        public readonly List<Npc> Npcs = new List<Npc>();
        public readonly int BeatLimit;

        public Pos Player;
        public int BeatsUsed;
        public int RunTiles;               // 냄새 고리 계산용(씻으면 0)
        public readonly int[] WorkDone;    // 임무별 '하기' 횟수
        public int WashProgress;
        public EndReason Ended = EndReason.None;
        public bool Finished;              // 임무를 모두 끝냄

        public float Hunger;
        public float Suspicion;

        /// <summary>마지막 한 수에서 생긴 일(화면 알림용).</summary>
        public readonly List<string> Events = new List<string>();

        bool realSawLastBeat;

        public int BeatsLeft { get { return BeatLimit - BeatsUsed; } }
        public int ScentRing { get { return Math.Min(Config.ScentMax, Config.ScentStart + RunTiles / Config.ScentPerRunTiles); } }

        public DayPhase(GameConfig config, MapData map, Person disguise, float hunger, float suspicion)
        {
            Config = config; Map = map; As = disguise;
            Hunger = hunger; Suspicion = suspicion;
            BeatLimit = disguise.DisguiseBeats(config);
            Player = map.Mark(Village.RatHole);
            WorkDone = new int[disguise.Missions.Length];
            foreach (var spec in Village.DayNpcs()) Npcs.Add(new Npc(spec, map));
        }

        public bool MissionDone(int i) { return WorkDone[i] >= Config.WorkBeatsPerMission; }

        public int MissionIndexAt(Pos p)
        {
            for (int i = 0; i < As.Missions.Length; i++)
                foreach (var q in Map.Marks(As.Missions[i])) if (q == p) return i;
            return -1;
        }

        /// <summary>지금 마을 사람들이 보는 칸 전부.</summary>
        public HashSet<Pos> WatchedTiles()
        {
            var set = new HashSet<Pos>();
            foreach (var n in Npcs) foreach (var p in n.Sight(Map, Config.SightLength)) set.Add(p);
            return set;
        }

        Npc RealSelf()
        {
            foreach (var n in Npcs) if (n.Spec.RealPerson.HasValue && n.Spec.RealPerson.Value == As.Id) return n;
            return null;
        }

        public HashSet<Pos> RealSelfSight()
        {
            var set = new HashSet<Pos>();
            var real = RealSelf();
            if (real != null) foreach (var p in real.Sight(Map, Config.SightLength)) set.Add(p);
            return set;
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

        /// <summary>내 칸에서 할 수 있는 일: 임무 '하기', 우물에서 씻기, 아니면 기다리기.</summary>
        public Move ActHere()
        {
            int mi = MissionIndexAt(Player);
            bool can = (mi >= 0 && !MissionDone(mi)) || Map.MarkAt(Player) == Village.Well;
            return new Move { Kind = can ? MoveKind.Act : MoveKind.Wait, Target = Player, Middle = Player };
        }

        public string ActLabel()
        {
            int mi = MissionIndexAt(Player);
            if (mi >= 0 && !MissionDone(mi)) return As.MissionNames[mi];
            if (Map.MarkAt(Player) == Village.Well) return "우물에서 씻기";
            return "기다리기";
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
                    Hunger -= Config.WalkHunger;
                    break;
                case MoveKind.Run:
                    Player = m.Target;
                    RunTiles += 2;
                    Hunger -= Config.RunHunger;
                    if (watchedBefore.Contains(m.Middle) || watchedBefore.Contains(m.Target))
                    {
                        Suspicion += Config.RunSeenSuspicion;
                        Events.Add("네 발로 뛰다 들킴! 의심 +" + Config.RunSeenSuspicion);
                    }
                    break;
                case MoveKind.Act:
                    Hunger -= Config.WalkHunger;
                    int mi = MissionIndexAt(Player);
                    if (mi >= 0 && !MissionDone(mi))
                    {
                        WorkDone[mi]++;
                        if (MissionDone(mi)) Events.Add(As.MissionNames[mi] + " 끝");
                    }
                    else if (Map.MarkAt(Player) == Village.Well)
                    {
                        WashProgress++;
                        if (WashProgress >= Config.WashBeats) { WashProgress = 0; RunTiles = 0; Events.Add("냄새를 씻었다"); }
                    }
                    break;
                default:
                    Hunger -= Config.WalkHunger;
                    break;
            }
            if (m.Kind != MoveKind.Act) WashProgress = 0;

            BeatsUsed++;
            foreach (var n in Npcs) n.Step();

            // 진짜에게 보이면 걸어도 의심. 같은 노출이 이어지는 동안은 한 번만.
            bool realSees = RealSelfSight().Contains(Player);
            if (realSees && !realSawLastBeat)
            {
                Suspicion += Config.RealPersonSuspicion;
                Events.Add("진짜 " + As.Name + "와 마주침! 의심 +" + Config.RealPersonSuspicion);
            }
            realSawLastBeat = realSees;

            if (Suspicion >= Config.MaxSuspicion) { Suspicion = Config.MaxSuspicion; Ended = EndReason.Suspicion; return; }
            if (Hunger <= 0) { Hunger = 0; Ended = EndReason.Hunger; return; }

            bool all = true;
            for (int i = 0; i < WorkDone.Length; i++) if (!MissionDone(i)) all = false;
            if (all) { Finished = true; return; }
            if (BeatsUsed >= BeatLimit) Ended = EndReason.BeatsOut;
        }
    }
}
