using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// JDC Challenge (Trainingsprogramm der Junior Darts Corporation) in drei Abschnitten:
/// <list type="number">
/// <item>Shanghai 10 bis 15: Treffer zählen ihren Wert, Single + Double + Triple in einer Aufnahme bringen 100 Bonuspunkte.</item>
/// <item>Doppel: je ein Dart auf D1 bis D20 und Bull (21 Darts in 7 Aufnahmen), jeder Treffer bringt 50 Punkte.</item>
/// <item>Shanghai 15 bis 20 wie Abschnitt 1.</item>
/// </list>
/// </summary>
public sealed class JdcChallengeEngine : GameEngine
{
    public const int ShanghaiBonus = 100;
    public const int DoublePoints = 50;
    private const int FirstShanghaiRounds = 6;
    private const int DoubleRounds = 7;
    public const int TotalRounds = FirstShanghaiRounds + DoubleRounds + 6;

    private int[] _points = [];
    private int[] _doublesHit = [];
    private int[] _shanghais = [];

    public JdcChallengeEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        Reset();
    }

    public int PointsOf(int player) => _points[player];

    private static bool IsDoubleRound(int round) => round > FirstShanghaiRounds && round <= FirstShanghaiRounds + DoubleRounds;

    /// <summary>Zahl der Shanghai-Runde (10-15 bzw. 15-20).</summary>
    private static int ShanghaiNumber(int round) => round <= FirstShanghaiRounds ? 9 + round : round - FirstShanghaiRounds - DoubleRounds + 14;

    /// <summary>Doppelziel für den n-ten Dart einer Doppelrunde (1-20, 25 = Bull).</summary>
    private static int DoubleNumber(int round, int dartIndex)
    {
        var k = (round - FirstShanghaiRounds - 1) * 3 + dartIndex + 1;
        return k > 20 ? Dart.BullSegment : k;
    }

    private int CurrentRound => Math.Min(Round, TotalRounds);

    protected override void Reset()
    {
        _points = new int[PlayerCount];
        _doublesHit = new int[PlayerCount];
        _shanghais = new int[PlayerCount];
    }

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var me = turn.PlayerIndex;

        if (IsDoubleRound(turn.Round))
        {
            var target = DoubleNumber(turn.Round, turn.Darts.Count - 1);
            if (dart.Segment == target && dart.IsDouble)
            {
                _points[me] += DoublePoints;
                _doublesHit[me]++;
                turn.Score += DoublePoints;
            }
            return false;
        }

        var number = ShanghaiNumber(turn.Round);
        if (dart.Segment != number) return false;

        _points[me] += dart.Score;
        turn.Score += dart.Score;

        var multipliers = turn.Darts.Where(d => d.Segment == number).Select(d => d.Multiplier).ToHashSet();
        if (turn.Darts.Count == 3 && multipliers.SetEquals([1, 2, 3]))
        {
            _points[me] += ShanghaiBonus;
            _shanghais[me]++;
            turn.Score += ShanghaiBonus;
            turn.Note = "Shanghai!";
        }

        return false;
    }

    protected override void OnRoundCompleted(int round)
    {
        if (round < TotalRounds) return;
        var max = _points.Max();
        var leaders = Enumerable.Range(0, PlayerCount).Where(p => _points[p] == max).ToList();
        FinishGame(leaders.Count == 1 ? leaders[0] : null);
    }

    private int CurrentTarget => IsDoubleRound(CurrentRound)
        ? DoubleNumber(CurrentRound, DartsInCurrentTurn)
        : ShanghaiNumber(CurrentRound);

    public override IReadOnlySet<int> RelevantSegments => new HashSet<int> { CurrentTarget };

    public override Dart BotAim()
    {
        var target = CurrentTarget;
        if (IsDoubleRound(CurrentRound)) return target == Dart.BullSegment ? Dart.Bullseye : Dart.D(target);

        var hit = CurrentTurn?.Darts.Where(d => d.Segment == target).Select(d => d.Multiplier).ToHashSet() ?? [];
        return new Dart(target, new[] { 3, 2, 1 }.FirstOrDefault(m => !hit.Contains(m), 3));
    }

    private string Section => CurrentRound <= FirstShanghaiRounds ? "Shanghai 10–15" : IsDoubleRound(CurrentRound) ? "Doppel" : "Shanghai 15–20";

    public override string Status => $"{Section} · Runde {CurrentRound} / {TotalRounds}";

    public override string? Hint
    {
        get
        {
            if (IsFinished) return null;
            var target = CurrentTarget;
            return IsDoubleRound(CurrentRound)
                ? $"Ziel: {(target == Dart.BullSegment ? "Bullseye" : $"D{target}")} ({DoublePoints} Punkte)"
                : $"Ziel: {target} – Single, Double und Triple = +{ShanghaiBonus}";
        }
    }

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, _points[player].ToString(), "Punkte",
            Details(("Doppel", $"{_doublesHit[player]}/21"), ("Shanghais", _shanghais[player].ToString())),
            IsActive: !IsFinished && player == CurrentPlayer);

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _points[i];
            r.CheckoutAttempts = TurnsOf(i).Where(t => IsDoubleRound(t.Round)).Sum(t => t.Darts.Count);
            r.CheckoutsHit = _doublesHit[i];
            r.Extra["doubles"] = _doublesHit[i];
            r.Extra["shanghai"] = _shanghais[i];
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
