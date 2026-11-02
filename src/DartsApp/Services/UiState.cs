namespace DartsApp.Services;

/// <summary>Wo die Hauptnavigation angezeigt wird.</summary>
public enum NavigationStyle
{
    /// <summary>Seitenleiste links, auf kleinen Bildschirmen über das Menü-Symbol.</summary>
    Drawer,
    /// <summary>Leiste am unteren Bildschirmrand, mit dem Daumen erreichbar.</summary>
    BottomBar
}

/// <summary>Persistente UI-Einstellungen (Theme, Navigation, Tempo der Computergegner) im localStorage.</summary>
public sealed class UiState(LocalStore store)
{
    private const string Key = "darts.ui";

    private sealed class Data
    {
        public bool DarkMode { get; set; } = true;
        public bool FastBots { get; set; }
        /// <summary>Neue Spiele standardmäßig privat starten (nur die Einstellung, keine Spieldaten).</summary>
        public bool PrivateGames { get; set; }
        public NavigationStyle Navigation { get; set; } = NavigationStyle.Drawer;
    }

    private Data _data = new();

    public bool DarkMode => _data.DarkMode;
    public bool FastBots => _data.FastBots;
    public bool PrivateGames => _data.PrivateGames;
    public NavigationStyle Navigation => _data.Navigation;
    public bool BottomNavigation => _data.Navigation == NavigationStyle.BottomBar;

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

    public Task SetDarkModeAsync(bool value)
    {
        _data.DarkMode = value;
        return SaveAsync();
    }

    public Task SetNavigationAsync(NavigationStyle value)
    {
        _data.Navigation = value;
        return SaveAsync();
    }

    public Task SetFastBotsAsync(bool value)
    {
        _data.FastBots = value;
        return SaveAsync();
    }

    public Task SetPrivateGamesAsync(bool value)
    {
        _data.PrivateGames = value;
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
