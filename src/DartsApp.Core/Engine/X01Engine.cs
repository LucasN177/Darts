using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>X01 (301/501/701/...) mit Single/Double/Master In und Out, Legs und Sets.</summary>
public sealed class X01Engine : GameEngine
{
    private sealed class State
    {
        public int Remaining;
        public bool Started;
        public int LegsInSet;
        public int LegsTotal;
        public int Sets;
        public int CheckoutAttempts;
        public int CheckoutsHit;
        public int HighestCheckout;
    }

    private State[] _state = [];
    private int _legStarter;
    private int _turnStartRemaining;

    public X01Engine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        Reset();
    }

    public int RemainingOf(int player) => _state[player].Remaining;
    public bool HasStarted(int player) => _state[player].Started;
    public int LegsOf(int player) => _state[player].LegsInSet;
    public int SetsOf(int player) => _state[player].Sets;

    protected override void Reset()
    {
        _state = Enumerable.Range(0, PlayerCount).Select(_ => new State()).ToArray();
        _legStarter = 0;
        ResetLeg();
    }

    private void ResetLeg()
    {
        foreach (var s in _state)
        {
            s.Remaining = Settings.StartScore;
            s.Started = Settings.InRule == InOutRule.Single;
        }
    }

    protected override void OnTurnStarted(Turn turn) => _turnStartRemaining = _state[turn.PlayerIndex].Remaining;

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var p = _state[turn.PlayerIndex];

        if (!p.Started)
        {
            if (!QualifiesForIn(dart)) return false;
            p.Started = true;
        }

        var before = p.Remaining;
        if (CheckoutCalculator.Suggest(before, 1, Settings.OutRule) is not null)
            p.CheckoutAttempts++;

        var after = before - dart.Score;
        var bust = after < 0
                   || (after == 0 && !CheckoutCalculator.IsFinisher(dart, Settings.OutRule))
                   || (after == 1 && Settings.OutRule != InOutRule.Single);

        if (bust)
        {
            p.Remaining = _turnStartRemaining;
            if (Settings.InRule != InOutRule.Single && _turnStartRemaining == Settings.StartScore)
                p.Started = false;
            turn.Bust = true;
            turn.Score = 0;
            turn.Note = "Bust";
            SetMessage($"Bust! {Players[turn.PlayerIndex].Name} bleibt auf {p.Remaining}");
            return true;
        }

        p.Remaining = after;
        turn.Score = _turnStartRemaining - after;

        if (after != 0) return false;

        p.CheckoutsHit++;
        p.HighestCheckout = Math.Max(p.HighestCheckout, _turnStartRemaining);
        turn.Note = $"Checkout {_turnStartRemaining}";
        WinLeg(turn.PlayerIndex);
        return true;
    }

    private bool QualifiesForIn(Dart dart) => Settings.InRule switch
    {
        InOutRule.Double => dart.IsDouble,
        InOutRule.Master => dart.IsDouble || dart.IsTriple,
        _ => !dart.IsMiss
    };

    private void WinLeg(int player)
    {
        var p = _state[player];
        p.LegsInSet++;
        p.LegsTotal++;
        var name = Players[player].Name;

        if (p.LegsInSet < Settings.LegsToWin)
        {
            SetMessage($"Leg an {name} ({_turnStartRemaining} Checkout)");
            NextLeg(newSet: false);
            return;
        }

        p.Sets++;
        if (Settings.SetsToWin <= 1 || p.Sets >= Settings.SetsToWin)
        {
            FinishGame(player);
            return;
        }

        SetMessage($"Set an {name}!");
        foreach (var s in _state) s.LegsInSet = 0;
        NextLeg(newSet: true);
    }

    private void NextLeg(bool newSet)
    {
        ResetLeg();
        _legStarter = (_legStarter + 1) % PlayerCount;
        StartNewLeg(_legStarter, newSet);
    }

    public override string Status
    {
        get
        {
            var parts = new List<string>();
            if (Settings.SetsToWin > 1) parts.Add($"Set {Set}");
            if (Settings.LegsToWin > 1 || Settings.SetsToWin > 1) parts.Add($"Leg {Leg}");
            parts.Add($"Runde {Round}");
            return string.Join(" · ", parts);
        }
    }

    public override string? Hint
    {
        get
        {
            if (IsFinished) return null;
            var p = _state[CurrentPlayer];
            if (!p.Started)
                return Settings.InRule == InOutRule.Double ? "Double zum Einstieg" : "Double oder Triple zum Einstieg";
            var text = CheckoutCalculator.SuggestText(p.Remaining, DartsLeftInTurn, Settings.OutRule);
            return text is null ? null : $"Checkout: {text}";
        }
    }

    public override PlayerView GetView(int player)
    {
        var p = _state[player];
        var turns = TurnsOf(player).ToList();
        var details = new List<KeyValuePair<string, string>>();

        if (Settings.SetsToWin > 1) details.Add(new("Sets", p.Sets.ToString()));
        if (Settings.LegsToWin > 1 || Settings.SetsToWin > 1) details.Add(new("Legs", p.LegsInSet.ToString()));
        details.Add(new("Ø", Fmt(Average(turns))));
        details.Add(new("Darts", turns.Where(t => t.Leg == Leg && t.Set == Set).Sum(t => t.Darts.Count).ToString()));
        var last = turns.LastOrDefault(t => t.IsComplete);
        details.Add(new("Letzte", last is null ? "–" : last.Bust ? "Bust" : last.Score.ToString()));

        string? badge = null;
        if (p.Started && player != CurrentPlayer && CheckoutCalculator.SuggestText(p.Remaining, 3, Settings.OutRule) is { } co)
            badge = co;

        return new PlayerView(Players[player].Name, p.Remaining.ToString(), "Rest", details,
            IsActive: !IsFinished && player == CurrentPlayer, Badge: badge);
    }

    private static double Average(IReadOnlyCollection<Turn> turns)
    {
        var darts = turns.Sum(t => t.Darts.Count);
        return darts == 0 ? 0 : turns.Sum(t => t.Score) * 3.0 / darts;
    }

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var p = _state[i];
            var turns = TurnsOf(i).ToList();
            var r = BaseResult(i);
            r.Score = turns.Sum(t => t.Score);
            r.Average = Math.Round(Average(turns), 2);

            var first9 = turns.GroupBy(t => (t.Set, t.Leg)).SelectMany(g => g.Take(3)).ToList();
            r.First9Average = first9.Count == 0 ? null : Math.Round(Average(first9), 2);

            r.HighestCheckout = p.HighestCheckout;
            r.CheckoutAttempts = p.CheckoutAttempts;
            r.CheckoutsHit = p.CheckoutsHit;
            r.Count180 = turns.Count(t => t.Score == 180);
            r.Count140 = turns.Count(t => t.Score is >= 140 and < 180);
            r.Count100 = turns.Count(t => t.Score is >= 100 and < 140);
            r.LegsWon = p.LegsTotal;
            r.SetsWon = p.Sets;
            r.Extra["remaining"] = p.Remaining;
            results.Add(r);
        }

        // Ohne Sieger (abgebrochen): nach gewonnenen Sets, Legs, dann Restpunkten.
        AssignRanks(results, r => r.SetsWon * 1_000_000 + r.LegsWon * 1_000 - r.Extra["remaining"], Winner);
        return results;
    }
}
