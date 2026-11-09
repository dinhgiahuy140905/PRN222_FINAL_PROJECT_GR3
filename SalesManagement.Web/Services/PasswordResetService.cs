using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;

namespace SalesManagement.Web.Services;

public sealed class PasswordResetService(IDataProtectionProvider protection, ApplicationDbContext db, IPasswordHasher<User> hasher)
{
    private readonly ITimeLimitedDataProtector protector = protection.CreateProtector("SalesHub.PasswordReset.v1").ToTimeLimitedDataProtector();
    public string CreateToken(User user, TimeSpan? lifetime = null) =>
        protector.Protect($"{user.Id}:{user.SessionVersion}", lifetime ?? TimeSpan.FromMinutes(30));

    public async Task<bool> ResetAsync(string token, string password)
    {
        int id, version;
        try
        {
            var parts = protector.Unprotect(token).Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out id) || !int.TryParse(parts[1], out version) || version == int.MaxValue)
                return false;
        }
        catch (Exception error) when (error is CryptographicException or FormatException or ArgumentException) { return false; }
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id && user.SessionVersion == version);
        if (user is null) return false;
        var hash = hasher.HashPassword(user, password);
        var now = DateTime.Now;
        // One atomic update prevents concurrent redemption and token replays.
        return await db.Users.Where(user => user.Id == id && user.SessionVersion == version)
            .ExecuteUpdateAsync(update => update
                .SetProperty(user => user.PasswordHash, hash)
                .SetProperty(user => user.PasswordChangedAt, now)
                .SetProperty(user => user.SessionVersion, version + 1)) == 1;
    }
}
