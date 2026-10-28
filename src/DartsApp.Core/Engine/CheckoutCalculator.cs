using System.Collections.Concurrent;
using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Berechnet Checkout-Wege. Bevorzugt werden: möglichst wenige Darts, wenig riskante
/// Vorbereitungswürfe (Singles vor Triples) und beliebte Doppel (D20, D16, D8, ...).
/// </summary>
public static class CheckoutCalculator
{
    private static readonly ConcurrentDictionary<(int, int, InOutRule), IReadOnlyList<Dart>?> Cache = new();

    private static readonly int[] DoublePreference = [20, 16, 8, 12, 18, 10, 4, 14, 6, 2, 19, 17, 15, 13, 11, 9, 7, 5, 3, 1, 25];

    private static readonly Dart[] AllDarts = BuildAllDarts();

    private static Dart[] BuildAllDarts()
    {
        var list = new List<Dart>();
        for (var s = 1; s <= 20; s++)
        for (var m = 1; m <= 3; m++)
            list.Add(new Dart(s, m));
        list.Add(Dart.OuterBull);
        list.Add(Dart.Bullseye);
        return list.ToArray();
    }

    public static bool IsFinisher(Dart dart, InOutRule outRule) => outRule switch
    {
        InOutRule.Double => dart.IsDouble,
        InOutRule.Master => dart.IsDouble || dart.IsTriple,
        _ => !dart.IsMiss
    };

    /// <summary>Höchster mit drei Darts möglicher Checkout für die Regel.</summary>
    public static int MaxCheckout(InOutRule outRule) => outRule == InOutRule.Double ? 170 : 180;

    /// <summary>Liefert einen Checkout-Weg oder null, falls mit den verbleibenden Darts nicht möglich.</summary>
    public static IReadOnlyList<Dart>? Suggest(int remaining, int dartsLeft, InOutRule outRule)
    {
        if (remaining <= 0 || dartsLeft <= 0 || remaining > 180) return null;
        dartsLeft = Math.Min(dartsLeft, 3);
        return Cache.GetOrAdd((remaining, dartsLeft, outRule), key => Compute(key.Item1, key.Item2, key.Item3));
    }

    public static string? SuggestText(int remaining, int dartsLeft, InOutRule outRule)
    {
        var path = Suggest(remaining, dartsLeft, outRule);
        return path is null ? null : string.Join(" ", path);
    }

    private static readonly Dart[] SetupOrder = BuildSetupOrder();

    private static Dart[] BuildSetupOrder()
    {
        var list = new List<Dart> { Dart.T(20), Dart.T(19), Dart.T(18), Dart.T(17) };
        for (var s = 20; s >= 1; s--) list.Add(Dart.S(s));
        return list.ToArray();
    }

    /// <summary>
    /// Bester nächster Dart für den Computer: erster Dart eines Checkout-Wegs, sonst ein Vorbereitungswurf,
    /// der keinen Bust riskiert und möglichst einen auscheckbaren Rest übrig lässt.
    /// </summary>
    public static Dart BestDart(int remaining, int dartsLeft, InOutRule outRule)
    {
        if (Suggest(remaining, dartsLeft, outRule) is { Count: > 0 } path) return path[0];

        var minimumLeft = outRule == InOutRule.Single ? 1 : 2;
        Dart? fallback = null;
        foreach (var dart in SetupOrder)
        {
            var left = remaining - dart.Score;
            if (left < minimumLeft) continue;
            if (left > 170 || Suggest(left, 3, outRule) is not null) return dart;
            fallback ??= dart;
        }

        return fallback ?? Dart.S(1);
    }

    private static IReadOnlyList<Dart>? Compute(int remaining, int dartsLeft, InOutRule outRule)
    {
        Dart[]? best = null;
        var bestCost = double.MaxValue;

        var finishers = AllDarts.Where(d => IsFinisher(d, outRule)).ToArray();

        for (var count = 1; count <= dartsLeft && best is null; count++)
        {
            foreach (var finisher in finishers)
            {
                var rest = remaining - finisher.Score;
                if (rest < 0) continue;

                if (count == 1)
                {
                    if (rest != 0) continue;
                    Consider([finisher]);
                }
                else if (count == 2)
                {
                    foreach (var a in AllDarts)
                        if (a.Score == rest) Consider([a, finisher]);
                }
                else
                {
                    foreach (var a in AllDarts)
                    {
                        if (a.Score >= rest) continue;
                        foreach (var b in AllDarts)
                            if (a.Score + b.Score == rest) Consider([a, b, finisher]);
                    }
                }
            }
        }

        return best;

        void Consider(Dart[] path)
        {
            var cost = Cost(path, outRule);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = path;
            }
        }
    }

    private static double Cost(Dart[] path, InOutRule outRule)
    {
        double cost = 0;
        for (var i = 0; i < path.Length - 1; i++)
            cost += SetupCost(path[i]);

        var finisher = path[^1];
        var preference = Array.IndexOf(DoublePreference, finisher.Segment);
        cost += outRule == InOutRule.Single
            ? (finisher.IsTriple ? 1 : finisher.IsDouble ? 0.5 : 0) + (20 - Math.Min(finisher.Segment, 20)) * 0.001
            : 0.05 + preference * 0.01 + (finisher.IsTriple ? 0.5 : 0);

        // Höhere Vorbereitungswürfe zuerst (lesbarer, üblicher Weg).
        if (path.Length == 3 && path[0].Score < path[1].Score) cost += 0.0005;
        return cost;
    }

    private static double SetupCost(Dart d)
    {
        if (d.IsBull) return d.IsDouble ? 2 : 1.5;
        var numberPenalty = (20 - d.Segment) * 0.01;
        return d.Multiplier switch
        {
            3 => 1 + numberPenalty,
            2 => 1.2 + numberPenalty,
            _ => numberPenalty * 0.1
        };
    }
}
