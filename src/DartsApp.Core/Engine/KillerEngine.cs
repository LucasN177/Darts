using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Killer: Jeder Spieler bekommt eine Zahl. Wer das Doppel seiner Zahl trifft, wird zum Killer und kann
/// anderen Spielern durch Treffer auf deren Doppel Leben abziehen. Trifft ein Killer sein eigenes Doppel,
/// verliert er selbst ein Leben. Der letzte Spieler mit Leben gewinnt.
/// </summary>
public sealed class KillerEngine : GameEngine
{
    private readonly int[] _numbers;
    private int[] _lives = [];
    private bool[] _killer = [];
    private int[] _kills = [];
    private int[] _eliminatedAt = [];

    public KillerEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (players.Count < 2) throw new ArgumentException("Killer benötigt mindestens zwei Spieler.", nameof(players));
        if (settings.KillerLives < 1) throw new ArgumentOutOfRangeException(nameof(settings), "Mindestens ein Leben.");

        // Zahlen aus dem Seed vergeben, damit das Nachspielen dieselbe Zuordnung ergibt.
        var random = new Random(settings.Seed);
        _numbers = Enumerable.Range(1, 20).OrderBy(_ => random.Next()).Take(players.Count).ToArray();
        Reset();
    }

    public int NumberOf(int player) => _numbers[player];
    public int LivesOf(int player) => _lives[player];
    public bool IsKiller(int player) => _killer[player];

    protected override void Reset()
    {
        _lives = Enumerable.Repeat(Settings.KillerLives, PlayerCount).ToArray();
        _killer = new bool[PlayerCount];
        _kills = new int[PlayerCount];
        _eliminatedAt = Enumerable.Repeat(int.MaxValue, PlayerCount).ToArray();
    }

    private bool IsAlive(int player) => _lives[player] > 0;

    protected override bool IsPlayerOut(int player) => !IsAlive(player);

    protected override bool OnDart(Turn turn, Dart dart)
    {
        if (!dart.IsDouble || dart.IsBull) return false;
        var owner = Array.IndexOf(_numbers, dart.Segment);
        if (owner < 0) return false;

        var me = turn.PlayerIndex;
        var name = Players[me].Name;

        if (owner == me)
        {
            if (!_killer[me])
            {
                _killer[me] = true;
                turn.Score++;
                turn.Note = "Killer";
                SetMessage($"{name} ist jetzt Killer!");
                return false;
            }

            // Eigenes Doppel als Killer: eigenes Leben weg.
            Hit(me, me);
            turn.Score--;
            var finished = CheckWinner();
            return finished || !IsAlive(me);
        }

        if (!_killer[me] || !IsAlive(owner)) return false;

        turn.Score++;
        Hit(owner, me);
        return CheckWinner();
    }

    private void Hit(int victim, int by)
    {
        _lives[victim]--;
        if (IsAlive(victim)) return;

        _eliminatedAt[victim] = Turns.Count * 3 + (CurrentTurn?.Darts.Count ?? 0);
        if (victim != by) _kills[by]++;
        SetMessage(victim == by
            ? $"{Players[victim].Name} hat sich selbst eliminiert"
            : $"{Players[by].Name} eliminiert {Players[victim].Name}");
    }

    private bool CheckWinner()
    {
        var alive = Enumerable.Range(0, PlayerCount).Where(IsAlive).ToList();
        if (alive.Count > 1) return false;
        FinishGame(alive.Count == 1 ? alive[0] : null);
        return true;
    }

    public override IReadOnlySet<int> RelevantSegments =>
        _killer[CurrentPlayer]
            ? Enumerable.Range(0, PlayerCount).Where(p => p != CurrentPlayer && IsAlive(p)).Select(p => _numbers[p]).ToHashSet()
            : new HashSet<int> { _numbers[CurrentPlayer] };

    public override Dart BotAim()
    {
        var me = CurrentPlayer;
        if (!_killer[me]) return Dart.D(_numbers[me]);

        // Gefährlichste Gegner zuerst: Killer mit wenigen Leben.
        var victim = Enumerable.Range(0, PlayerCount)
            .Where(p => p != me && IsAlive(p))
            .OrderBy(p => _lives[p])
            .ThenByDescending(p => _killer[p])
            .First();
        return Dart.D(_numbers[victim]);
    }

    public override string Status => $"Runde {Round} · {Enumerable.Range(0, PlayerCount).Count(IsAlive)} im Spiel";

    public override string? Hint
    {
        get
        {
            if (IsFinished) return null;
            var me = CurrentPlayer;
            if (!_killer[me]) return $"Werde Killer: D{_numbers[me]}";
            var targets = Enumerable.Range(0, PlayerCount)
                .Where(p => p != me && IsAlive(p))
                .Select(p => $"D{_numbers[p]} ({Players[p].Name})");
            return $"Ziele: {string.Join(", ", targets)} · eigenes D{_numbers[me]} meiden";
        }
    }

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, IsAlive(player) ? $"{_lives[player]} Leben" : "Ausgeschieden", $"Zahl {_numbers[player]}",
            Details(("Leben", _lives[player].ToString()), ("Kills", _kills[player].ToString())),
            IsActive: !IsFinished && player == CurrentPlayer,
            IsOut: !IsAlive(player),
            Badge: _killer[player] && IsAlive(player) ? "Killer" : null,
            Symbol: IsAlive(player) ? PlayerSymbol.Lives : PlayerSymbol.Eliminated,
            SymbolCount: Math.Max(0, _lives[player]));

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var r = BaseResult(i);
            r.Score = Math.Max(0, _lives[i]);
            r.Extra["lives"] = Math.Max(0, _lives[i]);
            r.Extra["kills"] = _kills[i];
            r.Extra["number"] = _numbers[i];
            results.Add(r);
        }

        // Wer später ausscheidet, landet weiter vorne.
        AssignRanks(results, r => _eliminatedAt[r.Position] == int.MaxValue
            ? 1_000_000 + r.Score
            : _eliminatedAt[r.Position], Winner);
        return results;
    }
}
