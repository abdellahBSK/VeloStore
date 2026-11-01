using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using VeloStore.Application.Options;
using VeloStore.Application.Services;
using VeloStore.Data;
using VeloStore.Domain.Interfaces;
using VeloStore.Infrastructure.AI;
using VeloStore.Infrastructure.Background;
using VeloStore.Infrastructure.Data;
using VeloStore.Models;
using VeloStore.Services;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// =====================
// CONFIGURATION
// =====================
builder.Services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
builder.Services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));

var cacheOptions = configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new CacheOptions();

string RequiredConnectionString(string name) =>
    configuration.GetConnectionString(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"Connection string '{name}' is missing. Set ConnectionStrings:{name} in appsettings, user secrets or environment variables.");

// =====================
// WEB
// =====================
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();

// The session identifies guest carts and stores the assistant's chat history.
// It is backed by IDistributedCache, which is Redis (registered below).
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(cacheOptions.CartExpirationHours);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// =====================
// DATA: SQL Server (catalog, users, carts)
// =====================
builder.Services.AddDbContext<VeloStoreDbContext>(options =>
    options.UseSqlServer(
        RequiredConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<VeloStoreDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// Development-only email sender (no email is actually sent).
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, EmailSender>();

// =====================
// CACHING: L1 memory -> L2 Redis -> L3 SQL Server
// =====================
builder.Services.AddMemoryCache();
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = RequiredConnectionString("Redis");
    options.InstanceName = cacheOptions.RedisInstanceName;
});

builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IProductCacheService, ProductCacheService>();

// =====================
// AI ASSISTANT: PostgreSQL + pgvector, Ollama
// =====================
var vectorDataSourceBuilder = new NpgsqlDataSourceBuilder(RequiredConnectionString("VelostoreAiConnection"));
vectorDataSourceBuilder.UseVector();
builder.Services.AddSingleton(vectorDataSourceBuilder.Build());

builder.Services.AddScoped<IVectorRepository, PgVectorRepository>();
builder.Services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddScoped<IRetrievalService, RetrievalService>();
builder.Services.AddScoped<IRagOrchestrator, RagOrchestrator>();
builder.Services.AddHostedService<ProductIndexingWorker>();

builder.Services.AddHttpClient(RagOrchestrator.HttpClientName, (services, client) =>
    {
        var ollama = services.GetRequiredService<IOptions<OllamaOptions>>().Value;
        client.BaseAddress = new Uri(ollama.BaseUrl);
    })
    .AddStandardResilienceHandler(options =>
    {
        // Local LLMs on modest hardware are slow, so the timeouts are generous.
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    });

// =====================
// HEALTH CHECKS
// =====================
builder.Services.AddHealthChecks()
    .AddDbContextCheck<VeloStoreDbContext>("sqlserver");

// =====================
// PIPELINE
// =====================
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHealthChecks("/health");

DbInitializer.Seed(app);

app.Run();

/// <summary>Exposed so integration tests can reference the app's entry point.</summary>
public partial class Program { }
