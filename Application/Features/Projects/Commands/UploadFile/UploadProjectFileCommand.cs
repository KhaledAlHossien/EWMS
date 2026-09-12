using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Projects.Commands.UploadFile
{
    public record UploadProjectFileCommand(int ProjectId, UploadProjectFileRequestDto FileDto)
        : IRequest<ProjectFileResponseDto>;
}
