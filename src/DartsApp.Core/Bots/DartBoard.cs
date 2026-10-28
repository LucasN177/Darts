using DartsApp.Core.Models;

namespace DartsApp.Core.Bots;

/// <summary>
/// Geometrie einer Standard-Steeldartscheibe (Maße in mm) und Simulation eines Wurfs:
/// Zielpunkt plus normalverteilte Streuung, danach wird das getroffene Feld bestimmt.
/// </summary>
public static class DartBoard
{
    /// <summary>Zahlen im Uhrzeigersinn, beginnend oben mit der 20.</summary>
    public static readonly int[] Order = [20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5];

    public const double InnerBullRadius = 6.35;
    public const double OuterBullRadius = 15.9;
    public const double TrebleInner = 99;
    public const double TrebleOuter = 107;
    public const double DoubleInner = 162;
    public const double DoubleOuter = 170;

    private const double SegmentAngle = 360.0 / 20;

    /// <summary>Punkt, auf den für ein Feld gezielt wird (x nach rechts, y nach oben).</summary>
    public static (double X, double Y) AimPoint(Dart target)
    {
        if (target.IsMiss) return (0, 0);
        if (target.IsBull)
            // Auf das Bullseye zielt man mittig; für das äußere Bull leicht darüber.
            return target.IsDouble ? (0, 0) : (0, (InnerBullRadius + OuterBullRadius) / 2);

        var radius = target.Multiplier switch
        {
            3 => (TrebleInner + TrebleOuter) / 2,
            2 => (DoubleInner + DoubleOuter) / 2,
            // Großes äußeres Single-Feld
            _ => (TrebleOuter + DoubleInner) / 2
        };
        var angle = Array.IndexOf(Order, target.Segment) * SegmentAngle * Math.PI / 180;
        return (radius * Math.Sin(angle), radius * Math.Cos(angle));
    }

    /// <summary>Feld, das an der Position (x, y) liegt.</summary>
    public static Dart FieldAt(double x, double y)
    {
        var r = Math.Sqrt(x * x + y * y);
        if (r <= InnerBullRadius) return Dart.Bullseye;
        if (r <= OuterBullRadius) return Dart.OuterBull;
        if (r > DoubleOuter) return Dart.Miss;

        var angle = Math.Atan2(x, y) * 180 / Math.PI;
        if (angle < 0) angle += 360;
        var index = (int)Math.Floor((angle + SegmentAngle / 2) / SegmentAngle) % 20;
        var multiplier = r is >= TrebleInner and <= TrebleOuter ? 3 : r >= DoubleInner ? 2 : 1;
        return new Dart(Order[index], multiplier);
    }

    /// <summary>Simuliert einen Wurf auf <paramref name="target"/> mit Streuung <paramref name="sigma"/> (mm).</summary>
    public static Dart Throw(Dart target, double sigma, Random random)
    {
        var (x, y) = AimPoint(target);
        return FieldAt(x + Gaussian(random) * sigma, y + Gaussian(random) * sigma);
    }

    private static double Gaussian(Random random)
    {
        // Box-Muller
        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
