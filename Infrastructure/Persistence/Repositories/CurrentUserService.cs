using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Infrastructure.Persistence.Repositories
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly DataContext _context;
        private User? _user;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor, DataContext context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        public int UserId => GetIntClaim(ClaimTypes.NameIdentifier);
        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

        public async Task<User> GetUserAsync()
        {
            if (_user != null) return _user;

            var user = await _context.Users.AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == UserId);

            // حساب حُذف أو عُطّل أثناء الجلسة: لا يُعامَل كمستخدم صالح
            if (user == null || !user.IsActive)
                throw new UnauthorizedAccessException("الحساب غير موجود أو غير فعّال");

            return _user = user;
        }

        private int GetIntClaim(string claimType)
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirst(claimType)?.Value;
            return int.TryParse(value, out var result) ? result : 0;
        }
    }
}
