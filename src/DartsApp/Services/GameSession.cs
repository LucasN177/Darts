using DartsApp.Core.Bots;
using DartsApp.Core.Engine;
using DartsApp.Core.Models;

namespace DartsApp.Services;

/// <summary>
/// Das aktuell laufende Spiel. Wird nach jedem Wurf im localStorage gesichert, damit ein
/// Neuladen der Seite oder ein leerer Akku das Spiel nicht beendet.
/// </summary>
public sealed class GameSession(LocalStore store, DataService data, UiState ui)
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
        StopBots();
        // Neuer Seed je Spiel (z.B. Zahlenvergabe bei Killer); er wird mit den Einstellungen gespeichert.
        settings = settings with { Seed = Random.Shared.Next(1, int.MaxValue) };
        Engine = GameModes.Create(settings, players.ToList());
        GameId = Guid.NewGuid();
        StartedAt = DateTimeOffset.UtcNow;
        IsSaved = false;
        SaveError = null;
        await PersistAsync();
        Changed?.Invoke();
        EnsureBotsPlaying();
    }

    /// <summary>Gleiche Einstellungen, Spielerreihenfolge rotiert (der Zweite beginnt).</summary>
    public Task RematchAsync()
    {
        if (Engine is null) return Task.CompletedTask;
        var players = Engine.Players.Skip(1).Append(Engine.Players[0]).ToList();
        return StartAsync(Engine.Settings, players);
    }

    /// <summary>Wurf eines Menschen. Während ein Computergegner dran ist, wird die Eingabe ignoriert.</summary>
    public Task ThrowAsync(Dart dart) => IsBotTurn ? Task.CompletedTask : MutateAsync(e => e.Throw(dart));

    public Task FinishTurnAsync() => IsBotTurn ? Task.CompletedTask : MutateAsync(e => e.FinishTurn());

    /// <summary>
    /// Nimmt den letzten Dart eines Menschen zurück. Würfe von Computergegnern danach werden ebenfalls
    /// entfernt, sonst würde der Computer sofort wieder werfen.
    /// </summary>
    public async Task UndoAsync()
    {
        StopBots();
        await MutateAsync(e =>
        {
            while (e.Turns.Count > 0)
            {
                var thrower = e.Turns[^1].PlayerIndex;
                e.Undo();
                if (!e.Players[thrower].IsBot) break;
            }
        });
    }

    private async Task MutateAsync(Action<GameEngine> action)
    {
        await MutateCoreAsync(action);
        EnsureBotsPlaying();
    }

    private async Task MutateCoreAsync(Action<GameEngine> action)
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

    // ---------- Computergegner ----------

    private CancellationTokenSource? _botCts;

    public bool IsBotTurn => Engine?.IsBotTurn == true;

    /// <summary>Startet die Würfe der Computergegner, falls einer dran ist und sie nicht schon laufen.</summary>
    public void EnsureBotsPlaying()
    {
        if (!IsBotTurn || _botCts is not null) return;
        var cts = new CancellationTokenSource();
        _botCts = cts;
        _ = RunBotsAsync(cts);
    }

    private void StopBots()
    {
        _botCts?.Cancel();
        _botCts = null;
    }

    private async Task RunBotsAsync(CancellationTokenSource cts)
    {
        try
        {
            while (!cts.IsCancellationRequested && Engine is { IsBotTurn: true } engine)
            {
                // Zwischen zwei Aufnahmen etwas länger warten, damit man den Spielstand sieht.
                var delay = engine.DartsInCurrentTurn == 0 ? ui.BotTurnDelay : ui.BotDartDelay;
                await Task.Delay(delay, cts.Token);
                if (cts.IsCancellationRequested || Engine != engine || !engine.IsBotTurn) break;

                var level = engine.Players[engine.CurrentPlayer].Bot!.Value;
                var dart = Bot.NextDart(engine, level, Random.Shared);
                await MutateCoreAsync(e => e.Throw(dart));
            }
        }
        catch (OperationCanceledException)
        {
            // Spiel abgebrochen oder Undo – nichts zu tun.
        }
        finally
        {
            if (_botCts == cts) _botCts = null;
        }
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
        StopBots();
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
