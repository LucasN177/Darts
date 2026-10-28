using System.Text.Json.Serialization;
using DartsApp.Core.Models;

namespace DartsApp.Services;

public interface IDataRepository
{
    Task<IReadOnlyList<Player>> GetPlayersAsync();
    Task<Player> AddPlayerAsync(string name);
    Task RenamePlayerAsync(Guid id, string name);
    Task DeletePlayerAsync(Guid id);

    Task SaveGameAsync(GameRecord game);
    Task<IReadOnlyList<GameRecord>> GetGamesAsync();
    Task DeleteGameAsync(Guid id);
}

/// <summary>Speicherung im Browser (localStorage) für Spiele ohne Anmeldung.</summary>
public sealed class LocalRepository(LocalStore store) : IDataRepository
{
    private const string PlayersKey = "darts.players";
    private const string GamesKey = "darts.games";
    private const int MaxGames = 500;

    public async Task<IReadOnlyList<Player>> GetPlayersAsync() =>
        (await store.GetAsync<List<Player>>(PlayersKey) ?? []).OrderBy(p => p.Name).ToList();

    public async Task<Player> AddPlayerAsync(string name)
    {
        var players = await store.GetAsync<List<Player>>(PlayersKey) ?? [];
        var player = new Player { Name = name.Trim() };
        players.Add(player);
        await store.SetAsync(PlayersKey, players);
        return player;
    }

    public async Task RenamePlayerAsync(Guid id, string name)
    {
        var players = await store.GetAsync<List<Player>>(PlayersKey) ?? [];
        if (players.FirstOrDefault(p => p.Id == id) is { } player) player.Name = name.Trim();
        await store.SetAsync(PlayersKey, players);
    }

    public async Task DeletePlayerAsync(Guid id)
    {
        var players = await store.GetAsync<List<Player>>(PlayersKey) ?? [];
        players.RemoveAll(p => p.Id == id);
        await store.SetAsync(PlayersKey, players);
    }

    public async Task SaveGameAsync(GameRecord game)
    {
        var games = await store.GetAsync<List<GameRecord>>(GamesKey) ?? [];
        games.RemoveAll(g => g.Id == game.Id);
        games.Insert(0, game);
        if (games.Count > MaxGames) games.RemoveRange(MaxGames, games.Count - MaxGames);
        await store.SetAsync(GamesKey, games);
    }

    public async Task<IReadOnlyList<GameRecord>> GetGamesAsync() =>
        (await store.GetAsync<List<GameRecord>>(GamesKey) ?? []).OrderByDescending(g => g.FinishedAt ?? g.StartedAt).ToList();

    public async Task DeleteGameAsync(Guid id)
    {
        var games = await store.GetAsync<List<GameRecord>>(GamesKey) ?? [];
        games.RemoveAll(g => g.Id == id);
        await store.SetAsync(GamesKey, games);
    }

    public Task ClearGamesAsync() => store.RemoveAsync(GamesKey);
}

/// <summary>Speicherung in Supabase für angemeldete Benutzer.</summary>
public sealed class SupabaseRepository(SupabaseRest rest) : IDataRepository
{
    private const string Players = "darts_players";
    private const string Games = "darts_games";
    private const string GamePlayers = "darts_game_players";
    private const string Profiles = "darts_profiles";

    public async Task<IReadOnlyList<Player>> GetPlayersAsync()
    {
        var rows = await rest.GetAsync<DbPlayer>(Players, "select=id,name,is_self,created_at&order=is_self.desc,name.asc");
        return rows.Select(r => new Player { Id = r.Id, Name = r.Name, IsSelf = r.IsSelf, CreatedAt = r.CreatedAt ?? default }).ToList();
    }

    public async Task<Player> AddPlayerAsync(string name)
    {
        var row = await rest.InsertSingleAsync(Players, new DbPlayer { Id = Guid.NewGuid(), Name = name.Trim() });
        return new Player { Id = row.Id, Name = row.Name, IsSelf = row.IsSelf };
    }

    public Task RenamePlayerAsync(Guid id, string name) =>
        rest.PatchAsync(Players, $"id=eq.{id}", new { name = name.Trim() });

    public Task DeletePlayerAsync(Guid id) => rest.DeleteAsync(Players, $"id=eq.{id}&is_self=eq.false");

    public async Task SaveGameAsync(GameRecord game)
    {
        // Spieler-IDs, die nicht (mehr) in der Spielerliste existieren (z.B. Gäste oder lokal
        // angelegte Spieler), würden den Fremdschlüssel verletzen.
        var knownIds = (await GetPlayersAsync()).Select(p => p.Id).ToHashSet();
        Guid? Known(Guid? id) => id is { } g && knownIds.Contains(g) ? g : null;

        await rest.UpsertAsync(Games, new DbGame
        {
            Id = game.Id,
            Mode = game.Mode.ToString(),
            Settings = game.Settings,
            Players = game.Players.Select(p => new PlayerRef(Known(p.Id), p.Name)).ToList(),
            Darts = game.Darts,
            WinnerName = game.WinnerName,
            StartedAt = game.StartedAt,
            FinishedAt = game.FinishedAt
        });

        await rest.DeleteAsync(GamePlayers, $"game_id=eq.{game.Id}");
        await rest.InsertAsync(GamePlayers, game.Results.Select(r => DbGamePlayer.From(game.Id, r, Known(r.PlayerId))).ToList());
    }

    public async Task<IReadOnlyList<GameRecord>> GetGamesAsync()
    {
        var rows = await rest.GetAsync<DbGame>(Games,
            "select=id,mode,settings,players,darts,winner_name,started_at,finished_at,darts_game_players(*)&order=finished_at.desc.nullslast&limit=1000");

        return rows.Select(r => new GameRecord
        {
            Id = r.Id,
            Settings = r.Settings ?? GameSettings.DefaultFor(Enum.TryParse<GameMode>(r.Mode, out var m) ? m : GameMode.X01),
            Players = r.Players ?? [],
            Darts = r.Darts ?? [],
            WinnerName = r.WinnerName,
            StartedAt = r.StartedAt,
            FinishedAt = r.FinishedAt,
            Results = (r.GamePlayers ?? []).OrderBy(p => p.Position).Select(p => p.ToResult()).ToList()
        }).ToList();
    }

    public Task DeleteGameAsync(Guid id) => rest.DeleteAsync(Games, $"id=eq.{id}");

    /// <summary>Legt nach der Anmeldung Profil und den eigenen Spieler an, falls noch nicht vorhanden.</summary>
    public async Task EnsureProfileAsync(Guid userId, string displayName)
    {
        await rest.UpsertAsync(Profiles, new DbProfile { Id = userId, DisplayName = displayName }, ignoreDuplicates: true);

        var self = await rest.GetAsync<DbPlayer>(Players, "select=id,name,is_self&is_self=eq.true&limit=1");
        if (self.Count == 0)
            await rest.InsertAsync(Players, new[] { new DbPlayer { Id = Guid.NewGuid(), Name = displayName, IsSelf = true } });
    }

    public async Task<DbProfile?> GetProfileAsync(Guid userId) =>
        (await rest.GetAsync<DbProfile>(Profiles, $"select=id,display_name,first_name,last_name&id=eq.{userId}")).FirstOrDefault();

    public async Task SaveProfileAsync(DbProfile profile)
    {
        await rest.UpsertAsync(Profiles, profile);
        await rest.PatchAsync(Players, "is_self=eq.true", new { name = profile.DisplayName });
    }
}

public sealed class DbProfile
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public sealed class DbPlayer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsSelf { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
}

public sealed class DbGame
{
    public Guid Id { get; set; }
    public string Mode { get; set; } = "";
    public GameSettings? Settings { get; set; }
    public List<PlayerRef>? Players { get; set; }
    public List<string>? Darts { get; set; }
    public string? WinnerName { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    [JsonPropertyName("darts_game_players")]
    public List<DbGamePlayer>? GamePlayers { get; set; }
}

public sealed class DbGamePlayer
{
    public Guid GameId { get; set; }
    public Guid? PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public int Position { get; set; }
    public int Rank { get; set; }
    public bool IsWinner { get; set; }
    public int DartsThrown { get; set; }
    public int Score { get; set; }
    public double Average { get; set; }
    [JsonPropertyName("first9_average")]
    public double? First9Average { get; set; }
    public int HighestTurn { get; set; }
    public int HighestCheckout { get; set; }
    public int CheckoutAttempts { get; set; }
    public int CheckoutsHit { get; set; }
    [JsonPropertyName("count_180")]
    public int Count180 { get; set; }
    [JsonPropertyName("count_140")]
    public int Count140 { get; set; }
    [JsonPropertyName("count_100")]
    public int Count100 { get; set; }
    public int LegsWon { get; set; }
    public int SetsWon { get; set; }
    public Dictionary<string, double>? Extra { get; set; }

    public static DbGamePlayer From(Guid gameId, PlayerResult r, Guid? playerId) => new()
    {
        GameId = gameId,
        PlayerId = playerId,
        PlayerName = r.Name,
        Position = r.Position,
        Rank = r.Rank,
        IsWinner = r.IsWinner,
        DartsThrown = r.DartsThrown,
        Score = r.Score,
        Average = r.Average,
        First9Average = r.First9Average,
        HighestTurn = r.HighestTurn,
        HighestCheckout = r.HighestCheckout,
        CheckoutAttempts = r.CheckoutAttempts,
        CheckoutsHit = r.CheckoutsHit,
        Count180 = r.Count180,
        Count140 = r.Count140,
        Count100 = r.Count100,
        LegsWon = r.LegsWon,
        SetsWon = r.SetsWon,
        Extra = r.Extra
    };

    public PlayerResult ToResult() => new()
    {
        PlayerId = PlayerId,
        Name = PlayerName,
        Position = Position,
        Rank = Rank,
        IsWinner = IsWinner,
        DartsThrown = DartsThrown,
        Score = Score,
        Average = Average,
        First9Average = First9Average,
        HighestTurn = HighestTurn,
        HighestCheckout = HighestCheckout,
        CheckoutAttempts = CheckoutAttempts,
        CheckoutsHit = CheckoutsHit,
        Count180 = Count180,
        Count140 = Count140,
        Count100 = Count100,
        LegsWon = LegsWon,
        SetsWon = SetsWon,
        Extra = Extra ?? new()
    };
}
