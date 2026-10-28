using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// 121 Checkout-Training: Ausgehend vom Startwert (Standard 121) hat man pro Versuch eine
/// begrenzte Anzahl Aufnahmen, um auszuchecken. Gelingt es, steigt das Ziel um eins,
/// sonst greift die eingestellte Fehlregel.
/// </summary>
public sealed class Checkout121Engine : GameEngine
{
    private const string SuccessNote = "Checkout";

    private sealed class State
    {
        public int Target;
        public int Remaining;
        public int TurnsUsed;
        public int Attempts;
        public int Successes;
        public int HighestTarget;
    }

    private State[] _state = [];
    private int _turnStartRemaining;

    public Checkout121Engine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (settings.CheckoutStart < 2 || settings.CheckoutStart > CheckoutCalculator.MaxCheckout(settings.OutRule))
            throw new ArgumentOutOfRangeException(nameof(settings), "Ungültiger Startwert.");
        Reset();
    }

    private int MaxTarget => CheckoutCalculator.MaxCheckout(Settings.OutRule);
    private int TurnsPerAttempt => Math.Max(1, Settings.CheckoutTurnsPerAttempt);

    public int TargetOf(int player) => _state[player].Target;
    public int RemainingOf(int player) => _state[player].Remaining;

    protected override void Reset()
    {
        _state = Enumerable.Range(0, PlayerCount).Select(_ => new State
        {
            Target = Settings.CheckoutStart,
            Remaining = Settings.CheckoutStart
        }).ToArray();
    }

    protected override bool IsPlayerOut(int player) => _state[player].Attempts >= Settings.CheckoutAttempts;

    protected override void OnTurnStarted(Turn turn) => _turnStartRemaining = _state[turn.PlayerIndex].Remaining;

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var p = _state[turn.PlayerIndex];
        var after = p.Remaining - dart.Score;
        var bust = after < 0
                   || (after == 0 && !CheckoutCalculator.IsFinisher(dart, Settings.OutRule))
                   || (after == 1 && Settings.OutRule != InOutRule.Single);

        if (bust)
        {
            p.Remaining = _turnStartRemaining;
            turn.Bust = true;
            turn.Note = "Bust";
            return true;
        }

        p.Remaining = after;
        turn.Score = _turnStartRemaining - after;
        if (after != 0) return false;

        turn.Note = SuccessNote;
        return true;
    }

    protected override void OnTurnCompleted(Turn turn)
    {
        var p = _state[turn.PlayerIndex];
        p.TurnsUsed++;
        var name = Players[turn.PlayerIndex].Name;

        if (turn.Note == SuccessNote)
        {
            p.Successes++;
            p.HighestTarget = Math.Max(p.HighestTarget, p.Target);
            SetMessage($"{name} checkt {p.Target}!");
            p.Target = Math.Min(MaxTarget, p.Target + 1);
            NextAttempt(p);
        }
        else if (p.TurnsUsed >= TurnsPerAttempt)
        {
            SetMessage($"{name} verpasst {p.Target}");
            p.Target = Settings.CheckoutOnFail switch
            {
                CheckoutFailRule.MinusOne => Math.Max(Settings.OutRule == InOutRule.Single ? 1 : 2, p.Target - 1),
                CheckoutFailRule.Reset => Settings.CheckoutStart,
                _ => p.Target
            };
            NextAttempt(p);
        }

        if (_state.All(s => s.Attempts >= Settings.CheckoutAttempts))
            FinishGame(BestPlayer());
    }

    private static void NextAttempt(State p)
    {
        p.Attempts++;
        p.TurnsUsed = 0;
        p.Remaining = p.Target;
    }

    private int? BestPlayer()
    {
        var ranking = Enumerable.Range(0, PlayerCount)
            .OrderByDescending(i => _state[i].Successes)
            .ThenByDescending(i => _state[i].HighestTarget)
            .ToList();
        if (PlayerCount == 1) return 0;
        var a = _state[ranking[0]];
        var b = _state[ranking[1]];
        return a.Successes == b.Successes && a.HighestTarget == b.HighestTarget ? null : ranking[0];
    }

    public override string Status
    {
        get
        {
            var p = _state[CurrentPlayer];
            return $"Versuch {Math.Min(p.Attempts + 1, Settings.CheckoutAttempts)} / {Settings.CheckoutAttempts} · Aufnahme {p.TurnsUsed + 1} / {TurnsPerAttempt}";
        }
    }

    public override string? Hint
    {
        get
        {
            if (IsFinished) return null;
            var p = _state[CurrentPlayer];
            var text = CheckoutCalculator.SuggestText(p.Remaining, DartsLeftInTurn, Settings.OutRule);
            return $"Ziel {p.Target}" + (text is null ? "" : $" · Weg: {text}");
        }
    }

    public override PlayerView GetView(int player)
    {
        var p = _state[player];
        return new PlayerView(Players[player].Name, p.Remaining.ToString(), $"Rest (Ziel {p.Target})",
            Details(("Checkouts", $"{p.Successes}/{p.Attempts}"),
                ("Bestes Ziel", p.HighestTarget == 0 ? "–" : p.HighestTarget.ToString())),
            IsActive: !IsFinished && player == CurrentPlayer,
            IsOut: IsPlayerOut(player) && !IsFinished);
    }

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var p = _state[i];
            var r = BaseResult(i);
            r.Score = p.Successes;
            r.CheckoutAttempts = p.Attempts;
            r.CheckoutsHit = p.Successes;
            r.HighestCheckout = p.HighestTarget;
            r.Extra["highest_target"] = p.HighestTarget;
            r.Extra["final_target"] = p.Target;
            results.Add(r);
        }

        AssignRanks(results, r => r.Score * 1_000 + r.HighestCheckout, Winner);
        return results;
    }
}
