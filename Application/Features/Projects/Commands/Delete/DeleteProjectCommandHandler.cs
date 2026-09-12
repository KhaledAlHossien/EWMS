using Application.Interfaces;
using MediatR;

namespace Application.Features.Projects.Commands.Delete
{
    public class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, bool>
    {
        private readonly IProjectService _projectService;
        private readonly ICurrentUserService _currentUserService;

        public DeleteProjectCommandHandler(IProjectService projectService, ICurrentUserService currentUserService)
        {
            _projectService = projectService;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
        {
            var project = await _projectService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المشروع غير موجود");

            if (!_currentUserService.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                && project.CreatedById != _currentUserService.UserId)
                throw new UnauthorizedAccessException("لا يمكنك حذف هذا المشروع");

            return await _projectService.DeleteAsync(project);
        }
    }
}
