using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Warhammer;
using Warhammer.Services;
using Warhammer.Calculator;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<GameSystemService>();
builder.Services.AddScoped<CoreRulesService>();
builder.Services.AddScoped<BundleService>();
builder.Services.AddScoped<IGameDataRepository, GameDataRepository>();
builder.Services.AddTransient<CombatEngine>();
builder.Services.AddSingleton<ArmyListValidator>();
builder.Services.AddScoped<ArmyListSerializer>();
builder.Services.AddScoped<ArmyRosterBuilder>();
builder.Services.AddScoped<PortraitService>();

var host = builder.Build();

// Restore the persisted game system before any service loads data.
// WASM exposes a synchronous JS runtime, so this runs before RunAsync.
var systemService = host.Services.GetRequiredService<GameSystemService>();
try
{
    var js = (IJSInProcessRuntime)host.Services.GetRequiredService<IJSRuntime>();
    systemService.Initialize(js.Invoke<string?>("localStorage.getItem", GameSystemService.StorageKey));
}
catch
{
    // localStorage unavailable — keep the default system.
}

await host.RunAsync();
