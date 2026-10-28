using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Application.Features.Projects;
using static Application.Features.Studies.StudySupport;

namespace Application.Features.Studies
{
    // أنواع المخرجات الفنية (إعدادات الفرع): قراءتها لمن يسند أو يفتح أمراً أو يفلتر، وإدارتها بصلاحيات الأنواع الأربع.

    public record GetDeliverableTypesQuery(int? BranchId, bool IncludeInactive) : IRequest<List<DeliverableTypeResponseDto>>;
    public record CreateDeliverableTypeCommand(DeliverableTypeRequestDto Dto) : IRequest<DeliverableTypeResponseDto>;
    public record UpdateDeliverableTypeCommand(int Id, DeliverableTypeRequestDto Dto) : IRequest<DeliverableTypeResponseDto>;
    public record DeleteDeliverableTypeCommand(int Id) : IRequest<bool>;

    internal static class DeliverableTypeChecks
    {
        public static IEnumerable<string> Problems(DeliverableTypeRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) yield return "اسم نوع المخرج مطلوب";
            else if (dto.Name.Trim().Length > 100) yield return "اسم نوع المخرج لا يتجاوز 100 حرف";
            if (!ProjectRules.IsValidKind(dto.DocumentKind)) yield return "نوع الوثيقة غير معروف";
            if (dto.SortOrder is < 0 or > 1000) yield return "الترتيب بين 0 و1000";
        }
    }

    public class CreateDeliverableTypeCommandValidator : AbstractValidator<CreateDeliverableTypeCommand>
    {
        public CreateDeliverableTypeCommandValidator() => RuleFor(x => x.Dto).Custom((dto, c) => { foreach (var problem in DeliverableTypeChecks.Problems(dto)) c.AddFailure(problem); });
    }

    public class UpdateDeliverableTypeCommandValidator : AbstractValidator<UpdateDeliverableTypeCommand>
    {
        public UpdateDeliverableTypeCommandValidator() => RuleFor(x => x.Dto).Custom((dto, c) => { foreach (var problem in DeliverableTypeChecks.Problems(dto)) c.AddFailure(problem); });
    }

    public class GetDeliverableTypesHandler(StudyContext ctx) : IRequestHandler<GetDeliverableTypesQuery, List<DeliverableTypeResponseDto>>
    {
        public async Task<List<DeliverableTypeResponseDto>> Handle(GetDeliverableTypesQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var branch = viewer.IsOrganizationWide ? request.BranchId : viewer.User.BranchId;
            if (!viewer.IsOrganizationWide && branch == null) return [];
            var types = await ctx.Studies.GetTypesAsync(branch, request.IncludeInactive && StudyContext.CanManageTypes(viewer));
            return types.Select(ToDto).ToList();
        }
    }

    public class CreateDeliverableTypeHandler(StudyContext ctx) : IRequestHandler<CreateDeliverableTypeCommand, DeliverableTypeResponseDto>
    {
        public async Task<DeliverableTypeResponseDto> Handle(CreateDeliverableTypeCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var branch = StudyContext.ResolveBranch(viewer, dto.BranchId);
            var name = dto.Name.Trim();
            if (await ctx.Studies.TypeNameExistsAsync(branch, name)) throw new InvalidOperationException("يوجد نوع مخرج بهذا الاسم في الفرع");

            var type = await ctx.Studies.AddTypeAsync(
                new DeliverableType { BranchId = branch, Name = name, DocumentKind = (ProjectDocumentKind)dto.DocumentKind, IsActive = dto.IsActive, SortOrder = dto.SortOrder },
                new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.DeliverableType, null, $"إضافة نوع مخرج «{name}»",
                    $"نوع الوثيقة: {ProjectRules.KindAr((ProjectDocumentKind)dto.DocumentKind)}"));
            return ToDto(type);
        }
    }

    public class UpdateDeliverableTypeHandler(StudyContext ctx) : IRequestHandler<UpdateDeliverableTypeCommand, DeliverableTypeResponseDto>
    {
        public async Task<DeliverableTypeResponseDto> Handle(UpdateDeliverableTypeCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var type = await ctx.Studies.GetTypeAsync(request.Id) ?? throw new KeyNotFoundException("نوع المخرج غير موجود");
            StudyContext.EnsureBranchAccess(viewer, type.BranchId);

            var name = dto.Name.Trim();
            if (await ctx.Studies.TypeNameExistsAsync(type.BranchId, name, type.Id)) throw new InvalidOperationException("يوجد نوع مخرج بهذا الاسم في الفرع");

            var kind = (ProjectDocumentKind)dto.DocumentKind;
            var details = Lines(
                Change("الاسم", type.Name, name),
                Change("نوع الوثيقة", ProjectRules.KindAr(type.DocumentKind), ProjectRules.KindAr(kind)),
                Change("فعّال", type.IsActive ? "نعم" : "لا", dto.IsActive ? "نعم" : "لا"));
            type.Name = name;
            type.DocumentKind = kind;
            type.IsActive = dto.IsActive;
            type.SortOrder = dto.SortOrder;

            await ctx.Studies.UpdateTypeAsync(type, new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.DeliverableType, type.Id, $"تعديل نوع المخرج «{name}»", details));
            return ToDto(type);
        }
    }

    public class DeleteDeliverableTypeHandler(StudyContext ctx) : IRequestHandler<DeleteDeliverableTypeCommand, bool>
    {
        public async Task<bool> Handle(DeleteDeliverableTypeCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var type = await ctx.Studies.GetTypeAsync(request.Id) ?? throw new KeyNotFoundException("نوع المخرج غير موجود");
            StudyContext.EnsureBranchAccess(viewer, type.BranchId);
            if (await ctx.Studies.TypeInUseAsync(type.Id)) throw new InvalidOperationException("عليه مخرجات في أوامر دراسة: لا يُحذف — عطّله بدلاً من ذلك");

            await ctx.Studies.DeleteTypeAsync(type, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.DeliverableType, type.Id, $"حذف نوع مخرج «{type.Name}»"));
            return true;
        }
    }
}
