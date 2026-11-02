using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Scram (genau zwei Spieler): Ein Spieler ist Stopper und schließt Felder mit einem Treffer, der andere ist
/// Scorer und punktet auf allen noch offenen Feldern. Sind alle Felder (1-20 und Bull) zu, werden die Rollen
/// getauscht. Nach beiden Hälften gewinnt die höhere Punktzahl.
/// </summary>
public sealed class ScramEngine : GameEngine
{
    /// <summary>Reihenfolge, in der der Stopper schließt: wertvolle Felder zuerst.</summary>
    private static readonly int[] StopperOrder = [20, 19, 18, 17, 16, Dart.BullSegment, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1];
    public static readonly int[] Fields = Enumerable.Range(1, 20).Reverse().Append(Dart.BullSegment).ToArray();

    private HashSet<int> _closed = [];
    private int[] _points = [];
    private int[] _closedAsStopper = [];
    private int _half = 1;

    public ScramEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (players.Count != 2) throw new ArgumentException("Scram wird mit genau zwei Spielern gespielt.", nameof(players));
        Reset();
    }

    public int Half => _half;
    /// <summary>In Hälfte 1 ist Spieler 1 Stopper, in Hälfte 2 Spieler 2.</summary>
    public int Stopper => _half == 1 ? 0 : 1;
    public int Scorer => 1 - Stopper;
    public bool IsClosed(int field) => _closed.Contains(field);
    public int PointsOf(int player) => _points[player];

    protected override void Reset()
    {
        _closed = [];
        _points = new int[2];
        _closedAsStopper = new int[2];
        _half = 1;
    }

    protected override bool OnDart(Turn turn, Dart dart)
    {
        if (dart.IsMiss || _closed.Contains(dart.Segment)) return false;

        if (turn.PlayerIndex == Scorer)
        {
            _points[Scorer] += dart.Score;
            turn.Score += dart.Score;
            return false;
        }

        _closed.Add(dart.Segment);
        _closedAsStopper[Stopper]++;
        turn.Score++;

        if (_closed.Count < Fields.Length) return false;

        if (_half == 1)
        {
            SetMessage($"Halbzeit! {Players[1].Name} ist jetzt Stopper.");
            turn.Note = "Halbzeit";
            _half = 2;
            _closed = [];
            StartNewLeg(1, newSet: false);
        }
        else
        {
            FinishGame(_points[0] == _points[1] ? null : _points[0] > _points[1] ? 0 : 1);
        }

        return true;
    }

    public override IReadOnlySet<int> RelevantSegments => Fields.Where(f => !_closed.Contains(f)).ToHashSet();

    public override Dart BotAim()
    {
        if (CurrentPlayer == Stopper)
        {
            var field = StopperOrder.First(f => !_closed.Contains(f));
            // Zum Schließen reicht ein Treffer: aufs große Single-Feld zielen.
            return field == Dart.BullSegment ? Dart.Bullseye : Dart.S(field);
        }

        var best = Fields.Where(f => f != Dart.BullSegment && !_closed.Contains(f)).DefaultIfEmpty(Dart.BullSegment).First();
        return best == Dart.BullSegment ? Dart.Bullseye : Dart.T(best);
    }

    public override string Status => $"Hälfte {_half} / 2 · {Fields.Length - _closed.Count} Felder offen";

    public override string? Hint
    {
        get
        {
            if (IsFinished) return null;
            var open = Fields.Where(f => !_closed.Contains(f)).Select(f => f == Dart.BullSegment ? "Bull" : f.ToString());
            return CurrentPlayer == Stopper
                ? $"Stopper: ein Treffer schließt ein Feld – offen: {string.Join(", ", open)}"
                : "Scorer: punkte auf allen offenen Feldern";
        }
    }

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, _points[player].ToString(), "Punkte",
            Details(("Geschlossen", _closedAsStopper[player].ToString()),
                ("Darts", TurnsOf(player).Sum(t => t.Darts.Count).ToString())),
            IsActive: !IsFinished && player == CurrentPlayer,
            Badge: IsFinished ? null : player == Stopper ? "Stopper" : "Scorer");

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < 2; i++)
        {
            var r = BaseResult(i);
            r.Score = _points[i];
            r.Extra["closed"] = _closedAsStopper[i];
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
