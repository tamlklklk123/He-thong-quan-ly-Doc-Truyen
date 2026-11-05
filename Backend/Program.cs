using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using ServerCSharp.Services;
using Microsoft.OpenApi.Models;
using ServerCSharp.Data;

var builder = WebApplication.CreateBuilder(args);

// Add Database with SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=WebDocTruyenDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add API Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

// Enable CORS for Frontend & External clients
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Authentication (JWT for REST API)
var jwtKey = builder.Configuration["Jwt:Key"] ?? "web_doc_truyen_secret_key_csharp_2026_super_secure!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var claim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user = int.TryParse(claim, out int id)
                ? await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) : null;
            if (UserModeration.IsLoginBlocked(user) || user == null ||
                context.Principal?.IsInRole(user.Role) != true)
                context.Fail("Tài khoản không còn quyền sử dụng phiên đăng nhập này.");
        }
    };
});

builder.Services.AddAuthorization();

// Add Swagger with Bearer Token support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Web Đọc Truyện - Backend REST API (ASP.NET Core 8)",
        Version = "v1",
        Description = "Hệ thống Backend REST API cung cấp dịch vụ quản lý truyện, chương, tương tác, phân quyền tác giả & quản trị viên."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập JWT Bearer token theo định dạng: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed Database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbInitializer.Initialize(context);
}

// Enable CORS
app.UseCors("AllowAll");

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Web Đọc Truyện API v1");
    c.RoutePrefix = "swagger";
});

// Redirect root to Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
