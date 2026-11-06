using System.Security.Claims;
using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;

namespace SalesManagement.Web.Services;

public sealed class SessionValidationEvents(ApplicationDbContext db) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var versionClaim = context.Principal?.FindFirstValue(StoreClaimTypes.SessionVersion);
        if (int.TryParse(userIdClaim, out var userId) &&
            int.TryParse(versionClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            // Check every authenticated request so old sessions are revoked immediately.
            var currentVersion = await db.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => (int?)user.SessionVersion)
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (currentVersion is not null && currentVersion == version)
                return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
