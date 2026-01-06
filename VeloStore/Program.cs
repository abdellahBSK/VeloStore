using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using VeloStore.Data;
using VeloStore.Models;
using VeloStore.Services;

var builder = WebApplication.CreateBuilder(args);

// =====================
// CONFIGURATION
// =====================
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis connection string not found in configuration");

var redisInstanceName = builder.Configuration["CacheSettings:RedisInstanceName"] ?? "VeloStore:";

// =====================
// SERVICES REGISTRATION
// =====================

// Razor Pages
builder.Services.AddRazorPages();

// Session (for guest cart identification)
builder.Services.AddDistributedMemoryCache(); // Fallback if Redis unavailable
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(6); // Match cart expiration
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// DbContext
builder.Services.AddDbContext<VeloStoreDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Database connection string not found")));

// Identity with stronger password policy
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;
    
    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    
    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<VeloStoreDbContext>()
.AddDefaultTokenProviders();

// =====================
// CACHING - Multi-Layer Strategy
// =====================

// L1: In-Memory Cache (Fastest, per-server)
builder.Services.AddMemoryCache();

// L2: Redis Distributed Cache (Shared across servers)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = redisInstanceName;
});

// HttpContext Accessor
builder.Services.AddHttpContextAccessor();

// =====================
// BUSINESS SERVICES
// =====================

// Register services with interfaces for testability
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IProductCacheService, ProductCacheService>();

// Email sender (required by Identity)
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, EmailSender>();

// =====================
// LOGGING
// =====================
// Logging is configured in appsettings.json

// =====================
// BUILD APPLICATION
// =====================
var app = builder.Build();

// =====================
// MIDDLEWARE PIPELINE
// =====================

// Error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSession(); // Enable session middleware

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// =====================
// START APPLICATION
// =====================
app.Run();
