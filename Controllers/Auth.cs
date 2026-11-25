using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text;
using System.Text.Unicode;
using Trpo8.Models;
using Trpo8.Services;


using Microsoft.AspNetCore.Authorization;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Trpo8.Controllers
{
    [Route("api/v1/auth")]
    [ApiController]

    public class Auth : ControllerBase
    {
        private readonly AppDbContext _db;

        private readonly IConfiguration _configuration;

        private readonly IPasswordHasher<User> _hasher;

        private readonly RefreshTokenService _refreshService;

        private readonly RevokedTokenService _revokedService;

        public Auth
            (
            AppDbContext users,
            IPasswordHasher<User> haseher,
            IConfiguration configuration,
            RefreshTokenService refreshService,
            RevokedTokenService revokedService
            )
        {
            _db = users;
            _hasher = haseher;
            _configuration = configuration;
            _refreshService = refreshService;
            _revokedService = revokedService;
        }


        public record RefreshRequest([property: JsonPropertyName("refresh_token")] string? RefreshToken);


        public record AuthRequest(string Login, string Password);

        [HttpPost("register")]
        public IActionResult Register(AuthRequest request)
        {
            if (_db.Users.Any(u => u.Login == request.Login))
                return Conflict("Логин уже существует");

            var user = new User
            {
                Login = request.Login,
                Role = "User"
            };

            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            //user.PasswordHash = _hasher.HashPassword(user, request.Password);

            _db.Users.Add(user);
            _db.SaveChanges();

            return Ok("Пользователь добавлен");
        }

        [HttpPost("login")]
        public IActionResult Login(AuthRequest request)
        {
            var user = _db.Users.FirstOrDefault(u => u.Login == request.Login);

            if (user == null)
            {
                return Unauthorized("неверная пара логин пароль");
            }

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("неверный логин или пароль");

            //var token = GenerateJwtToken(user);

            //return Ok(new
            //{
            //    access_token = token
            //});

            var accessToken = GenerateJwtToken(user);
            var refreshToken = _refreshService.Generate();

            _refreshService.Add(user.Id, refreshToken);
            _db.SaveChanges();

            return Ok(new
            {
                access_token = accessToken,
                refresh_token = refreshToken
            });


        }

        [HttpPost("refresh")]
        public IActionResult Refresh(RefreshRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken))
                return Unauthorized("refresh_token не передан");

            var stored = _refreshService.Find(request.RefreshToken);

            if (stored == null || stored.ExpiresAt < DateTime.UtcNow)
                return Unauthorized("refresh_token недействителен или истёк");

            var user = _db.Users.FirstOrDefault(u => u.Id == stored.UserId);
            if (user == null)
                return Unauthorized("пользователь не найден");

            using var tx = _db.Database.BeginTransaction();

            try
            {
                _refreshService.Remove(stored);

                var newAccessToken = GenerateJwtToken(user);
                var newRefreshToken = _refreshService.Generate();

                _refreshService.Add(user.Id, newRefreshToken);

                _db.SaveChanges();
                tx.Commit();

                return Ok(new
                {
                    access_token = newAccessToken,
                    refresh_token = newRefreshToken
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                tx.Rollback();
                return Unauthorized("refresh_token уже использован");
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout([FromBody] RefreshRequest? request = null)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken))
                return Unauthorized("refresh_token не передан");

            var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrEmpty(jti))
                return Unauthorized("В JWT нет jti");

            var login = User.FindFirst(ClaimTypes.Name)?.Value;
            var user = _db.Users.FirstOrDefault(u => u.Login == login);
            if (user == null)
                return Unauthorized("пользователь не найден");

            var stored = _refreshService.Find(request.RefreshToken);

            if (stored == null || stored.ExpiresAt < DateTime.UtcNow)
                return Unauthorized("refresh_token недействителен или истёк");

            if (stored.UserId != user.Id)
                return Forbid();

            using var tx = _db.Database.BeginTransaction();

            _refreshService.Remove(stored);
            _revokedService.Revoke(jti, DateTime.UtcNow.AddSeconds(60));

            _db.SaveChanges();
            tx.Commit();

            var revokedTokens = HttpContext.RequestServices
                .GetRequiredService<ConcurrentDictionary<string, DateTime>>();
            revokedTokens.TryAdd(jti, DateTime.UtcNow.AddSeconds(60));

            return NoContent();
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Login),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8. GetBytes(_configuration["Jwt:Key"]!));

            var credentials = new SigningCredentials (key,SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                
                issuer: "ExampleApi",
                audience: "ExampleApiClient",
                claims:  claims,
                expires: DateTime.UtcNow.AddSeconds(60),
                signingCredentials: credentials);



            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}
