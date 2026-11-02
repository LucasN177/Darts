using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Doubles Ladder: Der Reihe nach D1 bis D20 und Bullseye mit je einer Aufnahme (drei Darts).
/// Gezählt werden die Treffer; die Quote pro Doppel landet in der Statistik.
/// </summary>
public sealed class DoublesLadderEngine : GameEngine
{
    private static readonly int[] Ladder = Enumerable.Range(1, 20).Append(Dart.BullSegment).ToArray();
    public static int TotalRounds => Ladder.Length;

    private int[] _hits = [];
    private int[,] _hitsPerDouble = new int[0, 0];

    public DoublesLadderEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        Reset();
    }

    private int CurrentDouble => Ladder[Math.Min(Round, Ladder.Length) - 1];
    public int HitsOf(int player) => _hits[player];

    protected override void Reset()
    {
        _hits = new int[PlayerCount];
        _hitsPerDouble = new int[PlayerCount, Ladder.Length];
    }

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var target = Ladder[turn.Round - 1];
        if (dart.Segment != target || !dart.IsDouble) return false;

        _hits[turn.PlayerIndex]++;
        _hitsPerDouble[turn.PlayerIndex, turn.Round - 1]++;
        turn.Score++;
        return false;
    }

    protected override void OnRoundCompleted(int round)
    {
        if (round < Ladder.Length) return;
        var max = _hits.Max();
        var leaders = Enumerable.Range(0, PlayerCount).Where(p => _hits[p] == max).ToList();
        FinishGame(leaders.Count == 1 ? leaders[0] : null);
    }

    public override IReadOnlySet<int> RelevantSegments => new HashSet<int> { CurrentDouble };
    public override Dart BotAim() => CurrentDouble == Dart.BullSegment ? Dart.Bullseye : Dart.D(CurrentDouble);

    public override string Status => $"Runde {Math.Min(Round, Ladder.Length)} / {Ladder.Length}";

    public override string? Hint => IsFinished ? null : $"Ziel: {(CurrentDouble == Dart.BullSegment ? "Bullseye" : $"D{CurrentDouble}")}";

    private double Rate(int player)
    {
        var darts = TurnsOf(player).Sum(t => t.Darts.Count);
        return darts == 0 ? 0 : (double)_hits[player] / darts;
    }

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, _hits[player].ToString(), "Treffer",
            Details(("Quote", $"{Rate(player):P0}"), ("Darts", TurnsOf(player).Sum(t => t.Darts.Count).ToString())),
            IsActive: !IsFinished && player == CurrentPlayer);

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _hits[i];
            r.HighestTurn = 0;
            r.CheckoutAttempts = r.DartsThrown;
            r.CheckoutsHit = _hits[i];
            r.Extra["hit_rate"] = Math.Round(Rate(i), 4);
            for (var d = 0; d < Ladder.Length; d++)
                r.Extra[$"d{Ladder[d]}"] = _hitsPerDouble[i, d];
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
