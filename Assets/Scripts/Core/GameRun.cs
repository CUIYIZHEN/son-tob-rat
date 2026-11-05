using System;

namespace SonTob.Core
{
    public enum Phase { Night, Day, Over }

    /// <summary>
    /// 한 판: 밤 1 → 낮 1 → 밤 2 → 낮 2 → 밤 3 → 낮 3. 허기와 의심은 판 내내 이어집니다.
    /// 첫날 밤에 시작하고, 손톱을 먹는 순간 해가 뜨며, 낮 임무를 다 하면 해가 집니다.
    /// 날마다 GameConfig.ForDay(Day)의 값으로 어려워집니다.
    /// 같은 씨앗(seed)이면 같은 판이 나와서, 플레이 기록을 그대로 다시 돌려 볼 수 있습니다.
    /// </summary>
    public class GameRun
    {
        public readonly GameConfig Config;
        public readonly MapData Map;
        readonly Random rng;

        public int Day = 1;              // 1..Days
        public Phase Phase = Phase.Night;
        public NightPhase Night;
        public DayPhase DayState;
        public float Hunger;
        public float Suspicion;
        public EndReason Ended = EndReason.None;

        /// <summary>마지막 한 수에서 생긴 일. 밤↔낮이 바뀌어도 남아 있습니다.</summary>
        public readonly System.Collections.Generic.List<string> LastEvents = new System.Collections.Generic.List<string>();

        /// <summary>오늘의 난이도 값.</summary>
        public GameConfig Today { get { return Config.ForDay(Day); } }

        public GameRun(GameConfig config, int seed)
        {
            Config = config;
            Map = Village.CreateMap();
            rng = new Random(seed);
            Hunger = config.StartHunger;
            Suspicion = 0;
            StartNight();
        }

        void StartNight()
        {
            Phase = Phase.Night;
            if (Day > 1) Suspicion = Math.Max(0, Suspicion - Config.NightCalm);
            Hunger -= Config.NightHunger;
            Night = new NightPhase(Today, Map, rng);
            DayState = null;
            if (Hunger <= 0) { Hunger = 0; Finish(EndReason.Hunger); }
        }

        void StartDay(Nail eaten)
        {
            Phase = Phase.Day;
            if (eaten.Method == NailMethod.Burn)
            {
                Suspicion += Config.BurnSuspicion;
                LastEvents.Add("탄내가 배었다. 의심 +" + Config.BurnSuspicion);
            }
            DayState = new DayPhase(Today, Map, Village.Get(eaten.Owner), Hunger, Suspicion);
            if (Suspicion >= Config.MaxSuspicion) { Suspicion = Config.MaxSuspicion; Finish(EndReason.Suspicion); }
        }

        void Finish(EndReason why) { Ended = why; Phase = Phase.Over; }

        /// <summary>한 수 두기. 밤과 낮 모두 같은 조작입니다.</summary>
        public void Apply(Move m)
        {
            if (Phase == Phase.Over) return;
            LastEvents.Clear();
            if (Phase == Phase.Night)
            {
                Night.Apply(m);
                LastEvents.AddRange(Night.Events);
                if (Night.Ended != EndReason.None) { Finish(Night.Ended); return; }
                if (Night.Eaten != null)
                {
                    Hunger = Math.Min(Config.MaxHunger, Hunger + Config.NailHunger);
                    StartDay(Night.Eaten);
                }
                return;
            }

            DayState.Apply(m);
            LastEvents.AddRange(DayState.Events);
            Hunger = DayState.Hunger;
            Suspicion = DayState.Suspicion;
            if (DayState.Ended != EndReason.None) { Finish(DayState.Ended); return; }
            if (DayState.Finished)
            {
                if (Day >= Config.Days) { Finish(EndReason.Win); return; }
                Day++;
                StartNight();
            }
        }
    }
}
