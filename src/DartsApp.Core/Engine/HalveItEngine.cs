using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Halve-It: Neun Runden mit festen Zielen (15, 16, beliebiges Doppel, 17, 18, beliebiges Triple, 19, 20, Bull).
/// Treffer zählen ihren Wert; wer in einer Runde nichts trifft, verliert die Hälfte seiner Punkte.
/// </summary>
public sealed class HalveItEngine : GameEngine
{
    public const int StartPoints = 40;

    private sealed record Target(string Label, Func<Dart, bool> Hits, Dart Aim, IReadOnlySet<int> Segments);

    private static readonly IReadOnlySet<int> AllNumbers = Enumerable.Range(1, 20).ToHashSet();

    private static readonly Target[] Targets =
    [
        Number(15),
        Number(16),
        new("Doppel", d => d.IsDouble, Dart.D(20), AllNumbers.Append(Dart.BullSegment).ToHashSet()),
        Number(17),
        Number(18),
        new("Triple", d => d.IsTriple, Dart.T(20), AllNumbers),
        Number(19),
        Number(20),
        new("Bull", d => d.IsBull, Dart.Bullseye, new HashSet<int> { Dart.BullSegment })
    ];

    private static Target Number(int n) => new(n.ToString(), d => d.Segment == n, Dart.T(n), new HashSet<int> { n });

    private int[] _points = [];
    private int[] _halved = [];

    public HalveItEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        Reset();
    }

    public static int TotalRounds => Targets.Length;
    private Target CurrentTarget => Targets[Math.Min(Round, Targets.Length) - 1];
    public string TargetLabel => CurrentTarget.Label;
    public int PointsOf(int player) => _points[player];

    protected override void Reset()
    {
        _points = Enumerable.Repeat(StartPoints, PlayerCount).ToArray();
        _halved = new int[PlayerCount];
    }

    protected override bool OnDart(Turn turn, Dart dart)
    {
        if (!Targets[turn.Round - 1].Hits(dart)) return false;
        _points[turn.PlayerIndex] += dart.Score;
        turn.Score += dart.Score;
        return false;
    }

    protected override void OnTurnCompleted(Turn turn)
    {
        if (turn.Score > 0) return;
        var p = turn.PlayerIndex;
        var before = _points[p];
        _points[p] /= 2;
        _halved[p]++;
        turn.Score = _points[p] - before;
        turn.Note = "Halbiert";
        SetMessage($"{Players[p].Name}: halbiert auf {_points[p]}");
    }

    protected override void OnRoundCompleted(int round)
    {
        if (round < Targets.Length) return;
        var max = _points.Max();
        var leaders = Enumerable.Range(0, PlayerCount).Where(p => _points[p] == max).ToList();
        FinishGame(leaders.Count == 1 ? leaders[0] : null);
    }

    public override IReadOnlySet<int> RelevantSegments => CurrentTarget.Segments;
    public override Dart BotAim() => CurrentTarget.Aim;

    public override string Status => $"Runde {Math.Min(Round, Targets.Length)} / {Targets.Length}";
    public override string? Hint => IsFinished ? null : $"Ziel: {CurrentTarget.Label} – nichts getroffen = Punkte halbiert";

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, _points[player].ToString(), "Punkte",
            Details(("Halbiert", _halved[player].ToString()),
                ("Letzte", TurnsOf(player).LastOrDefault(t => t.IsComplete) is { } t ? (t.Score > 0 ? $"+{t.Score}" : t.Score.ToString()) : "–")),
            IsActive: !IsFinished && player == CurrentPlayer);

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _points[i];
            r.Extra["halved"] = _halved[i];
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
