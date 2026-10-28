using System.Net.Http.Json;

namespace DartsApp.Services;

/// <summary>Minimaler PostgREST-Client für die Supabase Data API.</summary>
public sealed class SupabaseRest(HttpClient http, SupabaseConfig config, AuthService auth)
{
    public async Task<List<T>> GetAsync<T>(string table, string query)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{table}?{query}", null, null);
        return await response.Content.ReadFromJsonAsync<List<T>>(SupabaseConfig.JsonOptions) ?? [];
    }

    public async Task InsertAsync<T>(string table, IReadOnlyCollection<T> rows)
    {
        if (rows.Count == 0) return;
        using var _ = await SendAsync(HttpMethod.Post, table, rows, "return=minimal");
    }

    public async Task<T> InsertSingleAsync<T>(string table, T row)
    {
        using var response = await SendAsync(HttpMethod.Post, table, row, "return=representation");
        var list = await response.Content.ReadFromJsonAsync<List<T>>(SupabaseConfig.JsonOptions);
        return list is { Count: > 0 } ? list[0] : row;
    }

    public async Task UpsertAsync<T>(string table, T row, bool ignoreDuplicates = false)
    {
        var prefer = ignoreDuplicates ? "resolution=ignore-duplicates,return=minimal" : "resolution=merge-duplicates,return=minimal";
        using var _ = await SendAsync(HttpMethod.Post, table, new[] { row }, prefer);
    }

    public async Task PatchAsync(string table, string filter, object values)
    {
        using var _ = await SendAsync(HttpMethod.Patch, $"{table}?{filter}", values, "return=minimal");
    }

    public async Task DeleteAsync(string table, string filter)
    {
        using var _ = await SendAsync(HttpMethod.Delete, $"{table}?{filter}", null, "return=minimal");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, string? prefer)
    {
        var token = await auth.GetAccessTokenAsync();
        var request = new HttpRequestMessage(method, config.RestUrl(path));
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType(), options: SupabaseConfig.JsonOptions);
        if (prefer is not null)
            request.Headers.Add("Prefer", prefer);
        auth.AddHeaders(request, token);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            throw new SupabaseException($"Keine Verbindung zu Supabase: {ex.Message}");
        }
        finally
        {
            request.Dispose();
        }

        try
        {
            await AuthService.EnsureSuccessAsync(response);
        }
        catch
        {
            response.Dispose();
            throw;
        }

        return response;
    }
}
