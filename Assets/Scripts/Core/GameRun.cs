using System.Collections.Generic;

namespace SonTob.Core
{
    public enum Phase { Night, Day, Over }

    /// <summary>
    /// 한 판: 밤 1번 → 낮 1번. 밤에 잡히거나 해가 뜨면 밤을 처음부터 다시 하고,
    /// 손톱을 먹으면 낮으로 넘어가며 먹은 걸음에 따라 낮 걸음이 늘어난다.
    /// 낮에 의심이 100이 되거나 걸음이 다하면 끝(다시 하기는 밤부터).
    /// </summary>
    public class GameRun
    {
        public readonly GameConfig Config;
        public readonly MapData Map;

        public Phase Phase = Phase.Night;
        public NightPhase Night;
        public DayPhase DayState;
        public int NightTries = 1;          // 밤을 몇 번째 하는가
        public int Bonus;                   // 밤에서 얻은 낮 걸음
        public EndReason Ended = EndReason.None;

        /// <summary>마지막 한 수에서 생긴 일. 밤에서 낮으로 바뀌어도 남아 있습니다.</summary>
        public readonly List<string> LastEvents = new List<string>();

        public GameRun(GameConfig config)
        {
            Config = config;
            Map = Village.CreateMap();
            Night = new NightPhase(config, Map);
        }

        /// <summary>밤을 처음부터 다시(잡혔거나 해가 떴을 때).</summary>
        public void RetryNight()
        {
            NightTries++;
            Night = new NightPhase(Config, Map);
            DayState = null;
            Phase = Phase.Night;
            Ended = EndReason.None;
            Bonus = 0;
        }

        /// <summary>밤 1번부터 완전히 새로.</summary>
        public void Restart()
        {
            NightTries = 0;
            RetryNight();
            LastEvents.Clear();
        }

        /// <summary>밤이 실패로 끝났나(화면에서 「다시 하기」를 보여 줄 때).</summary>
        public bool NightFailed { get { return Phase == Phase.Night && Night.Ended != EndReason.None; } }

        /// <summary>한 수 두기. 밤과 낮 모두 같은 조작입니다.</summary>
        public void Apply(Move m)
        {
            if (Phase == Phase.Over || NightFailed) return;
            LastEvents.Clear();
            if (Phase == Phase.Night)
            {
                Night.Apply(m);
                LastEvents.AddRange(Night.Events);
                if (Night.Eaten)
                {
                    Bonus = Night.Bonus;
                    DayState = new DayPhase(Config, Map, Bonus);
                    Phase = Phase.Day;
                }
                return;
            }

            DayState.Apply(m);
            LastEvents.AddRange(DayState.Events);
            if (DayState.Ended != EndReason.None) { Ended = DayState.Ended; Phase = Phase.Over; }
            else if (DayState.Finished) { Ended = EndReason.Win; Phase = Phase.Over; }
        }
    }
}
