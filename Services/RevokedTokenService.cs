using Trpo8.Models;

namespace Trpo8.Services;

public class RevokedTokenService
{
    private readonly AppDbContext _db;

    public RevokedTokenService(AppDbContext db)
    {
        _db = db;
    }

    public bool IsRevoked(string jti)
    {
        RemoveExpired();
        return _db.RevokedTokens.Any(t => t.Jti == jti);
    }

    public void Revoke(string jti, DateTime expiresAt)
    {
        _db.RevokedTokens.Add(new RevokedToken
        {
            Jti = jti,
            ExpiresAt = expiresAt
        });
    }

    public void RemoveExpired()
    {
        var expired = _db.RevokedTokens
            .Where(t => t.ExpiresAt < DateTime.UtcNow)
            .ToList();

        if (expired.Count > 0)
        {
            _db.RevokedTokens.RemoveRange(expired);
            _db.SaveChanges();
        }
    }
}
