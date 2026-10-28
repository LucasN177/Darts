using DartsApp.Core.Bots;
using DartsApp.Core.Engine;
using DartsApp.Core.Models;
using Xunit.Abstractions;

namespace DartsApp.Core.Tests;

public class DartBoardTests
{
    [Theory]
    [InlineData("T20")]
    [InlineData("D16")]
    [InlineData("S5")]
    [InlineData("BULL")]
    [InlineData("25")]
    [InlineData("T19")]
    [InlineData("D1")]
    public void AimPoint_LiesInsideTheField(string field)
    {
        var dart = Dart.Parse(field);
        var (x, y) = DartBoard.AimPoint(dart);
        Assert.Equal(dart, DartBoard.FieldAt(x, y));
    }

    [Fact]
    public void OutsideTheBoard_IsMiss() => Assert.Equal(Dart.Miss, DartBoard.FieldAt(0, 200));

    [Fact]
    public void ZeroSigma_AlwaysHits()
    {
        var random = new Random(1);
        for (var i = 0; i < 100; i++) Assert.Equal(Dart.T(20), DartBoard.Throw(Dart.T(20), 0, random));
    }
}

public class BotTests(ITestOutputHelper output)
{
    /// <summary>3-Dart-Average eines Bots über viele Solo-Legs 501 Double Out.</summary>
    private static double MeasureAverage(BotLevel level, int legs, int seed)
    {
        var random = new Random(seed);
        double points = 0, darts = 0;
        for (var i = 0; i < legs; i++)
        {
            var engine = new X01Engine(GameSettings.DefaultFor(GameMode.X01), [new PlayerRef(null, "Bot", level)]);
            Bot.PlayUntilHuman(engine, random);
            Assert.True(engine.IsFinished);
            var r = engine.GetResults()[0];
            points += r.Score;
            darts += r.DartsThrown;
        }
        return points * 3 / darts;
    }

    [Fact]
    public void Levels_MatchTheirApproximateAverage()
    {
        double previous = 0;
        foreach (var level in BotLevels.All)
        {
            var average = MeasureAverage(level, 150, 42);
            output.WriteLine($"{level,-14} sigma={level.Sigma(),5} avg={average:0.0} (Ziel {level.ApproxAverage()})");
            Assert.True(average > previous, $"{level} sollte stärker sein als die Stufe davor.");
            Assert.InRange(average, level.ApproxAverage() * 0.8, level.ApproxAverage() * 1.2);
            previous = average;
        }
    }

    public static TheoryData<GameMode> AllModes => new(GameModes.All.Select(m => m.Mode));

    [Theory]
    [MemberData(nameof(AllModes))]
    public void BotsFinishEveryMode(GameMode mode)
    {
        var random = new Random(7);
        for (var game = 0; game < 20; game++)
        {
            var settings = GameSettings.DefaultFor(mode) with { Seed = game + 1 };
            PlayerRef[] players = [new(null, "A", BotLevel.Intermediate), new(null, "B", BotLevel.League), new(null, "C", BotLevel.Beginner)];
            var engine = GameModes.Create(settings, players);
            Bot.PlayUntilHuman(engine, random, maxDarts: 50_000);

            Assert.True(engine.IsFinished, $"{mode}: Spiel {game} nicht beendet");
            var results = engine.GetResults();
            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.Equal(1, r.Extra["bot"] > 0 ? 1 : 0));

            // Nachspielen ergibt denselben Endstand.
            var replay = GameModes.Restore(settings, players, engine.DartLog.Select(d => d.ToString()));
            Assert.Equal(engine.Winner, replay.Winner);
            Assert.Equal(results.Select(r => r.Score), replay.GetResults().Select(r => r.Score));
        }
    }

    [Fact]
    public void BotStopsAtHumanTurn()
    {
        var engine = new X01Engine(GameSettings.DefaultFor(GameMode.X01), [new PlayerRef(null, "Bot", BotLevel.Pro), new PlayerRef(null, "Mensch")]);
        Bot.PlayUntilHuman(engine, new Random(3));
        Assert.Equal(1, engine.CurrentPlayer);
        Assert.Single(engine.Turns);
    }

    [Theory]
    [InlineData(170, "T20")]
    [InlineData(40, "D20")]
    [InlineData(501, "T20")]
    [InlineData(169, "T20")]
    public void BestDart_UsesCheckoutOrSetup(int remaining, string expected) =>
        Assert.Equal(Dart.Parse(expected), CheckoutCalculator.BestDart(remaining, 3, InOutRule.Double));

    [Fact]
    public void BestDart_NeverBustsOnSetup()
    {
        for (var rem = 2; rem <= 501; rem++)
        for (var left = 1; left <= 3; left++)
        {
            var dart = CheckoutCalculator.BestDart(rem, left, InOutRule.Double);
            var after = rem - dart.Score;
            Assert.True(after == 0 ? dart.IsDouble : after >= 2, $"Rest {rem}, {left} Darts: {dart}");
        }
    }
}

public class NewModeTests
{
    private static readonly PlayerRef[] Two = [new(null, "A"), new(null, "B")];

    [Fact]
    public void Killer_BecomeKillerThenEliminate()
    {
        var e = new KillerEngine(GameSettings.DefaultFor(GameMode.Killer) with { KillerLives = 1, Seed = 5 }, Two);
        var a = e.NumberOf(0);
        var b = e.NumberOf(1);
        Assert.NotEqual(a, b);

        e.Throw(Dart.D(b)); // A ist noch kein Killer: zählt nicht
        Assert.Equal(1, e.LivesOf(1));
        e.Throw(Dart.D(a)); // A wird Killer
        Assert.True(e.IsKiller(0));
        e.Throw(Dart.D(b)); // B eliminiert
        Assert.True(e.IsFinished);
        Assert.Equal(0, e.Winner);
    }

    [Fact]
    public void Killer_OwnDoubleAsKillerCostsLife()
    {
        var e = new KillerEngine(GameSettings.DefaultFor(GameMode.Killer) with { Seed = 9 }, Two);
        var a = e.NumberOf(0);
        e.Throw(Dart.D(a));
        e.Throw(Dart.D(a));
        Assert.Equal(2, e.LivesOf(0));
    }

    [Fact]
    public void Killer_RequiresTwoPlayers() =>
        Assert.Throws<ArgumentException>(() => new KillerEngine(GameSettings.DefaultFor(GameMode.Killer), [new(null, "Solo")]));

    [Fact]
    public void HalveIt_HalvesOnMiss()
    {
        var e = new HalveItEngine(GameSettings.DefaultFor(GameMode.HalveIt), [new(null, "Solo")]);
        e.Throw(Dart.T(15)); e.FinishTurn(); // 40 + 45
        Assert.Equal(85, e.PointsOf(0));
        e.FinishTurn(); // 16 verfehlt
        Assert.Equal(42, e.PointsOf(0));
        e.Throw(Dart.D(3)); e.FinishTurn(); // beliebiges Doppel
        Assert.Equal(48, e.PointsOf(0));
    }

    [Fact]
    public void Baseball_CountsMultiplierAsRuns()
    {
        var e = new BaseballEngine(GameSettings.DefaultFor(GameMode.Baseball) with { Rounds = 2 }, [new(null, "Solo")]);
        e.Throw(Dart.T(1)); e.Throw(Dart.S(1)); e.Throw(Dart.S(2));
        Assert.Equal(4, e.RunsOf(0));
        e.Throw(Dart.D(2)); e.FinishTurn();
        Assert.True(e.IsFinished);
        Assert.Equal(6, e.RunsOf(0));
    }

    [Fact]
    public void Gotcha_KnocksOpponentBackAndBusts()
    {
        var e = new GotchaEngine(GameSettings.DefaultFor(GameMode.Gotcha) with { GotchaTarget = 100 }, Two);
        e.Throw(Dart.T(20)); e.FinishTurn();       // A: 60
        e.Throw(Dart.T(20));                        // B: 60 → A auf 0
        Assert.Equal(0, e.ScoreOf(0));
        e.FinishTurn();
        e.Throw(Dart.T(20)); e.FinishTurn();        // A: 60 → B auf 0
        Assert.Equal(0, e.ScoreOf(1));
        e.Throw(Dart.Bullseye); e.Throw(Dart.T(20)); // B: 50, dann 110 > 100 → Bust, ganze Aufnahme zählt nicht
        Assert.Equal(0, e.ScoreOf(1));
        Assert.True(e.Turns[^1].Bust);
        e.Throw(Dart.D(20));                        // A: 60 + 40 = 100
        Assert.True(e.IsFinished);
        Assert.Equal(0, e.Winner);
    }
}
