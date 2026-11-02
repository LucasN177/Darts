using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

public enum TurnTotalKind
{
    Score,
    Checkout,
    Bust
}

/// <summary>Ergebnis der Umrechnung einer Aufnahme-Summe: die Darts für die Engine oder eine Fehlermeldung.</summary>
public sealed record TurnTotalResult(IReadOnlyList<Dart> Darts, TurnTotalKind Kind, string? Error = null)
{
    public bool IsValid => Error is null;

    public static TurnTotalResult Fail(string error) => new([], TurnTotalKind.Score, error);
}

/// <summary>
/// Schnelleingabe: Eine Aufnahme wird nur als Summe eingegeben und in eine passende Dart-Kombination umgerechnet
/// (z.B. 100 → T20 D20 0). Die Spiellogik bleibt dadurch dart-basiert – Bust, Checkout, Averages und das Nachspielen
/// gespeicherter Spiele funktionieren unverändert. Bei einem Checkout gibt der Spieler an, mit wie vielen Darts er
/// ausgecheckt hat; die Kombination endet dann mit einem passenden Finish.
/// </summary>
public static class FastInput
{
    public const int MaxTotal = 180;

    /// <summary>Alle möglichen Werte eines einzelnen Darts, absteigend.</summary>
    private static readonly int[] DartScores = BuildDartScores();

    /// <summary>Summen, die mit drei Darts möglich sind.</summary>
    private static readonly bool[] Possible = BuildPossible();

    private static int[] BuildDartScores()
    {
        var scores = new HashSet<int> { 0, 25, 50 };
        for (var s = 1; s <= 20; s++)
        {
            scores.Add(s);
            scores.Add(s * 2);
            scores.Add(s * 3);
        }
        return scores.OrderDescending().ToArray();
    }

    private static bool[] BuildPossible()
    {
        var possible = new bool[MaxTotal + 1];
        foreach (var a in DartScores)
        foreach (var b in DartScores)
        foreach (var c in DartScores)
            if (a + b + c <= MaxTotal) possible[a + b + c] = true;
        return possible;
    }

    /// <summary>True, wenn die Summe mit drei Darts erreichbar ist (nicht z.B. 163, 166, 169, 172, …).</summary>
    public static bool IsPossible(int total) => total is >= 0 and <= MaxTotal && Possible[total];

    /// <summary>True, wenn diese Summe den aktuellen Rest genau auf 0 bringen würde.</summary>
    public static bool WouldFinish(GameEngine engine, int total) => engine.CheckoutTarget is { } t && total == t.Remaining;

    /// <summary>Mit wie vielen Darts ein Checkout des Rests möglich ist (z.B. 100 → 2 oder 3).</summary>
    public static IReadOnlyList<int> CheckoutDartOptions(int remaining, InOutRule outRule) =>
        new[] { 1, 2, 3 }.Where(k => CheckoutCalculator.Suggest(remaining, k, outRule) is not null).ToList();

    /// <summary>
    /// Rechnet eine Aufnahme-Summe in Darts um.
    /// </summary>
    /// <param name="checkoutDarts">Nur wenn die Summe den Rest genau trifft: 1-3 = ausgecheckt mit so vielen Darts,
    /// 0 = nicht ausgecheckt (z.B. letzter Dart kein Doppel → Bust).</param>
    public static TurnTotalResult ToDarts(GameEngine engine, int total, int? checkoutDarts = null)
    {
        if (!IsPossible(total))
            return TurnTotalResult.Fail(total > MaxTotal ? $"Mehr als {MaxTotal} geht mit drei Darts nicht." : $"{total} ist mit drei Darts nicht möglich.");

        if (engine.CheckoutTarget is not { } target)
            return new TurnTotalResult(Decompose(total), TurnTotalKind.Score);

        var (remaining, outRule) = target;

        if (total == remaining && checkoutDarts is not 0)
        {
            if (checkoutDarts is not { } k)
                return TurnTotalResult.Fail("Mit wie vielen Darts wurde ausgecheckt?");
            if (CheckoutCalculator.Suggest(remaining, k, outRule) is not { } path)
                return TurnTotalResult.Fail($"{remaining} lässt sich nicht mit {k} {(k == 1 ? "Dart" : "Darts")} auschecken.");

            // Fehlwürfe zuerst, damit der Checkout mit genau k Darts endet.
            var darts = Enumerable.Repeat(Dart.Miss, k - path.Count).Concat(path).ToList();
            return new TurnTotalResult(darts, TurnTotalKind.Checkout);
        }

        var minimumLeft = outRule == InOutRule.Single ? 1 : 2;
        var bust = total > remaining || remaining - total is > 0 and < 2 && minimumLeft == 2 || total == remaining;
        if (!bust) return new TurnTotalResult(Decompose(total), TurnTotalKind.Score);

        return BustDarts(total, remaining, outRule) is { } bustDarts
            ? new TurnTotalResult(bustDarts, TurnTotalKind.Bust)
            : TurnTotalResult.Fail("Diese Summe lässt sich nicht als Bust darstellen.");
    }

    /// <summary>Drei Darts mit genau dieser Summe (größter Wert zuerst, z.B. 100 = T20 D20 0).</summary>
    public static IReadOnlyList<Dart> Decompose(int total) =>
        Combinations(total).FirstOrDefault() ?? throw new ArgumentOutOfRangeException(nameof(total));

    private static IEnumerable<IReadOnlyList<Dart>> Combinations(int total)
    {
        foreach (var a in DartScores)
        {
            if (a > total) continue;
            foreach (var b in DartScores)
            {
                if (b > a || a + b > total) continue;
                var c = total - a - b;
                if (c > b || Array.IndexOf(DartScores, c) < 0) continue;
                yield return [ToDart(a), ToDart(b), ToDart(c)];
            }
        }
    }

    /// <summary>Dart-Reihenfolge für einen Bust, bei der unterwegs kein gültiger Checkout entsteht.</summary>
    private static IReadOnlyList<Dart>? BustDarts(int total, int remaining, InOutRule outRule)
    {
        foreach (var combination in Combinations(total))
        foreach (var order in Permutations(combination))
        {
            var rest = remaining;
            var thrown = new List<Dart>();
            foreach (var dart in order)
            {
                thrown.Add(dart);
                rest -= dart.Score;
                if (rest == 0 && CheckoutCalculator.IsFinisher(dart, outRule)) break; // wäre ein Checkout
                if (rest < 0 || rest == 0 || (rest == 1 && outRule != InOutRule.Single)) return thrown;
            }
        }
        return null;
    }

    private static IEnumerable<IReadOnlyList<Dart>> Permutations(IReadOnlyList<Dart> d)
    {
        int[][] orders = [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];
        return orders.Select(o => (IReadOnlyList<Dart>)o.Select(i => d[i]).ToList()).Distinct(new SequenceComparer());
    }

    private static Dart ToDart(int score) => score switch
    {
        0 => Dart.Miss,
        <= 20 => Dart.S(score),
        25 => Dart.OuterBull,
        50 => Dart.Bullseye,
        _ when score % 3 == 0 && score <= 60 => Dart.T(score / 3),
        _ when score % 2 == 0 && score <= 40 => Dart.D(score / 2),
        _ => throw new ArgumentOutOfRangeException(nameof(score))
    };

    private sealed class SequenceComparer : IEqualityComparer<IReadOnlyList<Dart>>
    {
        public bool Equals(IReadOnlyList<Dart>? x, IReadOnlyList<Dart>? y) => x!.SequenceEqual(y!);
        public int GetHashCode(IReadOnlyList<Dart> obj) => HashCode.Combine(obj[0], obj[1], obj[2]);
    }
}
