using DartsApp.Core.Bots;
using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Hare and Hounds: Spieler 1 ist der Hase und startet auf der 20, die Hunde starten zwei Felder dahinter.
/// Alle laufen im Uhrzeigersinn ums Board; jeder Treffer auf das eigene Feld bringt ein Feld weiter.
/// Der Hase gewinnt, wenn er einmal herum wieder an der 20 ankommt; ein Hund gewinnt, wenn er den Hasen einholt.
/// </summary>
public sealed class HareAndHoundsEngine : GameEngine
{
    public const int Hare = 0;
    public const int HoundHeadStart = 2;
    private const int Lap = 20;

    private int[] _position = [];

    public HareAndHoundsEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (players.Count < 2) throw new ArgumentException("Hare and Hounds braucht mindestens zwei Spieler.", nameof(players));
        Reset();
    }

    public int PositionOf(int player) => _position[player];
    public int TargetOf(int player) => DartBoard.Order[((_position[player] % Lap) + Lap) % Lap];

    /// <summary>Felder Vorsprung des Hasen vor diesem Hund.</summary>
    public int GapTo(int hound) => _position[Hare] - _position[hound];

    protected override void Reset()
    {
        _position = Enumerable.Range(0, PlayerCount).Select(p => p == Hare ? 0 : -HoundHeadStart).ToArray();
    }

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var me = turn.PlayerIndex;
        if (dart.IsMiss || dart.IsBull || dart.Segment != TargetOf(me)) return false;

        _position[me]++;
        turn.Score++;

        if (me == Hare && _position[Hare] >= Lap)
        {
            FinishGame(Hare);
            SetMessage($"{Players[Hare].Name} ist entkommen!");
            return true;
        }

        if (me != Hare && _position[me] >= _position[Hare])
        {
            FinishGame(me);
            SetMessage($"{Players[me].Name} hat den Hasen gefangen!");
            return true;
        }

        return false;
    }

    public override IReadOnlySet<int> RelevantSegments => new HashSet<int> { TargetOf(CurrentPlayer) };

    public override Dart BotAim() => Dart.S(TargetOf(CurrentPlayer));

    public override string Status => $"Runde {Round}";

    public override string? Hint
    {
        get
        {
            if (IsFinished) return null;
            var me = CurrentPlayer;
            return me == Hare
                ? $"Hase: Ziel {TargetOf(me)} – noch {Lap - _position[me]} Felder bis zur 20"
                : $"Hund: Ziel {TargetOf(me)} – {GapTo(me)} Felder hinter dem Hasen";
        }
    }

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, TargetOf(player).ToString(), "Ziel",
            player == Hare
                ? Details(("Bis zur 20", (Lap - _position[player]).ToString()))
                : Details(("Abstand zum Hasen", GapTo(player).ToString())),
            IsActive: !IsFinished && player == CurrentPlayer,
            Badge: player == Hare ? "Hase" : "Hund");

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _position[i] + (i == Hare ? 0 : HoundHeadStart);
            r.Extra["hare"] = i == Hare ? 1 : 0;
            results.Add(r);
        }

        AssignRanks(results, r => r.Position == Hare ? _position[Hare] : _position[r.Position], Winner);
        return results;
    }
}
