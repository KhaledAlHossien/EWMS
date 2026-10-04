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
                .Where(s => s.UserId == userId && s.IsCurrent)
                .Select(s => s.Image)
                .FirstOrDefaultAsync();

        public async Task<int?> GetCurrentIdAsync(int userId) =>
            await _context.UserSignatures
                .Where(s => s.UserId == userId && s.IsCurrent)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

        public async Task<string?> GetImageAsync(int? signatureId) =>
            signatureId == null
                ? null
                : await _context.UserSignatures
                    .Where(s => s.Id == signatureId)
                    .Select(s => s.Image)
                    .FirstOrDefaultAsync();

        public async Task SetAsync(int userId, string? image)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            // النسخة الحالية تتقاعد ولا تُحذف — قد تكون محفوظة مع قرارات سابقة
            var current = await _context.UserSignatures.Where(s => s.UserId == userId && s.IsCurrent).ToListAsync();
            foreach (var signature in current) signature.IsCurrent = false;

            // حفظ التقاعد أولاً: الفهرس الفريد (UserId حيث IsCurrent = 1) لا يقبل نسختين حاليتين ولو لحظياً
            await _context.SaveChangesAsync();

            if (image != null)
            {
                await _context.UserSignatures.AddAsync(new UserSignature
                {
                    UserId = userId,
                    Image = image,
                    CreatedAt = DateTime.UtcNow,
                    IsCurrent = true
                });
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
    }
}
