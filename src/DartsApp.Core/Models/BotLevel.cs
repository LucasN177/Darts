namespace DartsApp.Core.Models;

/// <summary>Schwierigkeitsgrad eines Computergegners.</summary>
public enum BotLevel
{
    Beginner = 1,
    Casual = 2,
    Intermediate = 3,
    League = 4,
    Pro = 5,
    WorldClass = 6
}

public static class BotLevels
{
    public static readonly IReadOnlyList<BotLevel> All = Enum.GetValues<BotLevel>();

    public static string Name(this BotLevel level) => level switch
    {
        BotLevel.Beginner => "Anfänger",
        BotLevel.Casual => "Hobby",
        BotLevel.Intermediate => "Fortgeschritten",
        BotLevel.League => "Ligaspieler",
        BotLevel.Pro => "Profi",
        BotLevel.WorldClass => "Weltklasse",
        _ => level.ToString()
    };

    /// <summary>Ungefährer 3-Dart-Average in X01 (siehe Kalibrierungstest).</summary>
    public static int ApproxAverage(this BotLevel level) => level switch
    {
        BotLevel.Beginner => 25,
        BotLevel.Casual => 40,
        BotLevel.Intermediate => 55,
        BotLevel.League => 70,
        BotLevel.Pro => 85,
        _ => 100
    };

    /// <summary>Streuung der Würfe in mm (Standardabweichung je Achse).</summary>
    public static double Sigma(this BotLevel level) => level switch
    {
        BotLevel.Beginner => 37,
        BotLevel.Casual => 21,
        BotLevel.Intermediate => 15,
        BotLevel.League => 11.5,
        BotLevel.Pro => 8.3,
        _ => 6.3
    };
}
