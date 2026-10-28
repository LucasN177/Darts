using System.Globalization;
using DartsApp;
using DartsApp.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("de-DE");

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton(builder.Configuration.GetSection("Supabase").Get<SupabaseConfig>() ?? new SupabaseConfig());

builder.Services.AddScoped<LocalStore>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SupabaseRest>();
builder.Services.AddScoped<LocalRepository>();
builder.Services.AddScoped<SupabaseRepository>();
builder.Services.AddScoped<DataService>();
builder.Services.AddScoped<GameSession>();
builder.Services.AddScoped<UiState>();

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.VisibleStateDuration = 3000;
});

var host = builder.Build();

var auth = host.Services.GetRequiredService<AuthService>();
var data = host.Services.GetRequiredService<DataService>();
await auth.InitializeAsync();
if (auth.IsLoggedIn) _ = data.EnsureProfileSafeAsync();
await host.Services.GetRequiredService<GameSession>().InitializeAsync();
await host.Services.GetRequiredService<UiState>().InitializeAsync();

await host.RunAsync();
