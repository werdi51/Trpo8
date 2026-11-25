using Microsoft.AspNetCore.Identity;
using System.Text;
using Trpo8.Models;
using Trpo8.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>();

builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddScoped<RevokedTokenService>();

// Add services to the container.

var key = builder.Configuration["Jwt:Key"] ?? throw new Exception("Не задан секретный ключ JWT");

if (Encoding.UTF8.GetByteCount(key)<32)
{
    throw new Exception("Ключ JWT короткий");
}



builder.Services.AddSingleton<ConcurrentDictionary<string, DateTime>>();



builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {



        ValidateIssuer = true,
        ValidIssuer = "ExampleApi",

        ValidateAudience = true,
        ValidAudience = "ExampleApiClient",

        ValidateLifetime = true,

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)
        ),
        ClockSkew = TimeSpan.Zero
    };



    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            if (string.IsNullOrEmpty(jti))
            {
                context.Fail("В JWT нет jti");
                return System.Threading.Tasks.Task.CompletedTask;
            }

            var revokedTokens = context.HttpContext.RequestServices
                .GetRequiredService<ConcurrentDictionary<string, DateTime>>();

            var revokedService = context.HttpContext.RequestServices
                .GetRequiredService<RevokedTokenService>();

            if (revokedTokens.ContainsKey(jti) || revokedService.IsRevoked(jti))
            {
                context.Fail("JWT отозван");
            }

            return System.Threading.Tasks.Task.CompletedTask;
        }
    };
});


//builder.Services.AddSingleton<List<User>>();

builder.Services.AddSingleton<IPasswordHasher<User>,PasswordHasher<User>>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    var admin = db.Users.FirstOrDefault(u => u.Login == "admin123");

    if (admin == null)
    {
        db.Users.Add(new User
        {
            Login = "admin123",
            Role = "Admin",
            PasswordHash = hasher.HashPassword(null!, "P@s$w0rd")
        });
        db.SaveChanges();
    }
    else if (admin.PasswordHash == "PLACEHOLDER")
    {
        admin.PasswordHash = hasher.HashPassword(admin, "P@s$w0rd");
        db.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
