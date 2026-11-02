using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Prisoner (Digitalversion): Die Zahlen 1 bis 20 der Reihe nach treffen. Ein Fehlwurf wird zum Gefangenen und
/// bleibt auf der aktuellen Zahl des Werfers liegen. Wer eine Zahl trifft, auf der Gefangene anderer Spieler sitzen,
/// befreit sie: Für jeden befreiten Gefangenen rückt der Befreier ein Feld vor und der Besitzer fällt eins zurück.
/// Eigene Gefangene werden durch einen Treffer einfach gerettet. Wer zuerst die 20 schafft, gewinnt.
/// </summary>
public sealed class PrisonerEngine : GameEngine
{
    private const int Targets = 20;

    private int[] _progress = [];
    private int[] _freed = [];
    private int[] _captured = [];
    /// <summary>Zahl → Besitzer der dort liegenden Gefangenen.</summary>
    private Dictionary<int, List<int>> _prisoners = [];

    public PrisonerEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        Reset();
    }

    public int TargetOf(int player) => Math.Min(_progress[player] + 1, Targets);
    public int ProgressOf(int player) => _progress[player];

    public IReadOnlyList<int> PrisonersOn(int number) =>
        _prisoners.TryGetValue(number, out var list) ? list : [];

    protected override void Reset()
    {
        _progress = new int[PlayerCount];
        _freed = new int[PlayerCount];
        _captured = new int[PlayerCount];
        _prisoners = [];
    }

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var me = turn.PlayerIndex;
        var target = TargetOf(me);

        if (dart.IsMiss)
        {
            if (!_prisoners.TryGetValue(target, out var list)) _prisoners[target] = list = [];
            list.Add(me);
            _captured[me]++;
            SetMessage($"Gefangener von {Players[me].Name} auf der {target}");
            return false;
        }

        // Gefangene auf der getroffenen Zahl befreien bzw. retten.
        if (dart.Segment <= Targets && _prisoners.Remove(dart.Segment, out var held))
        {
            foreach (var owner in held.Where(o => o != me))
            {
                _progress[owner] = Math.Max(0, _progress[owner] - 1);
                _progress[me] = Math.Min(Targets, _progress[me] + 1);
                _freed[me]++;
                turn.Score++;
            }
            if (held.Any(o => o != me)) SetMessage($"{Players[me].Name} befreit {held.Count(o => o != me)} Gefangene auf der {dart.Segment}");
        }

        if (dart.Segment == target && _progress[me] < Targets)
        {
            _progress[me]++;
            turn.Score++;
        }

        if (_progress[me] < Targets) return false;
        FinishGame(me);
        return true;
    }

    public override IReadOnlySet<int> RelevantSegments =>
        _prisoners.Where(kv => kv.Value.Any(o => o != CurrentPlayer)).Select(kv => kv.Key).Append(TargetOf(CurrentPlayer)).ToHashSet();

    public override Dart BotAim()
    {
        var me = CurrentPlayer;
        // Lohnende Befreiung (mindestens zwei fremde Gefangene) vor dem eigenen Ziel.
        var rescue = _prisoners
            .Select(kv => (Number: kv.Key, Count: kv.Value.Count(o => o != me)))
            .Where(x => x.Count >= 2)
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();
        return Dart.S(rescue.Count >= 2 ? rescue.Number : TargetOf(me));
    }

    public override string Status => $"Runde {Round} · {_prisoners.Sum(kv => kv.Value.Count)} Gefangene";

    public override string? Hint
    {
        get
        {
            if (IsFinished) return null;
            var others = _prisoners
                .Where(kv => kv.Value.Any(o => o != CurrentPlayer))
                .Select(kv => $"{kv.Key} ({kv.Value.Count(o => o != CurrentPlayer)})")
                .ToList();
            return $"Ziel: {TargetOf(CurrentPlayer)}" + (others.Count > 0 ? $" · Gefangene befreien auf {string.Join(", ", others)}" : "");
        }
    }

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, TargetOf(player).ToString(), "Ziel",
            Details(("Fortschritt", $"{_progress[player]}/{Targets}"),
                ("Befreit", _freed[player].ToString()),
                ("Gefangen", _prisoners.Sum(kv => kv.Value.Count(o => o == player)).ToString())),
            IsActive: !IsFinished && player == CurrentPlayer);

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _progress[i];
            r.HighestTurn = 0;
            r.Extra["freed"] = _freed[i];
            r.Extra["captured"] = _captured[i];
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
