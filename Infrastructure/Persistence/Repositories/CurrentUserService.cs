using Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Infrastructure.Persistence.Repositories
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int UserId => GetIntClaim(ClaimTypes.NameIdentifier);
        public int DepartmentId => GetIntClaim("DepartmentId");
        public int BranchId => GetIntClaim("BranchId");
        public string Role => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

        private int GetIntClaim(string claimType)
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirst(claimType)?.Value;
            return int.TryParse(value, out var result) ? result : 0;
        }
    }
}
