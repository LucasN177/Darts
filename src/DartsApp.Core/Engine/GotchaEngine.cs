using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Gotcha: Alle zählen von 0 hoch, wer das Ziel (Standard 301) genau erreicht, gewinnt. Wer darüber kommt,
/// überwirft sich (Bust). Trifft man genau den Punktestand eines Gegners, fällt dieser auf 0 zurück.
/// </summary>
public sealed class GotchaEngine : GameEngine
{
    private int[] _score = [];
    private int[] _knockouts = [];
    private int _turnStart;

    public GotchaEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (settings.GotchaTarget is < 10 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(settings), "Ungültiges Ziel.");
        Reset();
    }

    private int Target => Settings.GotchaTarget;
    public int ScoreOf(int player) => _score[player];

    protected override void Reset()
    {
        _score = new int[PlayerCount];
        _knockouts = new int[PlayerCount];
    }

    protected override void OnTurnStarted(Turn turn) => _turnStart = _score[turn.PlayerIndex];

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var me = turn.PlayerIndex;
        var after = _score[me] + dart.Score;

        if (after > Target)
        {
            _score[me] = _turnStart;
            turn.Score = 0;
            turn.Bust = true;
            turn.Note = "Bust";
            SetMessage($"Bust! {Players[me].Name} bleibt auf {_score[me]}");
            return true;
        }

        _score[me] = after;
        turn.Score = after - _turnStart;

        if (after == Target)
        {
            FinishGame(me);
            return true;
        }

        if (dart.IsMiss) return false;
        for (var o = 0; o < PlayerCount; o++)
        {
            if (o == me || _score[o] != after || after == 0) continue;
            _score[o] = 0;
            _knockouts[me]++;
            turn.Note = "Gotcha!";
            SetMessage($"Gotcha! {Players[o].Name} fällt auf 0 zurück");
        }

        return false;
    }

    public override Dart BotAim()
    {
        var me = CurrentPlayer;
        var remaining = Target - _score[me];
        if (FinishingDart(remaining) is { } finish) return finish;

        // Einen Gegner, der knapp vor einem liegt, auf 0 schicken.
        var knock = Enumerable.Range(0, PlayerCount)
            .Where(o => o != me && _score[o] > _score[me])
            .Select(o => FinishingDart(_score[o] - _score[me]))
            .FirstOrDefault(d => d is not null);
        if (knock is { } k && remaining - k.Score > 0) return k;

        // Nah am Ziel vorsichtig werfen, sodass danach ein Single zum Finish reicht.
        if (remaining > 60) return Dart.T(20);
        if (remaining > 40) return Dart.S(20);
        return Dart.S(remaining - 20); // 21-40 ohne direktes Finish: Rest 20 übrig lassen

    }

    /// <summary>Ein Dart mit genau diesem Wert, bevorzugt Single, oder null.</summary>
    private static Dart? FinishingDart(int value)
    {
        if (value is >= 1 and <= 20) return Dart.S(value);
        if (value == 25) return Dart.OuterBull;
        if (value == 50) return Dart.Bullseye;
        if (value <= 40 && value % 2 == 0) return Dart.D(value / 2);
        if (value <= 60 && value % 3 == 0) return Dart.T(value / 3);
        return null;
    }

    public override IReadOnlySet<int>? RelevantSegments =>
        FinishingDart(Target - _score[CurrentPlayer]) is { } d ? new HashSet<int> { d.Segment } : null;

    public override string Status => $"Runde {Round}";

    public override string? Hint
    {
        get
        {
            if (IsFinished) return null;
            var remaining = Target - _score[CurrentPlayer];
            return FinishingDart(remaining) is { } d ? $"Noch {remaining} – Finish mit {d}" : $"Noch {remaining}";
        }
    }

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, _score[player].ToString(), $"Punkte (noch {Target - _score[player]})",
            Details(("Gotchas", _knockouts[player].ToString()),
                ("Letzte", TurnsOf(player).LastOrDefault(t => t.IsComplete) is { } t ? (t.Bust ? "Bust" : t.Score.ToString()) : "–")),
            IsActive: !IsFinished && player == CurrentPlayer);

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = _score[i];
            r.Extra["knockouts"] = _knockouts[i];
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
