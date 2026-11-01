using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using static Application.Features.Pricing.PricingSupport;
using static Application.Features.ReferencePrices.ReferencePriceImportRules;

namespace Application.Features.ReferencePrices
{
    // استيراد الأسعار المرجعية من Excel: القالب، ثم المعاينة (dryRun) أو الحفظ بعد فحص كامل — الكل أو لا شيء.

    public record GetReferencePriceImportTemplateQuery : IRequest<byte[]>;
    public record ImportReferencePricesCommand(Stream File, string FileName, bool DryRun, int? BranchId) : IRequest<ReferencePriceImportReportDto>;

    public class GetReferencePriceImportTemplateHandler(ReferencePriceContext ctx) : IRequestHandler<GetReferencePriceImportTemplateQuery, byte[]>
    {
        public async Task<byte[]> Handle(GetReferencePriceImportTemplateQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            if (!viewer.IsSuperAdmin && !viewer.Has(AppPermissions.ImportReferencePrices))
                throw new UnauthorizedAccessException("لا تملك صلاحية استيراد الأسعار المرجعية");
            return ctx.Spreadsheet.Template();
        }
    }

    /// <summary>
    /// يفحص كل سطر بقواعد الإضافة نفسها، ثم التكرار داخل الملف ومع المسجَّل. المعاينة لا تحفظ؛ والحفظ لا يتم إن وُجد سطر خاطئ واحد.
    /// الأسعار المحفوظة «تاريخية» (المصدر: استيراد) تنتظر اعتماد غير المستورِد.
    /// </summary>
    public class ImportReferencePricesHandler(ReferencePriceContext ctx) : IRequestHandler<ImportReferencePricesCommand, ReferencePriceImportReportDto>
    {
        public async Task<ReferencePriceImportReportDto> Handle(ImportReferencePricesCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var branch = ReferencePriceContext.ResolveBranch(viewer, null, request.BranchId);
            EnsureCanImport(viewer, branch);

            var sheetRows = ctx.Spreadsheet.Read(request.File, MaxRows);
            if (sheetRows.Count == 0) throw new InvalidOperationException("لا أسطر في الملف (غير سطر العناوين والمثال)");

            var suppliers = (await ctx.Contracts.GetContractorsAsync(branch, includeInactive: true)).ToDictionary(c => NormName(c.Name));
            var today = DateTime.Today;
            var results = sheetRows.Select(r => ValidateRow(r, suppliers, today)).ToList();
            FlagFileDuplicates(results);

            var parsed = results.Where(r => r.Valid).Select(r => r.Price!).ToList();
            if (parsed.Count > 0)
            {
                var existing = await ctx.Prices.GetPossibleDuplicatesAsync(branch, parsed.Select(p => p.SupplierId).Distinct().ToList(), parsed.Select(p => p.PriceDate).Distinct().ToList());
                FlagExistingDuplicates(results, existing);
            }

            var report = new ReferencePriceImportReportDto
            {
                DryRun = request.DryRun, FileName = request.FileName, Total = results.Count, Valid = results.Count(r => r.Valid), Invalid = results.Count(r => !r.Valid),
                Rows = results.Select(r => new ReferencePriceImportRowDto
                {
                    Row = r.Row.Row, ItemCode = r.Row.ItemCode, ItemName = r.Row.ItemName, Unit = r.Row.Unit, Supplier = r.Row.Supplier, UnitPrice = r.Row.UnitPrice,
                    Currency = r.Row.Currency, PriceDate = r.Row.PriceDate, SourceDocument = r.Row.SourceDocument, Valid = r.Valid, Errors = r.Errors
                }).ToList()
            };

            if (report.Invalid > 0)
            {
                report.Message = $"لم يُحفظ شيء: عدد الأسطر الخاطئة {report.Invalid} من {report.Total}. صحّح الملف وارفعه من جديد (الكل أو لا شيء).";
                return report;
            }
            if (request.DryRun)
            {
                report.Message = $"الملف سليم: عدد الأسعار الجاهزة للحفظ {report.Valid}. أكّد الاستيراد ليُحفظ كله.";
                return report;
            }

            var batch = new ReferencePriceImport { BranchId = branch, FileName = Shorten(request.FileName), ImportedByUserId = viewer.Id };
            var prices = parsed.Select(p => new ReferencePrice
            {
                BranchId = branch, ItemCode = p.ItemCode, ItemName = p.ItemName, Specifications = p.Specifications, Unit = p.Unit, SupplierId = p.SupplierId, UnitPrice = p.UnitPrice,
                Currency = p.Currency, PriceDate = p.PriceDate, Source = ReferencePriceSource.Import, SourceDocument = p.SourceDocument, Notes = p.Notes,
                Status = ReferencePriceStatus.Historical, CreatedByUserId = viewer.Id
            }).ToList();

            var saved = await ctx.Prices.ImportAsync(batch, prices,
                new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.ReferencePrice, null, $"استيراد أسعار مرجعية (عددها {prices.Count}) من الملف «{batch.FileName}»",
                    Lines($"الموردون: {parsed.Select(p => p.SupplierName).Distinct().Count()}", $"المواد: {parsed.Select(p => Key(p)).Distinct().Count()}")));

            report.Saved = true;
            report.Imported = prices.Count;
            report.ImportId = saved.Id;
            report.Message = $"استُوردت الأسعار (عددها {prices.Count}) تاريخية وهي بانتظار الاعتماد من غير من استوردها.";

            await ctx.NotifyAsync(await ctx.HoldersAsync(branch, AppPermissions.ApproveReferencePrice), viewer.Id, prices[0], NotificationType.ReferencePriceAwaiting,
                "أسعار مرجعية مستوردة بانتظار اعتمادك", $"استورد {viewer.User.FullName} أسعاراً عددها {prices.Count} من الملف «{batch.FileName}»");
            return report;
        }

        private static string Shorten(string name) => string.IsNullOrWhiteSpace(name) ? "ملف Excel" : name.Trim().Length > 200 ? name.Trim()[..200] : name.Trim();

        private static string Key(ParsedPrice p) => ReferencePriceRules.Key(p.ItemCode, p.ItemName, p.Unit, p.Currency);
    }
}
