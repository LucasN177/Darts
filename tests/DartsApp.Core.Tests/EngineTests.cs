using DartsApp.Core.Engine;
using DartsApp.Core.Models;

namespace DartsApp.Core.Tests;

public class DartTests
{
    [Theory]
    [InlineData("T20", 20, 3)]
    [InlineData("d16", 16, 2)]
    [InlineData("5", 5, 1)]
    [InlineData("25", 25, 1)]
    [InlineData("BULL", 25, 2)]
    [InlineData("0", 0, 1)]
    public void Parse_RoundTrips(string text, int segment, int multiplier)
    {
        var dart = Dart.Parse(text);
        Assert.Equal(segment, dart.Segment);
        Assert.Equal(multiplier, dart.Multiplier);
        Assert.Equal(dart, Dart.Parse(dart.ToString()));
    }

    [Fact]
    public void TripleBull_IsInvalid() => Assert.Throws<ArgumentOutOfRangeException>(() => new Dart(25, 3));
}

public class X01Tests
{
    private static readonly PlayerRef[] TwoPlayers = [new(null, "Anna"), new(null, "Ben")];

    private static X01Engine Create(int start = 501, InOutRule inRule = InOutRule.Single, InOutRule outRule = InOutRule.Double, int legs = 1, int sets = 1, params PlayerRef[] players) =>
        new(new GameSettings { Mode = GameMode.X01, StartScore = start, InRule = inRule, OutRule = outRule, LegsToWin = legs, SetsToWin = sets },
            players.Length == 0 ? TwoPlayers : players);

    private static void Throw(GameEngine e, params string[] darts)
    {
        foreach (var d in darts) e.Throw(Dart.Parse(d));
    }

    [Fact]
    public void Turn_SubtractsAndSwitchesPlayer()
    {
        var e = Create();
        Throw(e, "T20", "T20", "T20");
        Assert.Equal(321, e.RemainingOf(0));
        Assert.Equal(1, e.CurrentPlayer);
        Assert.Equal(180, e.Turns[0].Score);
    }

    [Fact]
    public void Bust_RestoresTurnStart()
    {
        var e = Create(start: 40);
        Throw(e, "S20", "S19", "S5");
        Assert.Equal(40, e.RemainingOf(0));
        Assert.True(e.Turns[0].Bust);
        Assert.Equal(1, e.CurrentPlayer);
    }

    [Fact]
    public void LeavingOne_IsBustWithDoubleOut()
    {
        var e = Create(start: 21);
        Throw(e, "S20");
        Assert.Equal(21, e.RemainingOf(0));
        Assert.Equal(1, e.CurrentPlayer);
    }

    [Fact]
    public void FinishingOnSingle_IsBustWithDoubleOut_ButFineWithSingleOut()
    {
        var e = Create(start: 20);
        Throw(e, "S20");
        Assert.Equal(20, e.RemainingOf(0));

        var single = Create(start: 20, outRule: InOutRule.Single);
        Throw(single, "S20");
        Assert.True(single.IsFinished);
        Assert.Equal(0, single.Winner);
    }

    [Fact]
    public void MasterOut_AllowsTriple()
    {
        var e = Create(start: 60, outRule: InOutRule.Master);
        Throw(e, "T20");
        Assert.True(e.IsFinished);
    }

    [Fact]
    public void DoubleIn_IgnoresDartsUntilDouble()
    {
        var e = Create(start: 301, inRule: InOutRule.Double);
        Throw(e, "T20", "D10", "S20");
        Assert.Equal(301 - 20 - 20, e.RemainingOf(0));
    }

    [Fact]
    public void Checkout_WinsGame_AndEndsTurnEarly()
    {
        var e = Create(start: 101);
        Throw(e, "T17", "BULL");
        Assert.True(e.IsFinished);
        Assert.Equal(0, e.Winner);
        var result = e.GetResults().Single(r => r.Position == 0);
        Assert.Equal(101, result.HighestCheckout);
        Assert.Equal(1, result.CheckoutsHit);
        Assert.True(result.IsWinner);
        Assert.Equal(1, result.Rank);
    }

    [Fact]
    public void Legs_RotateStarterAndCountLegs()
    {
        var e = Create(start: 40, legs: 2);
        Throw(e, "D20");
        Assert.False(e.IsFinished);
        Assert.Equal(1, e.LegsOf(0));
        Assert.Equal(2, e.Leg);
        Assert.Equal(1, e.CurrentPlayer); // Ben beginnt Leg 2
        Assert.Equal(40, e.RemainingOf(0));

        Throw(e, "S1", "S1", "S1"); // Ben
        Throw(e, "D20"); // Anna
        Assert.True(e.IsFinished);
        Assert.Equal(0, e.Winner);
    }

    [Fact]
    public void Sets_ResetLegs()
    {
        var e = Create(start: 40, legs: 1, sets: 2);
        Throw(e, "D20");
        Assert.Equal(1, e.SetsOf(0));
        Assert.Equal(0, e.LegsOf(0));
        Assert.Equal(2, e.Set);
        Assert.False(e.IsFinished);
    }

    [Fact]
    public void Undo_RestoresPreviousState()
    {
        var e = Create();
        Throw(e, "T20", "T20", "T20", "S1");
        e.Undo();
        Assert.Equal(1, e.CurrentPlayer);
        Assert.Equal(501, e.RemainingOf(1));
        e.Undo();
        Assert.Equal(0, e.CurrentPlayer);
        Assert.Equal(381, e.RemainingOf(0));
        Assert.Equal(2, e.DartsInCurrentTurn);
    }

    [Fact]
    public void FinishTurn_FillsWithMisses()
    {
        var e = Create();
        Throw(e, "T20");
        e.FinishTurn();
        Assert.Equal(1, e.CurrentPlayer);
        Assert.Equal(3, e.Turns[0].Darts.Count);

        e.FinishTurn(); // ohne geworfenen Dart
        Assert.Equal(0, e.CurrentPlayer);
        Assert.Equal(3, e.Turns[1].Darts.Count);
    }

    [Fact]
    public void Average_IsPerThreeDarts()
    {
        var e = Create(players: [new PlayerRef(null, "Solo")]);
        Throw(e, "T20", "T20", "T20", "S20", "S20", "S20");
        var r = e.GetResults()[0];
        Assert.Equal(120, r.Average);
        Assert.Equal(1, r.Count180);
        Assert.Equal(6, r.DartsThrown);
    }
}

public class CheckoutCalculatorTests
{
    [Theory]
    [InlineData(170, "T20 T20 BULL")]
    [InlineData(100, "T20 D20")]
    [InlineData(40, "D20")]
    [InlineData(50, "BULL")]
    public void Suggests_StandardRoutes(int remaining, string expected) =>
        Assert.Equal(expected, CheckoutCalculator.SuggestText(remaining, 3, InOutRule.Double));

    [Theory]
    [InlineData(169)]
    [InlineData(168)]
    [InlineData(166)]
    [InlineData(165)]
    [InlineData(163)]
    [InlineData(162)]
    [InlineData(159)]
    [InlineData(1)]
    public void Bogeys_HaveNoCheckout(int remaining) =>
        Assert.Null(CheckoutCalculator.Suggest(remaining, 3, InOutRule.Double));

    [Fact]
    public void AllSuggestionsAreValid()
    {
        for (var rem = 2; rem <= 170; rem++)
        {
            var path = CheckoutCalculator.Suggest(rem, 3, InOutRule.Double);
            if (path is null) continue;
            Assert.Equal(rem, path.Sum(d => d.Score));
            Assert.True(path[^1].IsDouble);
        }
    }

    [Fact]
    public void RespectsDartsLeft() => Assert.Null(CheckoutCalculator.Suggest(100, 1, InOutRule.Double));
}

public class CricketTests
{
    private static CricketEngine Create(bool cutThroat = false, int players = 2) =>
        new(new GameSettings { Mode = GameMode.Cricket, CutThroat = cutThroat, Rounds = 0 },
            Enumerable.Range(1, players).Select(i => new PlayerRef(null, $"P{i}")).ToList());

    [Fact]
    public void ExtraMarks_ScoreWhileOpponentOpen()
    {
        var e = Create();
        e.Throw(Dart.T(20));
        e.Throw(Dart.T(20));
        Assert.Equal(3, e.MarksOf(0, 20));
        Assert.Equal(60, e.PointsOf(0));
    }

    [Fact]
    public void NoPoints_WhenClosedByAll()
    {
        var e = Create();
        e.Throw(Dart.T(20)); e.FinishTurn();
        e.Throw(Dart.T(20)); e.FinishTurn();
        e.Throw(Dart.T(20));
        Assert.Equal(0, e.PointsOf(0));
        Assert.True(e.IsClosedByAll(20));
    }

    [Fact]
    public void CutThroat_GivesPointsToOpponents()
    {
        var e = Create(cutThroat: true);
        e.Throw(Dart.T(19));
        e.Throw(Dart.S(19));
        Assert.Equal(0, e.PointsOf(0));
        Assert.Equal(19, e.PointsOf(1));
    }

    [Fact]
    public void ClosingAllWithMorePoints_Wins()
    {
        var e = Create(players: 1);
        foreach (var t in new[] { 20, 19, 18, 17, 16, 15 })
        {
            e.Throw(Dart.T(t));
            if (!e.IsFinished && e.DartsInCurrentTurn == 0) continue;
        }
        Assert.False(e.IsFinished);
        e.Throw(Dart.Bullseye);
        e.Throw(Dart.OuterBull);
        Assert.True(e.IsFinished);
        Assert.Equal(0, e.Winner);
    }
}

public class TrainingModeTests
{
    private static readonly PlayerRef[] Solo = [new(null, "Solo")];

    [Fact]
    public void AroundTheClock_AdvancesAndFinishes()
    {
        var e = new AroundTheClockEngine(new GameSettings { Mode = GameMode.AroundTheClock, ClockIncludeBull = false }, Solo);
        for (var i = 1; i <= 20; i++) e.Throw(Dart.S(i));
        Assert.True(e.IsFinished);
        Assert.Equal(20, e.GetResults()[0].DartsThrown);
    }

    [Fact]
    public void AroundTheClock_DoubleOnly_IgnoresSingles()
    {
        var e = new AroundTheClockEngine(new GameSettings { Mode = GameMode.AroundTheClock, ClockHit = AroundTheClockHit.DoubleOnly }, Solo);
        e.Throw(Dart.S(1));
        Assert.Equal(0, e.ProgressOf(0));
        e.Throw(Dart.D(1));
        Assert.Equal(2, e.TargetOf(0));
    }

    [Fact]
    public void AroundTheClock_MultiplierSkips()
    {
        var e = new AroundTheClockEngine(new GameSettings { Mode = GameMode.AroundTheClock, ClockMultiplierSkips = true }, Solo);
        e.Throw(Dart.T(1));
        Assert.Equal(4, e.TargetOf(0));
    }

    [Fact]
    public void Shanghai_WinsInstantly()
    {
        var e = new ShanghaiEngine(new GameSettings { Mode = GameMode.Shanghai, Rounds = 7 }, [new(null, "A"), new(null, "B")]);
        e.Throw(Dart.S(1)); e.Throw(Dart.D(1)); e.Throw(Dart.T(1));
        Assert.True(e.IsFinished);
        Assert.Equal(0, e.Winner);
    }

    [Fact]
    public void Shanghai_EndsAfterRounds()
    {
        var e = new ShanghaiEngine(new GameSettings { Mode = GameMode.Shanghai, Rounds = 2 }, Solo);
        e.Throw(Dart.T(1)); e.FinishTurn();
        Assert.False(e.IsFinished);
        e.Throw(Dart.D(2)); e.FinishTurn();
        Assert.True(e.IsFinished);
        Assert.Equal(7, e.PointsOf(0));
    }

    [Fact]
    public void Bobs27_PenaltyAndElimination()
    {
        var e = new Bobs27Engine(new GameSettings { Mode = GameMode.Bobs27 }, Solo);
        e.FinishTurn(); // D1 verfehlt: 27 - 2
        Assert.Equal(25, e.PointsOf(0));
        e.Throw(Dart.D(2)); e.FinishTurn(); // +4
        Assert.Equal(29, e.PointsOf(0));
        e.FinishTurn(); // -6
        e.FinishTurn(); // -8
        e.FinishTurn(); // -10
        e.FinishTurn(); // -12 → -7
        Assert.True(e.IsFinished);
        Assert.Null(e.Winner);
    }

    [Fact]
    public void Checkout121_SuccessRaisesTarget()
    {
        var e = new Checkout121Engine(GameSettings.DefaultFor(GameMode.Checkout121) with { CheckoutAttempts = 2 }, Solo);
        e.Throw(Dart.T(20)); e.Throw(Dart.S(11)); e.Throw(Dart.Bullseye);
        Assert.Equal(122, e.TargetOf(0));
        Assert.Equal(122, e.RemainingOf(0));

        e.FinishTurn(); e.FinishTurn(); e.FinishTurn();
        Assert.True(e.IsFinished);
        var r = e.GetResults()[0];
        Assert.Equal(1, r.CheckoutsHit);
        Assert.Equal(2, r.CheckoutAttempts);
        Assert.Equal(121, r.HighestCheckout);
    }

    [Fact]
    public void Checkout121_ResetRule()
    {
        var e = new Checkout121Engine(GameSettings.DefaultFor(GameMode.Checkout121) with { CheckoutOnFail = CheckoutFailRule.MinusOne }, Solo);
        e.FinishTurn(); e.FinishTurn(); e.FinishTurn();
        Assert.Equal(120, e.TargetOf(0));
    }

    [Fact]
    public void HighScore_SumsRounds()
    {
        var e = new HighScoreEngine(new GameSettings { Mode = GameMode.HighScore, Rounds = 2 }, [new(null, "A"), new(null, "B")]);
        e.Throw(Dart.T(20)); e.FinishTurn();
        e.FinishTurn();
        e.FinishTurn();
        Assert.False(e.IsFinished);
        e.FinishTurn();
        Assert.True(e.IsFinished);
        Assert.Equal(0, e.Winner);
        Assert.Equal(60, e.PointsOf(0));
    }

    [Fact]
    public void Restore_ReplaysDarts()
    {
        var settings = GameSettings.DefaultFor(GameMode.X01);
        var original = GameModes.Create(settings, Solo);
        foreach (var d in new[] { "T20", "T19", "S5" }) original.Throw(Dart.Parse(d));
        var restored = (X01Engine)GameModes.Restore(settings, Solo, original.DartLog.Select(d => d.ToString()));
        Assert.Equal(((X01Engine)original).RemainingOf(0), restored.RemainingOf(0));
    }
}
