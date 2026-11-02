using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Football: Wer das Bull trifft, bekommt den Ball. Mit Ballbesitz zählt jedes Doppel als Tor; ein Gegner holt sich
/// den Ball mit einem Bull-Treffer zurück. Wer zuerst die eingestellte Torzahl erreicht, gewinnt.
/// </summary>
public sealed class FootballEngine : GameEngine
{
    private static readonly IReadOnlySet<int> BullOnly = new HashSet<int> { Dart.BullSegment };
    private static readonly IReadOnlySet<int> Numbers = Enumerable.Range(1, 20).ToHashSet();

    private int[] _goals = [];
    private int[] _steals = [];
    private int? _possession;

    public FootballEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (settings.FootballGoals < 1) throw new ArgumentOutOfRangeException(nameof(settings), "Mindestens ein Tor.");
        Reset();
    }

    public int? Possession => _possession;
    public int GoalsOf(int player) => _goals[player];

    protected override void Reset()
    {
        _goals = new int[PlayerCount];
        _steals = new int[PlayerCount];
        _possession = null;
    }

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var me = turn.PlayerIndex;

        if (dart.IsBull)
        {
            if (_possession == me) return false;
            _possession = me;
            _steals[me]++;
            turn.Note = "Ballbesitz";
            SetMessage($"{Players[me].Name} hat den Ball");
            return false;
        }

        if (_possession != me || !dart.IsDouble) return false;

        _goals[me]++;
        turn.Score++;
        turn.Note = "Tor!";
        SetMessage($"Tor für {Players[me].Name}! ({_goals[me]}/{Settings.FootballGoals})");

        if (_goals[me] < Settings.FootballGoals) return false;
        FinishGame(me);
        return true;
    }

    public override IReadOnlySet<int> RelevantSegments => _possession == CurrentPlayer ? Numbers : BullOnly;

    public override Dart BotAim() => _possession == CurrentPlayer ? Dart.D(20) : Dart.Bullseye;

    public override string Status => $"Runde {Round} · bis {Settings.FootballGoals} Tore";

    public override string? Hint => IsFinished
        ? null
        : _possession == CurrentPlayer
            ? "Du hast den Ball – jedes Doppel ist ein Tor"
            : "Triff das Bull, um den Ball zu bekommen";

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, _goals[player].ToString(), "Tore",
            Details(("Ballgewinne", _steals[player].ToString()),
                ("Darts", TurnsOf(player).Sum(t => t.Darts.Count).ToString())),
            IsActive: !IsFinished && player == CurrentPlayer,
            Badge: _possession == player ? "Ballbesitz" : null);

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _goals[i];
            r.Extra["steals"] = _steals[i];
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
