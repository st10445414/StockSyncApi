using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StockSyncApi.Data;
using StockSyncApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure database (PostgreSQL in production, InMemory for testing)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<StockSyncDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    builder.Services.AddDbContext<StockSyncDbContext>(options =>
        options.UseInMemoryDatabase("StockSyncDev"));
}

// Configure JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY");
if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException("JWT_KEY must be configured (appsettings or env var) and be at least 32 characters.");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "StockSync";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "StockSyncApp";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "StockSync API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
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

// Register services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductService, ProductService>();

// CORS for mobile client
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://10.0.2.2:5000" };
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Auto-apply migrations in production
if (!string.IsNullOrEmpty(connectionString))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<StockSyncDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment() ||
    app.Configuration.GetValue<bool>("EnableSwaggerForDemo"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
