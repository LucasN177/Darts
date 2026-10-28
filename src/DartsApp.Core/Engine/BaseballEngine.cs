using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Baseball: Neun Innings, im n-ten Inning zählt nur die Zahl n. Single = 1 Run, Double = 2, Triple = 3.
/// Die meisten Runs gewinnen.
/// </summary>
public sealed class BaseballEngine : GameEngine
{
    private int[] _runs = [];

    public BaseballEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (settings.Rounds is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(settings), "Baseball benötigt 1-20 Innings.");
        Reset();
    }

    public int Inning => Math.Min(Round, Settings.Rounds);
    public int RunsOf(int player) => _runs[player];

    protected override void Reset() => _runs = new int[PlayerCount];

    protected override bool OnDart(Turn turn, Dart dart)
    {
        if (dart.Segment != turn.Round) return false;
        _runs[turn.PlayerIndex] += dart.Multiplier;
        turn.Score += dart.Multiplier;
        return false;
    }

    protected override void OnRoundCompleted(int round)
    {
        if (round < Settings.Rounds) return;
        var max = _runs.Max();
        var leaders = Enumerable.Range(0, PlayerCount).Where(p => _runs[p] == max).ToList();
        FinishGame(leaders.Count == 1 ? leaders[0] : null);
    }

    public override IReadOnlySet<int> RelevantSegments => new HashSet<int> { Inning };
    public override Dart BotAim() => Dart.T(Inning);

    public override string Status => $"Inning {Inning} / {Settings.Rounds}";
    public override string? Hint => IsFinished ? null : $"Ziel: {Inning} (S = 1, D = 2, T = 3 Runs)";

    public override PlayerView GetView(int player)
    {
        var last = TurnsOf(player).LastOrDefault(t => t.IsComplete);
        return new PlayerView(Players[player].Name, _runs[player].ToString(), "Runs",
            Details(("Letztes Inning", last?.Score.ToString() ?? "–"),
                ("Darts", TurnsOf(player).Sum(t => t.Darts.Count).ToString())),
            IsActive: !IsFinished && player == CurrentPlayer);
    }

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _runs[i];
            var hits = TurnsOf(i).SelectMany(t => t.Darts.Where(d => d.Segment == t.Round)).Count();
            r.Extra["hit_rate"] = Math.Round((double)hits / Math.Max(1, r.DartsThrown), 4);
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
