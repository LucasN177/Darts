using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace DartsApp.Services;

/// <summary>Kleiner Wrapper um window.localStorage mit JSON-Serialisierung.</summary>
public sealed class LocalStore(IJSRuntime js)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", key);
            return string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or JSException or NotSupportedException)
        {
            Console.WriteLine($"LocalStore: '{key}' konnte nicht gelesen werden: {ex.Message}");
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", key, JsonSerializer.Serialize(value, JsonOptions));
        }
        catch (JSException ex)
        {
            Console.WriteLine($"LocalStore: '{key}' konnte nicht gespeichert werden: {ex.Message}");
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (JSException)
        {
            // Speicher nicht verfügbar (z.B. privater Modus) – nichts zu tun.
        }
    }
}
