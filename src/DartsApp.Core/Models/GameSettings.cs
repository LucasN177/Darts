namespace DartsApp.Core.Models;

public enum GameMode
{
    X01,
    Cricket,
    AroundTheClock,
    Shanghai,
    Bobs27,
    Checkout121,
    HighScore,
    Killer,
    HalveIt,
    Baseball,
    Gotcha,
    Golf,
    Scram,
    TicTacToe,
    Football,
    CheckoutRoulette,
    Catch40,
    JdcChallenge,
    ScoringDrill,
    BullTraining
}

/// <summary>Regel für den Einstieg (X01) bzw. Ausstieg.</summary>
public enum InOutRule
{
    /// <summary>Jedes Feld zählt.</summary>
    Single,
    /// <summary>Nur Doppel (inkl. Bullseye).</summary>
    Double,
    /// <summary>Doppel oder Triple.</summary>
    Master
}

public enum AroundTheClockHit
{
    /// <summary>Jedes Feld der Zahl zählt.</summary>
    Any,
    /// <summary>Nur das Doppelfeld zählt.</summary>
    DoubleOnly,
    /// <summary>Nur das Triplefeld zählt.</summary>
    TripleOnly
}

public enum CheckoutFailRule
{
    /// <summary>Gleiches Ziel erneut versuchen.</summary>
    Stay,
    /// <summary>Ziel sinkt um eins.</summary>
    MinusOne,
    /// <summary>Zurück auf den Startwert.</summary>
    Reset
}

public enum CricketVariant
{
    /// <summary>15 bis 20 und Bull.</summary>
    Standard,
    /// <summary>10 bis 20 und Bull.</summary>
    Tactics,
    /// <summary>Sechs zufällige Zahlen und Bull.</summary>
    Random,
    /// <summary>12 bis 20, beliebiges Doppel, beliebiges Triple und Bull.</summary>
    MickeyMouse
}

public enum HalveItVariant
{
    /// <summary>15, 16, D, 17, 18, T, 19, 20, Bull – Start mit 40 Punkten.</summary>
    Classic,
    /// <summary>Bermuda Triangle: 12, 13, 14, D, 15, 16, 17, T, 18, 19, 20, 25, 50 – Start mit 0 Punkten.</summary>
    Bermuda
}

/// <summary>
/// Alle Einstellungen eines Spiels. Nicht jede Eigenschaft ist für jeden Modus relevant.
/// </summary>
public sealed record GameSettings
{
    public GameMode Mode { get; init; } = GameMode.X01;

    // X01
    public int StartScore { get; init; } = 501;
    public InOutRule InRule { get; init; } = InOutRule.Single;
    public InOutRule OutRule { get; init; } = InOutRule.Double;
    /// <summary>Legs, die zum Gewinn eines Sets (bzw. des Spiels ohne Sets) nötig sind ("First to").</summary>
    public int LegsToWin { get; init; } = 1;
    /// <summary>Sets, die zum Gewinn des Spiels nötig sind. 1 = ohne Sets.</summary>
    public int SetsToWin { get; init; } = 1;

    // Cricket
    public bool CutThroat { get; init; }
    public CricketVariant CricketVariant { get; init; } = CricketVariant.Standard;

    // Around the Clock
    public AroundTheClockHit ClockHit { get; init; } = AroundTheClockHit.Any;
    public bool ClockIncludeBull { get; init; } = true;
    /// <summary>Double/Triple überspringen entsprechend viele Ziele (nur bei "Any").</summary>
    public bool ClockMultiplierSkips { get; init; }

    // Shanghai / HighScore / Baseball / Golf / Drills / Cricket-Rundenlimit
    /// <summary>Anzahl Runden (bzw. Löcher bei Golf). Bei Cricket 0 = unbegrenzt.</summary>
    public int Rounds { get; init; } = 7;

    // Bob's 27
    public bool Bobs27Elimination { get; init; } = true;

    // 121 Checkout / Checkout Roulette
    public int CheckoutStart { get; init; } = 121;
    public int CheckoutTurnsPerAttempt { get; init; } = 3;
    public int CheckoutAttempts { get; init; } = 10;
    public CheckoutFailRule CheckoutOnFail { get; init; } = CheckoutFailRule.Stay;
    /// <summary>Bereich der zufälligen Checkouts bei Checkout Roulette.</summary>
    public int RouletteMin { get; init; } = 2;
    public int RouletteMax { get; init; } = 170;

    // Killer
    public int KillerLives { get; init; } = 3;

    // Halve-It
    public HalveItVariant HalveItVariant { get; init; } = HalveItVariant.Classic;

    // Gotcha
    public int GotchaTarget { get; init; } = 301;

    // Football
    public int FootballGoals { get; init; } = 10;

    // Scoring-Drill: Zielfeld in Kurzschreibweise ("T20", "T19", "D16", "BULL", ...)
    public string DrillTarget { get; init; } = "T20";

    /// <summary>
    /// Zufallswert, der beim Spielstart gesetzt wird (z.B. für die Zahlenvergabe bei Killer).
    /// Er ist Teil der Einstellungen, damit das Nachspielen eines Spiels dasselbe Ergebnis liefert.
    /// </summary>
    public int Seed { get; init; }

    public static GameSettings DefaultFor(GameMode mode) => mode switch
    {
        GameMode.X01 => new GameSettings { Mode = mode },
        GameMode.Cricket => new GameSettings { Mode = mode, Rounds = 20 },
        GameMode.AroundTheClock => new GameSettings { Mode = mode },
        GameMode.Shanghai => new GameSettings { Mode = mode, Rounds = 7 },
        GameMode.Bobs27 => new GameSettings { Mode = mode },
        GameMode.Checkout121 => new GameSettings { Mode = mode, OutRule = InOutRule.Double },
        GameMode.HighScore => new GameSettings { Mode = mode, Rounds = 10 },
        GameMode.Killer => new GameSettings { Mode = mode },
        GameMode.HalveIt => new GameSettings { Mode = mode },
        GameMode.Baseball => new GameSettings { Mode = mode, Rounds = 9 },
        GameMode.Gotcha => new GameSettings { Mode = mode },
        GameMode.Golf => new GameSettings { Mode = mode, Rounds = 9 },
        GameMode.Scram => new GameSettings { Mode = mode },
        GameMode.TicTacToe => new GameSettings { Mode = mode },
        GameMode.Football => new GameSettings { Mode = mode },
        GameMode.CheckoutRoulette => new GameSettings { Mode = mode, CheckoutAttempts = 10, CheckoutTurnsPerAttempt = 1, OutRule = InOutRule.Double },
        GameMode.Catch40 => new GameSettings { Mode = mode, OutRule = InOutRule.Double },
        GameMode.JdcChallenge => new GameSettings { Mode = mode },
        GameMode.ScoringDrill => new GameSettings { Mode = mode, Rounds = 33 },
        GameMode.BullTraining => new GameSettings { Mode = mode, Rounds = 10, DrillTarget = "BULL" },
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    /// <summary>Kurzbeschreibung der Einstellungen, z.B. "501 · Double Out · First to 3 Legs".</summary>
    public string Describe() => Mode switch
    {
        GameMode.X01 => string.Join(" · ", new[]
        {
            StartScore.ToString(),
            InRule == InOutRule.Single ? null : $"{InRule} In",
            $"{OutRule} Out",
            SetsToWin > 1 ? $"First to {SetsToWin} Sets ({LegsToWin} Legs)" : LegsToWin > 1 ? $"First to {LegsToWin} Legs" : null
        }.Where(s => s is not null)),
        GameMode.Cricket => string.Join(" · ", new[]
        {
            CricketVariant switch
            {
                CricketVariant.Tactics => "Tactics",
                CricketVariant.Random => "Random",
                CricketVariant.MickeyMouse => "Mickey Mouse",
                _ => null
            },
            CutThroat ? "Cut Throat" : "Standard",
            Rounds > 0 ? $"{Rounds} Runden" : null
        }.Where(s => s is not null)),
        GameMode.AroundTheClock => (ClockHit switch
        {
            AroundTheClockHit.DoubleOnly => "Nur Doppel",
            AroundTheClockHit.TripleOnly => "Nur Triple",
            _ => "Alle Felder"
        }) + (ClockIncludeBull ? " · mit Bull" : "") + (ClockMultiplierSkips && ClockHit == AroundTheClockHit.Any ? " · Multiplikator springt" : ""),
        GameMode.Shanghai => $"{Rounds} Runden",
        GameMode.Bobs27 => Bobs27Elimination ? "Mit Ausscheiden" : "Ohne Ausscheiden",
        GameMode.Checkout121 => $"Start {CheckoutStart} · {CheckoutTurnsPerAttempt * 3} Darts · {CheckoutAttempts} Versuche · {OutRule} Out",
        GameMode.HighScore => $"{Rounds} Runden",
        GameMode.Killer => $"{KillerLives} Leben",
        GameMode.HalveIt => HalveItVariant == HalveItVariant.Bermuda
            ? "Bermuda Triangle · 13 Runden"
            : "15 · 16 · D · 17 · 18 · T · 19 · 20 · Bull",
        GameMode.Baseball => $"{Rounds} Innings",
        GameMode.Gotcha => $"Ziel {GotchaTarget}",
        GameMode.Golf => $"{Rounds} Löcher",
        GameMode.Scram => "2 Hälften · 1 bis 20 und Bull",
        GameMode.TicTacToe => "3 Marks pro Feld",
        GameMode.Football => $"{FootballGoals} Tore",
        GameMode.CheckoutRoulette => $"{RouletteMin}–{RouletteMax} · {CheckoutTurnsPerAttempt * 3} Darts · {CheckoutAttempts} Versuche · {OutRule} Out",
        GameMode.Catch40 => $"61–100 · 6 Darts · {OutRule} Out",
        GameMode.JdcChallenge => "Shanghai 10–15 · Doppel · Shanghai 15–20",
        GameMode.ScoringDrill => $"{Rounds * 3} Darts auf {DrillTarget}",
        GameMode.BullTraining => $"{Rounds * 3} Darts auf Bull",
        _ => ""
    };
}
