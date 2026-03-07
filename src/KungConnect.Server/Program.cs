using System.Text;
using KungConnect.Server.Configuration;
using KungConnect.Server.Data;
using KungConnect.Server.Hubs;
using KungConnect.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ────────────────────────────────────────────────────────────
builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection(ServerOptions.Section));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.Configure<RelayOptions>(builder.Configuration.GetSection(RelayOptions.Section));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.Section));
builder.Services.Configure<DownloadsOptions>(builder.Configuration.GetSection(DownloadsOptions.Section));

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Authentication / Authorization ───────────────────────────────────────────
var jwtOpts = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOpts.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOpts.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        // Support JWT in SignalR query string
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Application Services ─────────────────────────────────────────────────────
builder.Services.AddSingleton<IMachineRegistry, MachineRegistry>();builder.Services.AddSingleton<ISetupService, SetupService>();builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IJoinCodeService, JoinCodeService>();
builder.Services.AddSingleton<UpdateCheckStatusCache>();

// ── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR(opts =>
    opts.EnableDetailedErrors = builder.Environment.IsDevelopment());
// ── Health checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();
// ── API / OpenAPI ─────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new() { Title = "KungConnect API", Version = "v1" };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT bearer token"
        };
        return Task.CompletedTask;
    });
});

builder.Services.AddCors(opts => opts.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ── Auto-migrate DB on startup ───────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();          // /openapi/v1.json
    app.MapScalarApiReference(); // /scalar/v1
}

app.UseMiddleware<KungConnect.Server.Middleware.SetupMiddleware>(); // redirect to /setup.html when not configured

// Serve Blazor WASM static files — must register .dat (ICU globalization data)
// and other non-standard extensions that UseStaticFiles ignores by default.
var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".dat"] = "application/octet-stream";   // ICU globalization data
contentTypeProvider.Mappings[".blat"] = "application/octet-stream";  // Blazor assembly table (older builds)
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypeProvider });
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<SignalingHub>("/hubs/signaling");
app.MapFallbackToFile("index.html");  // SPA fallback for Blazor WASM routes

app.Run();

