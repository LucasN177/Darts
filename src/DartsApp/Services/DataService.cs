using DartsApp.Core.Models;

namespace DartsApp.Services;

/// <summary>
/// Zentrale Datenschicht der App: angemeldet wird in Supabase gespeichert, sonst lokal im Browser.
/// Spielliste und Spieler werden zwischengespeichert, bis sich etwas ändert.
/// </summary>
public sealed class DataService
{
    private readonly AuthService _auth;
    private readonly LocalRepository _local;
    private readonly SupabaseRepository _remote;
    private IReadOnlyList<GameRecord>? _games;
    private IReadOnlyList<Player>? _players;

    public DataService(AuthService auth, LocalRepository local, SupabaseRepository remote)
    {
        _auth = auth;
        _local = local;
        _remote = remote;
        _auth.Changed += () =>
        {
            Invalidate();
            if (_auth.IsLoggedIn) _ = EnsureProfileSafeAsync();
        };
    }

    public bool IsCloud => _auth.IsLoggedIn;
    private IDataRepository Repo => _auth.IsLoggedIn ? _remote : _local;

    public SupabaseRepository Remote => _remote;

    public event Action? Changed;

    /// <summary>Letzter Fehler beim Anlegen des Profils (z.B. fehlendes Schema).</summary>
    public string? SetupError { get; private set; }

    private void Invalidate()
    {
        _games = null;
        _players = null;
        Changed?.Invoke();
    }

    public async Task EnsureProfileSafeAsync()
    {
        if (_auth.User is not { } user) return;
        try
        {
            var name = user.MetadataString("display_name");
            if (string.IsNullOrWhiteSpace(name)) name = user.Email?.Split('@')[0] ?? "Ich";
            await _remote.EnsureProfileAsync(user.Id, name);
            SetupError = null;
            _players = null;
        }
        catch (Exception ex)
        {
            SetupError = ex.Message;
            Console.WriteLine($"Profil konnte nicht angelegt werden: {ex.Message}");
        }
        Changed?.Invoke();
    }

    public async Task<IReadOnlyList<Player>> GetPlayersAsync() => _players ??= await Repo.GetPlayersAsync();

    public async Task<Player> AddPlayerAsync(string name)
    {
        var player = await Repo.AddPlayerAsync(name);
        _players = null;
        Changed?.Invoke();
        return player;
    }

    public async Task RenamePlayerAsync(Guid id, string name)
    {
        await Repo.RenamePlayerAsync(id, name);
        _players = null;
        Changed?.Invoke();
    }

    public async Task DeletePlayerAsync(Guid id)
    {
        await Repo.DeletePlayerAsync(id);
        _players = null;
        Changed?.Invoke();
    }

    public async Task<IReadOnlyList<GameRecord>> GetGamesAsync() => _games ??= await Repo.GetGamesAsync();

    public async Task SaveGameAsync(GameRecord game)
    {
        await Repo.SaveGameAsync(game);
        _games = null;
        Changed?.Invoke();
    }

    public async Task DeleteGameAsync(Guid id)
    {
        await Repo.DeleteGameAsync(id);
        _games = null;
        Changed?.Invoke();
    }

    /// <summary>Anzahl lokal gespeicherter Spiele, die in das Konto übernommen werden können.</summary>
    public async Task<int> CountLocalGamesAsync() => (await _local.GetGamesAsync()).Count;

    /// <summary>Überträgt lokal gespeicherte Spiele ins Supabase-Konto und entfernt sie danach lokal.</summary>
    public async Task<int> ImportLocalGamesAsync()
    {
        if (!_auth.IsLoggedIn) return 0;
        var games = await _local.GetGamesAsync();
        foreach (var game in games)
            await _remote.SaveGameAsync(game);
        await _local.ClearGamesAsync();
        _games = null;
        Changed?.Invoke();
        return games.Count;
    }
}
