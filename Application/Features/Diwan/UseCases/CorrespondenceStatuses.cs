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
    // حالات معاملات الفرع: الاسم واللون من الفرع، والمرحلة ثابتة وهي التي تحدد السلوك (مسجّلة، قيد المعالجة، تمّت الإجابة، مغلقة).

    public record GetCorrespondenceStatusesQuery(int? BranchId, bool IncludeInactive) : IRequest<List<CorrespondenceStatusResponseDto>>;
    public record CreateCorrespondenceStatusCommand(CorrespondenceStatusRequestDto Dto) : IRequest<CorrespondenceStatusResponseDto>;
    public record UpdateCorrespondenceStatusCommand(int Id, CorrespondenceStatusRequestDto Dto) : IRequest<CorrespondenceStatusResponseDto>;
    public record DeleteCorrespondenceStatusCommand(int Id) : IRequest<bool>;

    public class CreateCorrespondenceStatusCommandValidator : AbstractValidator<CreateCorrespondenceStatusCommand>
    {
        public CreateCorrespondenceStatusCommandValidator() =>
            RuleFor(x => x.Dto).Custom((dto, context) =>
            {
                if (DiwanRules.ValidateStatus(dto.Name, dto.Stage, dto.Color) is string problem) context.AddFailure(problem);
            });
    }

    public class UpdateCorrespondenceStatusCommandValidator : AbstractValidator<UpdateCorrespondenceStatusCommand>
    {
        public UpdateCorrespondenceStatusCommandValidator() =>
            RuleFor(x => x.Dto).Custom((dto, context) =>
            {
                if (DiwanRules.ValidateStatus(dto.Name, dto.Stage, dto.Color) is string problem) context.AddFailure(problem);
            });
    }

    public class GetCorrespondenceStatusesHandler(DiwanContext ctx) : IRequestHandler<GetCorrespondenceStatusesQuery, List<CorrespondenceStatusResponseDto>>
    {
        public async Task<List<CorrespondenceStatusResponseDto>> Handle(GetCorrespondenceStatusesQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var branch = viewer.IsOrganizationWide ? request.BranchId : viewer.User.BranchId;
            if (!viewer.IsOrganizationWide && branch == null) return [];

            var statuses = await ctx.Diwan.GetStatusesAsync(branch, request.IncludeInactive && DiwanContext.CanManageLookups(viewer));
            return statuses.Select(s => ToDto(s)).ToList();
        }
    }

    public class CreateCorrespondenceStatusHandler(DiwanContext ctx) : IRequestHandler<CreateCorrespondenceStatusCommand, CorrespondenceStatusResponseDto>
    {
        public async Task<CorrespondenceStatusResponseDto> Handle(CreateCorrespondenceStatusCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var branch = DiwanContext.ResolveBranch(viewer, dto.BranchId);
            var name = dto.Name.Trim();
            if (await ctx.Diwan.StatusNameExistsAsync(branch, name)) throw new InvalidOperationException("توجد حالة بهذا الاسم في الفرع");

            var stage = (CorrespondenceStage)dto.Stage;
            var status = await ctx.Diwan.AddStatusAsync(
                new CorrespondenceStatus { BranchId = branch, Name = name, Stage = stage, Color = dto.Color.ToUpperInvariant(), IsActive = dto.IsActive, SortOrder = dto.SortOrder },
                new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.CorrespondenceStatus, null, $"إضافة حالة معاملة «{name}»", Lines($"المرحلة: {DiwanRules.StageAr(stage)}")));
            return ToDto(status);
        }
    }

    public class UpdateCorrespondenceStatusHandler(DiwanContext ctx) : IRequestHandler<UpdateCorrespondenceStatusCommand, CorrespondenceStatusResponseDto>
    {
        public async Task<CorrespondenceStatusResponseDto> Handle(UpdateCorrespondenceStatusCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var status = await ctx.Diwan.GetStatusAsync(request.Id) ?? throw new KeyNotFoundException("الحالة غير موجودة");
            DiwanContext.EnsureBranchAccess(viewer, status.BranchId);

            var name = dto.Name.Trim();
            if (await ctx.Diwan.StatusNameExistsAsync(status.BranchId, name, status.Id)) throw new InvalidOperationException("توجد حالة بهذا الاسم في الفرع");

            // مرحلة حالة مستعملة تحدد سلوك معاملات قائمة (قد تُغلقها بصمت): لا تتغير، تُضاف حالة جديدة بدلها
            var stage = (CorrespondenceStage)dto.Stage;
            if (stage != status.Stage && await ctx.Diwan.StatusInUseAsync(status.Id))
                throw new InvalidOperationException("لا تتغير مرحلة حالة عليها معاملات: أضف حالة جديدة بالمرحلة المطلوبة");

            var details = Lines(
                Change("الاسم", status.Name, name),
                Change("المرحلة", DiwanRules.StageAr(status.Stage), DiwanRules.StageAr(stage)),
                Change("اللون", status.Color, dto.Color.ToUpperInvariant()),
                Change("فعّالة", status.IsActive ? "نعم" : "لا", dto.IsActive ? "نعم" : "لا"));
            status.Name = name;
            status.Stage = stage;
            status.Color = dto.Color.ToUpperInvariant();
            status.IsActive = dto.IsActive;
            status.SortOrder = dto.SortOrder;

            await ctx.Diwan.UpdateStatusAsync(status, new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.CorrespondenceStatus, status.Id, $"تعديل حالة المعاملة «{name}»", details));
            return ToDto(status);
        }
    }

    public class DeleteCorrespondenceStatusHandler(DiwanContext ctx) : IRequestHandler<DeleteCorrespondenceStatusCommand, bool>
    {
        public async Task<bool> Handle(DeleteCorrespondenceStatusCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var status = await ctx.Diwan.GetStatusAsync(request.Id) ?? throw new KeyNotFoundException("الحالة غير موجودة");
            DiwanContext.EnsureBranchAccess(viewer, status.BranchId);
            if (await ctx.Diwan.StatusInUseAsync(status.Id))
                throw new InvalidOperationException("لا تُحذف حالة عليها معاملات: عطّلها بدل حذفها");

            await ctx.Diwan.DeleteStatusAsync(status, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.CorrespondenceStatus, status.Id, $"حذف حالة المعاملة «{status.Name}»"));
            return true;
        }
    }
}
