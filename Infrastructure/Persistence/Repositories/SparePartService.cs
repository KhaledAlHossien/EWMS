using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class SparePartService : ISparePartService
    {
        private readonly DataContext _context;

        public SparePartService(DataContext context)
        {
            _context = context;
        }

        private IQueryable<SparePart> WithDetails() => _context.SpareParts
            .Include(p => p.Department)
            .Include(p => p.DeviceTypes).ThenInclude(t => t.DeviceType)
            .Include(p => p.DeviceCompanies).ThenInclude(c => c.DeviceCompany)
            .AsSplitQuery();

        public async Task<SparePart?> GetByIdAsync(int id) =>
            await WithDetails().FirstOrDefaultAsync(p => p.Id == id);

        public async Task<(List<SparePart> Items, int TotalCount)> SearchAsync(int? departmentId, SparePartFilterDto filter, int page, int pageSize)
        {
            var query = _context.SpareParts.AsQueryable();

            if (departmentId is int d)
                query = query.Where(p => p.DepartmentId == d);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim();
                query = query.Where(p => p.Name.Contains(s) || p.PartNumber.StartsWith(s));
            }

            // نوع الجهاز: القطع المتوافقة معه، والقطع العامة التي لم يُحدَّد لها توافق
            if (filter.DeviceTypeId is int typeId)
                query = query.Where(p => !p.DeviceTypes.Any() || p.DeviceTypes.Any(t => t.DeviceTypeId == typeId));

            if (filter.LowStock)
                query = query.Where(p => p.MinQuantity > 0 && p.Quantity < p.MinQuantity);

            var total = await query.CountAsync();
            var ids = await query.OrderBy(p => p.Name).ThenBy(p => p.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => p.Id).ToListAsync();

            var items = await WithDetails().AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync();
            return (items.OrderBy(p => ids.IndexOf(p.Id)).ToList(), total);
        }

        public async Task<List<SparePart>> GetAvailableForRequestAsync(int departmentId, int deviceTypeId, int deviceCompanyId, string? search, int take)
        {
            var query = _context.SpareParts.Where(p => p.DepartmentId == departmentId && p.Quantity > 0);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(p => p.Name.Contains(s) || p.PartNumber.StartsWith(s));
            }

            var ids = await query
                .OrderByDescending(p => (p.DeviceTypes.Any(t => t.DeviceTypeId == deviceTypeId) ? 2 : 0)
                                      + (p.DeviceCompanies.Any(c => c.DeviceCompanyId == deviceCompanyId) ? 1 : 0))
                .ThenBy(p => p.Name)
                .Take(take)
                .Select(p => p.Id).ToListAsync();

            var items = await WithDetails().AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync();
            return items.OrderBy(p => ids.IndexOf(p.Id)).ToList();
        }

        public async Task<bool> NameExistsAsync(int departmentId, string name, int? excludeId = null) =>
            await _context.SpareParts.AnyAsync(p => p.DepartmentId == departmentId && p.Name == name && (excludeId == null || p.Id != excludeId));

        public async Task<SparePart> AddAsync(SparePart part)
        {
            await _context.SpareParts.AddAsync(part);
            await _context.SaveChangesAsync();
            return part;
        }

        public async Task UpdateAsync(SparePart part, IReadOnlyCollection<int> deviceTypeIds, IReadOnlyCollection<int> deviceCompanyIds)
        {
            part.DeviceTypes.RemoveAll(t => !deviceTypeIds.Contains(t.DeviceTypeId));
            foreach (var id in deviceTypeIds.Where(id => part.DeviceTypes.All(t => t.DeviceTypeId != id)))
                part.DeviceTypes.Add(new SparePartDeviceType { SparePartId = part.Id, DeviceTypeId = id });

            part.DeviceCompanies.RemoveAll(c => !deviceCompanyIds.Contains(c.DeviceCompanyId));
            foreach (var id in deviceCompanyIds.Where(id => part.DeviceCompanies.All(c => c.DeviceCompanyId != id)))
                part.DeviceCompanies.Add(new SparePartDeviceCompany { SparePartId = part.Id, DeviceCompanyId = id });

            // الكمية والمتوسط تملكهما الحركات: لا يُكتبان من هنا حتى لا تمحو نسخة قديمة حركة متزامنة
            _context.Entry(part).Property(p => p.Quantity).IsModified = false;
            _context.Entry(part).Property(p => p.AverageCost).IsModified = false;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(SparePart part)
        {
            _context.SpareParts.Remove(part);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> HasMovementsAsync(int id) =>
            await _context.SparePartMovements.AnyAsync(m => m.SparePartId == id);

        // ════════════════════ الحركات الذرّية ════════════════════

        private async Task<T> InTransactionAsync<T>(Func<Task<T>> action)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var result = await action();
            await transaction.CommitAsync();
            return result;
        }

        // قفل صف القطعة حتى نهاية المعاملة: الحركات المتزامنة على نفس القطعة تنتظر بعضها فتقرأ الرصيد الصحيح
        private async Task<(decimal Quantity, decimal Average)> LockAsync(int partId)
        {
            var row = await _context.SpareParts
                .FromSqlInterpolated($"SELECT * FROM SpareParts WITH (UPDLOCK, ROWLOCK) WHERE Id = {partId}")
                .AsNoTracking()
                .Select(p => new { p.Quantity, p.AverageCost })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("قطعة الغيار غير موجودة");
            return (row.Quantity, row.AverageCost);
        }

        private async Task SetStockAsync(int partId, decimal quantity, decimal average)
        {
            var now = DateTime.UtcNow;
            await _context.SpareParts.Where(p => p.Id == partId).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Quantity, quantity)
                .SetProperty(p => p.AverageCost, average)
                .SetProperty(p => p.UpdatedAt, now));

            // ExecuteUpdate لا يمر بالمتتبّع: نسخة القطعة المحمّلة في هذا الطلب تُحدَّث وإلا أعادت القراءة التالية الرصيد القديم
            if (_context.SpareParts.Local.FirstOrDefault(p => p.Id == partId) is { } tracked)
            {
                var entry = _context.Entry(tracked);
                foreach (var (property, value) in new (string, object)[] { (nameof(SparePart.Quantity), quantity), (nameof(SparePart.AverageCost), average), (nameof(SparePart.UpdatedAt), now) })
                {
                    entry.Property(property).CurrentValue = value;
                    entry.Property(property).OriginalValue = value;
                }
            }
        }

        /// <summary>متوسط مرجّح: (الرصيد × المتوسط + الداخل × سعره) ÷ الرصيد الجديد — والرصيد الفارغ يبدأ بسعر الداخل</summary>
        private static decimal Average(decimal quantity, decimal average, decimal inQuantity, decimal inCost) =>
            quantity <= 0 ? inCost : Math.Round((quantity * average + inQuantity * inCost) / (quantity + inQuantity), 2);

        private static string Amount(decimal value) => value.ToString("0.##");

        private SparePartMovement Movement(int partId, SparePartMovementType type, decimal quantity, decimal unitCost,
            decimal balanceAfter, DateTime date, string note, int userId, int? requestId = null) => new()
        {
            SparePartId = partId,
            Type = type,
            Quantity = quantity,
            UnitCost = unitCost,
            BalanceAfter = balanceAfter,
            Date = date,
            Note = note,
            MaintenanceRequestId = requestId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        public Task<StockChange> ReceiveAsync(int partId, decimal quantity, decimal unitCost, DateTime date, string source, int userId) =>
            InTransactionAsync(async () =>
            {
                var (before, average) = await LockAsync(partId);
                var after = before + quantity;
                var newAverage = Average(before, average, quantity, unitCost);

                await SetStockAsync(partId, after, newAverage);
                _context.SparePartMovements.Add(Movement(partId, SparePartMovementType.Receive, quantity, unitCost, after, date.Date, source, userId));
                await _context.SaveChangesAsync();
                return new StockChange(before, after, newAverage);
            });

        public Task<StockChange> AdjustAsync(int partId, decimal delta, string reason, int userId) =>
            InTransactionAsync(async () =>
            {
                var (before, average) = await LockAsync(partId);
                var after = before + delta;
                if (after < 0)
                    throw new InvalidOperationException($"الرصيد الحالي {Amount(before)} لا يكفي لإنقاص {Amount(-delta)}");

                await SetStockAsync(partId, after, average);
                _context.SparePartMovements.Add(Movement(partId, SparePartMovementType.Adjust, delta, average, after, DateTime.Today, reason, userId));
                await _context.SaveChangesAsync();
                return new StockChange(before, after, average);
            });

        public Task<(MaintenanceRequestPart Part, StockChange Change)> IssueAsync(int requestId, int partId, decimal quantity, int userId) =>
            InTransactionAsync(async () =>
            {
                var (before, average) = await LockAsync(partId);
                if (before < quantity)
                    throw new InvalidOperationException($"الكمية المتوفرة من القطعة ({Amount(before)}) لا تكفي لصرف {Amount(quantity)}");

                var after = before - quantity;
                await SetStockAsync(partId, after, average);

                // السعر يُثبَّت لحظة الصرف (متوسط الإدخال الحالي)
                var part = new MaintenanceRequestPart
                {
                    MaintenanceRequestId = requestId,
                    SparePartId = partId,
                    Quantity = quantity,
                    UnitCost = average,
                    IssuedById = userId,
                    IssuedAt = DateTime.UtcNow
                };
                _context.MaintenanceRequestParts.Add(part);
                _context.SparePartMovements.Add(Movement(partId, SparePartMovementType.Issue, -quantity, average, after, DateTime.Today, string.Empty, userId, requestId));
                await _context.SaveChangesAsync();
                return (part, new StockChange(before, after, average));
            });

        public Task<StockChange> ReturnAsync(MaintenanceRequestPart requestPart, int userId) =>
            InTransactionAsync(async () =>
            {
                var (before, average) = await LockAsync(requestPart.SparePartId);
                var after = before + requestPart.Quantity;
                var newAverage = Average(before, average, requestPart.Quantity, requestPart.UnitCost);

                await SetStockAsync(requestPart.SparePartId, after, newAverage);
                _context.MaintenanceRequestParts.Remove(requestPart);
                _context.SparePartMovements.Add(Movement(requestPart.SparePartId, SparePartMovementType.Return, requestPart.Quantity,
                    requestPart.UnitCost, after, DateTime.Today, string.Empty, userId, requestPart.MaintenanceRequestId));
                await _context.SaveChangesAsync();
                return new StockChange(before, after, newAverage);
            });

        public async Task<(List<SparePartMovement> Items, int TotalCount)> GetMovementsAsync(int partId, int page, int pageSize)
        {
            var query = _context.SparePartMovements.AsNoTracking().Where(m => m.SparePartId == partId);
            var total = await query.CountAsync();
            var items = await query
                .Include(m => m.User)
                .Include(m => m.MaintenanceRequest)
                .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            return (items, total);
        }

        public async Task<List<MaintenanceRequestPart>> GetRequestPartsAsync(int requestId) =>
            await _context.MaintenanceRequestParts.AsNoTracking()
                .Include(p => p.SparePart)
                .Include(p => p.IssuedBy)
                .Where(p => p.MaintenanceRequestId == requestId)
                .OrderBy(p => p.IssuedAt).ThenBy(p => p.Id)
                .ToListAsync();

        public async Task<MaintenanceRequestPart?> GetRequestPartAsync(int id) =>
            await _context.MaintenanceRequestParts.Include(p => p.SparePart).FirstOrDefaultAsync(p => p.Id == id);

        public async Task<decimal> GetDeviceCostAsync(int deviceMaintenanceId) =>
            await _context.MaintenanceRequestParts
                .Where(p => p.MaintenanceRequest.DeviceMaintenanceId == deviceMaintenanceId)
                .SumAsync(p => (decimal?)(p.Quantity * p.UnitCost)) ?? 0;

        public async Task<SparePartReportDto> GetReportAsync(int? departmentId, DateTime from, DateTime to)
        {
            var parts = _context.SpareParts.AsNoTracking();
            if (departmentId is int d) parts = parts.Where(p => p.DepartmentId == d);

            // الفترة بتاريخ الخادم المحلي، والصرف محفوظ UTC
            var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Local).ToUniversalTime();
            var toUtc = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

            var issued = _context.MaintenanceRequestParts.AsNoTracking().Where(p => p.IssuedAt >= fromUtc && p.IssuedAt < toUtc);
            if (departmentId is int d2) issued = issued.Where(p => p.SparePart.DepartmentId == d2);

            var report = new SparePartReportDto
            {
                From = from.Date,
                To = to.Date,
                PartsCount = await parts.CountAsync(),
                LowStockCount = await parts.CountAsync(p => p.MinQuantity > 0 && p.Quantity < p.MinQuantity),
                StockValue = await parts.SumAsync(p => (decimal?)(p.Quantity * p.AverageCost)) ?? 0,
                IssuedCost = await issued.SumAsync(p => (decimal?)(p.Quantity * p.UnitCost)) ?? 0,
                MostUsed = await issued
                    .GroupBy(p => new { p.SparePartId, p.SparePart.Name, p.SparePart.Unit })
                    .Select(g => new SparePartUsageDto
                    {
                        SparePartId = g.Key.SparePartId,
                        Name = g.Key.Name,
                        Unit = g.Key.Unit,
                        Quantity = g.Sum(x => x.Quantity),
                        Cost = g.Sum(x => x.Quantity * x.UnitCost)
                    })
                    .OrderByDescending(x => x.Cost).ThenByDescending(x => x.Quantity)
                    .Take(10)
                    .ToListAsync()
            };

            // تكلفة الأجهزة على مدى عمرها (طلبات القسم)
            var lifetime = _context.MaintenanceRequestParts.AsNoTracking();
            if (departmentId is int d3) lifetime = lifetime.Where(p => p.MaintenanceRequest.DepartmentId == d3);

            var costs = await lifetime
                .GroupBy(p => p.MaintenanceRequest.DeviceMaintenanceId)
                .Select(g => new
                {
                    DeviceId = g.Key,
                    Cost = g.Sum(x => x.Quantity * x.UnitCost),
                    Requests = g.Select(x => x.MaintenanceRequestId).Distinct().Count()
                })
                .OrderByDescending(x => x.Cost)
                .Take(20)
                .ToListAsync();

            var deviceIds = costs.Select(c => c.DeviceId).ToList();
            var devices = await _context.DeviceMaintenances.AsNoTracking()
                .Include(x => x.DeviceType)
                .Where(x => deviceIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            report.DeviceCosts = costs.Where(c => devices.ContainsKey(c.DeviceId)).Select(c =>
            {
                var device = devices[c.DeviceId];
                return new DeviceCostDto
                {
                    DeviceMaintenanceId = c.DeviceId,
                    SerialNumber = device.SerialNumber,
                    DeviceName = device.Name,
                    DeviceTypeName = device.DeviceType?.Name ?? string.Empty,
                    RequestsCount = c.Requests,
                    Cost = c.Cost,
                };
            }).ToList();

            return report;
        }

        // حركات المخزون تشير إلى الطلب حتى بعد إعادة قطعه (سجل لا يُحذف)
        public async Task<bool> HasRequestPartsAsync(int requestId) =>
            await _context.SparePartMovements.AnyAsync(m => m.MaintenanceRequestId == requestId)
            || await _context.MaintenanceRequestParts.AnyAsync(p => p.MaintenanceRequestId == requestId);

        public async Task<bool> ExistsForUserAsync(int userId) =>
            await _context.SparePartMovements.AnyAsync(m => m.UserId == userId)
            || await _context.MaintenanceRequestParts.AnyAsync(p => p.IssuedById == userId);

        public async Task<bool> ExistsForDepartmentAsync(int departmentId) =>
            await _context.SpareParts.AnyAsync(p => p.DepartmentId == departmentId);
    }
}
