using Application.DTOs.Response;
using Application.Features.CentralStudies.Assignments;
using Application.Features.CentralStudies.Drawings;
using Application.Features.CentralStudies.Requests;
using Application.Features.CentralStudies.Responses;
using Application.Features.CentralStudies.StartOrders;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Print
{
    /// <summary>
    /// طباعة نموذج ورقي من مستندات الدراسات (الطلب، التكليف، الرد، أمر البدء، اعتماد المخططات) على نمط Vacations/Print:
    /// تحتاج PrintStudyDocuments مع صلاحية عرض المستند نفسه، وحدّها حدّ عرضه (فرع الدراسة).
    /// تُرجع بيانات المستند كاملة وترويسة الطباعة، وكل توقيع بصورة النسخة المحفوظة وقت القرار لا التوقيع الحالي.
    /// </summary>
    public record GetStudyPrintQuery(StudyEntityType Type, int Id) : IRequest<StudyPrintDto>;

    public class GetStudyPrintQueryHandler : IRequestHandler<GetStudyPrintQuery, StudyPrintDto>
    {
        private readonly IMediator _mediator;
        private readonly StudyKit _kit;
        private readonly IStudyApprovalService _approvals;
        private readonly IStudyProjectService _projects;
        private readonly IUserSignatureService _signatures;

        public GetStudyPrintQueryHandler(IMediator mediator, StudyKit kit, IStudyApprovalService approvals,
            IStudyProjectService projects, IUserSignatureService signatures)
        {
            _mediator = mediator;
            _kit = kit;
            _approvals = approvals;
            _projects = projects;
            _signatures = signatures;
        }

        private static string ViewPermissionOf(StudyEntityType type) => type switch
        {
            StudyEntityType.Request => StudyPermissions.ViewRequests,
            StudyEntityType.Assignment => StudyPermissions.ViewAssignments,
            StudyEntityType.Response => StudyPermissions.ViewResponses,
            StudyEntityType.StartOrder => StudyPermissions.ViewStartOrders,
            StudyEntityType.DesignApproval => StudyPermissions.ViewDrawings,
            _ => throw new ArgumentException("هذا المستند لا يُطبع بنموذج ورقي")
        };

        public async Task<StudyPrintDto> Handle(GetStudyPrintQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            StudyRules.Require(viewer, StudyPermissions.PrintDocuments, "لا تملك صلاحية طباعة مستندات الدراسات");
            StudyRules.Require(viewer, ViewPermissionOf(request.Type), "لا تملك صلاحية عرض هذا المستند");

            var dto = new StudyPrintDto { DocumentType = request.Type.ToString(), Title = StudyRules.Name(request.Type) };
            switch (request.Type)
            {
                case StudyEntityType.Request:
                    var rq = await _mediator.Send(new GetStudyRequestQuery(request.Id), ct);
                    dto.Request = rq;
                    (dto.Number, dto.BranchName, dto.DocumentDate) = (rq.RequestNumber, rq.BranchName, rq.RequestDate);
                    break;
                case StudyEntityType.Assignment:
                    var a = await _mediator.Send(new GetStudyAssignmentQuery(request.Id), ct);
                    dto.Assignment = a;
                    (dto.Number, dto.BranchName, dto.DocumentDate) = (a.AssignmentNumber, a.BranchName, a.AssignmentDate);
                    break;
                case StudyEntityType.Response:
                    var r = await _mediator.Send(new GetStudyResponseQuery(request.Id), ct);
                    dto.Response = r;
                    (dto.Number, dto.BranchName, dto.DocumentDate) = (r.ResponseNumber, r.BranchName, r.ResponseDate);
                    break;
                case StudyEntityType.StartOrder:
                    var o = await _mediator.Send(new GetStudyStartOrderQuery(request.Id), ct);
                    dto.StartOrder = o;
                    (dto.Number, dto.BranchName, dto.DocumentDate) = (o.OrderNumber, o.BranchName, o.OrderDate);
                    break;
                default:
                    var c = await _mediator.Send(new GetDesignApprovalQuery(request.Id), ct);
                    dto.DesignApproval = c;
                    var project = await _projects.GetByIdAsync(c.ProjectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
                    StudyRules.EnsureInScope(viewer, project.BranchId);
                    (dto.Number, dto.BranchName, dto.DocumentDate) = (c.CdaNumber, project.Branch.Name, c.CdaDate);
                    dto.StartOrderNumber = project.StartOrder.OrderNumber;
                    break;
            }
            dto.DocumentDateHijri = StudyRules.Hijri(dto.DocumentDate);

            // كل توقيع بنسخته المحفوظة وقت القرار (SignatureId) — القرار بلا توقيع يُطبع بمربع فارغ (Image = null)
            var rows = await _approvals.GetForOwnerAsync(request.Type, request.Id);
            foreach (var row in rows)
            {
                var decided = row.Decision != StudyDecision.Pending;
                dto.Signatures.Add(new StudyPrintSignatureDto
                {
                    SignerRole = (int)row.SignerRole,
                    SignerRoleName = StudyRules.Name(row.SignerRole),
                    Decision = (int)row.Decision,
                    DecisionName = StudyRules.Name(row.Decision),
                    SignerName = row.Signer?.FullName,
                    JobTitle = row.JobTitle,
                    DecidedAt = row.DecidedAt,
                    DecidedAtHijri = row.DecidedAt is DateTime at ? StudyRules.Hijri(at) : null,
                    Note = row.Note,
                    Image = decided ? await _signatures.GetImageAsync(row.SignatureId) : null
                });
            }
            return dto;
        }
    }
}
