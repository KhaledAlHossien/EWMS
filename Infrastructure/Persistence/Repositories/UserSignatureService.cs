using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class UserSignatureService : IUserSignatureService
    {
        private readonly DataContext _context;

        public UserSignatureService(DataContext context)
        {
            _context = context;
        }

        public async Task<string?> GetAsync(int userId) =>
            await _context.UserSignatures
                .Where(s => s.UserId == userId)
                .Select(s => s.Image)
                .FirstOrDefaultAsync();

        public async Task SetAsync(int userId, string? image)
        {
            var existing = await _context.UserSignatures.FirstOrDefaultAsync(s => s.UserId == userId);

            if (image == null)
            {
                if (existing == null) return;
                _context.UserSignatures.Remove(existing);
            }
            else if (existing == null)
            {
                await _context.UserSignatures.AddAsync(new UserSignature
                {
                    UserId = userId,
                    Image = image,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Image = image;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }
}
