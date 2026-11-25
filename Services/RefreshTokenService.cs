using System.Security.Cryptography;
using System.Text;
using Trpo8.Models;

namespace Trpo8.Services;

public class RefreshTokenService
{
    private readonly AppDbContext _db;

    public RefreshTokenService(AppDbContext db)
    {
        _db = db;
    }

    public string Generate()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public RefreshToken? Find(string token)
    {
        var hash = Hash(token);
        return _db.RefreshTokens.FirstOrDefault(t => t.TokenHash == hash);
    }

    public void Add(int userId, string token)
    {
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(token),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
    }

    public void Remove(RefreshToken token)
    {
        _db.RefreshTokens.Remove(token);
    }
}
