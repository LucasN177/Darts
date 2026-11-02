using System.Reflection;

namespace DartsApp;

/// <summary>Versionsangaben der App für Navigation und Einstellungen.</summary>
public static class AppInfo
{
    // InformationalVersion enthält beim Build aus Git den Commit, z.B. "2.0.0+fe96620a…".
    public static readonly string InformationalVersion =
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";

    public static readonly string Version = FormatVersion(InformationalVersion);

    private static string FormatVersion(string informational)
    {
        if (string.IsNullOrEmpty(informational)) return "–";
        var parts = informational.Split('+', 2);
        return parts.Length == 2 && parts[1].Length >= 7 ? $"{parts[0]} ({parts[1][..7]})" : parts[0];
    }
}
