namespace DartsApp.Services;

/// <summary>Persistente UI-Einstellungen (Theme, Tempo der Computergegner).</summary>
public sealed class UiState(LocalStore store)
{
    private const string Key = "darts.ui";

    private sealed class Data
    {
        public bool DarkMode { get; set; } = true;
        public bool FastBots { get; set; }
    }

    private Data _data = new();

    public bool DarkMode => _data.DarkMode;
    public bool FastBots => _data.FastBots;

    /// <summary>Pause vor dem ersten Wurf einer Computer-Aufnahme (ms).</summary>
    public int BotTurnDelay => FastBots ? 350 : 1100;
    /// <summary>Pause zwischen zwei Würfen eines Computergegners (ms).</summary>
    public int BotDartDelay => FastBots ? 200 : 650;

    public event Action? Changed;

    public async Task InitializeAsync() => _data = await store.GetAsync<Data>(Key) ?? new Data();

    public Task ToggleDarkModeAsync()
    {
        _data.DarkMode = !_data.DarkMode;
        return SaveAsync();
    }

    public Task ToggleFastBotsAsync()
    {
        _data.FastBots = !_data.FastBots;
        return SaveAsync();
    }

    private async Task SaveAsync()
    {
        await store.SetAsync(Key, _data);
        Changed?.Invoke();
    }
}
