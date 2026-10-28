using DartsApp.Core.Engine;
using DartsApp.Core.Models;

namespace DartsApp.Services;

/// <summary>
/// Das aktuell laufende Spiel. Wird nach jedem Wurf im localStorage gesichert, damit ein
/// Neuladen der Seite oder ein leerer Akku das Spiel nicht beendet.
/// </summary>
public sealed class GameSession(LocalStore store, DataService data)
{
    private const string ActiveGameKey = "darts.activeGame";

    private sealed class Snapshot
    {
        public Guid Id { get; set; }
        public GameSettings Settings { get; set; } = new();
        public List<PlayerRef> Players { get; set; } = [];
        public List<string> Darts { get; set; } = [];
        public DateTimeOffset StartedAt { get; set; }
        public bool Saved { get; set; }
    }

    public GameEngine? Engine { get; private set; }
    public Guid GameId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public bool IsSaved { get; private set; }
    public bool IsSaving { get; private set; }
    public string? SaveError { get; private set; }

    public bool HasActiveGame => Engine is { IsFinished: false };

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        var snapshot = await store.GetAsync<Snapshot>(ActiveGameKey);
        if (snapshot is null || snapshot.Players.Count == 0) return;

        try
        {
            Engine = GameModes.Restore(snapshot.Settings, snapshot.Players, snapshot.Darts);
            GameId = snapshot.Id;
            StartedAt = snapshot.StartedAt;
            IsSaved = snapshot.Saved;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Gespeichertes Spiel konnte nicht wiederhergestellt werden: {ex.Message}");
            await store.RemoveAsync(ActiveGameKey);
        }
    }

    public async Task StartAsync(GameSettings settings, IReadOnlyList<PlayerRef> players)
    {
        Engine = GameModes.Create(settings, players.ToList());
        GameId = Guid.NewGuid();
        StartedAt = DateTimeOffset.UtcNow;
        IsSaved = false;
        SaveError = null;
        await PersistAsync();
        Changed?.Invoke();
    }

    /// <summary>Gleiche Einstellungen, Spielerreihenfolge rotiert (der Zweite beginnt).</summary>
    public Task RematchAsync()
    {
        if (Engine is null) return Task.CompletedTask;
        var players = Engine.Players.Skip(1).Append(Engine.Players[0]).ToList();
        return StartAsync(Engine.Settings, players);
    }

    public Task ThrowAsync(Dart dart) => MutateAsync(e => e.Throw(dart));
    public Task UndoAsync() => MutateAsync(e => e.Undo());
    public Task FinishTurnAsync() => MutateAsync(e => e.FinishTurn());

    private async Task MutateAsync(Action<GameEngine> action)
    {
        if (Engine is null) return;
        var wasFinished = Engine.IsFinished;
        action(Engine);

        // Wird ein gespeichertes Spiel per Undo wieder geöffnet, muss es erneut gespeichert werden.
        if (wasFinished && !Engine.IsFinished) IsSaved = false;

        await PersistAsync();
        Changed?.Invoke();

        if (Engine.IsFinished && !IsSaved)
            await SaveResultAsync();
    }

    public async Task SaveResultAsync()
    {
        if (Engine is null || IsSaving) return;
        IsSaving = true;
        SaveError = null;
        Changed?.Invoke();

        try
        {
            await data.SaveGameAsync(ToRecord());
            IsSaved = true;
            await PersistAsync();
        }
        catch (Exception ex)
        {
            SaveError = ex.Message;
        }
        finally
        {
            IsSaving = false;
            Changed?.Invoke();
        }
    }

    /// <summary>Bricht das laufende Spiel ab, ohne es zu speichern.</summary>
    public async Task AbandonAsync()
    {
        Engine = null;
        IsSaved = false;
        SaveError = null;
        await store.RemoveAsync(ActiveGameKey);
        Changed?.Invoke();
    }

    public GameRecord ToRecord()
    {
        var engine = Engine ?? throw new InvalidOperationException("Kein Spiel aktiv.");
        return new GameRecord
        {
            Id = GameId,
            Settings = engine.Settings,
            Players = engine.Players.ToList(),
            StartedAt = StartedAt,
            FinishedAt = engine.IsFinished ? DateTimeOffset.UtcNow : null,
            WinnerName = engine.Winner is { } w ? engine.Players[w].Name : null,
            Darts = engine.DartLog.Select(d => d.ToString()).ToList(),
            Results = engine.GetResults().ToList()
        };
    }

    private Task PersistAsync()
    {
        if (Engine is null) return Task.CompletedTask;
        return store.SetAsync(ActiveGameKey, new Snapshot
        {
            Id = GameId,
            Settings = Engine.Settings,
            Players = Engine.Players.ToList(),
            Darts = Engine.DartLog.Select(d => d.ToString()).ToList(),
            StartedAt = StartedAt,
            Saved = IsSaved
        });
    }
}
