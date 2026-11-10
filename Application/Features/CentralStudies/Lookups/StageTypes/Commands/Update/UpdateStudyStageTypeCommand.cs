using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StageTypes.Commands.Update
{
    public record UpdateStudyStageTypeCommand(int Id, StudyStageTypeFormDto Dto) : IRequest<StudyStageTypeDto>;
}
