namespace DartsApp.Core.Models;

/// <summary>
/// Ein einzelner Wurf. Segment 0 = Miss, 1-20 = Zahlenfelder, 25 = Bull.
/// Multiplier 1 = Single, 2 = Double, 3 = Triple (Bull nur 1 oder 2).
/// </summary>
public readonly record struct Dart
{
    public const int BullSegment = 25;

    public Dart(int segment, int multiplier)
    {
        if (segment is < 0 or > 20 && segment != BullSegment)
            throw new ArgumentOutOfRangeException(nameof(segment));
        if (multiplier is < 1 or > 3 || (segment == BullSegment && multiplier == 3))
            throw new ArgumentOutOfRangeException(nameof(multiplier));

        Segment = segment;
        Multiplier = segment == 0 ? 1 : multiplier;
    }

    public int Segment { get; }
    public int Multiplier { get; }

    public int Score => Segment * Multiplier;
    public bool IsMiss => Segment == 0;
    public bool IsDouble => Multiplier == 2;
    public bool IsTriple => Multiplier == 3;
    public bool IsBull => Segment == BullSegment;

    public static Dart Miss => new(0, 1);
    public static Dart S(int segment) => new(segment, 1);
    public static Dart D(int segment) => new(segment, 2);
    public static Dart T(int segment) => new(segment, 3);
    public static Dart OuterBull => new(BullSegment, 1);
    public static Dart Bullseye => new(BullSegment, 2);

    /// <summary>Kurzschreibweise, z.B. "T20", "D16", "S5", "25", "BULL", "0".</summary>
    public override string ToString() => this switch
    {
        { IsMiss: true } => "0",
        { IsBull: true, Multiplier: 1 } => "25",
        { IsBull: true } => "BULL",
        _ => $"{(Multiplier switch { 2 => "D", 3 => "T", _ => "S" })}{Segment}"
    };

    public static Dart Parse(string text)
    {
        if (!TryParse(text, out var dart))
            throw new FormatException($"Ungültiger Dart: '{text}'");
        return dart;
    }

    public static bool TryParse(string? text, out Dart dart)
    {
        dart = Miss;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim().ToUpperInvariant();

        switch (t)
        {
            case "0" or "M" or "MISS":
                dart = Miss;
                return true;
            case "25" or "S25" or "SB":
                dart = OuterBull;
                return true;
            case "BULL" or "50" or "D25" or "DB":
                dart = Bullseye;
                return true;
        }

        var multiplier = t[0] switch { 'S' => 1, 'D' => 2, 'T' => 3, _ => 0 };
        var numberPart = multiplier == 0 ? t : t[1..];
        if (multiplier == 0) multiplier = 1;
        if (!int.TryParse(numberPart, out var segment)) return false;
        if (segment is < 1 or > 20) return false;

        dart = new Dart(segment, multiplier);
        return true;
    }
}
