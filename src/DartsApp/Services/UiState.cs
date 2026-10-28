namespace DartsApp.Services;

/// <summary>Persistente UI-Einstellungen (Theme, Eingabe).</summary>
public sealed class UiState(LocalStore store)
{
    private const string Key = "darts.ui";

    private sealed class Data
    {
        public bool DarkMode { get; set; } = true;
    }

    private Data _data = new();

    public bool DarkMode => _data.DarkMode;

    public event Action? Changed;

    public async Task InitializeAsync() => _data = await store.GetAsync<Data>(Key) ?? new Data();

    public async Task ToggleDarkModeAsync()
    {
        _data.DarkMode = !_data.DarkMode;
        await store.SetAsync(Key, _data);
        Changed?.Invoke();
    }
}
