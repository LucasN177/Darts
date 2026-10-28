using System.Text.Json;
using System.Text.Json.Serialization;

namespace DartsApp.Services;

public sealed class SupabaseConfig
{
    public string Url { get; set; } = "";
    /// <summary>Publishable (anon) Key – darf im Client stehen, der Schutz erfolgt über Row Level Security.</summary>
    public string Key { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Key);

    public string AuthUrl(string path) => $"{Url.TrimEnd('/')}/auth/v1/{path}";
    public string RestUrl(string path) => $"{Url.TrimEnd('/')}/rest/v1/{path}";

    /// <summary>JSON-Optionen für die Supabase-APIs (snake_case).</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = null,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed class SupabaseException(string message, int statusCode = 0) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
