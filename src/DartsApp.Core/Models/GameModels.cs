namespace DartsApp.Core.Models;

/// <summary>
/// Ein Spieler in einem Spiel. Id ist null für spontane Gastspieler und Computergegner;
/// Bot ist gesetzt, wenn der Computer für diesen Spieler wirft.
/// </summary>
public sealed record PlayerRef(Guid? Id, string Name, BotLevel? Bot = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsBot => Bot is not null;
}

/// <summary>Eine Aufnahme (bis zu drei Darts) eines Spielers.</summary>
public sealed class Turn
{
    public Turn(int playerIndex, int round, int leg, int set)
    {
        PlayerIndex = playerIndex;
        Round = round;
        Leg = leg;
        Set = set;
    }

    public int PlayerIndex { get; }
    public int Round { get; }
    public int Leg { get; }
    public int Set { get; }
    public List<Dart> Darts { get; } = new(3);

    /// <summary>Gewertete Punkte (X01: abgezogene Punkte, Cricket: Marks, sonst Punkte).</summary>
    public int Score { get; set; }
    public bool Bust { get; set; }
    /// <summary>Optionaler Hinweis wie "Checkout", "Shanghai!", "Leg gewonnen".</summary>
    public string? Note { get; set; }
    public bool IsComplete { get; set; }
}

/// <summary>Anzeige-Daten eines Spielers für die Spielansicht.</summary>
public sealed record PlayerView(
    string Name,
    string Primary,
    string PrimaryLabel,
    IReadOnlyList<KeyValuePair<string, string>> Details,
    bool IsActive,
    bool IsOut = false,
    string? Badge = null);

/// <summary>Ergebnis eines Spielers am Ende eines Spiels (wird gespeichert).</summary>
public sealed class PlayerResult
{
    public int Position { get; set; }
    public Guid? PlayerId { get; set; }
    public string Name { get; set; } = "";
    public int Rank { get; set; }
    public bool IsWinner { get; set; }
    public int DartsThrown { get; set; }
    /// <summary>Modusabhängiger Hauptwert (X01: geworfene Punkte, Cricket: Punkte, Shanghai: Punkte, ...).</summary>
    public int Score { get; set; }
    /// <summary>3-Dart-Average (Punkte pro Aufnahme).</summary>
    public double Average { get; set; }
    public double? First9Average { get; set; }
    public int HighestTurn { get; set; }
    public int HighestCheckout { get; set; }
    public int CheckoutAttempts { get; set; }
    public int CheckoutsHit { get; set; }
    public int Count180 { get; set; }
    public int Count140 { get; set; }
    public int Count100 { get; set; }
    public int LegsWon { get; set; }
    public int SetsWon { get; set; }
    /// <summary>Modusspezifische Kennzahlen, z.B. "mpr" (Cricket) oder "hit_rate".</summary>
    public Dictionary<string, double> Extra { get; set; } = new();

    public double? CheckoutRate => CheckoutAttempts > 0 ? (double)CheckoutsHit / CheckoutAttempts : null;
}

/// <summary>Ein abgeschlossenes (gespeichertes) Spiel.</summary>
public sealed class GameRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public GameSettings Settings { get; set; } = new();
    public List<PlayerRef> Players { get; set; } = new();
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? WinnerName { get; set; }
    /// <summary>Alle Darts in Wurfreihenfolge (Kurzschreibweise), damit das Spiel nachgespielt werden kann.</summary>
    public List<string> Darts { get; set; } = new();
    public List<PlayerResult> Results { get; set; } = new();

    public GameMode Mode => Settings.Mode;
}

/// <summary>Ein gespeicherter Spieler (Spielerliste des Benutzers).</summary>
public sealed class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    /// <summary>Der angemeldete Benutzer selbst.</summary>
    public bool IsSelf { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
