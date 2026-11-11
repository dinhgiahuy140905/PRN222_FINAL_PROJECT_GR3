using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;

namespace SalesManagement.Web.Services;

public sealed class PasswordResetService(IDataProtectionProvider protection, ApplicationDbContext db, IPasswordHasher<User> hasher)
{
    private readonly ITimeLimitedDataProtector protector = protection.CreateProtector("SalesHub.PasswordReset.v2.SecurityStamp").ToTimeLimitedDataProtector();
    public string CreateToken(User user, TimeSpan? lifetime = null) =>
        protector.Protect($"{user.Id}:{user.SecurityStamp}", lifetime ?? TimeSpan.FromMinutes(30));

    public async Task<bool> ResetAsync(string token, string password)
    {
        int id;
        string stamp;
        try
        {
            var parts = protector.Unprotect(token).Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out id) || string.IsNullOrEmpty(parts[1]))
                return false;
            stamp = parts[1];
        }
        catch (Exception error) when (error is CryptographicException or FormatException or ArgumentException) { return false; }
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id && user.SecurityStamp == stamp);
        if (user is null) return false;
        var hash = hasher.HashPassword(user, password);
        var now = DateTime.Now;
        var nextStamp = Guid.NewGuid().ToString();
        // One atomic update prevents concurrent redemption and token replays.
        return await db.Users.Where(user => user.Id == id && user.SecurityStamp == stamp)
            .ExecuteUpdateAsync(update => update
                .SetProperty(user => user.PasswordHash, hash)
                .SetProperty(user => user.PasswordChangedAt, now)
                .SetProperty(user => user.SecurityStamp, nextStamp)) == 1;
    }
}
