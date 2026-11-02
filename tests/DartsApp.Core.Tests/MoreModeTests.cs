using DartsApp.Core.Bots;
using DartsApp.Core.Engine;
using DartsApp.Core.Models;

namespace DartsApp.Core.Tests;

public class CricketVariantTests
{
    private static readonly PlayerRef[] Two = [new(null, "A"), new(null, "B")];

    private static CricketEngine Create(CricketVariant variant, int seed = 1) =>
        new(GameSettings.DefaultFor(GameMode.Cricket) with { CricketVariant = variant, Rounds = 0, Seed = seed }, Two);

    [Fact]
    public void Tactics_Uses10To20()
    {
        var e = Create(CricketVariant.Tactics);
        Assert.Equal([20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 25], e.Targets.Select(t => t.Key));
        e.Throw(Dart.T(10));
        Assert.Equal(3, e.MarksOf(0, 10));
    }

    [Fact]
    public void Random_UsesSixSeededNumbers()
    {
        var a = Create(CricketVariant.Random, seed: 42);
        var b = Create(CricketVariant.Random, seed: 42);
        Assert.Equal(7, a.Targets.Count);
        Assert.Equal(a.Targets.Select(t => t.Key), b.Targets.Select(t => t.Key));
        Assert.Equal(CricketEngine.RandomNumbers(42).Append(25), a.Targets.Select(t => t.Key));
    }

    [Fact]
    public void MickeyMouse_DoubleCountsForNumberFirst_ThenForDoubles()
    {
        var e = Create(CricketVariant.MickeyMouse);
        Assert.Contains(e.Targets, t => t.Key == CricketEngine.DoublesKey);
        Assert.Contains(e.Targets, t => t.Key == CricketEngine.TriplesKey);

        e.Throw(Dart.D(20)); // 20 noch offen → zählt auf die 20
        Assert.Equal(2, e.MarksOf(0, 20));
        Assert.Equal(0, e.MarksOf(0, CricketEngine.DoublesKey));

        e.Throw(Dart.D(5)); // 5 ist kein Ziel → zählt auf "Doppel"
        Assert.Equal(1, e.MarksOf(0, CricketEngine.DoublesKey));
    }
}

public class VariantBotTests
{
    public static TheoryData<GameSettings> Variants => new(
        GameSettings.DefaultFor(GameMode.Cricket) with { CricketVariant = CricketVariant.Tactics },
        GameSettings.DefaultFor(GameMode.Cricket) with { CricketVariant = CricketVariant.Random },
        GameSettings.DefaultFor(GameMode.Cricket) with { CricketVariant = CricketVariant.MickeyMouse, CutThroat = true },
        GameSettings.DefaultFor(GameMode.HalveIt) with { HalveItVariant = HalveItVariant.Bermuda },
        GameSettings.DefaultFor(GameMode.CheckoutRoulette) with { RouletteMin = 2, RouletteMax = 40 });

    [Theory]
    [MemberData(nameof(Variants))]
    public void BotsFinishVariants(GameSettings settings)
    {
        var random = new Random(11);
        for (var game = 0; game < 10; game++)
        {
            var engine = GameModes.Create(settings with { Seed = game + 1 },
                [new(null, "A", BotLevel.Casual), new(null, "B", BotLevel.Pro)]);
            Bot.PlayUntilHuman(engine, random, maxDarts: 50_000);
            Assert.True(engine.IsFinished, $"{settings.Describe()}: Spiel {game} nicht beendet");
        }
    }
}

public class HalveItVariantTests
{
    [Fact]
    public void Bermuda_Has13RoundsAndStartsAtZero()
    {
        var e = new HalveItEngine(GameSettings.DefaultFor(GameMode.HalveIt) with { HalveItVariant = HalveItVariant.Bermuda }, [new(null, "Solo")]);
        Assert.Equal(13, e.TotalRounds);
        Assert.Equal(0, e.PointsOf(0));
        e.Throw(Dart.T(12)); e.FinishTurn();
        Assert.Equal(36, e.PointsOf(0));
        for (var i = 0; i < 10; i++) e.FinishTurn();
        Assert.Equal("25", e.TargetLabel);
        e.Throw(Dart.Bullseye); e.FinishTurn(); // 50 zählt nicht auf "25" → halbiert
        Assert.Equal("Bull (50)", e.TargetLabel);
    }
}

public class CheckoutDrillTests
{
    private static readonly PlayerRef[] Solo = [new(null, "Solo")];

    [Fact]
    public void Roulette_TargetsAreSeededAndFinishable()
    {
        var settings = GameSettings.DefaultFor(GameMode.CheckoutRoulette) with { Seed = 7, RouletteMin = 100, RouletteMax = 170 };
        var targets = CheckoutTrainingEngine.RouletteTargets(settings);
        Assert.Equal(10, targets.Length);
        Assert.Equal(targets, CheckoutTrainingEngine.RouletteTargets(settings));
        Assert.All(targets, t =>
        {
            Assert.InRange(t, 100, 170);
            Assert.NotNull(CheckoutCalculator.Suggest(t, 3, InOutRule.Double));
        });

        var e = new CheckoutTrainingEngine(settings, Solo);
        Assert.Equal(targets[0], e.TargetOf(0));
        e.FinishTurn(); // eine Aufnahme pro Versuch
        Assert.Equal(targets[1], e.TargetOf(0));
    }

    [Fact]
    public void Catch40_ScoresByDartsUsed()
    {
        var e = new CheckoutTrainingEngine(GameSettings.DefaultFor(GameMode.Catch40), Solo);
        Assert.Equal(61, e.TargetOf(0));
        e.Throw(Dart.S(21 - 10)); e.Throw(Dart.Bullseye); // 11 + 50 = 61 mit 2 Darts → 3 Punkte
        Assert.Equal(3, e.PointsOf(0));
        Assert.Equal(62, e.TargetOf(0));

        e.FinishTurn();                                   // 3 Darts verbraucht
        e.Throw(Dart.T(10)); e.Throw(Dart.D(16));         // 30 + 32 = 62 mit 5 Darts → 1 Punkt
        Assert.Equal(4, e.PointsOf(0));

        e.FinishTurn(); e.FinishTurn();                   // 63 verpasst → 0 Punkte
        Assert.Equal(64, e.TargetOf(0));
        Assert.Equal(4, e.PointsOf(0));
    }
}

public class GolfTests
{
    [Fact]
    public void LastHitCounts_AndMissIsFive()
    {
        var e = new GolfEngine(GameSettings.DefaultFor(GameMode.Golf) with { Rounds = 3 }, [new(null, "Solo")]);
        e.Throw(Dart.T(1)); e.Throw(Dart.S(1)); e.FinishTurn(); // letzter Treffer Single = 3
        Assert.Equal(3, e.StrokesOf(0));
        e.Throw(Dart.T(2)); e.FinishTurn();                     // stehen geblieben = 1
        Assert.Equal(4, e.StrokesOf(0));
        e.FinishTurn();                                         // kein Treffer = 5
        Assert.Equal(9, e.StrokesOf(0));
        Assert.True(e.IsFinished);
    }

    [Fact]
    public void FewestStrokesWin()
    {
        var e = new GolfEngine(GameSettings.DefaultFor(GameMode.Golf) with { Rounds = 1 }, [new(null, "A"), new(null, "B")]);
        e.Throw(Dart.D(1)); e.FinishTurn();
        e.Throw(Dart.T(1)); e.FinishTurn();
        Assert.True(e.IsFinished);
        Assert.Equal(1, e.Winner);
    }

    [Fact]
    public void Bot_StandsOnDoubleOrTriple()
    {
        var e = new GolfEngine(GameSettings.DefaultFor(GameMode.Golf), [new(null, "Bot", BotLevel.Pro)]);
        Assert.False(e.BotWantsToEndTurn);
        e.Throw(Dart.S(1));
        Assert.False(e.BotWantsToEndTurn);
        e.Throw(Dart.D(1));
        Assert.True(e.BotWantsToEndTurn);
    }
}

public class ScramTests
{
    [Fact]
    public void StopperClosesScorerScores_ThenRolesSwap()
    {
        var e = new ScramEngine(GameSettings.DefaultFor(GameMode.Scram), [new(null, "A"), new(null, "B")]);
        Assert.Equal(0, e.Stopper);
        e.Throw(Dart.S(20)); e.FinishTurn();   // A schließt 20
        Assert.True(e.IsClosed(20));
        e.Throw(Dart.T(20)); e.Throw(Dart.T(19)); e.FinishTurn(); // B: 20 zu, T19 = 57
        Assert.Equal(57, e.PointsOf(1));

        // A schließt alle übrigen Felder
        foreach (var f in ScramEngine.Fields.Where(f => !e.IsClosed(f)).ToList())
        {
            if (e.CurrentPlayer == 1) e.FinishTurn();
            e.Throw(f == Dart.BullSegment ? Dart.OuterBull : Dart.S(f));
        }

        Assert.Equal(2, e.Half);
        Assert.Equal(1, e.Stopper);
        Assert.Equal(1, e.CurrentPlayer); // neuer Stopper beginnt
        Assert.False(e.IsClosed(20));
    }

    [Fact]
    public void RequiresExactlyTwoPlayers() =>
        Assert.Throws<ArgumentException>(() => new ScramEngine(GameSettings.DefaultFor(GameMode.Scram), [new(null, "A")]));
}

public class TicTacToeTests
{
    [Fact]
    public void ThreeMarksClaim_LineWins()
    {
        var settings = GameSettings.DefaultFor(GameMode.TicTacToe) with { Seed = 3 };
        var e = new TicTacToeEngine(settings, [new(null, "A"), new(null, "B")]);
        Assert.Equal(Dart.BullSegment, e.Cells[4]);
        Assert.Equal(9, e.Cells.Distinct().Count());

        Dart Claim(int cell) => e.Cells[cell] == Dart.BullSegment ? Dart.Bullseye : Dart.T(e.Cells[cell]);

        e.Throw(Claim(0)); e.FinishTurn();          // A belegt Feld 0
        Assert.Equal(0, e.OwnerOf(0));
        e.FinishTurn();                              // B
        e.Throw(Claim(1)); e.FinishTurn();          // A belegt Feld 1
        e.FinishTurn();                              // B
        e.Throw(Dart.S(e.Cells[2]));                 // 1 Mark – noch nicht belegt
        Assert.Null(e.OwnerOf(2));
        e.Throw(Dart.D(e.Cells[2]));                 // 3 Marks → Reihe 0-1-2
        Assert.True(e.IsFinished);
        Assert.Equal(0, e.Winner);
        Assert.Equal([0, 1, 2], e.WinningLine);
    }
}

public class FootballTests
{
    [Fact]
    public void BullGivesPossession_DoublesScore()
    {
        var e = new FootballEngine(GameSettings.DefaultFor(GameMode.Football) with { FootballGoals = 2 }, [new(null, "A"), new(null, "B")]);
        e.Throw(Dart.D(20));                  // ohne Ball kein Tor
        Assert.Equal(0, e.GoalsOf(0));
        e.Throw(Dart.OuterBull);              // A hat den Ball
        Assert.Equal(0, e.Possession);
        e.Throw(Dart.D(5));                   // Tor
        Assert.Equal(1, e.GoalsOf(0));
        e.Throw(Dart.Bullseye);               // B erobert den Ball
        Assert.Equal(1, e.Possession);
        e.FinishTurn();
        e.Throw(Dart.D(1));                   // A ohne Ball
        Assert.Equal(1, e.GoalsOf(0));
    }
}

public class JdcChallengeTests
{
    [Fact]
    public void Sections_ScoreAsDefined()
    {
        var e = new JdcChallengeEngine(GameSettings.DefaultFor(GameMode.JdcChallenge), [new(null, "Solo")]);
        e.Throw(Dart.S(10)); e.Throw(Dart.D(10)); e.Throw(Dart.T(10)); // Shanghai auf 10: 60 + 100
        Assert.Equal(160, e.PointsOf(0));

        for (var i = 0; i < 5; i++) e.FinishTurn(); // 11-15
        e.Throw(Dart.D(1)); e.Throw(Dart.D(3)); e.Throw(Dart.D(3)); // Doppelrunde 1: D1 ✓, D2 ✗, D3 ✓
        Assert.Equal(260, e.PointsOf(0));

        for (var i = 0; i < 5; i++) e.FinishTurn(); // Doppelrunden 2-6
        e.Throw(Dart.Bullseye);                   // Runde 13: D19, D20, Bull – Bull auf D19 zählt nicht …
        e.Throw(Dart.Miss);
        Assert.Equal(260, e.PointsOf(0));
        e.Throw(Dart.Bullseye);                   // … erst der dritte Dart zielt aufs Bull ✓
        Assert.Equal(310, e.PointsOf(0));

        for (var i = 0; i < 6; i++) e.FinishTurn(); // 15-20
        Assert.True(e.IsFinished);
    }
}

public class DrillTests
{
    [Fact]
    public void ScoringDrill_CountsExactHits()
    {
        var e = new ScoringDrillEngine(GameSettings.DefaultFor(GameMode.ScoringDrill) with { Rounds = 1, DrillTarget = "T19" }, [new(null, "Solo")]);
        e.Throw(Dart.T(19)); e.Throw(Dart.S(19)); e.Throw(Dart.T(20));
        Assert.True(e.IsFinished);
        var r = e.GetResults()[0];
        Assert.Equal(1, r.Score);
        Assert.Equal(Math.Round(2 / 3.0, 4), r.Extra["segment_rate"]);
    }

    [Fact]
    public void BullTraining_ScoresOneOrTwo()
    {
        var e = new ScoringDrillEngine(GameSettings.DefaultFor(GameMode.BullTraining) with { Rounds = 1 }, [new(null, "Solo")]);
        e.Throw(Dart.OuterBull); e.Throw(Dart.Bullseye); e.Throw(Dart.S(20));
        Assert.Equal(3, e.ScoreOf(0));
    }

    [Fact]
    public void InvalidTarget_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScoringDrillEngine(GameSettings.DefaultFor(GameMode.ScoringDrill) with { DrillTarget = "X" }, [new(null, "Solo")]));
}
