using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Shanghai: In Runde n zählt nur die Zahl n. Wer in einer Aufnahme Single, Double und
/// Triple der Zahl trifft ("Shanghai"), gewinnt sofort. Sonst gewinnt die höchste Punktzahl.
/// </summary>
public sealed class ShanghaiEngine : GameEngine
{
    private int[] _points = [];
    private int[] _hits = [];

    public ShanghaiEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (settings.Rounds is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(settings), "Shanghai benötigt 1-20 Runden.");
        Reset();
    }

    public int Target => Math.Min(Round, Settings.Rounds);
    public int PointsOf(int player) => _points[player];

    protected override void Reset()
    {
        _points = new int[PlayerCount];
        _hits = new int[PlayerCount];
    }

    public override IReadOnlySet<int> RelevantSegments => new HashSet<int> { Target };

    protected override bool OnDart(Turn turn, Dart dart)
    {
        if (dart.Segment != turn.Round) return false;

        _points[turn.PlayerIndex] += dart.Score;
        _hits[turn.PlayerIndex]++;
        turn.Score += dart.Score;

        var multipliers = turn.Darts.Where(d => d.Segment == turn.Round).Select(d => d.Multiplier).ToHashSet();
        if (multipliers.SetEquals([1, 2, 3]))
        {
            turn.Note = "Shanghai!";
            FinishGame(turn.PlayerIndex);
            SetMessage($"Shanghai! {Players[turn.PlayerIndex].Name} gewinnt sofort.");
            return true;
        }

        return false;
    }

    protected override void OnRoundCompleted(int round)
    {
        if (round < Settings.Rounds) return;
        var max = _points.Max();
        var leaders = Enumerable.Range(0, PlayerCount).Where(p => _points[p] == max).ToList();
        FinishGame(leaders.Count == 1 ? leaders[0] : null);
    }

    public override Dart BotAim()
    {
        // Für einen möglichen Shanghai fehlende Felder bevorzugen, Triple zuerst.
        var hit = CurrentTurn?.Darts.Where(d => d.Segment == Target).Select(d => d.Multiplier).ToHashSet() ?? [];
        var multiplier = new[] { 3, 2, 1 }.FirstOrDefault(m => !hit.Contains(m), 3);
        return new Dart(Target, multiplier);
    }

    public override string Status => $"Runde {Target} / {Settings.Rounds}";
    public override string? Hint => IsFinished ? null : $"Ziel: {Target} (S, D und T = Shanghai)";

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, _points[player].ToString(), "Punkte",
            Details(("Treffer", _hits[player].ToString()),
                ("Darts", TurnsOf(player).Sum(t => t.Darts.Count).ToString())),
            IsActive: !IsFinished && player == CurrentPlayer);

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _points[i];
            var darts = Math.Max(1, r.DartsThrown);
            r.Extra["hit_rate"] = Math.Round((double)_hits[i] / darts, 4);
            r.Extra["shanghai"] = TurnsOf(i).Any(t => t.Note == "Shanghai!") ? 1 : 0;
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
