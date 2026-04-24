using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Warhammer;
using Warhammer.Services;
using Warhammer.Calculator;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<CoreRulesService>();
builder.Services.AddScoped<FactionDataService>();
builder.Services.AddScoped<IGameDataRepository, GameDataRepository>();
builder.Services.AddTransient<CombatEngine>();
builder.Services.AddSingleton<ArmyListValidator>();
builder.Services.AddScoped<ArmyListSerializer>();
builder.Services.AddScoped<PortraitService>();

await builder.Build().RunAsync();
