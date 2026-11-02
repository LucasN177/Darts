using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// All Fives (Fives): Nur Aufnahmen, deren Summe durch 5 teilbar ist, zählen – und zwar Summe ÷ 5 Punkte.
/// Wer genau die Zielpunktzahl (Standard 51) erreicht, gewinnt; wer darüber käme, bekommt für die Aufnahme nichts.
/// </summary>
public sealed class AllFivesEngine : GameEngine
{
    private int[] _points = [];
    private int[] _scoringTurns = [];

    public AllFivesEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (settings.FivesTarget < 1) throw new ArgumentOutOfRangeException(nameof(settings), "Ungültiges Ziel.");
        Reset();
    }

    private int Target => Settings.FivesTarget;
    public int PointsOf(int player) => _points[player];

    protected override void Reset()
    {
        _points = new int[PlayerCount];
        _scoringTurns = new int[PlayerCount];
    }

    protected override bool OnDart(Turn turn, Dart dart) => false;

    protected override void OnTurnCompleted(Turn turn)
    {
        var me = turn.PlayerIndex;
        var total = turn.Darts.Sum(d => d.Score);
        if (total == 0 || total % 5 != 0)
        {
            turn.Score = 0;
            turn.Note = total == 0 ? null : "nicht durch 5 teilbar";
            return;
        }

        var points = total / 5;
        if (_points[me] + points > Target)
        {
            turn.Score = 0;
            turn.Bust = true;
            turn.Note = "Bust";
            SetMessage($"Bust! {Players[me].Name} braucht genau {Target - _points[me]}");
            return;
        }

        _points[me] += points;
        _scoringTurns[me]++;
        turn.Score = points;
        if (_points[me] == Target) FinishGame(me);
    }

    public override Dart BotAim()
    {
        var needed = Target - _points[CurrentPlayer];
        var sum = CurrentTurn?.Darts.Sum(d => d.Score) ?? 0;
        var dartsLeft = DartsLeftInTurn;
        var capacity = needed * 5 - sum; // so viele Punkte darf die Aufnahme noch bringen

        if (dartsLeft == 1)
        {
            // Letzter Dart: Summe auf ein Vielfaches von 5 bringen, möglichst hoch, aber ohne Bust.
            var best = Candidates()
                .Where(d => (sum + d.Score) % 5 == 0 && d.Score <= capacity)
                .OrderByDescending(d => d.Score)
                .ThenBy(d => d.Multiplier)
                .FirstOrDefault();
            return best == default ? Dart.S(Math.Clamp(5 - sum % 5, 1, 20)) : best;
        }

        if (capacity >= 60 * dartsLeft) return Dart.T(20);
        // Knapp vor dem Ziel: Singles mit Vielfachen von 5, die zum Rest passen.
        var perDart = Math.Max(5, capacity / dartsLeft / 5 * 5);
        return Dart.S(Math.Clamp(perDart, 5, 20));
    }

    private static IEnumerable<Dart> Candidates()
    {
        for (var s = 1; s <= 20; s++)
        {
            yield return Dart.S(s);
            yield return Dart.T(s);
        }
        yield return Dart.OuterBull;
    }

    public override string Status => $"Runde {Round}";

    public override string? Hint
    {
        get
        {
            if (IsFinished) return null;
            var sum = CurrentTurn?.Darts.Sum(d => d.Score) ?? 0;
            var needed = Target - _points[CurrentPlayer];
            return $"Summe {sum}{(sum % 5 == 0 ? "" : $" – noch {5 - sum % 5} bis zur nächsten 5er-Zahl")} · noch {needed} Punkte";
        }
    }

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, _points[player].ToString(), $"Punkte (Ziel {Target})",
            Details(("Wertungen", _scoringTurns[player].ToString()),
                ("Letzte", TurnsOf(player).LastOrDefault(t => t.IsComplete) is { } t ? (t.Bust ? "Bust" : t.Score.ToString()) : "–")),
            IsActive: !IsFinished && player == CurrentPlayer);

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _points[i];
            r.Extra["scoring_turns"] = _scoringTurns[i];
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
