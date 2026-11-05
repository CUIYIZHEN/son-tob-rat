using System.Collections.Generic;
using NUnit.Framework;
using SonTob.Core;

namespace SonTob.Tests
{
    public class MapTests
    {
        [Test]
        public void EveryPersonTourIsThirtyTilesFromRatHole()
        {
            var map = Village.CreateMap();
            var start = map.Mark(Village.RatHole);
            foreach (var p in Village.People)
            {
                var stops = new List<Pos>();
                foreach (var c in p.Missions) stops.Add(map.Mark(c));
                Assert.AreEqual(30, map.ShortestTour(start, stops, false), p.Name);
            }
        }

        [Test]
        public void DogHolesShortenBuniAndHunjang()
        {
            var map = Village.CreateMap();
            var start = map.Mark(Village.RatHole);
            var buni = new List<Pos> { map.Mark('e'), map.Mark('f'), map.Mark('g') };
            var hun = new List<Pos> { map.Mark('h'), map.Mark('i'), map.Mark('j') };
            Assert.AreEqual(23, map.ShortestTour(start, buni, true));
            Assert.AreEqual(26, map.ShortestTour(start, hun, true));
        }

        [Test]
        public void SightStopsAtWalls()
        {
            var map = Village.CreateMap();
            // (4,6)에서 -X 쪽: (3,6)은 담장이라 아무것도 못 봄
            Assert.AreEqual(0, Npc.SightLine(map, new Pos(4, 6), Dir.MinusX, 3).Count);
            // (4,6)에서 +Y 쪽: (4,7),(4,8),(4,9) 길
            Assert.AreEqual(3, Npc.SightLine(map, new Pos(4, 6), Dir.PlusY, 3).Count);
        }

        [Test]
        public void WalkingCannotEnterDogHoleButRunningCan()
        {
            var map = Village.CreateMap();
            var from = new Pos(3, 4);            // 훈장네 마당, 서쪽 (2,4)가 개구멍
            var moves = MoveRules.Candidates(map, from, false);
            Assert.IsFalse(moves.Exists(m => m.Kind == MoveKind.Walk && m.Target == new Pos(2, 4)));
            Assert.IsTrue(moves.Exists(m => m.Kind == MoveKind.Run && m.Target == new Pos(1, 4)));
        }
    }

    public class DayTests
    {
        [Test]
        public void DisguiseBeatsFollowPersonOffset()
        {
            var c = new GameConfig();
            Assert.AreEqual(28, Village.Get(PersonId.Dolsoe).DisguiseBeats(c));
            Assert.AreEqual(30, Village.Get(PersonId.Buni).DisguiseBeats(c));
            Assert.AreEqual(32, Village.Get(PersonId.Hunjang).DisguiseBeats(c));
        }

        [Test]
        public void MissionNeedsTwoActs()
        {
            var c = new GameConfig();
            var map = Village.CreateMap();
            var day = new DayPhase(c, map, Village.Get(PersonId.Buni), 70, 0);
            day.Player = map.Mark('f');
            day.Apply(day.ActHere());
            Assert.IsFalse(day.MissionDone(1));
            day.Apply(day.ActHere());
            Assert.IsTrue(day.MissionDone(1));
        }

        [Test]
        public void HungerCostsMatchLab()
        {
            var c = new GameConfig();
            var map = Village.CreateMap();
            var day = new DayPhase(c, map, Village.Get(PersonId.Buni), 70, 0);
            day.Apply(new Move { Kind = MoveKind.Wait, Target = day.Player });
            Assert.AreEqual(70 - 0.8f, day.Hunger, 1e-4);
        }

        [Test]
        public void BeatsRunOutEndsDay()
        {
            var c = new GameConfig();
            c.BaseDisguiseBeats = 2;
            var map = Village.CreateMap();
            var day = new DayPhase(c, map, Village.Get(PersonId.Buni), 70, 0);
            day.Apply(new Move { Kind = MoveKind.Wait, Target = day.Player });
            day.Apply(new Move { Kind = MoveKind.Wait, Target = day.Player });
            Assert.AreEqual(EndReason.BeatsOut, day.Ended);
        }
    }

    public class DifficultyTests
    {
        [Test]
        public void DaysGetHarder()
        {
            var c = new GameConfig();
            var d1 = c.ForDay(1); var d3 = c.ForDay(3);
            Assert.AreEqual(32, d1.BaseDisguiseBeats);
            Assert.AreEqual(1, d1.CatRing);
            Assert.AreEqual(20, d1.ThrowShiftPercent);
            Assert.AreEqual(24, d1.NightBeats);
            Assert.AreEqual(28, d3.BaseDisguiseBeats);
            Assert.AreEqual(3, d3.CatRing);
            Assert.AreEqual(-20, d3.ThrowShiftPercent);
            Assert.AreEqual(20, d3.NightBeats);
        }

        [Test]
        public void RunStartsOnFirstNightWithDayOneValues()
        {
            var r = new GameRun(new GameConfig(), 7);
            Assert.AreEqual(Phase.Night, r.Phase);
            Assert.AreEqual(24, r.Night.BeatLimit);
            Assert.AreEqual(1, r.Night.Config.CatRing);
        }

        [Test]
        public void RunSeenCostsThirty()
        {
            Assert.AreEqual(30, new GameConfig().RunSeenSuspicion);
        }
    }

    public class NightTests
    {
        static NightPhase Night(GameConfig c, NailMethod d, NailMethod b, NailMethod h)
        {
            return new NightPhase(c, Village.CreateMap(), new[] { d, b, h });
        }

        static Move Wait(NightPhase n) { return new Move { Kind = MoveKind.Wait, Target = n.Player, Middle = n.Player }; }

        [Test]
        public void CatRouteIsAClosedLoopOnRoads()
        {
            var map = Village.CreateMap();
            var cat = new Npc(Village.CatRoute(map), map);
            Assert.Greater(cat.LoopLength, 10);
            var start = cat.Pos;
            for (int i = 0; i < cat.LoopLength; i++)
            {
                var before = cat.Pos;
                cat.Step();
                Assert.AreEqual(1, before.Manhattan(cat.Pos));
                Assert.IsTrue(map.Walkable(cat.Pos, false));
            }
            Assert.AreEqual(start, cat.Pos);
        }

        [Test]
        public void NailsAreHiddenUntilSniffed()
        {
            var n = Night(new GameConfig(), NailMethod.Throw, NailMethod.Throw, NailMethod.Burn);
            var dolsoe = n.Nails[0];
            Assert.IsFalse(dolsoe.Found);
            n.Player = dolsoe.Tile + new Pos(-2, 0);
            n.Apply(Wait(n));
            Assert.IsTrue(dolsoe.Found);
        }

        [Test]
        public void ThrownNailIsEatenInOneBeat()
        {
            var n = Night(new GameConfig(), NailMethod.Throw, NailMethod.Throw, NailMethod.Burn);
            n.Player = n.Map.Mark('1');
            n.Apply(n.ActHere());
            Assert.AreEqual(PersonId.Dolsoe, n.Eaten.Owner);
        }

        [Test]
        public void BuriedNailTakesThreeDigs()
        {
            var n = Night(new GameConfig(), NailMethod.Bury, NailMethod.Throw, NailMethod.Burn);
            n.Player = n.Map.Mark('1');
            n.Apply(n.ActHere());
            n.Apply(n.ActHere());
            Assert.IsNull(n.Eaten);
            n.Apply(n.ActHere());
            Assert.IsNotNull(n.Eaten);
        }

        [Test]
        public void SunriseWithoutNailFails()
        {
            var c = new GameConfig().ForDay(1);
            c.NightBeats = 2;
            var n = Night(c, NailMethod.Throw, NailMethod.Throw, NailMethod.Burn);
            n.Apply(Wait(n));
            Assert.AreEqual(EndReason.None, n.Ended);
            n.Apply(Wait(n));
            Assert.AreEqual(EndReason.Sunrise, n.Ended);
        }

        [Test]
        public void CatNoticesThenChasesAndCatches()
        {
            var n = Night(new GameConfig().ForDay(2), NailMethod.Throw, NailMethod.Throw, NailMethod.Burn);
            // 고양이 바로 다음 칸에 서서 기다리면 고리 안에 들어간다
            n.Player = n.CatPatrol.NextPos();
            n.Apply(Wait(n));
            Assert.IsTrue(n.CatChasing || n.Ended == EndReason.CaughtByCat);
            for (int i = 0; i < 4 && n.Ended == EndReason.None; i++) n.Apply(Wait(n));
            Assert.AreEqual(EndReason.CaughtByCat, n.Ended);
        }

        [Test]
        public void RatHoleShakesOffTheCat()
        {
            var n = Night(new GameConfig().ForDay(2), NailMethod.Throw, NailMethod.Throw, NailMethod.Burn);
            n.CatChasing = true;
            n.Cat = n.Hole + new Pos(0, 3);
            n.Player = n.Hole;
            n.Apply(Wait(n));
            Assert.IsFalse(n.CatChasing);
            Assert.AreEqual(EndReason.None, n.Ended);
        }

        [Test]
        public void GettingFarAwayLosesTheCat()
        {
            var n = Night(new GameConfig().ForDay(1), NailMethod.Throw, NailMethod.Throw, NailMethod.Burn);
            n.CatChasing = true;
            n.Player = n.Map.Mark('1');   // 장독대에서 먼 동쪽
            n.Apply(Wait(n));
            Assert.IsFalse(n.CatChasing);
        }
    }

    public class RunTests
    {
        [Test]
        public void SameSeedSameNails()
        {
            var a = new GameRun(new GameConfig(), 42);
            var b = new GameRun(new GameConfig(), 42);
            for (int i = 0; i < 3; i++) Assert.AreEqual(a.Night.Nails[i].Method, b.Night.Nails[i].Method);
        }

        [Test]
        public void FirstNightCostsHunger()
        {
            var r = new GameRun(new GameConfig(), 1);
            Assert.AreEqual(50f, r.Hunger, 1e-4);
        }

        [Test]
        public void EatingNailBringsMorningAsThatPerson()
        {
            var r = new GameRun(new GameConfig(), 3);
            var nail = r.Night.Nails[1];
            nail.Method = NailMethod.Throw;
            r.Night.Player = nail.Tile;
            r.Apply(r.Night.ActHere());
            Assert.AreEqual(Phase.Day, r.Phase);
            Assert.AreEqual(PersonId.Buni, r.DayState.As.Id);
            Assert.AreEqual(32, r.DayState.BeatLimit); // 1일 분이 = B 32
            Assert.AreEqual(100f, r.Hunger, 1e-4);     // 70 - 20 + 50
        }

        [Test]
        public void BurnedNailRaisesSuspicionNextDay()
        {
            var r = new GameRun(new GameConfig(), 3);
            var nail = r.Night.Nails[2];
            nail.Method = NailMethod.Burn;
            r.Night.Player = nail.Tile;
            r.Apply(r.Night.ActHere());
            Assert.AreEqual(Phase.Day, r.Phase);
            Assert.AreEqual(10f, r.Suspicion, 1e-4);
        }
    }
}
