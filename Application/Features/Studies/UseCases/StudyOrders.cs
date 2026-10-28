using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.Studies.StudySupport;

namespace Application.Features.Studies
{
    // أوامر الدراسة: البحث ضمن حدّ المشاريع، التفصيل، الفتح، التعديل، الإلغاء، وإعادة الفتح لجولة جديدة.

    /// <param name="Mine">true = ما أُسند إليّ فيه مخرج</param>
    public record SearchStudiesQuery(int? ProjectId, int? BranchId, int? Status, string? Search, bool OverdueOnly, bool Mine, int Page, int PageSize)
        : IRequest<PagedResultDto<StudyListItemDto>>;
    public record GetStudyQuery(int Id) : IRequest<StudyDetailDto>;
    public record CreateStudyOrderCommand(StudyOrderRequestDto Dto) : IRequest<StudyDetailDto>;
    public record UpdateStudyOrderCommand(int Id, StudyOrderRequestDto Dto) : IRequest<StudyDetailDto>;
    public record CancelStudyOrderCommand(int Id, byte[]? RowVersion) : IRequest<StudyDetailDto>;
    public record ReopenStudyOrderCommand(int Id, byte[]? RowVersion) : IRequest<StudyDetailDto>;

    internal static class StudyOrderChecks
    {
        public static IEnumerable<string> Problems(StudyOrderRequestDto dto)
        {
            if (StudyRules.ValidateOrder(dto.Title, dto.Notes, dto.DueDate, DateTime.Today) is string problem) yield return problem;
            if (!StudyRules.IsValidMode(dto.Mode)) yield return "ترتيب المخرجات: بالتسلسل أو بالتوازي";
            if ((dto.ExternalNumber ?? string.Empty).Length > 100) yield return "رقم الأمر لا يتجاوز 100 حرف";
        }
    }

    public class CreateStudyOrderCommandValidator : AbstractValidator<CreateStudyOrderCommand>
    {
        public CreateStudyOrderCommandValidator() =>
            RuleFor(x => x.Dto).Custom((dto, c) => { foreach (var problem in StudyOrderChecks.Problems(dto)) c.AddFailure(problem); });
    }

    public class UpdateStudyOrderCommandValidator : AbstractValidator<UpdateStudyOrderCommand>
    {
        public UpdateStudyOrderCommandValidator() =>
            RuleFor(x => x.Dto).Custom((dto, c) => { foreach (var problem in StudyOrderChecks.Problems(dto)) c.AddFailure(problem); });
    }

    public class SearchStudiesHandler(StudyContext ctx) : IRequestHandler<SearchStudiesQuery, PagedResultDto<StudyListItemDto>>
    {
        public async Task<PagedResultDto<StudyListItemDto>> Handle(SearchStudiesQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var today = DateTime.Today;

            StudyStatus? status = request.Status is int s && Enum.IsDefined(typeof(StudyStatus), s) ? (StudyStatus)s : null;
            var filter = new StudyFilter(request.ProjectId, request.BranchId, status, request.Search, request.OverdueOnly, request.Mine ? viewer.Id : null, today);

            var (items, total) = await ctx.Studies.SearchAsync(ProjectRules.Visible(viewer), filter, page, pageSize);
            return new PagedResultDto<StudyListItemDto>
            {
                Items = items.Select(o => ToListItem(o, today)).ToList(), TotalCount = total, Page = page, PageSize = pageSize
            };
        }
    }

    public class GetStudyHandler(StudyContext ctx) : IRequestHandler<GetStudyQuery, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(GetStudyQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var order = await ctx.LoadAsync(viewer, request.Id);
            return ToDetail(order, viewer, DateTime.Today);
        }
    }

    public class CreateStudyOrderHandler(StudyContext ctx) : IRequestHandler<CreateStudyOrderCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(CreateStudyOrderCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var project = await ctx.Projects.GetByIdAsync(dto.ProjectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            StudyRules.EnsureCanCreate(viewer, project);
            var correspondence = await ResolveIncomingAsync(ctx, viewer, dto.CorrespondenceId, project.BranchId);

            var order = await ctx.Studies.AddOrderAsync(new StudyOrder
            {
                ProjectId = project.Id,
                CorrespondenceId = correspondence?.Id,
                ExternalNumber = string.IsNullOrWhiteSpace(dto.ExternalNumber) ? null : dto.ExternalNumber.Trim(),
                Title = dto.Title.Trim(),
                Notes = (dto.Notes ?? string.Empty).Trim(),
                Mode = (StudyMode)dto.Mode,
                Status = StudyStatus.Open,
                DueDate = dto.DueDate?.Date,
                CreatedByUserId = viewer.Id
            }, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Study, null, $"فتح أمر دراسة «{dto.Title.Trim()}» على المشروع «{project.Name}»",
                Lines($"الترتيب: {StudyRules.ModeAr((StudyMode)dto.Mode)}", correspondence is null ? null : "مرتبط بمعاملة واردة")));
            return await ctx.DetailAsync(viewer, order.Id);
        }

        internal static async Task<Correspondence?> ResolveIncomingAsync(StudyContext ctx, Viewer viewer, int? id, int branchId)
        {
            var c = await ctx.ResolveCorrespondenceAsync(viewer, id, branchId);
            if (c != null && c.Direction != CorrespondenceDirection.Incoming) throw new InvalidOperationException("يُربط أمر الدراسة بمعاملة واردة");
            return c;
        }
    }

    public class UpdateStudyOrderHandler(StudyContext ctx) : IRequestHandler<UpdateStudyOrderCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(UpdateStudyOrderCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var order = await ctx.LoadAsync(viewer, request.Id);
            StudyRules.EnsureCanEdit(viewer, order);
            var correspondence = await CreateStudyOrderHandler.ResolveIncomingAsync(ctx, viewer, dto.CorrespondenceId, order.Project.BranchId);

            var external = string.IsNullOrWhiteSpace(dto.ExternalNumber) ? null : dto.ExternalNumber.Trim();
            var mode = (StudyMode)dto.Mode;
            var details = Lines(
                Change("العنوان", order.Title, dto.Title),
                Change("رقم الأمر", order.ExternalNumber, external),
                Change("الترتيب", StudyRules.ModeAr(order.Mode), StudyRules.ModeAr(mode)),
                Change("الموعد", order.DueDate?.ToString("yyyy-MM-dd"), dto.DueDate?.ToString("yyyy-MM-dd")),
                Change("الملاحظات", order.Notes, dto.Notes),
                Change("المعاملة المرتبطة", order.CorrespondenceId?.ToString(), correspondence?.Id.ToString()));

            order.Title = dto.Title.Trim();
            order.ExternalNumber = external;
            order.Mode = mode;
            order.DueDate = dto.DueDate?.Date;
            order.Notes = (dto.Notes ?? string.Empty).Trim();
            order.CorrespondenceId = correspondence?.Id;

            await ctx.Studies.UpdateOrderAsync(order, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.Study, order.Id, $"تعديل أمر الدراسة {StudyContext.Label(order)}", details));
            return await ctx.DetailAsync(viewer, order.Id);
        }
    }

    public class CancelStudyOrderHandler(StudyContext ctx) : IRequestHandler<CancelStudyOrderCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(CancelStudyOrderCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var order = await ctx.LoadAsync(viewer, request.Id);
            StudyRules.EnsureCanCancel(viewer, order);

            order.Status = StudyStatus.Cancelled;
            await ctx.Studies.UpdateOrderAsync(order, request.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Cancelled, AuditEntities.Study, order.Id, $"إلغاء أمر الدراسة {StudyContext.Label(order)}"));
            return await ctx.DetailAsync(viewer, order.Id);
        }
    }

    /// <summary>جولة جديدة بعد ملاحظات المركزية: يعود الأمر مفتوحاً (والمخرجات المعتمدة تبقى كما هي حتى يعيد رئيس القسم ما يحتاج تعديلاً)</summary>
    public class ReopenStudyOrderHandler(StudyContext ctx) : IRequestHandler<ReopenStudyOrderCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(ReopenStudyOrderCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var order = await ctx.LoadAsync(viewer, request.Id);
            StudyRules.EnsureCanReopen(viewer, order);

            order.Status = StudyStatus.Open;
            await ctx.Studies.UpdateOrderAsync(order, request.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Reopened, AuditEntities.Study, order.Id, $"إعادة فتح أمر الدراسة {StudyContext.Label(order)} لجولة جديدة"));
            return await ctx.DetailAsync(viewer, order.Id);
        }
    }
}
