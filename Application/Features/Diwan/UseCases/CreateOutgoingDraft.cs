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
    public record CreateOutgoingDraftCommand(CreateOutgoingDraftRequestDto Dto) : IRequest<CorrespondenceResponseDto>;

    public class CreateOutgoingDraftCommandValidator : AbstractValidator<CreateOutgoingDraftCommand>
    {
        public CreateOutgoingDraftCommandValidator()
        {
            RuleFor(x => x.Dto.TypeId).GreaterThan(0).WithMessage("نوع المعاملة مطلوب");
            RuleFor(x => x.Dto).Custom((dto, context) =>
            {
                if (DiwanRules.ValidateRegistration(dto.Party, dto.Subject, dto.Summary, dto.ExternalNumber) is string problem) context.AddFailure(problem);
            });
        }
    }

    /// <summary>
    /// مسودة كتاب صادر: بلا رقم حتى الصدور. يعدّها من يملك التسجيل، ثم يعتمدها غيره بتوقيعه، ثم تُصدَر برقمها.
    /// </summary>
    public class CreateOutgoingDraftHandler(DiwanContext ctx) : IRequestHandler<CreateOutgoingDraftCommand, CorrespondenceResponseDto>
    {
        public async Task<CorrespondenceResponseDto> Handle(CreateOutgoingDraftCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var branch = DiwanContext.ResolveBranch(viewer, dto.BranchId);
            DiwanRules.EnsureCanRegister(viewer, branch);

            var type = await ctx.ResolveTypeAsync(branch, dto.TypeId);
            var project = await ctx.ResolveProjectAsync(viewer, dto.ProjectId, branch);
            var replyTo = await ctx.ResolveReplyToAsync(viewer, dto.ReplyToId, branch);

            var now = DateTime.UtcNow;
            var external = (dto.ExternalNumber ?? string.Empty).Trim();
            var c = new Correspondence
            {
                Direction = CorrespondenceDirection.Outgoing,
                OutgoingState = OutgoingState.Draft,
                BranchId = branch,
                DepartmentId = OrganizationRole.InBranch(viewer.User, branch) ? viewer.User.DepartmentId : null,
                Year = DateTime.Today.Year,
                ExternalNumber = external.Length == 0 ? null : external,
                Party = dto.Party.Trim(),
                Subject = dto.Subject.Trim(),
                Summary = (dto.Summary ?? string.Empty).Trim(),
                TypeId = type.Id,
                ProjectId = project?.Id,
                ReplyToId = replyTo?.Id,
                RegisteredByUserId = viewer.Id,
                RegisteredAt = now,
                UpdatedAt = now
            };

            var saved = await ctx.Diwan.AddAsync(c, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Correspondence, null,
                $"إعداد مسودة صادر «{c.Subject}»", Lines($"إلى: {c.Party}", $"النوع: {type.Name}", replyTo is null ? null : $"ردّ على: {Label(replyTo)}")));
            return await ctx.DetailAsync(viewer, saved.Id);
        }
    }
}
