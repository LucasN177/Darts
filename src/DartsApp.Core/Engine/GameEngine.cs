using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Basis aller Spielmodi. Ein Spiel ist vollständig durch Einstellungen, Spieler und die
/// Liste aller geworfenen Darts beschrieben. Undo spielt deshalb einfach alle Darts bis auf
/// den letzten neu ab – das hält die Modi frei von eigener Undo-Logik.
/// </summary>
public abstract class GameEngine
{
    private readonly List<Dart> _log = new();
    private readonly List<Turn> _turns = new();
    private Turn? _currentTurn;
    private int _turnsInRound;
    private bool _legRestarted;
    private bool _replaying;

    protected GameEngine(GameSettings settings, IReadOnlyList<PlayerRef> players)
    {
        if (players.Count == 0)
            throw new ArgumentException("Mindestens ein Spieler wird benötigt.", nameof(players));
        if (players.Count > MaxPlayers)
            throw new ArgumentException($"Maximal {MaxPlayers} Spieler.", nameof(players));

        Settings = settings;
        Players = players;
    }

    public const int MaxPlayers = 8;

    public GameSettings Settings { get; }
    public IReadOnlyList<PlayerRef> Players { get; }
    public int PlayerCount => Players.Count;

    public int CurrentPlayer { get; private set; }
    /// <summary>Aktuelle Runde (1-basiert) im aktuellen Leg.</summary>
    public int Round { get; private set; } = 1;
    public int Leg { get; private set; } = 1;
    public int Set { get; private set; } = 1;

    public bool IsFinished { get; private set; }
    public int? Winner { get; private set; }

    public IReadOnlyList<Dart> DartLog => _log;
    public IReadOnlyList<Turn> Turns => _turns;

    /// <summary>Die laufende (noch nicht abgeschlossene) Aufnahme, falls schon ein Dart geworfen wurde.</summary>
    public Turn? CurrentTurn => _currentTurn;
    public int DartsInCurrentTurn => _currentTurn?.Darts.Count ?? 0;
    public int DartsLeftInTurn => 3 - DartsInCurrentTurn;

    /// <summary>Letzte Meldung, z.B. "Bust!" oder "Leg an Anna". Wird beim nächsten Dart gelöscht.</summary>
    public string? Message { get; private set; }

    /// <summary>Nach jedem Wurf/Undo ausgelöst (nicht während des Nachspielens).</summary>
    public event Action? Changed;

    /// <summary>Hinweis für den aktiven Spieler, z.B. Checkout-Weg oder aktuelles Ziel.</summary>
    public virtual string? Hint => null;

    /// <summary>Felder, die für den aktiven Spieler gerade relevant sind (zum Hervorheben im Eingabefeld).</summary>
    public virtual IReadOnlySet<int>? RelevantSegments => null;

    /// <summary>Feld, auf das ein Computergegner als Nächstes zielt.</summary>
    public virtual Dart BotAim() => Dart.T(20);

    public bool IsBotTurn => !IsFinished && Players[CurrentPlayer].IsBot;

    /// <summary>Statuszeile über dem Scoreboard, z.B. "Set 1 · Leg 2 · Runde 4".</summary>
    public virtual string Status => $"Runde {Round}";

    public abstract PlayerView GetView(int player);
    public abstract IReadOnlyList<PlayerResult> GetResults();

    public void Throw(Dart dart)
    {
        if (IsFinished) return;
        _log.Add(dart);
        Apply(dart);
        Changed?.Invoke();
    }

    /// <summary>Füllt die laufende Aufnahme mit Fehlwürfen auf und beendet sie.</summary>
    public void FinishTurn()
    {
        if (IsFinished) return;
        var turn = _currentTurn;
        do
        {
            _log.Add(Dart.Miss);
            Apply(Dart.Miss);
            turn ??= _turns[^1];
        } while (!IsFinished && !turn.IsComplete);

        Changed?.Invoke();
    }

    public bool CanUndo => _log.Count > 0;

    public void Undo()
    {
        if (_log.Count == 0) return;
        var darts = _log.Take(_log.Count - 1).ToList();
        Replay(darts);
        Changed?.Invoke();
    }

    /// <summary>Spielt eine Liste von Darts von Beginn an ab (z.B. beim Wiederherstellen eines Spiels).</summary>
    public void Replay(IEnumerable<Dart> darts)
    {
        _replaying = true;
        try
        {
            _log.Clear();
            _turns.Clear();
            _currentTurn = null;
            _turnsInRound = 0;
            _legRestarted = false;
            CurrentPlayer = 0;
            Round = 1;
            Leg = 1;
            Set = 1;
            IsFinished = false;
            Winner = null;
            Message = null;
            Reset();

            foreach (var dart in darts)
            {
                if (IsFinished) break;
                _log.Add(dart);
                Apply(dart);
            }
        }
        finally
        {
            _replaying = false;
        }
    }

    /// <summary>True, solange ein Spiel nachgespielt wird (z.B. um Effekte zu unterdrücken).</summary>
    protected bool IsReplaying => _replaying;

    private void Apply(Dart dart)
    {
        if (IsFinished) return;

        if (_currentTurn is null)
        {
            _currentTurn = new Turn(CurrentPlayer, Round, Leg, Set);
            _turns.Add(_currentTurn);
            Message = null;
            OnTurnStarted(_currentTurn);
        }

        var turn = _currentTurn;
        turn.Darts.Add(dart);
        var endTurn = OnDart(turn, dart) || turn.Darts.Count >= 3 || IsFinished;
        if (!endTurn) return;

        turn.IsComplete = true;
        _currentTurn = null;
        OnTurnCompleted(turn);
        if (IsFinished) return;

        if (_legRestarted)
        {
            _legRestarted = false;
            return;
        }

        _turnsInRound++;
        CurrentPlayer = (CurrentPlayer + 1) % PlayerCount;
        if (_turnsInRound >= PlayerCount)
        {
            _turnsInRound = 0;
            Round++;
            OnRoundCompleted(Round - 1);
        }

        // Ausgeschiedene Spieler überspringen.
        var guard = 0;
        while (!IsFinished && IsPlayerOut(CurrentPlayer) && guard++ < PlayerCount)
        {
            _turnsInRound++;
            CurrentPlayer = (CurrentPlayer + 1) % PlayerCount;
            if (_turnsInRound >= PlayerCount)
            {
                _turnsInRound = 0;
                Round++;
                OnRoundCompleted(Round - 1);
            }
        }
    }

    /// <summary>Setzt den modusspezifischen Zustand auf den Spielbeginn zurück.</summary>
    protected abstract void Reset();

    /// <summary>Wertet einen Dart aus. Rückgabe true beendet die Aufnahme vorzeitig.</summary>
    protected abstract bool OnDart(Turn turn, Dart dart);

    protected virtual void OnTurnStarted(Turn turn) { }
    protected virtual void OnTurnCompleted(Turn turn) { }
    /// <summary>Wird aufgerufen, nachdem alle Spieler die Runde <paramref name="round"/> gespielt haben.</summary>
    protected virtual void OnRoundCompleted(int round) { }
    protected virtual bool IsPlayerOut(int player) => false;

    protected void SetMessage(string? message) => Message = message;

    protected void FinishGame(int? winner)
    {
        IsFinished = true;
        Winner = winner;
        Message = winner is { } w ? $"{Players[w].Name} gewinnt!" : "Spiel beendet";
    }

    /// <summary>Startet ein neues Leg mit dem angegebenen Spieler (X01).</summary>
    protected void StartNewLeg(int startingPlayer, bool newSet)
    {
        if (newSet)
        {
            Set++;
            Leg = 1;
        }
        else
        {
            Leg++;
        }

        Round = 1;
        _turnsInRound = 0;
        CurrentPlayer = startingPlayer;
        _legRestarted = true;
    }

    /// <summary>Platzierung nach absteigendem Schlüssel; Gleichstand teilt sich den Rang.</summary>
    protected static void AssignRanks(IList<PlayerResult> results, Func<PlayerResult, double> key, int? winner)
    {
        var ordered = results
            .OrderByDescending(r => r.Position == winner)
            .ThenByDescending(key)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var r = ordered[i];
            var sameAsPrevious = i > 0 && ordered[i - 1].Position != winner && r.Position != winner
                                 && Math.Abs(key(ordered[i - 1]) - key(r)) < 1e-9;
            r.Rank = sameAsPrevious ? ordered[i - 1].Rank : i + 1;
            r.IsWinner = r.Position == winner;
        }
    }

    protected IEnumerable<Turn> TurnsOf(int player) => _turns.Where(t => t.PlayerIndex == player);

    protected PlayerResult BaseResult(int player)
    {
        var turns = TurnsOf(player).ToList();
        var darts = turns.Sum(t => t.Darts.Count);
        return new PlayerResult
        {
            Position = player,
            PlayerId = Players[player].Id,
            Name = Players[player].Name,
            DartsThrown = darts,
            HighestTurn = turns.Count == 0 ? 0 : turns.Max(t => t.Score),
            Extra = Players[player].Bot is { } bot ? new() { ["bot"] = (int)bot } : new()
        };
    }

    protected static List<KeyValuePair<string, string>> Details(params (string Label, string Value)[] items) =>
        items.Select(i => new KeyValuePair<string, string>(i.Label, i.Value)).ToList();

    protected static string Fmt(double value) => value.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
}
