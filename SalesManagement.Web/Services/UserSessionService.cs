using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SalesManagement.Web.Models;

namespace SalesManagement.Web.Services
{
    public interface IUserSessionService
    {
        Task SignInAsync(User user, StoreMember? membership, bool isPersistent);
    }

    public sealed class UserSessionService : IUserSessionService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserSessionService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Task SignInAsync(User user, StoreMember? membership, bool isPersistent)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email)
            };

            if (!string.IsNullOrWhiteSpace(user.AvatarPath))
            {
                claims.Add(new Claim(StoreClaimTypes.AvatarPath, user.AvatarPath));
            }

            if (membership?.Store is not null)
            {
                claims.Add(new Claim(StoreClaimTypes.StoreId, membership.StoreId.ToString()));
                claims.Add(new Claim(StoreClaimTypes.StoreName, membership.Store.Name));
                claims.Add(new Claim(StoreClaimTypes.StoreRole, membership.Role.ToString()));
            }

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("Không tìm thấy HTTP context hiện tại.");

            return httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = isPersistent });
        }
    }
}
