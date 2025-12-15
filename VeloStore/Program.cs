using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using VeloStore.Data;
using VeloStore.Services;

var builder = WebApplication.CreateBuilder(args);

// =====================
// SERVICES
// =====================

// Razor Pages
builder.Services.AddRazorPages();

// DbContext + SQL Server
builder.Services.AddDbContext<VeloStoreDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"))
);

// 🔹 SESSION (PANIER)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 🔹 HttpContextAccessor (OBLIGATOIRE pour Session)
builder.Services.AddHttpContextAccessor();

// 🔹 Service Panier
builder.Services.AddScoped<CartService>();

var app = builder.Build();

// =====================
// MIDDLEWARE
// =====================

app.UseStaticFiles();
app.UseRouting();

// ⚠️ OBLIGATOIRE POUR LE PANIER
app.UseSession();

app.MapRazorPages();

app.Run();
