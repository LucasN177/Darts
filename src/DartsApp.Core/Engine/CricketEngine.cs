using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>Ein Cricket-Feld: Zahl, Bull oder (Mickey Mouse) beliebiges Doppel/Triple.</summary>
/// <param name="Key">Zahl (1-20), 25 für Bull, <see cref="CricketEngine.DoublesKey"/> bzw. <see cref="CricketEngine.TriplesKey"/>.</param>
/// <param name="Marks">Marks, die ein Dart auf diesem Feld bringt (0 = trifft das Feld nicht).</param>
/// <param name="Value">Punkte für einen Dart, der überzählige Marks (zweiter Parameter) bringt.</param>
public sealed record CricketTarget(int Key, string Label, Func<Dart, int> Marks, Func<Dart, int, int> Value, Dart Aim, IReadOnlySet<int> Segments);

/// <summary>
/// Cricket in den Varianten Standard (15-20, Bull), Tactics (10-20, Bull), Random (sechs zufällige Zahlen, Bull)
/// und Mickey Mouse (12-20, Doppel, Triple, Bull). Standard: Punkte gehen an den Werfer, gewonnen hat, wer alles
/// geschlossen hat und mindestens gleich viele Punkte hat. Cut Throat: Punkte gehen an die Gegner, die das Feld
/// noch offen haben, gewonnen hat, wer alles geschlossen hat und die wenigsten Punkte hat.
/// </summary>
public sealed class CricketEngine : GameEngine
{
    public const int DoublesKey = -2;
    public const int TriplesKey = -3;

    private static readonly IReadOnlySet<int> AllSegments = Enumerable.Range(1, 20).Append(Dart.BullSegment).ToHashSet();

    private readonly CricketTarget[] _targets;
    private readonly IReadOnlySet<int> _relevant;
    private int[,] _marks = new int[0, 0];
    private int[] _points = [];
    private int[] _totalMarks = [];

    public CricketEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        _targets = BuildTargets(settings);
        _relevant = _targets.SelectMany(t => t.Segments).ToHashSet();
        Reset();
    }

    public IReadOnlyList<CricketTarget> Targets => _targets;
    public bool CutThroat => Settings.CutThroat;

    /// <summary>Die sechs Zahlen bei "Random", abhängig vom Seed der Partie.</summary>
    public static int[] RandomNumbers(int seed)
    {
        var random = new Random(seed);
        return Enumerable.Range(1, 20).OrderBy(_ => random.Next()).Take(6).OrderDescending().ToArray();
    }

    private static CricketTarget[] BuildTargets(GameSettings settings)
    {
        IEnumerable<int> numbers = settings.CricketVariant switch
        {
            CricketVariant.Tactics => Enumerable.Range(10, 11).Reverse(),
            CricketVariant.Random => RandomNumbers(settings.Seed),
            CricketVariant.MickeyMouse => Enumerable.Range(12, 9).Reverse(),
            _ => Enumerable.Range(15, 6).Reverse()
        };

        var targets = numbers.Select(Number).ToList();
        if (settings.CricketVariant == CricketVariant.MickeyMouse)
        {
            targets.Add(new CricketTarget(DoublesKey, "Doppel", d => d.IsDouble ? 1 : 0, (d, _) => d.Score, Dart.D(20), AllSegments));
            targets.Add(new CricketTarget(TriplesKey, "Triple", d => d.IsTriple ? 1 : 0, (d, _) => d.Score, Dart.T(20), AllSegments));
        }

        targets.Add(new CricketTarget(Dart.BullSegment, "Bull", d => d.IsBull ? d.Multiplier : 0, (_, extra) => Dart.BullSegment * extra,
            Dart.Bullseye, new HashSet<int> { Dart.BullSegment }));
        return targets.ToArray();
    }

    private static CricketTarget Number(int n) =>
        new(n, n.ToString(), d => d.Segment == n ? d.Multiplier : 0, (_, extra) => n * extra, Dart.T(n), new HashSet<int> { n });

    private int IndexOf(int key) => Array.FindIndex(_targets, t => t.Key == key);

    public int MarksOf(int player, int key) => _marks[player, IndexOf(key)];
    public int PointsOf(int player) => _points[player];
    public bool IsClosedByAll(int key) => Enumerable.Range(0, PlayerCount).All(p => MarksOf(p, key) >= 3);

    protected override void Reset()
    {
        _marks = new int[PlayerCount, _targets.Length];
        _points = new int[PlayerCount];
        _totalMarks = new int[PlayerCount];
    }

    public override IReadOnlySet<int> RelevantSegments => _relevant;

    private bool AnyOpponentOpen(int player, int idx) =>
        Enumerable.Range(0, PlayerCount).Any(o => o != player && _marks[o, idx] < 3);

    /// <summary>
    /// Feld, auf das ein Dart angerechnet wird. Ein Dart kann bei Mickey Mouse mehrere Felder treffen
    /// (z.B. D20 = "20" und "Doppel"); gewählt wird das erste, das dem Werfer noch etwas bringt.
    /// </summary>
    private int ChooseTarget(int player, Dart dart)
    {
        var candidates = Enumerable.Range(0, _targets.Length).Where(i => _targets[i].Marks(dart) > 0).ToList();
        if (candidates.Count == 0) return -1;

        foreach (var i in candidates)
            if (_marks[player, i] < 3) return i;
        foreach (var i in candidates)
            if (AnyOpponentOpen(player, i) || PlayerCount == 1) return i;
        return candidates[0];
    }

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var player = turn.PlayerIndex;
        var idx = ChooseTarget(player, dart);
        if (idx < 0) return false;

        var target = _targets[idx];
        var hits = target.Marks(dart);
        var current = _marks[player, idx];
        var closing = Math.Min(hits, Math.Max(0, 3 - current));
        var extra = hits - closing;

        _marks[player, idx] = current + closing;
        var effectiveMarks = closing;

        if (extra > 0)
        {
            var value = target.Value(dart, extra);
            if (CutThroat)
            {
                var anyOpen = false;
                for (var o = 0; o < PlayerCount; o++)
                {
                    if (o == player || _marks[o, idx] >= 3) continue;
                    _points[o] += value;
                    anyOpen = true;
                }
                if (anyOpen) effectiveMarks += extra;
            }
            else if (AnyOpponentOpen(player, idx) || PlayerCount == 1)
            {
                // Solo-Training: Punkte zählen auch ohne Gegner.
                _points[player] += value;
                effectiveMarks += extra;
            }
        }

        _totalMarks[player] += effectiveMarks;
        turn.Score += effectiveMarks;

        if (HasWon(player))
        {
            FinishGame(player);
            return true;
        }

        return false;
    }

    private bool AllClosed(int player)
    {
        for (var i = 0; i < _targets.Length; i++)
            if (_marks[player, i] < 3) return false;
        return true;
    }

    private bool HasWon(int player)
    {
        if (!AllClosed(player)) return false;
        for (var o = 0; o < PlayerCount; o++)
        {
            if (o == player) continue;
            if (CutThroat ? _points[player] > _points[o] : _points[player] < _points[o]) return false;
        }
        return true;
    }

    protected override void OnRoundCompleted(int round)
    {
        if (Settings.Rounds <= 0 || round < Settings.Rounds) return;

        // Rundenlimit erreicht: Punkte, dann Marks entscheiden.
        var best = Enumerable.Range(0, PlayerCount)
            .OrderBy(p => CutThroat ? _points[p] : -_points[p])
            .ThenByDescending(p => _totalMarks[p])
            .First();
        FinishGame(best);
    }

    public override string Status => Settings.Rounds > 0 ? $"Runde {Round} / {Settings.Rounds}" : $"Runde {Round}";

    public override string? Hint
    {
        get
        {
            if (IsFinished) return null;
            var open = _targets.Where(t => MarksOf(CurrentPlayer, t.Key) < 3).Select(t => t.Label).ToList();
            return open.Count == 0 ? "Alles geschlossen – Punkte sammeln!" : $"Offen: {string.Join(", ", open)}";
        }
    }

    public override Dart BotAim()
    {
        var me = CurrentPlayer;
        var opponents = Enumerable.Range(0, PlayerCount).Where(o => o != me).ToList();

        // Felder, die ich geschlossen habe und mindestens ein Gegner noch offen hat.
        var scoring = _targets.Where(t => MarksOf(me, t.Key) >= 3 && opponents.Any(o => MarksOf(o, t.Key) < 3)).ToList();
        var open = _targets.Where(t => MarksOf(me, t.Key) < 3 && !IsClosedByAll(t.Key)).ToList();

        var behind = opponents.Count > 0 && (CutThroat
            ? opponents.Any(o => _points[o] <= _points[me])
            : opponents.Any(o => _points[o] >= _points[me]));

        // Zurückliegend wird gepunktet, sonst das höchste offene Feld geschlossen.
        if (behind && scoring.Count > 0) return scoring[0].Aim;
        if (open.Count > 0) return open[0].Aim;
        if (scoring.Count > 0) return scoring[0].Aim;
        return Dart.T(20);
    }

    private double Mpr(int player)
    {
        var darts = TurnsOf(player).Sum(t => t.Darts.Count);
        return darts == 0 ? 0 : _totalMarks[player] * 3.0 / darts;
    }

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, _points[player].ToString(), "Punkte",
            Details(("MPR", Fmt(Mpr(player))), ("Marks", _totalMarks[player].ToString())),
            IsActive: !IsFinished && player == CurrentPlayer);

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _points[i];
            r.Average = Math.Round(Mpr(i), 2);
            r.Extra["mpr"] = Math.Round(Mpr(i), 2);
            r.Extra["marks"] = _totalMarks[i];
            r.Extra["closed"] = _targets.Count(t => MarksOf(i, t.Key) >= 3);
            results.Add(r);
        }

        AssignRanks(results, r => r.Extra["closed"] * 10_000 + (CutThroat ? -r.Score : r.Score) + r.Extra["marks"] * 0.001, Winner);
        return results;
    }
}
