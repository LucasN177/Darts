using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Checkout-Training in drei Varianten. Pro Versuch hat man eine begrenzte Anzahl Aufnahmen, um das Ziel auszuchecken.
/// <list type="bullet">
/// <item>121 Checkout: Start beim eingestellten Wert, Erfolg = Ziel +1, sonst greift die Fehlregel.</item>
/// <item>Checkout Roulette: zufällige, auscheckbare Ziele im eingestellten Bereich (für alle Spieler dieselben).</item>
/// <item>Catch 40: Ziele 61 bis 100 mit je 6 Darts; 2 Darts = 3 Punkte, 3 Darts = 2 Punkte, 4-6 Darts = 1 Punkt.</item>
/// </list>
/// </summary>
public sealed class CheckoutTrainingEngine : GameEngine
{
    private const string SuccessNote = "Checkout";
    private const int Catch40First = 61;
    private const int Catch40Last = 100;

    private sealed class State
    {
        public int Target;
        public int Remaining;
        public int TurnsUsed;
        public int DartsUsed;
        public int Attempts;
        public int Successes;
        public int Points;
        public int HighestTarget;
    }

    private State[] _state = [];
    private int _turnStartRemaining;
    /// <summary>Feste Zielliste (Roulette, Catch 40); null bei 121.</summary>
    private readonly int[]? _targets;

    public CheckoutTrainingEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        var max = CheckoutCalculator.MaxCheckout(settings.OutRule);
        switch (settings.Mode)
        {
            case GameMode.CheckoutRoulette:
                _targets = RouletteTargets(settings);
                break;
            case GameMode.Catch40:
                _targets = Enumerable.Range(Catch40First, Catch40Last - Catch40First + 1).ToArray();
                break;
            default:
                if (settings.CheckoutStart < 2 || settings.CheckoutStart > max)
                    throw new ArgumentOutOfRangeException(nameof(settings), "Ungültiger Startwert.");
                break;
        }

        Reset();
    }

    /// <summary>Zufällige, mit drei Darts auscheckbare Ziele (aus dem Seed, damit Nachspielen identisch bleibt).</summary>
    public static int[] RouletteTargets(GameSettings settings)
    {
        var min = Math.Clamp(Math.Min(settings.RouletteMin, settings.RouletteMax), 2, 170);
        var max = Math.Clamp(Math.Max(settings.RouletteMin, settings.RouletteMax), 2, 170);
        var pool = Enumerable.Range(min, max - min + 1)
            .Where(t => CheckoutCalculator.Suggest(t, 3, settings.OutRule) is not null)
            .ToArray();
        if (pool.Length == 0) throw new ArgumentOutOfRangeException(nameof(settings), "Im Bereich gibt es keinen Checkout.");

        var random = new Random(settings.Seed);
        return Enumerable.Range(0, Math.Max(1, settings.CheckoutAttempts)).Select(_ => pool[random.Next(pool.Length)]).ToArray();
    }

    private bool IsCatch40 => Settings.Mode == GameMode.Catch40;
    private int MaxTarget => CheckoutCalculator.MaxCheckout(Settings.OutRule);
    private int TurnsPerAttempt => IsCatch40 ? 2 : Math.Max(1, Settings.CheckoutTurnsPerAttempt);
    private int AttemptCount => _targets?.Length ?? Settings.CheckoutAttempts;

    public int TargetOf(int player) => _state[player].Target;
    public int RemainingOf(int player) => _state[player].Remaining;
    public int PointsOf(int player) => IsCatch40 ? _state[player].Points : _state[player].Successes;

    protected override void Reset()
    {
        var first = _targets?[0] ?? Settings.CheckoutStart;
        _state = Enumerable.Range(0, PlayerCount).Select(_ => new State { Target = first, Remaining = first }).ToArray();
    }

    protected override bool IsPlayerOut(int player) => _state[player].Attempts >= AttemptCount;

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
        p.DartsUsed += turn.Darts.Count;
        var name = Players[turn.PlayerIndex].Name;

        if (turn.Note == SuccessNote)
        {
            p.Successes++;
            p.HighestTarget = Math.Max(p.HighestTarget, p.Target);
            var points = Catch40Points(p.DartsUsed);
            p.Points += points;
            SetMessage(IsCatch40
                ? $"{name} checkt {p.Target} mit {p.DartsUsed} Darts: +{points}"
                : $"{name} checkt {p.Target}!");
            if (_targets is null) p.Target = Math.Min(MaxTarget, p.Target + 1);
            NextAttempt(p);
        }
        else if (p.TurnsUsed >= TurnsPerAttempt)
        {
            SetMessage($"{name} verpasst {p.Target}");
            if (_targets is null)
            {
                p.Target = Settings.CheckoutOnFail switch
                {
                    CheckoutFailRule.MinusOne => Math.Max(Settings.OutRule == InOutRule.Single ? 1 : 2, p.Target - 1),
                    CheckoutFailRule.Reset => Settings.CheckoutStart,
                    _ => p.Target
                };
            }
            NextAttempt(p);
        }

        if (_state.All(s => s.Attempts >= AttemptCount))
            FinishGame(BestPlayer());
    }

    private static int Catch40Points(int darts) => darts switch
    {
        <= 2 => 3,
        3 => 2,
        _ => 1
    };

    private void NextAttempt(State p)
    {
        p.Attempts++;
        p.TurnsUsed = 0;
        p.DartsUsed = 0;
        if (_targets is not null && p.Attempts < _targets.Length) p.Target = _targets[p.Attempts];
        p.Remaining = p.Target;
    }

    private int? BestPlayer()
    {
        var ranking = Enumerable.Range(0, PlayerCount)
            .OrderByDescending(PointsOf)
            .ThenByDescending(i => _state[i].HighestTarget)
            .ToList();
        if (PlayerCount == 1) return 0;
        var a = ranking[0];
        var b = ranking[1];
        return PointsOf(a) == PointsOf(b) && _state[a].HighestTarget == _state[b].HighestTarget ? null : a;
    }

    public override Dart BotAim() =>
        CheckoutCalculator.BestDart(_state[CurrentPlayer].Remaining, DartsLeftInTurn, Settings.OutRule);

    public override string Status
    {
        get
        {
            var p = _state[CurrentPlayer];
            return $"Versuch {Math.Min(p.Attempts + 1, AttemptCount)} / {AttemptCount} · Aufnahme {p.TurnsUsed + 1} / {TurnsPerAttempt}";
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
        var details = IsCatch40
            ? Details(("Punkte", p.Points.ToString()), ("Checkouts", $"{p.Successes}/{p.Attempts}"))
            : Details(("Checkouts", $"{p.Successes}/{p.Attempts}"),
                ("Bestes Ziel", p.HighestTarget == 0 ? "–" : p.HighestTarget.ToString()));
        return new PlayerView(Players[player].Name, p.Remaining.ToString(), $"Rest (Ziel {p.Target})", details,
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
            r.Score = PointsOf(i);
            r.CheckoutAttempts = p.Attempts;
            r.CheckoutsHit = p.Successes;
            r.HighestCheckout = p.HighestTarget;
            r.Extra["highest_target"] = p.HighestTarget;
            r.Extra["final_target"] = p.Target;
            if (IsCatch40) r.Extra["points"] = p.Points;
            results.Add(r);
        }

        AssignRanks(results, r => r.Score * 1_000 + r.HighestCheckout, Winner);
        return results;
    }
}
