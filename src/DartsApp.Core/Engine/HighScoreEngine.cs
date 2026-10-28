using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>High Score: eine feste Anzahl Runden, die meisten Punkte gewinnen.</summary>
public sealed class HighScoreEngine : GameEngine
{
    private int[] _points = [];

    public HighScoreEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (settings.Rounds < 1) throw new ArgumentOutOfRangeException(nameof(settings), "Mindestens eine Runde.");
        Reset();
    }

    public int PointsOf(int player) => _points[player];

    protected override void Reset() => _points = new int[PlayerCount];

    protected override bool OnDart(Turn turn, Dart dart)
    {
        _points[turn.PlayerIndex] += dart.Score;
        turn.Score += dart.Score;
        return false;
    }

    protected override void OnRoundCompleted(int round)
    {
        if (round < Settings.Rounds) return;
        var max = _points.Max();
        var leaders = Enumerable.Range(0, PlayerCount).Where(p => _points[p] == max).ToList();
        FinishGame(leaders.Count == 1 ? leaders[0] : null);
    }

    public override string Status => $"Runde {Math.Min(Round, Settings.Rounds)} / {Settings.Rounds}";

    private double Average(int player)
    {
        var darts = TurnsOf(player).Sum(t => t.Darts.Count);
        return darts == 0 ? 0 : _points[player] * 3.0 / darts;
    }

    public override PlayerView GetView(int player)
    {
        var last = TurnsOf(player).LastOrDefault(t => t.IsComplete);
        return new PlayerView(Players[player].Name, _points[player].ToString(), "Punkte",
            Details(("Ø", Fmt(Average(player))), ("Letzte", last?.Score.ToString() ?? "–")),
            IsActive: !IsFinished && player == CurrentPlayer);
    }

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var turns = TurnsOf(i).ToList();
            var r = BaseResult(i);
            r.Score = _points[i];
            r.Average = Math.Round(Average(i), 2);
            r.Count180 = turns.Count(t => t.Score == 180);
            r.Count140 = turns.Count(t => t.Score is >= 140 and < 180);
            r.Count100 = turns.Count(t => t.Score is >= 100 and < 140);
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
