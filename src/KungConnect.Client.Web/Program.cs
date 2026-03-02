using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using KungConnect.Client.Web;
using KungConnect.Client.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ServerUrl: defaults to same origin (server also hosts the WASM shell)
var serverUrl = builder.Configuration["ServerUrl"]
    ?? builder.HostEnvironment.BaseAddress.TrimEnd('/');

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(serverUrl) });
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<JoinSessionService>();

// Make ServerUrl injectable as IConfiguration["ServerUrl"]
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ServerUrl"] = serverUrl
});

await builder.Build().RunAsync();
