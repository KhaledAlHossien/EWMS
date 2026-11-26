using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.RegisterDiwanDispatch
{
    public record RegisterStudyDiwanDispatchCommand(int Id, string Number, DateOnly Date, string RowVersion) : IRequest<StudyResponseDto>;
}
