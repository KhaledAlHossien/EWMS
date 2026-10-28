using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.Diwan.DiwanSupport;

namespace Application.Features.Diwan
{
    // أنواع معاملات الفرع (إعدادات): قراءتها لمن يسجّل أو يعالج، وإدارتها بصلاحيات الأنواع والحالات الأربع.

    public record GetCorrespondenceTypesQuery(int? BranchId, bool IncludeInactive) : IRequest<List<CorrespondenceTypeResponseDto>>;
    public record CreateCorrespondenceTypeCommand(CorrespondenceTypeRequestDto Dto) : IRequest<CorrespondenceTypeResponseDto>;
    public record UpdateCorrespondenceTypeCommand(int Id, CorrespondenceTypeRequestDto Dto) : IRequest<CorrespondenceTypeResponseDto>;
    public record DeleteCorrespondenceTypeCommand(int Id) : IRequest<bool>;

    public class CreateCorrespondenceTypeCommandValidator : AbstractValidator<CreateCorrespondenceTypeCommand>
    {
        public CreateCorrespondenceTypeCommandValidator() =>
            RuleFor(x => x.Dto).Custom((dto, context) =>
            {
                if (DiwanRules.ValidateType(dto.Name, dto.DeadlineDays) is string problem) context.AddFailure(problem);
            });
    }

    public class UpdateCorrespondenceTypeCommandValidator : AbstractValidator<UpdateCorrespondenceTypeCommand>
    {
        public UpdateCorrespondenceTypeCommandValidator() =>
            RuleFor(x => x.Dto).Custom((dto, context) =>
            {
                if (DiwanRules.ValidateType(dto.Name, dto.DeadlineDays) is string problem) context.AddFailure(problem);
            });
    }

    public class GetCorrespondenceTypesHandler(DiwanContext ctx) : IRequestHandler<GetCorrespondenceTypesQuery, List<CorrespondenceTypeResponseDto>>
    {
        public async Task<List<CorrespondenceTypeResponseDto>> Handle(GetCorrespondenceTypesQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var branch = viewer.IsOrganizationWide ? request.BranchId : viewer.User.BranchId;
            if (!viewer.IsOrganizationWide && branch == null) return [];

            var types = await ctx.Diwan.GetTypesAsync(branch, request.IncludeInactive && DiwanContext.CanManageLookups(viewer));
            return types.Select(t => ToDto(t)).ToList();
        }
    }

    public class CreateCorrespondenceTypeHandler(DiwanContext ctx) : IRequestHandler<CreateCorrespondenceTypeCommand, CorrespondenceTypeResponseDto>
    {
        public async Task<CorrespondenceTypeResponseDto> Handle(CreateCorrespondenceTypeCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var branch = DiwanContext.ResolveBranch(viewer, dto.BranchId);
            var name = dto.Name.Trim();
            if (await ctx.Diwan.TypeNameExistsAsync(branch, name)) throw new InvalidOperationException("يوجد نوع بهذا الاسم في الفرع");

            var type = await ctx.Diwan.AddTypeAsync(
                new CorrespondenceType { BranchId = branch, Name = name, DeadlineDays = dto.DeadlineDays, IsActive = dto.IsActive, SortOrder = dto.SortOrder },
                new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.CorrespondenceType, null, $"إضافة نوع معاملة «{name}»",
                    Lines($"المهلة: {(dto.DeadlineDays is int d ? d + " يوم" : "بلا مهلة")}")));
            return ToDto(type);
        }
    }

    public class UpdateCorrespondenceTypeHandler(DiwanContext ctx) : IRequestHandler<UpdateCorrespondenceTypeCommand, CorrespondenceTypeResponseDto>
    {
        public async Task<CorrespondenceTypeResponseDto> Handle(UpdateCorrespondenceTypeCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var type = await ctx.Diwan.GetTypeAsync(request.Id) ?? throw new KeyNotFoundException("النوع غير موجود");
            DiwanContext.EnsureBranchAccess(viewer, type.BranchId);

            var name = dto.Name.Trim();
            if (await ctx.Diwan.TypeNameExistsAsync(type.BranchId, name, type.Id)) throw new InvalidOperationException("يوجد نوع بهذا الاسم في الفرع");

            var details = Lines(
                Change("الاسم", type.Name, name),
                Change("المهلة (يوم)", type.DeadlineDays?.ToString(), dto.DeadlineDays?.ToString()),
                Change("فعّال", type.IsActive ? "نعم" : "لا", dto.IsActive ? "نعم" : "لا"));
            type.Name = name;
            type.DeadlineDays = dto.DeadlineDays;
            type.IsActive = dto.IsActive;
            type.SortOrder = dto.SortOrder;

            await ctx.Diwan.UpdateTypeAsync(type, new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.CorrespondenceType, type.Id, $"تعديل نوع المعاملة «{name}»", details));
            return ToDto(type);
        }
    }

    public class DeleteCorrespondenceTypeHandler(DiwanContext ctx) : IRequestHandler<DeleteCorrespondenceTypeCommand, bool>
    {
        public async Task<bool> Handle(DeleteCorrespondenceTypeCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var type = await ctx.Diwan.GetTypeAsync(request.Id) ?? throw new KeyNotFoundException("النوع غير موجود");
            DiwanContext.EnsureBranchAccess(viewer, type.BranchId);
            if (await ctx.Diwan.TypeInUseAsync(type.Id))
                throw new InvalidOperationException("لا يُحذف نوع عليه معاملات: عطّله بدل حذفه");

            await ctx.Diwan.DeleteTypeAsync(type, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.CorrespondenceType, type.Id, $"حذف نوع المعاملة «{type.Name}»"));
            return true;
        }
    }
}
