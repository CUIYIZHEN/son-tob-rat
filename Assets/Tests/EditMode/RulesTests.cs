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
            var c = GameConfig.Normal();
            Assert.AreEqual(28, Village.Get(PersonId.Dolsoe).DisguiseBeats(c));
            Assert.AreEqual(30, Village.Get(PersonId.Buni).DisguiseBeats(c));
            Assert.AreEqual(32, Village.Get(PersonId.Hunjang).DisguiseBeats(c));
        }

        [Test]
        public void MissionNeedsTwoActs()
        {
            var c = GameConfig.Normal();
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
            var c = GameConfig.Normal();
            var map = Village.CreateMap();
            var day = new DayPhase(c, map, Village.Get(PersonId.Buni), 70, 0);
            day.Apply(new Move { Kind = MoveKind.Wait, Target = day.Player });
            Assert.AreEqual(70 - 0.8f, day.Hunger, 1e-4);
        }

        [Test]
        public void BeatsRunOutEndsDay()
        {
            var c = GameConfig.Normal();
            c.BaseDisguiseBeats = 2;
            var map = Village.CreateMap();
            var day = new DayPhase(c, map, Village.Get(PersonId.Buni), 70, 0);
            day.Apply(new Move { Kind = MoveKind.Wait, Target = day.Player });
            day.Apply(new Move { Kind = MoveKind.Wait, Target = day.Player });
            Assert.AreEqual(EndReason.BeatsOut, day.Ended);
        }
    }

    public class NightTests
    {
        static NightPhase Night(GameConfig c, NailMethod d, NailMethod b, NailMethod h)
        {
            return new NightPhase(c, Village.CreateMap(), new[] { d, b, h });
        }

        [Test]
        public void DolsoeYardIsOutsideHearing()
        {
            var n = Night(GameConfig.Normal(), NailMethod.Throw, NailMethod.Throw, NailMethod.Burn);
            Assert.IsFalse(n.InHearing(n.Map.Mark('1')));
        }

        [Test]
        public void PickingBuniThrownNailTwitchesOnce()
        {
            var n = Night(GameConfig.Normal(), NailMethod.Throw, NailMethod.Throw, NailMethod.Burn);
            n.Player = n.Map.Mark('2');
            n.Apply(n.ActHere());
            Assert.AreEqual(1, n.Twitches);
            Assert.IsFalse(n.CatAwake);
            Assert.IsNotNull(n.Eaten);
        }

        [Test]
        public void DiggingBuniBuriedNailWakesCat()
        {
            var n = Night(GameConfig.Normal(), NailMethod.Throw, NailMethod.Bury, NailMethod.Burn);
            n.Player = n.Map.Mark('2');
            n.Apply(n.ActHere()); // 파기
            n.Apply(n.ActHere()); // 줍기
            Assert.IsTrue(n.CatAwake);
        }

        [Test]
        public void ReturningWithNailBringsMorningAndCatSleeps()
        {
            var n = Night(GameConfig.Normal(), NailMethod.Throw, NailMethod.Throw, NailMethod.Burn);
            n.Eaten = n.Nails[0];
            n.Player = n.Hole + new Pos(1, 0);
            n.Apply(new Move { Kind = MoveKind.Walk, Target = n.Hole });
            Assert.IsTrue(n.Morning);
        }

        [Test]
        public void HoleWithoutNailDoesNotEndNight()
        {
            var n = Night(GameConfig.Normal(), NailMethod.Throw, NailMethod.Throw, NailMethod.Burn);
            n.Player = n.Hole + new Pos(1, 0);
            n.Apply(new Move { Kind = MoveKind.Walk, Target = n.Hole });
            Assert.IsFalse(n.Morning);
        }
    }

    public class RunTests
    {
        [Test]
        public void SameSeedSameNails()
        {
            var a = new GameRun(GameConfig.Normal(), 42);
            var b = new GameRun(GameConfig.Normal(), 42);
            for (int i = 0; i < 3; i++) Assert.AreEqual(a.Night.Nails[i].Method, b.Night.Nails[i].Method);
        }

        [Test]
        public void FirstNightCostsHunger()
        {
            var r = new GameRun(GameConfig.Normal(), 1);
            Assert.AreEqual(50f, r.Hunger, 1e-4);
        }
    }
}
