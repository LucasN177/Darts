using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>Around the Clock: der Reihe nach 1 bis 20 (und optional Bull) treffen.</summary>
public sealed class AroundTheClockEngine : GameEngine
{
    private int[] _targetIndex = [];
    private int[] _hits = [];
    private readonly int[] _targets;

    public AroundTheClockEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        _targets = Enumerable.Range(1, 20).Concat(settings.ClockIncludeBull ? [Dart.BullSegment] : []).ToArray();
        Reset();
    }

    public int TargetCount => _targets.Length;
    public int ProgressOf(int player) => _targetIndex[player];
    public int? TargetOf(int player) => _targetIndex[player] < _targets.Length ? _targets[_targetIndex[player]] : null;

    protected override void Reset()
    {
        _targetIndex = new int[PlayerCount];
        _hits = new int[PlayerCount];
    }

    public override IReadOnlySet<int>? RelevantSegments =>
        TargetOf(CurrentPlayer) is { } t ? new HashSet<int> { t } : null;

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var player = turn.PlayerIndex;
        if (TargetOf(player) is not { } target || dart.Segment != target) return false;

        var counts = Settings.ClockHit switch
        {
            AroundTheClockHit.DoubleOnly => dart.IsDouble,
            // Bull hat kein Triple: dort zählt jeder Bull-Treffer.
            AroundTheClockHit.TripleOnly => dart.IsTriple || dart.IsBull,
            _ => true
        };
        if (!counts) return false;

        var step = Settings.ClockHit == AroundTheClockHit.Any && Settings.ClockMultiplierSkips ? dart.Multiplier : 1;
        _targetIndex[player] = Math.Min(_targets.Length, _targetIndex[player] + step);
        _hits[player]++;
        turn.Score++;

        if (_targetIndex[player] >= _targets.Length)
        {
            FinishGame(player);
            return true;
        }

        return false;
    }

    public override string Status => $"Runde {Round}";

    public override string? Hint => IsFinished || TargetOf(CurrentPlayer) is not { } t
        ? null
        : $"Ziel: {Settings.ClockHit switch { AroundTheClockHit.DoubleOnly => "D", AroundTheClockHit.TripleOnly when t != Dart.BullSegment => "T", _ => "" }}{(t == Dart.BullSegment ? "Bull" : t.ToString())}";

    private double HitRate(int player)
    {
        var darts = TurnsOf(player).Sum(t => t.Darts.Count);
        return darts == 0 ? 0 : (double)_hits[player] / darts;
    }

    public override PlayerView GetView(int player)
    {
        var target = TargetOf(player);
        return new PlayerView(Players[player].Name,
            target is null ? "✓" : target == Dart.BullSegment ? "Bull" : target.Value.ToString(), "Ziel",
            Details(("Fortschritt", $"{_targetIndex[player]}/{_targets.Length}"),
                ("Trefferquote", $"{HitRate(player):P0}"),
                ("Darts", TurnsOf(player).Sum(t => t.Darts.Count).ToString())),
            IsActive: !IsFinished && player == CurrentPlayer);
    }

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _targetIndex[i];
            r.Extra["hit_rate"] = Math.Round(HitRate(i), 4);
            r.Extra["hits"] = _hits[i];
            r.Extra["targets"] = _targets.Length;
            results.Add(r);
        }

        AssignRanks(results, r => r.Score - r.DartsThrown * 0.0001, Winner);
        return results;
    }
}
