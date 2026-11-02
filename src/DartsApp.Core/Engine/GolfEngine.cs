using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Golf: In Loch n zählt die Zahl n. Triple = 1 Schlag, Double = 2, Single = 3, kein Treffer = 5.
/// Es zählt der letzte Treffer der Aufnahme – wer zufrieden ist, beendet die Aufnahme ("stehen bleiben").
/// Die wenigsten Schläge gewinnen.
/// </summary>
public sealed class GolfEngine : GameEngine
{
    public const int Par = 3;
    public const int MissStrokes = 5;

    private int[] _strokes = [];
    private int? _currentHit;

    public GolfEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (settings.Rounds is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(settings), "Golf benötigt 1-20 Löcher.");
        Reset();
    }

    public int Hole => Math.Min(Round, Settings.Rounds);
    public int StrokesOf(int player) => _strokes[player];

    /// <summary>Schläge, die die laufende Aufnahme gerade wert wäre (null = noch kein Treffer).</summary>
    public int? CurrentHoleStrokes => CurrentTurn is null ? null : _currentHit;

    protected override void Reset()
    {
        _strokes = new int[PlayerCount];
        _currentHit = null;
    }

    public static int StrokesFor(Dart dart) => dart.Multiplier switch { 3 => 1, 2 => 2, _ => 3 };

    protected override void OnTurnStarted(Turn turn) => _currentHit = null;

    protected override bool OnDart(Turn turn, Dart dart)
    {
        if (dart.Segment == turn.Round) _currentHit = StrokesFor(dart);
        turn.Score = _currentHit ?? MissStrokes;
        return false;
    }

    protected override void OnTurnCompleted(Turn turn)
    {
        var strokes = _currentHit ?? MissStrokes;
        turn.Score = strokes;
        turn.Note = strokes switch { 1 => "Hole in One", 2 => "Birdie", 3 => null, _ => "Bogey" };
        _strokes[turn.PlayerIndex] += strokes;
        _currentHit = null;
    }

    protected override void OnRoundCompleted(int round)
    {
        if (round < Settings.Rounds) return;
        var min = _strokes.Min();
        var leaders = Enumerable.Range(0, PlayerCount).Where(p => _strokes[p] == min).ToList();
        FinishGame(leaders.Count == 1 ? leaders[0] : null);
    }

    public override IReadOnlySet<int> RelevantSegments => new HashSet<int> { Hole };

    public override Dart BotAim() => Dart.T(Hole);

    /// <summary>Mit Double oder Triple bleibt der Computer stehen – weiterwerfen kann es nur verschlechtern.</summary>
    public override bool BotWantsToEndTurn => CurrentTurn is not null && _currentHit is <= 2;

    public override string Status => $"Loch {Hole} / {Settings.Rounds}";

    public override string? Hint
    {
        get
        {
            if (IsFinished) return null;
            var now = CurrentHoleStrokes is { } s ? $" · aktuell {s} {(s == 1 ? "Schlag" : "Schläge")}" : "";
            return $"Loch {Hole}: T = 1, D = 2, S = 3, sonst 5 – der letzte Treffer zählt{now}";
        }
    }

    private int HolesPlayed(int player) => TurnsOf(player).Count(t => t.IsComplete);

    public override PlayerView GetView(int player)
    {
        var played = HolesPlayed(player);
        var toPar = _strokes[player] - played * Par;
        var last = TurnsOf(player).LastOrDefault(t => t.IsComplete);
        return new PlayerView(Players[player].Name, _strokes[player].ToString(), "Schläge",
            Details(("Par", played == 0 ? "–" : toPar > 0 ? $"+{toPar}" : toPar.ToString()),
                ("Letztes Loch", last?.Score.ToString() ?? "–")),
            IsActive: !IsFinished && player == CurrentPlayer);
    }

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _strokes[i];
            // Aufnahme-Score ist hier die Schlagzahl – "höchste Aufnahme" ergibt keinen Sinn.
            r.HighestTurn = 0;
            var holes = TurnsOf(i).Where(t => t.IsComplete).ToList();
            r.Extra["to_par"] = _strokes[i] - holes.Count * Par;
            r.Extra["holes_in_one"] = holes.Count(t => t.Score == 1);
            results.Add(r);
        }

        AssignRanks(results, r => -r.Score, Winner);
        return results;
    }
}
