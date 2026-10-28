using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Bob's 27 (Doppeltraining): Start mit 27 Punkten, Runde für Runde D1 bis D20 und Bull.
/// Jeder Treffer bringt den Doppelwert, eine Aufnahme ohne Treffer kostet den Doppelwert.
/// Optional scheidet aus, wer auf 0 oder darunter fällt.
/// </summary>
public sealed class Bobs27Engine : GameEngine
{
    private const int StartPoints = 27;
    private static readonly int[] Targets = Enumerable.Range(1, 20).Append(Dart.BullSegment).ToArray();

    private int[] _points = [];
    private int[] _hits = [];
    private bool[] _out = [];

    public Bobs27Engine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        Reset();
    }

    public static int TotalRounds => Targets.Length;
    public int Target => Targets[Math.Min(Round, Targets.Length) - 1];
    public int PointsOf(int player) => _points[player];

    protected override void Reset()
    {
        _points = Enumerable.Repeat(StartPoints, PlayerCount).ToArray();
        _hits = new int[PlayerCount];
        _out = new bool[PlayerCount];
    }

    protected override bool IsPlayerOut(int player) => _out[player];

    public override IReadOnlySet<int> RelevantSegments => new HashSet<int> { Target };

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var target = Targets[turn.Round - 1];
        if (dart.Segment == target && dart.IsDouble)
        {
            _points[turn.PlayerIndex] += dart.Score;
            _hits[turn.PlayerIndex]++;
            turn.Score += dart.Score;
        }
        return false;
    }

    protected override void OnTurnCompleted(Turn turn)
    {
        if (turn.Score > 0) return;

        var target = Targets[turn.Round - 1];
        var penalty = target * 2;
        _points[turn.PlayerIndex] -= penalty;
        turn.Score = -penalty;

        if (Settings.Bobs27Elimination && _points[turn.PlayerIndex] <= 0)
        {
            _out[turn.PlayerIndex] = true;
            turn.Note = "Ausgeschieden";
            SetMessage($"{Players[turn.PlayerIndex].Name} ist ausgeschieden");

            if (_out.All(o => o))
                FinishGame(PlayerCount == 1 ? null : BestPlayer());
        }
    }

    protected override void OnRoundCompleted(int round)
    {
        if (round >= Targets.Length) FinishGame(BestPlayer());
    }

    private int? BestPlayer()
    {
        var ranking = Enumerable.Range(0, PlayerCount)
            .OrderByDescending(p => _out[p] ? int.MinValue : _points[p])
            .ThenByDescending(p => _hits[p])
            .ToList();
        if (PlayerCount == 1) return _out[0] ? null : 0;
        return ranking[0];
    }

    public override string Status => $"Runde {Math.Min(Round, Targets.Length)} / {Targets.Length}";

    public override string? Hint => IsFinished ? null : $"Ziel: {(Target == Dart.BullSegment ? "Bullseye" : $"D{Target}")}";

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, _points[player].ToString(), "Punkte",
            Details(("Treffer", _hits[player].ToString()),
                ("Quote", $"{HitRate(player):P0}")),
            IsActive: !IsFinished && player == CurrentPlayer,
            IsOut: _out[player]);

    private double HitRate(int player)
    {
        var darts = TurnsOf(player).Sum(t => t.Darts.Count);
        return darts == 0 ? 0 : (double)_hits[player] / darts;
    }

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _points[i];
            r.CheckoutAttempts = r.DartsThrown;
            r.CheckoutsHit = _hits[i];
            r.Extra["hit_rate"] = Math.Round(HitRate(i), 4);
            r.Extra["eliminated"] = _out[i] ? 1 : 0;
            results.Add(r);
        }

        AssignRanks(results, r => (r.Extra["eliminated"] > 0 ? -1_000_000 : 0) + r.Score, Winner);
        return results;
    }
}
