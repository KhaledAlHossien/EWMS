using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StageTypes.Commands.Create
{
    public record CreateStudyStageTypeCommand(StudyStageTypeFormDto Dto) : IRequest<StudyStageTypeDto>;
}
