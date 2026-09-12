using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects.Commands.AssignUsers;
using Application.Features.Projects.Commands.Create;
using Application.Features.Projects.Commands.Delete;
using Application.Features.Projects.Commands.Transfer;
using Application.Features.Projects.Commands.Update;
using Application.Features.Projects.Commands.UploadFile;
using Application.Features.Projects.Queries.GetAll;
using Application.Features.Projects.Queries.GetByDepartment;
using Application.Features.Projects.Queries.GetById;
using Application.Features.Projects.Queries.GetMine;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/Projects")]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IWebHostEnvironment _environment;

        public ProjectsController(IMediator mediator, IWebHostEnvironment environment)
        {
            _mediator = mediator;
            _environment = environment;
        }

        [HttpPost("Create")]
        [Authorize(Policy = "CreateProject")]
        public async Task<ActionResult<ProjectResponseDto>> Create([FromForm] CreateProjectRequestDto dto)
        {
            var result = await _mediator.Send(new CreateProjectCommand(dto));
            return Ok(result);
        }

        [HttpPut("Update/{id}")]
        [Authorize(Policy = "EditProject")]
        public async Task<ActionResult<ProjectResponseDto>> Update(int id, [FromForm] UpdateProjectRequestDto dto)
        {
            var result = await _mediator.Send(new UpdateProjectCommand(id, dto));
            return Ok(result);
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Policy = "DeleteProject")]
        public async Task<ActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteProjectCommand(id));
            return Ok(new { message = "تم حذف المشروع بنجاح" });
        }

        [HttpPost("{projectId}/AssignUsers")]
        [Authorize(Policy = "AssignUser")]
        public async Task<ActionResult<List<ProjectAssignmentResponseDto>>> AssignUsers(
            int projectId,
            [FromForm] AssignProjectUsersRequestDto dto)
        {
            var result = await _mediator.Send(new AssignProjectUsersCommand(projectId, dto));
            return Ok(result);
        }

        [HttpPost("{projectId}/Transfer")]
        [Authorize(Policy = "TransferProject")]
        public async Task<ActionResult<ProjectTransferResponseDto>> Transfer(
            int projectId,
            [FromForm] TransferProjectRequestDto dto)
        {
            var result = await _mediator.Send(new TransferProjectCommand(projectId, dto));
            return Ok(result);
        }

        [HttpPost("{projectId}/Files")]
        [Authorize(Policy = "UploadProjectFile")]
        [RequestSizeLimit(200_000_000)]
        public async Task<ActionResult<ProjectFileResponseDto>> UploadFile(
            int projectId,
            IFormFile file,
            [FromForm] string notes = "")
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "الملف مطلوب" });

            var uploadsRoot = Path.Combine(_environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                "uploads", "projects", projectId.ToString());

            Directory.CreateDirectory(uploadsRoot);

            var safeFileName = Path.GetFileName(file.FileName);
            var storedFileName = $"{Guid.NewGuid():N}_{safeFileName}";
            var fullPath = Path.Combine(uploadsRoot, storedFileName);

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = Path.Combine("uploads", "projects", projectId.ToString(), storedFileName)
                .Replace('\\', '/');

            try
            {
                var result = await _mediator.Send(new UploadProjectFileCommand(
                    projectId,
                    new UploadProjectFileRequestDto
                    {
                        FileName = safeFileName,
                        FilePath = relativePath,
                        Notes = notes
                    }));

                return Ok(result);
            }
            catch
            {
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);

                throw;
            }
        }

        [HttpGet("{projectId}/Files/{fileId}/Download")]
        [Authorize(Policy = "ViewProjects")]
        public async Task<IActionResult> DownloadFile(int projectId, int fileId)
        {
            var project = await _mediator.Send(new GetProjectByIdQuery(projectId));
            var file = project.Files.FirstOrDefault(f => f.Id == fileId)
                ?? throw new KeyNotFoundException("الملف غير موجود");

            var webRoot = _environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var fullPath = Path.GetFullPath(Path.Combine(webRoot, file.FilePath));
            var allowedRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads", "projects"));

            if (!fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
                throw new KeyNotFoundException("الملف غير موجود على السيرفر");

            return PhysicalFile(fullPath, "application/octet-stream", file.FileName);
        }

        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewProjects")]
        public async Task<ActionResult<ProjectResponseDto>> GetById(int id)
        {
            var result = await _mediator.Send(new GetProjectByIdQuery(id));
            return Ok(result);
        }

        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewProjects")]
        public async Task<ActionResult<List<ProjectResponseDto>>> GetAll()
        {
            var result = await _mediator.Send(new GetAllProjectsQuery());
            return Ok(result);
        }

        [HttpGet("Department/{departmentId}")]
        [Authorize(Policy = "ViewProjects")]
        public async Task<ActionResult<List<ProjectResponseDto>>> GetByDepartment(int departmentId)
        {
            var result = await _mediator.Send(new GetProjectsByDepartmentQuery(departmentId));
            return Ok(result);
        }

        [HttpGet("Mine")]
        [Authorize(Policy = "ViewProjects")]
        public async Task<ActionResult<List<ProjectResponseDto>>> GetMine()
        {
            var result = await _mediator.Send(new GetMyProjectsQuery());
            return Ok(result);
        }
    }
}
