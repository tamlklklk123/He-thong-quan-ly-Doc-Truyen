using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using ServerCSharp.Services;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add Database with SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=WebDocTruyenDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add SignalR for Realtime features
builder.Services.AddSignalR();

// Add MVC Controllers & Razor Views
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

// Add Cookie Authentication for Web Session & Role switching
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    // Read DB for every request: existing cookies cannot bypass account suspension.
    options.Events.OnValidatePrincipal = async context =>
    {
        var idText = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = int.TryParse(idText, out int userId)
            ? await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId) : null;
        if (UserModeration.IsLoginBlocked(user))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }
        if (user != null && context.Principal?.IsInRole(user.Role) != true)
        {
            // Role was updated since cookie was issued; invalidate obsolete permissions.
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    };
});

builder.Services.AddAuthorization();

// HttpClient to communicate with Backend Web API
var backendUrl = builder.Configuration["BackendApi:BaseUrl"] ?? "http://localhost:5000";
builder.Services.AddHttpClient("BackendApi", client =>
{
    client.BaseAddress = new Uri(backendUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Enable Static Files (css/site.css, images, js)
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Map SignalR Hub
app.MapHub<RealtimeHub>("/realtimeHub");

// Map MVC Default Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed Database on startup if needed
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbInitializer.Initialize(context);
}

app.Run();
