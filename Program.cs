using Microsoft.AspNetCore.Identity;
using System.Text;
using Trpo8.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var key = builder.Configuration["Jwt:Key"] ?? throw new Exception("Не задан секретный ключ JWT");

if (Encoding.UTF8.GetByteCount(key)<32)
{
    throw new Exception("Ключ JWT короткий");
}
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
});


//builder.Services.AddSingleton<List<User>>();

builder.Services.AddSingleton<IPasswordHasher<User>,PasswordHasher<User>>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Создаём админа admin123 / P@s$w0rd, если его ещё нет в БД
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

app.UseAuthorization();

app.MapControllers();

app.Run();
