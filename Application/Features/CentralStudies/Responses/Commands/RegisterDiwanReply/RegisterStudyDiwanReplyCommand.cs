using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.RegisterDiwanReply
{
    public record RegisterStudyDiwanReplyCommand(int Id, string Number, DateOnly Date, StudyDiwanDecision Decision, string RowVersion, IUploadReader Upload)
        : IRequest<StudyResponseDto>;
}
