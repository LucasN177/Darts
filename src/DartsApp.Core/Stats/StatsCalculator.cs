using DartsApp.Core.Models;

namespace DartsApp.Core.Stats;

/// <summary>Aggregierte Kennzahlen eines Spielers in einem Spielmodus.</summary>
public sealed record ModeStats
{
    public GameMode Mode { get; init; }
    public int Games { get; init; }
    public int Wins { get; init; }
    public int DartsThrown { get; init; }
    /// <summary>Über alle Darts gewichteter Durchschnitt (X01/High Score: 3-Dart-Average, Cricket: MPR).</summary>
    public double Average { get; init; }
    public double BestAverage { get; init; }
    public double? First9Average { get; init; }
    public int HighestTurn { get; init; }
    public int HighestCheckout { get; init; }
    public int CheckoutAttempts { get; init; }
    public int CheckoutsHit { get; init; }
    public int Count180 { get; init; }
    public int Count140 { get; init; }
    public int Count100 { get; init; }
    public int BestScore { get; init; }
    public double AverageScore { get; init; }
    public double? HitRate { get; init; }
    /// <summary>Wenigste Darts für ein gewonnenes Spiel (z.B. bestes Leg bei 1 Leg oder Around the Clock).</summary>
    public int? FewestDartsWin { get; init; }

    public double WinRate => Games == 0 ? 0 : (double)Wins / Games;
    public double? CheckoutRate => CheckoutAttempts == 0 ? null : (double)CheckoutsHit / CheckoutAttempts;
}

/// <summary>Ein Datenpunkt für Verlaufsdiagramme.</summary>
public sealed record StatPoint(DateTimeOffset Date, double Value, bool Won);

public static class StatsCalculator
{
    /// <summary>Alle Ergebnisse eines Spielers (über Id oder – bei Gästen – über den Namen).</summary>
    public static IEnumerable<(GameRecord Game, PlayerResult Result)> ResultsOf(IEnumerable<GameRecord> games, Guid? playerId, string name) =>
        games.Where(g => g.FinishedAt is not null)
            .SelectMany(g => g.Results.Select(r => (Game: g, Result: r)))
            .Where(x => Matches(x.Result, playerId, name));

    private static bool Matches(PlayerResult r, Guid? playerId, string name) =>
        playerId is { } id && r.PlayerId == id
        || (r.PlayerId is null || playerId is null) && string.Equals(r.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase);

    public static ModeStats Aggregate(GameMode mode, IEnumerable<(GameRecord Game, PlayerResult Result)> entries)
    {
        var list = entries.Where(e => e.Game.Mode == mode).ToList();
        var results = list.Select(e => e.Result).ToList();
        if (results.Count == 0) return new ModeStats { Mode = mode };

        var darts = results.Sum(r => r.DartsThrown);

        double? hitRate = null;
        var withHitRate = results.Where(r => r.Extra.ContainsKey("hit_rate") && r.DartsThrown > 0).ToList();
        if (withHitRate.Count > 0)
            hitRate = withHitRate.Sum(r => r.Extra["hit_rate"] * r.DartsThrown) / withHitRate.Sum(r => r.DartsThrown);

        var first9 = results.Where(r => r.First9Average is not null).ToList();
        var wins = results.Where(r => r.IsWinner && r.DartsThrown > 0).ToList();

        return new ModeStats
        {
            Mode = mode,
            Games = results.Count,
            Wins = results.Count(r => r.IsWinner),
            DartsThrown = darts,
            Average = darts == 0 ? 0 : Math.Round(results.Sum(r => r.Average * r.DartsThrown) / darts, 2),
            BestAverage = results.Max(r => r.Average),
            First9Average = first9.Count == 0 ? null : Math.Round(first9.Average(r => r.First9Average!.Value), 2),
            HighestTurn = results.Max(r => r.HighestTurn),
            HighestCheckout = results.Max(r => r.HighestCheckout),
            CheckoutAttempts = results.Sum(r => r.CheckoutAttempts),
            CheckoutsHit = results.Sum(r => r.CheckoutsHit),
            Count180 = results.Sum(r => r.Count180),
            Count140 = results.Sum(r => r.Count140),
            Count100 = results.Sum(r => r.Count100),
            BestScore = results.Max(r => r.Score),
            AverageScore = Math.Round(results.Average(r => r.Score), 2),
            HitRate = hitRate,
            FewestDartsWin = wins.Count == 0 ? null : wins.Min(r => r.DartsThrown)
        };
    }

    /// <summary>Verlauf des wichtigsten Werts je Modus (chronologisch).</summary>
    public static IReadOnlyList<StatPoint> Trend(GameMode mode, IEnumerable<(GameRecord Game, PlayerResult Result)> entries, int take = 30) =>
        entries.Where(e => e.Game.Mode == mode)
            .OrderBy(e => e.Game.FinishedAt)
            .TakeLast(take)
            .Select(e => new StatPoint(e.Game.FinishedAt!.Value, KeyValue(mode, e.Result), e.Result.IsWinner))
            .ToList();

    public static double KeyValue(GameMode mode, PlayerResult r) => mode switch
    {
        GameMode.X01 or GameMode.HighScore or GameMode.Cricket => r.Average,
        GameMode.AroundTheClock => r.DartsThrown,
        GameMode.Checkout121 => r.CheckoutsHit,
        _ => r.Score
    };

    public static string KeyLabel(GameMode mode) => mode switch
    {
        GameMode.X01 or GameMode.HighScore => "3-Dart-Average",
        GameMode.Cricket => "Marks pro Runde",
        GameMode.AroundTheClock => "Darts",
        GameMode.Checkout121 => "Checkouts",
        _ => "Punkte"
    };
}
