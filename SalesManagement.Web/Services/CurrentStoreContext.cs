using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;

namespace SalesManagement.Web.Services
{
    public interface ICurrentStoreContext
    {
        int? UserId { get; }
        int? StoreId { get; }
        Task<StoreMember?> GetMembershipAsync(CancellationToken cancellationToken = default);
    }

    public sealed class CurrentStoreContext : ICurrentStoreContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;

        public CurrentStoreContext(
            IHttpContextAccessor httpContextAccessor,
            ApplicationDbContext context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        public int? UserId => ReadIntClaim(ClaimTypes.NameIdentifier);

        public int? StoreId => ReadIntClaim(StoreClaimTypes.StoreId);

        public Task<StoreMember?> GetMembershipAsync(CancellationToken cancellationToken = default)
        {
            if (UserId is not int userId || StoreId is not int storeId)
            {
                return Task.FromResult<StoreMember?>(null);
            }

            return _context.StoreMembers
                .AsNoTracking()
                .Include(member => member.Store)
                .SingleOrDefaultAsync(
                    member => member.UserId == userId && member.StoreId == storeId,
                    cancellationToken);
        }

        private int? ReadIntClaim(string claimType)
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
            return int.TryParse(value, out var id) ? id : null;
        }
    }
}
