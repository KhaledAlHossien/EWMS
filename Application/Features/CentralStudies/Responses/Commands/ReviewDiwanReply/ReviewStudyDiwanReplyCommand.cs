using Application.DTOs.Response;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.ReviewDiwanReply
{
    public record ReviewStudyDiwanReplyCommand(int Id, StudyDiwanReviewAction Action, string? Note, string RowVersion) : IRequest<StudyResponseDto>;
}
