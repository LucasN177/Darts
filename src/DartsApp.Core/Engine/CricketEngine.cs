using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Cricket auf 15-20 und Bull. Standard: Punkte gehen an den Werfer, gewonnen hat, wer alles
/// geschlossen hat und mindestens gleich viele Punkte hat. Cut Throat: Punkte gehen an die
/// Gegner, die das Feld noch offen haben, gewonnen hat, wer alles geschlossen hat und die
/// wenigsten Punkte hat.
/// </summary>
public sealed class CricketEngine : GameEngine
{
    public static readonly int[] Targets = [20, 19, 18, 17, 16, 15, Dart.BullSegment];
    private static readonly IReadOnlySet<int> TargetSet = new HashSet<int>(Targets);

    private int[,] _marks = new int[0, 0];
    private int[] _points = [];
    private int[] _totalMarks = [];

    public CricketEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        Reset();
    }

    public bool CutThroat => Settings.CutThroat;

    public int MarksOf(int player, int target) => _marks[player, Array.IndexOf(Targets, target)];
    public int PointsOf(int player) => _points[player];
    public bool IsClosedByAll(int target) => Enumerable.Range(0, PlayerCount).All(p => MarksOf(p, target) >= 3);

    protected override void Reset()
    {
        _marks = new int[PlayerCount, Targets.Length];
        _points = new int[PlayerCount];
        _totalMarks = new int[PlayerCount];
    }

    public override IReadOnlySet<int> RelevantSegments => TargetSet;

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var idx = Array.IndexOf(Targets, dart.Segment);
        if (idx < 0) return false;

        var player = turn.PlayerIndex;
        var hits = dart.Multiplier;
        var current = _marks[player, idx];
        var toClose = Math.Max(0, 3 - current);
        var closing = Math.Min(hits, toClose);
        var extra = hits - closing;

        _marks[player, idx] = current + closing;
        var effectiveMarks = closing;

        if (extra > 0)
        {
            var value = dart.Segment * extra;
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
            else if (Enumerable.Range(0, PlayerCount).Any(o => o != player && _marks[o, idx] < 3))
            {
                _points[player] += value;
                effectiveMarks += extra;
            }
            else if (PlayerCount == 1)
            {
                // Solo-Training: Punkte zählen trotzdem.
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
        for (var i = 0; i < Targets.Length; i++)
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
            var open = Targets.Where(t => MarksOf(CurrentPlayer, t) < 3).Select(Name).ToList();
            return open.Count == 0 ? "Alles geschlossen – Punkte sammeln!" : $"Offen: {string.Join(", ", open)}";
        }
    }

    private static string Name(int target) => target == Dart.BullSegment ? "Bull" : target.ToString();

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
            r.Extra["closed"] = Targets.Count(t => MarksOf(i, t) >= 3);
            results.Add(r);
        }

        AssignRanks(results, r => r.Extra["closed"] * 10_000 + (CutThroat ? -r.Score : r.Score) + r.Extra["marks"] * 0.001, Winner);
        return results;
    }
}
