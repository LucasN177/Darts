using DartsApp.Core.Models;

namespace DartsApp.Core.Engine;

/// <summary>
/// Tic-Tac-Toe (genau zwei Spieler): Ein 3×3-Raster aus acht zufälligen Zahlen und Bull in der Mitte.
/// Wer zuerst drei Marks auf einem Feld hat (Single = 1, Double = 2, Triple = 3), belegt es.
/// Drei belegte Felder in einer Reihe, Spalte oder Diagonale gewinnen; sind alle Felder vergeben, endet es unentschieden.
/// </summary>
public sealed class TicTacToeEngine : GameEngine
{
    public const int MarksToClaim = 3;
    private const int Center = 4;

    private static readonly int[][] Lines =
    [
        [0, 1, 2], [3, 4, 5], [6, 7, 8],
        [0, 3, 6], [1, 4, 7], [2, 5, 8],
        [0, 4, 8], [2, 4, 6]
    ];

    private readonly int[] _cells;
    private int[,] _marks = new int[0, 0];
    private int?[] _owner = [];
    private int[]? _winningLine;

    public TicTacToeEngine(GameSettings settings, IReadOnlyList<PlayerRef> players) : base(settings, players)
    {
        if (players.Count != 2) throw new ArgumentException("Tic-Tac-Toe wird mit genau zwei Spielern gespielt.", nameof(players));
        _cells = BuildCells(settings.Seed);
        Reset();
    }

    /// <summary>Feldbelegung des Rasters (Index 0-8, zeilenweise); die Mitte ist immer Bull.</summary>
    public static int[] BuildCells(int seed)
    {
        var random = new Random(seed);
        var numbers = Enumerable.Range(1, 20).OrderBy(_ => random.Next()).Take(8).ToList();
        numbers.Insert(Center, Dart.BullSegment);
        return numbers.ToArray();
    }

    public IReadOnlyList<int> Cells => _cells;
    public int? OwnerOf(int cell) => _owner[cell];
    public int MarksOf(int player, int cell) => _marks[player, cell];
    public IReadOnlyList<int>? WinningLine => _winningLine;

    protected override void Reset()
    {
        _marks = new int[2, 9];
        _owner = new int?[9];
        _winningLine = null;
    }

    private static int MarksFor(Dart dart) => dart.Multiplier;

    protected override bool OnDart(Turn turn, Dart dart)
    {
        var cell = Array.IndexOf(_cells, dart.Segment);
        if (dart.IsMiss || cell < 0 || _owner[cell] is not null) return false;

        var me = turn.PlayerIndex;
        _marks[me, cell] = Math.Min(MarksToClaim, _marks[me, cell] + MarksFor(dart));
        if (_marks[me, cell] < MarksToClaim) return false;

        _owner[cell] = me;
        turn.Score++;
        SetMessage($"{Players[me].Name} belegt {Label(cell)}");

        if (Lines.FirstOrDefault(l => l.All(c => _owner[c] == me)) is { } line)
        {
            _winningLine = line;
            FinishGame(me);
            return true;
        }

        if (_owner.All(o => o is not null))
        {
            FinishGame(null);
            SetMessage("Unentschieden – alle Felder belegt");
            return true;
        }

        return false;
    }

    public string Label(int cell) => _cells[cell] == Dart.BullSegment ? "Bull" : _cells[cell].ToString();

    public override IReadOnlySet<int> RelevantSegments =>
        Enumerable.Range(0, 9).Where(c => _owner[c] is null).Select(c => _cells[c]).ToHashSet();

    public override Dart BotAim()
    {
        var me = CurrentPlayer;
        var opponent = 1 - me;
        var free = Enumerable.Range(0, 9).Where(c => _owner[c] is null).ToList();
        if (free.Count == 0) return Dart.T(20);

        double Score(int cell)
        {
            double score = 0;
            foreach (var line in Lines.Where(l => l.Contains(cell)))
            {
                var mine = line.Count(c => _owner[c] == me);
                var theirs = line.Count(c => _owner[c] == opponent);
                if (mine == 2 && theirs == 0) score += 1000; // Gewinnzug
                if (theirs == 2 && mine == 0) score += 500;  // Blocken
                if (theirs == 0) score += 10 * (mine + 1);
            }
            score += cell == Center ? 30 : cell is 0 or 2 or 6 or 8 ? 20 : 10;
            score += _marks[me, cell] * 25; // angefangene Felder fertig machen
            return score;
        }

        var target = free.OrderByDescending(Score).First();
        return _cells[target] == Dart.BullSegment ? Dart.Bullseye : Dart.T(_cells[target]);
    }

    public override string Status => $"Runde {Round} · {_owner.Count(o => o is not null)} / 9 Felder belegt";

    public override string? Hint => IsFinished ? null : $"{MarksToClaim} Marks belegen ein Feld – drei in einer Reihe gewinnen";

    public override PlayerView GetView(int player) =>
        new(Players[player].Name, _owner.Count(o => o == player).ToString(), "Felder",
            Details(("Marks", Enumerable.Range(0, 9).Sum(c => _marks[player, c]).ToString()),
                ("Darts", TurnsOf(player).Sum(t => t.Darts.Count).ToString())),
            IsActive: !IsFinished && player == CurrentPlayer,
            Badge: player == 0 ? "Kreuz" : "Kreis");

    public override IReadOnlyList<PlayerResult> GetResults()
    {
        var results = new List<PlayerResult>();
        for (var i = 0; i < 2; i++)
        {
            var r = BaseResult(i);
            r.Score = _owner.Count(o => o == i);
            r.Extra["marks"] = Enumerable.Range(0, 9).Sum(c => _marks[i, c]);
            results.Add(r);
        }

        AssignRanks(results, r => r.Score, Winner);
        return results;
    }
}
