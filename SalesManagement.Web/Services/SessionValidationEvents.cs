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
        var stampClaim = context.Principal?.FindFirstValue(StoreClaimTypes.SecurityStamp);
        if (int.TryParse(userIdClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) &&
            !string.IsNullOrEmpty(stampClaim))
        {
            // Check every authenticated request so old sessions are revoked immediately.
            var currentStamp = await db.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.SecurityStamp)
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (currentStamp is not null && currentStamp == stampClaim)
                return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
