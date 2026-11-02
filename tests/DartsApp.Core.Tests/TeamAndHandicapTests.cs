using DartsApp.Core.Bots;
using DartsApp.Core.Engine;
using DartsApp.Core.Models;

namespace DartsApp.Core.Tests;

public class TeamTests
{
    private static readonly PlayerRef Anna = new(null, "Anna");
    private static readonly PlayerRef Ben = new(null, "Ben");
    private static readonly PlayerRef Cleo = new(null, "Cleo");
    private static readonly PlayerRef Dave = new(null, "Dave");

    [Fact]
    public void Team_NameAndValidation()
    {
        var team = PlayerRef.Team([Anna, Ben]);
        Assert.Equal("Anna & Ben", team.Name);
        Assert.True(team.IsTeam);
        Assert.Throws<ArgumentException>(() => PlayerRef.Team([Anna]));
        Assert.Throws<ArgumentException>(() => PlayerRef.Team([team, Cleo]));
    }

    [Fact]
    public void X01_TeamsShareScore_MembersAlternate()
    {
        PlayerRef[] teams = [PlayerRef.Team([Anna, Ben]), PlayerRef.Team([Cleo, Dave])];
        var e = new X01Engine(GameSettings.DefaultFor(GameMode.X01), teams);

        Assert.Equal("Anna", e.CurrentThrower.Name);
        e.Throw(Dart.T(20)); e.FinishTurn();          // Anna: 60
        Assert.Equal("Cleo", e.CurrentThrower.Name);
        e.FinishTurn();                                // Cleo
        Assert.Equal("Ben", e.CurrentThrower.Name);    // Team 1, zweites Mitglied
        e.Throw(Dart.T(20)); e.FinishTurn();          // Ben: 60
        Assert.Equal(381, e.RemainingOf(0));          // gemeinsamer Rest
        Assert.Equal("Dave", e.CurrentThrower.Name);
        Assert.Equal("Anna", e.ThrowerOf(e.Turns[0]).Name);
        Assert.Equal("Ben", e.ThrowerOf(e.Turns[2]).Name);
    }

    [Fact]
    public void Undo_RestoresThrowerRotation()
    {
        var e = new X01Engine(GameSettings.DefaultFor(GameMode.X01), [PlayerRef.Team([Anna, Ben])]);
        e.FinishTurn();
        Assert.Equal("Ben", e.CurrentThrower.Name);
        e.Undo(); e.Undo(); e.Undo();
        Assert.Equal("Anna", e.CurrentThrower.Name);
    }

    [Fact]
    public void Results_MarkTeams()
    {
        var e = new HighScoreEngine(GameSettings.DefaultFor(GameMode.HighScore) with { Rounds = 1 }, [PlayerRef.Team([Anna, Ben]), Cleo]);
        e.FinishTurn(); e.FinishTurn();
        var results = e.GetResults();
        Assert.Equal(2, results[0].Extra["team"]);
        Assert.False(results[1].Extra.ContainsKey("team"));
    }

    [Fact]
    public void MixedTeam_BotThrowsOnlyForItsTurn()
    {
        var bot = new PlayerRef(null, "Computer", BotLevel.Pro);
        var e = new X01Engine(GameSettings.DefaultFor(GameMode.X01), [PlayerRef.Team([Anna, bot]), Cleo]);
        Assert.False(e.IsBotTurn);
        e.FinishTurn(); // Anna
        e.FinishTurn(); // Cleo
        Assert.True(e.IsBotTurn);
        Bot.PlayUntilHuman(e, new Random(1));
        Assert.Equal(1, e.CurrentPlayer);              // Cleo ist dran
        Assert.Equal("Computer", e.ThrowerOf(e.Turns[^1]).Name);
        Assert.True(e.HasHumanThrower);
    }

    public static TheoryData<GameMode> TeamModes => new(GameModes.All.Select(m => m.Mode));

    [Theory]
    [MemberData(nameof(TeamModes))]
    public void BotTeamsFinishEveryMode(GameMode mode)
    {
        PlayerRef Bot(string name, BotLevel level) => new(null, name, level);
        PlayerRef[] teams =
        [
            PlayerRef.Team([Bot("A1", BotLevel.League), Bot("A2", BotLevel.Casual)]),
            PlayerRef.Team([Bot("B1", BotLevel.Intermediate), Bot("B2", BotLevel.Intermediate)])
        ];
        var engine = GameModes.Create(GameSettings.DefaultFor(mode) with { Seed = 5 }, teams);
        Core.Bots.Bot.PlayUntilHuman(engine, new Random(5), maxDarts: 50_000);
        Assert.True(engine.IsFinished, $"{mode} mit Teams nicht beendet");
        Assert.Contains(engine.Turns, t => t.Member == 1);
    }
}

public class HandicapTests
{
    [Fact]
    public void X01_UsesPlayerStartScore()
    {
        PlayerRef[] players = [new(null, "Profi", StartScore: 501), new(null, "Neuling", StartScore: 301)];
        var e = new X01Engine(GameSettings.DefaultFor(GameMode.X01), players);
        Assert.Equal(501, e.RemainingOf(0));
        Assert.Equal(301, e.RemainingOf(1));
        Assert.Equal(301, e.GetResults()[1].Extra["handicap"]);
    }

    [Fact]
    public void Handicap_AppliesToEveryLeg()
    {
        PlayerRef[] players = [new(null, "A", StartScore: 40), new(null, "B")];
        var e = new X01Engine(GameSettings.DefaultFor(GameMode.X01) with { LegsToWin = 2 }, players);
        e.Throw(Dart.D(20)); // A gewinnt Leg 1
        Assert.Equal(40, e.RemainingOf(0));
        Assert.Equal(501, e.RemainingOf(1));
    }
}

public class SuddenDeathTests
{
    [Fact]
    public void Solo_LosesAfterTurnLimit()
    {
        var e = new X01Engine(GameSettings.DefaultFor(GameMode.SuddenDeath) with { TurnLimit = 2 }, [new(null, "Solo")]);
        e.Throw(Dart.T(20)); e.FinishTurn();
        Assert.False(e.IsFinished);
        e.FinishTurn();
        Assert.True(e.IsFinished);
        Assert.Null(e.Winner);
        Assert.Equal(1, e.GetResults()[0].Extra["timed_out"]);
    }

    [Fact]
    public void Multi_LowestRemainingWinsLeg_AndNextLegStartsCorrectly()
    {
        var e = new X01Engine(GameSettings.DefaultFor(GameMode.SuddenDeath) with { TurnLimit = 1, LegsToWin = 2 }, [new(null, "A"), new(null, "B")]);
        e.Throw(Dart.T(20)); e.FinishTurn(); // A: 441
        e.Throw(Dart.T(19)); e.FinishTurn(); // B: 444 → Limit, Leg an A
        Assert.Equal(1, e.LegsOf(0));
        Assert.Equal(2, e.Leg);
        Assert.Equal(1, e.CurrentPlayer);    // B beginnt Leg 2
        Assert.Equal(501, e.RemainingOf(0));
        e.FinishTurn();                       // B
        Assert.Equal(0, e.CurrentPlayer);
    }
}

public class NewModesRuleTests
{
    private static readonly PlayerRef[] Two = [new(null, "Hase"), new(null, "Hund")];
    private static readonly PlayerRef[] Solo = [new(null, "Solo")];

    [Fact]
    public void HareAndHounds_HoundCatchesHare()
    {
        var e = new HareAndHoundsEngine(GameSettings.DefaultFor(GameMode.HareAndHounds), Two);
        Assert.Equal(20, e.TargetOf(0));
        Assert.Equal(12, e.TargetOf(1));
        e.FinishTurn();                                         // Hase verfehlt
        e.Throw(Dart.S(12)); e.Throw(Dart.S(5));                // Hund: 12, 5 → Position 0 = Hase
        Assert.True(e.IsFinished);
        Assert.Equal(1, e.Winner);
    }

    [Fact]
    public void HareAndHounds_HareEscapes()
    {
        var e = new HareAndHoundsEngine(GameSettings.DefaultFor(GameMode.HareAndHounds), Two);
        foreach (var n in Bots.DartBoard.Order)
        {
            if (e.IsFinished) break;
            if (e.CurrentPlayer == 1) e.FinishTurn();
            e.Throw(Dart.S(n));
        }
        Assert.True(e.IsFinished);
        Assert.Equal(0, e.Winner);
    }

    [Fact]
    public void NineLives_LosesLifeWithoutProgress()
    {
        var e = new NineLivesEngine(GameSettings.DefaultFor(GameMode.NineLives) with { KillerLives = 2 }, Solo);
        e.Throw(Dart.S(1)); e.FinishTurn();
        Assert.Equal(2, e.LivesOf(0));
        e.FinishTurn();
        Assert.Equal(1, e.LivesOf(0));
        e.FinishTurn();
        Assert.True(e.IsFinished);
    }

    [Fact]
    public void AllFives_OnlyMultiplesOfFiveScore()
    {
        var e = new AllFivesEngine(GameSettings.DefaultFor(GameMode.AllFives) with { FivesTarget = 10 }, Solo);
        e.Throw(Dart.S(20)); e.Throw(Dart.S(20)); e.Throw(Dart.S(5)); // 45 → 9
        Assert.Equal(9, e.PointsOf(0));
        e.Throw(Dart.S(20)); e.Throw(Dart.S(1)); e.Throw(Dart.Miss);   // 21 → 0
        Assert.Equal(9, e.PointsOf(0));
        e.Throw(Dart.S(10)); e.Throw(Dart.Miss); e.Throw(Dart.Miss);    // 10 → 2 → 11 > 10 → Bust
        Assert.Equal(9, e.PointsOf(0));
        e.Throw(Dart.S(5)); e.FinishTurn();                             // 5 → 1 → 10
        Assert.True(e.IsFinished);
    }

    [Fact]
    public void Prisoner_FreeingMovesBoth()
    {
        var e = new PrisonerEngine(GameSettings.DefaultFor(GameMode.Prisoner), [new(null, "A"), new(null, "B")]);
        e.Throw(Dart.S(1)); e.Throw(Dart.S(2)); e.Throw(Dart.Miss);     // A: 2 Felder, Gefangener auf der 3
        Assert.Equal(2, e.ProgressOf(0));
        Assert.Equal([0], e.PrisonersOn(3));
        e.Throw(Dart.S(3));                                             // B befreit auf der 3
        Assert.Equal(1, e.ProgressOf(0));
        Assert.Equal(1, e.ProgressOf(1));
        Assert.Empty(e.PrisonersOn(3));
    }

    [Fact]
    public void DoublesLadder_CountsDoublesPerRound()
    {
        var e = new DoublesLadderEngine(GameSettings.DefaultFor(GameMode.DoublesLadder), Solo);
        e.Throw(Dart.D(1)); e.Throw(Dart.S(1)); e.Throw(Dart.D(1));
        e.Throw(Dart.D(1)); e.FinishTurn();                             // D1 in Runde 2 zählt nicht
        Assert.Equal(2, e.HitsOf(0));
        for (var i = 0; i < 19; i++) e.FinishTurn();
        Assert.True(e.IsFinished);
        Assert.Equal(2, e.GetResults()[0].Extra["d1"]);
    }

    [Fact]
    public void BigFinish_TargetsBetween100And170()
    {
        var settings = GameSettings.DefaultFor(GameMode.BigFinish) with { Seed = 4 };
        Assert.All(CheckoutTrainingEngine.RouletteTargets(settings), t => Assert.InRange(t, 100, 170));
        var e = GameModes.Create(settings, Solo);
        Assert.IsType<CheckoutTrainingEngine>(e);
    }
}
