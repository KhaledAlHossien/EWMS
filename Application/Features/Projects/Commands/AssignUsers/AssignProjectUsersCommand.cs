using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Projects.Commands.AssignUsers
{
    public record AssignProjectUsersCommand(int ProjectId, AssignProjectUsersRequestDto AssignmentDto)
        : IRequest<List<ProjectAssignmentResponseDto>>;
}
