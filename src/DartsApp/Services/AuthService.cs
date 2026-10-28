using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;

namespace DartsApp.Services;

public sealed class AuthUser
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public Dictionary<string, JsonElement>? UserMetadata { get; set; }

    public string? MetadataString(string key) =>
        UserMetadata is not null && UserMetadata.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

public sealed class AuthSession
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public long ExpiresIn { get; set; }
    /// <summary>Ablaufzeitpunkt als Unix-Zeit (Sekunden).</summary>
    public long ExpiresAt { get; set; }
    public AuthUser? User { get; set; }

    [JsonIgnore]
    public bool IsExpiringSoon => DateTimeOffset.FromUnixTimeSeconds(ExpiresAt) < DateTimeOffset.UtcNow.AddMinutes(1);
}

/// <summary>
/// Anmeldung über Supabase Auth (GoTrue REST API). Die Sitzung wird im localStorage gehalten
/// und vor Ablauf automatisch erneuert.
/// </summary>
public sealed class AuthService(HttpClient http, SupabaseConfig config, LocalStore store, NavigationManager navigation)
{
    private const string SessionKey = "darts.session";
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthSession? Session { get; private set; }
    public AuthUser? User => Session?.User;
    public bool IsLoggedIn => Session?.User is not null;
    public bool IsAvailable => config.IsConfigured;

    /// <summary>True, wenn die App über einen Passwort-Reset-Link geöffnet wurde.</summary>
    public bool IsPasswordRecovery { get; set; }

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (!config.IsConfigured) return;

        if (await TryReadSessionFromUrlAsync()) return;

        var stored = await store.GetAsync<AuthSession>(SessionKey);
        if (stored?.User is null) return;

        Session = stored;
        if (stored.IsExpiringSoon)
        {
            try
            {
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sitzung konnte nicht erneuert werden: {ex.Message}");
                if (ex is SupabaseException { StatusCode: >= 400 and < 500 })
                    await ClearSessionAsync();
            }
        }
    }

    /// <summary>Verarbeitet Links aus Bestätigungs- oder Passwort-Reset-Mails (#access_token=...).</summary>
    private async Task<bool> TryReadSessionFromUrlAsync()
    {
        var uri = new Uri(navigation.Uri);
        if (string.IsNullOrEmpty(uri.Fragment) || !uri.Fragment.Contains("access_token=")) return false;

        var values = uri.Fragment.TrimStart('#').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

        if (!values.TryGetValue("access_token", out var accessToken)) return false;

        var session = new AuthSession
        {
            AccessToken = accessToken,
            RefreshToken = values.GetValueOrDefault("refresh_token", ""),
            ExpiresAt = long.TryParse(values.GetValueOrDefault("expires_at"), out var exp)
                ? exp
                : DateTimeOffset.UtcNow.AddSeconds(long.TryParse(values.GetValueOrDefault("expires_in"), out var ein) ? ein : 3600).ToUnixTimeSeconds()
        };

        try
        {
            session.User = await GetUserAsync(session.AccessToken);
            await SetSessionAsync(session);
            IsPasswordRecovery = values.GetValueOrDefault("type") == "recovery";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Token aus URL ungültig: {ex.Message}");
        }

        navigation.NavigateTo(uri.GetLeftPart(UriPartial.Path), replace: true);
        return Session is not null;
    }

    public async Task SignInAsync(string email, string password)
    {
        var session = await PostAuthAsync<AuthSession>("token?grant_type=password", new { email, password });
        await SetSessionAsync(session);
    }

    /// <summary>Registriert einen Benutzer. Rückgabe true, wenn direkt eine Sitzung besteht (keine Mail-Bestätigung nötig).</summary>
    public async Task<bool> SignUpAsync(string email, string password, string displayName)
    {
        var redirect = Uri.EscapeDataString(navigation.BaseUri);
        var json = await PostAuthAsync<JsonElement>($"signup?redirect_to={redirect}",
            new { email, password, data = new { display_name = displayName } });

        if (json.TryGetProperty("access_token", out _))
        {
            await SetSessionAsync(json.Deserialize<AuthSession>(SupabaseConfig.JsonOptions)!);
            return true;
        }

        return false;
    }

    public async Task SendPasswordResetAsync(string email)
    {
        var redirect = Uri.EscapeDataString(navigation.BaseUri);
        await PostAuthAsync<JsonElement>($"recover?redirect_to={redirect}", new { email });
    }

    public async Task UpdatePasswordAsync(string password)
    {
        await UpdateUserAsync(new { password });
        IsPasswordRecovery = false;
    }

    public async Task UpdateMetadataAsync(object data) => await UpdateUserAsync(new { data });

    private async Task UpdateUserAsync(object body)
    {
        var token = await GetAccessTokenAsync() ?? throw new SupabaseException("Nicht angemeldet.");
        using var request = new HttpRequestMessage(HttpMethod.Put, config.AuthUrl("user"))
        {
            Content = JsonContent.Create(body, options: SupabaseConfig.JsonOptions)
        };
        AddHeaders(request, token);
        using var response = await http.SendAsync(request);
        await EnsureSuccessAsync(response);
        var user = await response.Content.ReadFromJsonAsync<AuthUser>(SupabaseConfig.JsonOptions);
        if (Session is not null && user is not null)
        {
            Session.User = user;
            await SetSessionAsync(Session);
        }
    }

    public async Task SignOutAsync()
    {
        if (Session is { } session)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, config.AuthUrl("logout"));
                AddHeaders(request, session.AccessToken);
                using var _ = await http.SendAsync(request);
            }
            catch (HttpRequestException)
            {
                // Offline abmelden ist ok – die lokale Sitzung wird trotzdem verworfen.
            }
        }

        await ClearSessionAsync();
    }

    /// <summary>Gültiges Access-Token (wird bei Bedarf erneuert) oder null, wenn nicht angemeldet.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        if (Session is null) return null;
        if (!Session.IsExpiringSoon) return Session.AccessToken;

        await RefreshAsync();
        return Session?.AccessToken;
    }

    private async Task RefreshAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            if (Session is null || !Session.IsExpiringSoon) return;
            var refreshed = await PostAuthAsync<AuthSession>("token?grant_type=refresh_token", new { refresh_token = Session.RefreshToken });
            await SetSessionAsync(refreshed);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<AuthUser> GetUserAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, config.AuthUrl("user"));
        AddHeaders(request, accessToken);
        using var response = await http.SendAsync(request);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<AuthUser>(SupabaseConfig.JsonOptions))!;
    }

    private async Task SetSessionAsync(AuthSession session)
    {
        if (session.ExpiresAt == 0 && session.ExpiresIn > 0)
            session.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(session.ExpiresIn).ToUnixTimeSeconds();

        Session = session;
        await store.SetAsync(SessionKey, session);
        Changed?.Invoke();
    }

    private async Task ClearSessionAsync()
    {
        Session = null;
        IsPasswordRecovery = false;
        await store.RemoveAsync(SessionKey);
        Changed?.Invoke();
    }

    private async Task<T> PostAuthAsync<T>(string path, object body)
    {
        if (!config.IsConfigured) throw new SupabaseException("Supabase ist nicht konfiguriert.");

        using var request = new HttpRequestMessage(HttpMethod.Post, config.AuthUrl(path))
        {
            Content = JsonContent.Create(body, options: SupabaseConfig.JsonOptions)
        };
        AddHeaders(request, null);
        using var response = await http.SendAsync(request);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(SupabaseConfig.JsonOptions))!;
    }

    internal void AddHeaders(HttpRequestMessage request, string? accessToken)
    {
        request.Headers.Add("apikey", config.Key);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken ?? config.Key);
    }

    internal static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        string? message = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            foreach (var key in new[] { "msg", "error_description", "message", "error" })
            {
                if (doc.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                {
                    message = v.GetString();
                    break;
                }
            }

            if (doc.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String && code.GetString() == "PGRST205")
                message = "Die Darts-Tabellen fehlen in Supabase. Bitte supabase/schema.sql im SQL Editor ausführen.";
        }
        catch (JsonException)
        {
            // Kein JSON – Statuscode reicht.
        }

        throw new SupabaseException(Translate(message) ?? $"Supabase-Fehler {(int)response.StatusCode}", (int)response.StatusCode);
    }

    private static string? Translate(string? message) => message switch
    {
        "Invalid login credentials" => "E-Mail oder Passwort ist falsch.",
        "Email not confirmed" => "Bitte bestätige zuerst deine E-Mail-Adresse.",
        "User already registered" => "Für diese E-Mail-Adresse gibt es bereits ein Konto.",
        _ when message?.StartsWith("Password should be at least") == true => "Das Passwort ist zu kurz (mindestens 6 Zeichen).",
        _ => message
    };
}
