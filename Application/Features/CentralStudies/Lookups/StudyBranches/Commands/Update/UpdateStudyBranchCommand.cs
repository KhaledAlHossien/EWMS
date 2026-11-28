using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StudyBranches.Commands.Update
{
    /// <summary>يُعدَّل الاسم والتفعيل دائماً؛ الرمز والمسار ما لم يُستعمل الفرع في طلب؛ فرعه في النظام لا يتغير</summary>
    public record UpdateStudyBranchCommand(int Id, StudyBranchFormDto Dto) : IRequest<StudyBranchDto>;
}
