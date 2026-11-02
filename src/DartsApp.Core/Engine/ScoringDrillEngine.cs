using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Trefferquoten-Training auf ein festes Feld.
/// <list type="bullet">
/// <item>Scoring-Drill: z.B. 99 Darts auf T20; gezählt werden exakte Treffer, zusätzlich die Treffer im richtigen Segment.</item>
/// <item>Bull-Training: Darts auf das Bull; 25 = 1 Punkt, Bullseye = 2 Punkte.</item>
/// </list>
/// </summary>
public sealed class ScoringDrillEngine : GameEngine
{
    private readonly Dart _target;
    private int[] _hits = [];
    private int[] _segmentHits = [];
    private int[] _points = [];
    private int[] _scored = [];

    public ScoringDrillEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (settings.Rounds < 1) throw new ArgumentOutOfRangeException(nameof(settings), "Mindestens eine Runde.");
        _target = settings.Mode == GameMode.BullTraining
            ? Dart.Bullseye
            : Dart.TryParse(settings.DrillTarget, out var t) && !t.IsMiss
                ? t
                : throw new ArgumentOutOfRangeException(nameof(settings), $"Ungültiges Zielfeld '{settings.DrillTarget}'.");
        Reset();
    }

    public Dart Target => _target;
    private bool IsBullTraining => Settings.Mode == GameMode.BullTraining;
    public int ScoreOf(int player) => IsBullTraining ? _points[player] : _hits[player];

    protected override void Reset()
    {
        _hits = new int[PlayerCount];
        _segmentHits = new int[PlayerCount];
        _points = new int[PlayerCount];
        _scored = new int[PlayerCount];
    }

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var me = turn.PlayerIndex;
        _scored[me] += dart.Score;

        if (IsBullTraining)
        {
            if (!dart.IsBull) return false;
            var points = dart.IsDouble ? 2 : 1;
            _points[me] += points;
            _hits[me]++;
            if (dart.IsDouble) _segmentHits[me]++;
            turn.Score += points;
            return false;
        }

        if (dart.Segment == _target.Segment) _segmentHits[me]++;
        if (dart != _target) return false;
        _hits[me]++;
        turn.Score++;
        return false;
    }

    protected override void OnRoundCompleted(int round)
    {
        if (round < Settings.Rounds) return;
        var scores = Enumerable.Range(0, PlayerCount).Select(ScoreOf).ToArray();
        var max = scores.Max();
        var leaders = Enumerable.Range(0, PlayerCount).Where(p => scores[p] == max).ToList();
        FinishGame(leaders.Count == 1 ? leaders[0] : null);
    }

    public override IReadOnlySet<int> RelevantSegments => new HashSet<int> { _target.Segment };
    public override Dart BotAim() => _target;

    public override string Status => $"Runde {Math.Min(Round, Settings.Rounds)} / {Settings.Rounds}";

    public override string? Hint => IsFinished
        ? null
        : IsBullTraining ? "Ziel: Bull – 25 = 1 Punkt, Bullseye = 2 Punkte" : $"Ziel: {_target}";

    private double Rate(int[] counts, int player)
    {
        var darts = TurnsOf(player).Sum(t => t.Darts.Count);
        return darts == 0 ? 0 : (double)counts[player] / darts;
    }

    public override PlayerView GetView(int player) => IsBullTraining
        ? new PlayerView(Players[player].Name, _points[player].ToString(), "Punkte",
            Details(("Bull-Quote", $"{Rate(_hits, player):P0}"), ("Bullseyes", _segmentHits[player].ToString())),
            IsActive: !IsFinished && player == CurrentPlayer)
        : new PlayerView(Players[player].Name, _hits[player].ToString(), $"Treffer {_target}",
            Details(("Quote", $"{Rate(_hits, player):P0}"), ("Segment", $"{Rate(_segmentHits, player):P0}"),
                ("Ø", Fmt(TurnsOf(player).Sum(t => t.Darts.Count) is var d and > 0 ? _scored[player] * 3.0 / d : 0))),
            IsActive: !IsFinished && player == CurrentPlayer);

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = ScoreOf(i);
            r.HighestTurn = 0;
            r.Average = r.DartsThrown == 0 ? 0 : Math.Round(_scored[i] * 3.0 / r.DartsThrown, 2);
            r.Extra["hit_rate"] = Math.Round(Rate(_hits, i), 4);
            r.Extra["segment_rate"] = Math.Round(Rate(_segmentHits, i), 4);
            r.Extra["target"] = _target.Score;
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
