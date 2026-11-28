using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.CreateRegional
{
    /// <summary>إحالة طلب معتمد لفرع جغرافي إلى فرعه: مشروع تنسيق ومتابعة بلا تكليف ولا رد ولا ديوان ولا أمر بدء</summary>
    public record CreateRegionalStudyProjectCommand(StudyRegionalProjectFormDto Dto) : IRequest<StudyProjectDto>;
}
