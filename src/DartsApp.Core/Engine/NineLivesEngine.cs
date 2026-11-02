using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Nine Lives: Die Zahlen 1 bis 20 der Reihe nach treffen. Jede Aufnahme ohne Fortschritt kostet ein Leben,
/// wer keine Leben mehr hat, scheidet aus. Wer zuerst durch ist, gewinnt.
/// </summary>
public sealed class NineLivesEngine : GameEngine
{
    private const int Targets = 20;

    private int[] _progress = [];
    private int[] _lives = [];
    private int _progressAtTurnStart;

    public NineLivesEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (settings.KillerLives < 1) throw new ArgumentOutOfRangeException(nameof(settings), "Mindestens ein Leben.");
        Reset();
    }

    public int TargetOf(int player) => Math.Min(_progress[player] + 1, Targets);
    public int LivesOf(int player) => _lives[player];

    protected override void Reset()
    {
        _progress = new int[PlayerCount];
        _lives = Enumerable.Repeat(Settings.KillerLives, PlayerCount).ToArray();
    }

    protected override bool IsPlayerOut(int player) => _lives[player] <= 0;

    protected override void OnTurnStarted(Turn turn) => _progressAtTurnStart = _progress[turn.PlayerIndex];

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var me = turn.PlayerIndex;
        if (dart.Segment != TargetOf(me)) return false;

        _progress[me]++;
        turn.Score++;
        if (_progress[me] < Targets) return false;

        FinishGame(me);
        return true;
    }

    protected override void OnTurnCompleted(Turn turn)
    {
        var me = turn.PlayerIndex;
        if (IsFinished || _progress[me] > _progressAtTurnStart) return;

        _lives[me]--;
        turn.Note = _lives[me] > 0 ? "Leben verloren" : "Ausgeschieden";
        SetMessage(_lives[me] > 0 ? $"{Players[me].Name} verliert ein Leben ({_lives[me]} übrig)" : $"{Players[me].Name} ist ausgeschieden");

        if (_lives.All(l => l <= 0))
        {
            var best = Enumerable.Range(0, PlayerCount).OrderByDescending(p => _progress[p]).ToList();
            FinishGame(PlayerCount > 1 && _progress[best[0]] > _progress[best[1]] ? best[0] : null);
        }
    }

    public override IReadOnlySet<int> RelevantSegments => new HashSet<int> { TargetOf(CurrentPlayer) };
    public override Dart BotAim() => Dart.S(TargetOf(CurrentPlayer));

    public override string Status => $"Runde {Round} · {Enumerable.Range(0, PlayerCount).Count(p => !IsPlayerOut(p))} im Spiel";

    public override string? Hint => IsFinished
        ? null
        : $"Ziel: {TargetOf(CurrentPlayer)} – ohne Treffer in der Aufnahme geht ein Leben verloren";

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, TargetOf(player).ToString(), "Ziel",
            Details(("Leben", $"{Math.Max(0, _lives[player])}/{Settings.KillerLives}"), ("Fortschritt", $"{_progress[player]}/{Targets}")),
            IsActive: !IsFinished && player == CurrentPlayer,
            IsOut: IsPlayerOut(player));

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _progress[i];
            r.HighestTurn = 0;
            r.Extra["lives"] = Math.Max(0, _lives[i]);
            r.Extra["hit_rate"] = r.DartsThrown == 0 ? 0 : Math.Round((double)_progress[i] / r.DartsThrown, 4);
            results.Add(r);
        }

        AssignRanks(results, r => r.Score * 100 + r.Extra["lives"], Winner);
        return results;
    }
}
