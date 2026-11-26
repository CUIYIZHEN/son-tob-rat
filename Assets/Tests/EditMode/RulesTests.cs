using System.Collections.Generic;
using NUnit.Framework;
using SonTob.Core;

namespace SonTob.Tests
{
    /// <summary>밤을 처음부터 같은 수로 다시 돌려 보는 도우미. 고양이 길이 정해져 있어 결과가 늘 같습니다.</summary>
    static class NightSearch
    {
        public static NightPhase Replay(List<Move> moves)
        {
            var night = new NightPhase(new GameConfig(), Village.CreateMap());
            foreach (var m in moves) night.Apply(m);
            return night;
        }

        static List<Move> Plus(List<Move> seq, Move m) { var l = new List<Move>(seq); l.Add(m); return l; }

        /// <summary>안전하게 손톱을 먹을 수 있는 걸음들을 모두 구한다. 달리기를 안 쓰면 walkOnly.</summary>
        public static SortedSet<int> SafeFinishes(bool walkOnly, out List<Move> fastest)
        {
            var finishes = new SortedSet<int>();
            fastest = null;
            var layer = new Dictionary<Pos, List<Move>>();
            var start = new NightPhase(new GameConfig(), Village.CreateMap());
            layer[start.Player] = new List<Move>();
            for (int t = 0; t < start.Config.NightSteps; t++)
            {
                var next = new Dictionary<Pos, List<Move>>();
                foreach (var kv in layer)
                {
                    var now = Replay(kv.Value);
                    var options = new List<Move>();
                    foreach (var m in now.Candidates()) if (!walkOnly || m.Kind == MoveKind.Walk) options.Add(m);
                    options.Add(new Move { Kind = MoveKind.Wait, Target = now.Player, Middle = now.Player });
                    if (now.CanEat) options.Add(now.ActHere());
                    foreach (var m in options)
                    {
                        var seq = Plus(kv.Value, m);
                        var after = Replay(seq);
                        if (after.Ended != EndReason.None) continue;
                        if (after.Eaten)
                        {
                            if (finishes.Add(after.EatenAt) && (fastest == null || seq.Count < fastest.Count)) fastest = seq;
                            continue;
                        }
                        if (!next.ContainsKey(after.Player)) next[after.Player] = seq;
                    }
                }
                layer = next;
            }
            return finishes;
        }

        public static List<Move> WalkTo(MapData map, Pos from, Pos to)
        {
            var moves = new List<Move>();
            var cur = from;
            foreach (var p in map.Path(from, to, true))
            {
                Dir d;
                Dirs.TryFromDelta(p - cur, out d);
                moves.Add(new Move { Kind = MoveKind.Walk, Dir = d, Target = p, Middle = p });
                cur = p;
            }
            return moves;
        }

        public static Move Wait() { return new Move { Kind = MoveKind.Wait }; }
    }

    public class MapTests
    {
        [Test]
        public void TourOfTheThreeMissionsIsThirtyTilesFromRatHole()
        {
            var map = Village.CreateMap();
            var stops = new List<Pos>();
            foreach (var c in Village.TheLord.Missions) stops.Add(map.Mark(c));
            Assert.AreEqual(30, map.ShortestTour(map.Mark(Village.RatHole), stops, false));
        }

        [Test]
        public void KeyTilesAreWhereTheDocumentPutsThem()
        {
            var map = Village.CreateMap();
            Assert.AreEqual(12, map.Width);
            Assert.AreEqual(12, map.Height);
            Assert.AreEqual(new Pos(5, 6), map.Mark(Village.RatHole));
            Assert.AreEqual(new Pos(7, 10), map.Mark(Village.NailMark));
            Assert.AreEqual(new Pos(6, 3), map.Mark(Village.Well));
            Assert.AreEqual(new Pos(2, 1), map.Mark('h'));
            Assert.AreEqual(new Pos(4, 0), map.Mark('i'));
            Assert.AreEqual(new Pos(0, 11), map.Mark('j'));
            foreach (var p in new[] { new Pos(9, 3), new Pos(2, 4), new Pos(3, 8) })
                Assert.AreEqual(Tile.DogHole, map.TileAt(p));
        }

        [Test]
        public void SightStopsAtWallsAndDogHoles()
        {
            var map = Village.CreateMap();
            Assert.AreEqual(0, Npc.SightLine(map, new Pos(4, 6), Dir.MinusX, 3).Count);  // (3,6) 담장
            Assert.AreEqual(3, Npc.SightLine(map, new Pos(4, 6), Dir.PlusY, 3).Count);   // (4,7)~(4,9) 길
            Assert.AreEqual(1, Npc.SightLine(map, new Pos(4, 4), Dir.MinusX, 3).Count);  // (2,4) 개구멍이 막음
        }

        [Test]
        public void WalkingCannotEnterDogHoleButRunningCan()
        {
            var map = Village.CreateMap();
            var moves = MoveRules.Candidates(map, new Pos(3, 4), false);
            Assert.IsFalse(moves.Exists(m => m.Kind == MoveKind.Walk && m.Target == new Pos(2, 4)));
            Assert.IsTrue(moves.Exists(m => m.Kind == MoveKind.Run && m.Target == new Pos(1, 4)));
        }
    }

    public class PeopleTests
    {
        [Test]
        public void LoopLengthsMatchTheDocument()
        {
            var map = Village.CreateMap();
            Assert.AreEqual(52, new Npc(Village.CatSpec(), map).LoopLength);
            var lengths = new Dictionary<string, int>
            {
                { "우물가 아낙", 1 }, { "서당 아이", 8 }, { "장꾼", 8 }, { "농부", 12 }, { "진짜 도령", 4 },
            };
            foreach (var spec in Village.DayNpcs())
                Assert.AreEqual(lengths[spec.Name], new Npc(spec, map).LoopLength, spec.Name);
        }

        [Test]
        public void EveryoneIsBackAtTheStartAfterTwentyFourSteps()
        {
            var map = Village.CreateMap();
            foreach (var spec in Village.DayNpcs())
            {
                var npc = new Npc(spec, map);
                var start = npc.Pos;
                for (int i = 0; i < 24; i++) npc.Step();
                Assert.AreEqual(start, npc.Pos, spec.Name);
            }
        }

        [Test]
        public void WellWomanTurnsEveryTwoSteps()
        {
            var map = Village.CreateMap();
            var woman = new Npc(Village.DayNpcs()[0], map);
            var seen = new List<Dir>();
            for (int i = 0; i < 9; i++) { seen.Add(woman.Facing); woman.Step(); }
            CollectionAssert.AreEqual(new[] { Dir.MinusX, Dir.MinusX, Dir.PlusY, Dir.PlusY, Dir.PlusX, Dir.PlusX, Dir.MinusY, Dir.MinusY, Dir.MinusX }, seen);
        }

        [Test]
        public void CatStartsAtTenNineAndPassesTheNailAtStepEight()
        {
            var cat = new Npc(Village.CatSpec(), Village.CreateMap());
            Assert.AreEqual(new Pos(10, 9), cat.Pos);
            for (int i = 0; i < 8; i++) cat.Step();
            Assert.AreEqual(new Pos(7, 10), cat.Pos);
            for (int i = 0; i < 4; i++) cat.Step();
            Assert.AreEqual(4, cat.Pos.X);  // 12걸음: 쥐가 내려가는 왼쪽에서 다섯째 세로 길
        }

        [Test]
        public void CatNeverSeesTheRatHole()
        {
            var night = new NightPhase(new GameConfig(), Village.CreateMap());
            for (int i = 0; i < 52; i++)
            {
                Assert.IsFalse(night.CatCone().Contains(night.Hole), "step " + i);
                night.CatNpc.Step();
            }
        }
    }

    public class NightTests
    {
        [Test]
        public void BonusFollowsTheFormula()
        {
            var c = new GameConfig();
            Assert.AreEqual(3, c.NightBonus(12));
            Assert.AreEqual(2, c.NightBonus(13));
            Assert.AreEqual(2, c.NightBonus(16));
            Assert.AreEqual(1, c.NightBonus(17));
            Assert.AreEqual(1, c.NightBonus(20));
            Assert.AreEqual(0, c.NightBonus(21));
            Assert.AreEqual(0, c.NightBonus(24));
        }

        [Test]
        public void WalkingStraightToTheNailGetsCaughtAtStepSeven()
        {
            var map = Village.CreateMap();
            var night = new NightPhase(new GameConfig(), map);
            foreach (var m in NightSearch.WalkTo(map, night.Hole, night.NailTile))
            {
                night.Apply(m);
                if (night.Ended != EndReason.None) break;
            }
            Assert.AreEqual(EndReason.CaughtByCat, night.Ended);
            Assert.AreEqual(7, night.StepsUsed);
        }

        [Test]
        public void WaitingTwelveThenWalkingFinishesAtTwentyOneWithNoBonus()
        {
            var map = Village.CreateMap();
            var night = new NightPhase(new GameConfig(), map);
            for (int i = 0; i < 12; i++) night.Apply(NightSearch.Wait());
            Assert.AreEqual(EndReason.None, night.Ended);
            foreach (var m in NightSearch.WalkTo(map, night.Hole, night.NailTile)) night.Apply(m);
            Assert.AreEqual(EndReason.None, night.Ended);
            Assert.IsTrue(night.CanEat);
            night.Apply(night.ActHere());
            Assert.IsTrue(night.Eaten);
            Assert.AreEqual(21, night.EatenAt);
            Assert.AreEqual(0, night.Bonus);
        }

        [Test]
        public void FastestSafeFinishIsTwelveWithBonusThree()
        {
            List<Move> fastest;
            var finishes = NightSearch.SafeFinishes(false, out fastest);
            Assert.AreEqual(12, finishes.Min);
            Assert.AreEqual(3, NightSearch.Replay(fastest).Bonus);
            Assert.IsTrue(finishes.Contains(21));
        }

        [Test]
        public void WalkingOnlyCanStillFinishAtEighteenWithBonusOne()
        {
            List<Move> fastest;
            var finishes = NightSearch.SafeFinishes(true, out fastest);
            Assert.AreEqual(18, finishes.Min);
            Assert.AreEqual(1, NightSearch.Replay(fastest).Bonus);
        }

        [Test]
        public void WaitingAllNightEndsInSunriseNotCaught()
        {
            var night = new NightPhase(new GameConfig(), Village.CreateMap());
            for (int i = 0; i < 24; i++) night.Apply(NightSearch.Wait());
            Assert.AreEqual(EndReason.Sunrise, night.Ended);
            Assert.IsFalse(night.Eaten);
        }

        [Test]
        public void EatingButtonIsOnlyOnTheNailTile()
        {
            var night = new NightPhase(new GameConfig(), Village.CreateMap());
            Assert.IsFalse(night.CanEat);
            Assert.AreEqual(MoveKind.Wait, night.ActHere().Kind);
            night.Player = night.NailTile;
            Assert.IsTrue(night.CanEat);
            Assert.AreEqual(MoveKind.Act, night.ActHere().Kind);
        }
    }

    public class DayTests
    {
        static DayPhase NewDay(int bonus = 0, int steps = 26)
        {
            return new DayPhase(new GameConfig { DaySteps = steps }, Village.CreateMap(), bonus);
        }

        [Test]
        public void StepLimitIsTwentySixPlusBonus()
        {
            Assert.AreEqual(26, NewDay(0).StepLimit);
            Assert.AreEqual(29, NewDay(3).StepLimit);
        }

        [Test]
        public void WorkButtonIsOnlyOnAMissionTile()
        {
            var day = NewDay();
            Assert.AreEqual(MoveKind.Wait, day.ActHere().Kind);
            day.Player = day.MissionTile(0);
            Assert.AreEqual(MoveKind.Act, day.ActHere().Kind);
            day.Apply(day.ActHere());
            Assert.IsTrue(day.Done[0]);
            Assert.AreEqual(MoveKind.Wait, day.ActHere().Kind);   // 한 임무는 한 번
        }

        [Test]
        public void WalkingTheWholeTourTakesThirtyThreeSteps()
        {
            var map = Village.CreateMap();
            var day = NewDay(0, 33);
            foreach (var c in Village.TheLord.Missions)
            {
                foreach (var p in map.Path(day.Player, map.Mark(c), false))
                {
                    Dir d; Dirs.TryFromDelta(p - day.Player, out d);
                    day.Apply(new Move { Kind = MoveKind.Walk, Dir = d, Target = p, Middle = p });
                }
                day.Apply(day.ActHere());
            }
            Assert.IsTrue(day.Finished);
            Assert.AreEqual(33, day.StepsUsed);
            Assert.AreEqual(0, day.Suspicion % 40);   // 걷기만 하면 사람 눈에는 안 걸리고 진짜 도령에게만 +40
        }

        [Test]
        public void RunningThroughSeenTilesAddsThirty()
        {
            var map = Village.CreateMap();
            bool found = false;
            for (int y = 0; y < map.Height && !found; y++)
                for (int x = 0; x < map.Width && !found; x++)
                {
                    var day = NewDay();
                    day.Player = new Pos(x, y);
                    if (!map.Walkable(day.Player, false)) continue;
                    foreach (var m in day.Candidates())
                    {
                        if (m.Kind != MoveKind.Run || !m.Risky) continue;
                        day.Apply(m);
                        Assert.GreaterOrEqual(day.Suspicion, 30);
                        found = true;
                        break;
                    }
                }
            Assert.IsTrue(found);
        }

        [Test]
        public void WalkingIsNeverMarkedRisky()
        {
            var day = NewDay();
            Assert.IsTrue(day.WatchedTiles().Count > 0);
            foreach (var m in day.Candidates()) if (m.Kind == MoveKind.Walk) Assert.IsFalse(m.Risky);
        }

        [Test]
        public void MeetingTheRealLordCostsFortyEvenWhenWalking()
        {
            var map = Village.CreateMap();
            bool found = false;
            for (int y = 0; y < map.Height && !found; y++)
                for (int x = 0; x < map.Width && !found; x++)
                {
                    var p = new Pos(x, y);
                    if (!map.Walkable(p, false)) continue;
                    var day = NewDay();
                    day.Player = p;
                    day.Apply(new Move { Kind = MoveKind.Wait, Target = p, Middle = p });
                    if (day.Events.Exists(e => e.Contains("진짜 도령")))
                    {
                        Assert.AreEqual(40, day.Suspicion);
                        found = true;
                    }
                }
            Assert.IsTrue(found);
        }

        [Test]
        public void SuspicionHundredEndsTheDay()
        {
            var map = Village.CreateMap();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var day = NewDay();
                    day.Player = new Pos(x, y);
                    if (!map.Walkable(day.Player, false)) continue;
                    foreach (var m in day.Candidates())
                    {
                        if (m.Kind != MoveKind.Run || !m.Risky) continue;
                        day.Suspicion = 80;
                        day.Apply(m);
                        Assert.AreEqual(EndReason.Suspicion, day.Ended);
                        Assert.AreEqual(100, day.Suspicion);
                        return;
                    }
                }
            Assert.Fail("눈에 드는 달리기를 찾지 못했다");
        }

        [Test]
        public void RunningOutOfStepsBeforeThreeTasksFails()
        {
            var day = NewDay(0, 3);
            for (int i = 0; i < 3; i++) day.Apply(NightSearch.Wait());
            Assert.AreEqual(EndReason.StepsOut, day.Ended);
        }
    }

    public class RunTests
    {
        [Test]
        public void EatingStartsTheDayWithBonusSteps()
        {
            List<Move> fastest;
            NightSearch.SafeFinishes(false, out fastest);
            var run = new GameRun(new GameConfig());
            foreach (var m in fastest) run.Apply(m);
            Assert.AreEqual(Phase.Day, run.Phase);
            Assert.AreEqual(3, run.Bonus);
            Assert.AreEqual(29, run.DayState.StepLimit);
        }

        [Test]
        public void BeingCaughtAllowsRetryingTheNight()
        {
            var run = new GameRun(new GameConfig());
            var map = run.Map;
            foreach (var m in NightSearch.WalkTo(map, run.Night.Hole, run.Night.NailTile))
            {
                run.Apply(m);
                if (run.NightFailed) break;
            }
            Assert.IsTrue(run.NightFailed);
            Assert.AreEqual(1, run.NightTries);
            run.RetryNight();
            Assert.AreEqual(2, run.NightTries);
            Assert.AreEqual(Phase.Night, run.Phase);
            Assert.AreEqual(0, run.Night.StepsUsed);
        }
    }
}
